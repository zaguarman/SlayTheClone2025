using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// The telemetry's settings (Resources/TelemetrySettings, Debug > Telemetry > Settings): whether games are recorded, and
    /// where they're sent. Recording is local: every game is saved as a JSON line in the telemetry folder. Sending is off
    /// until an endpoint is set (the collector server, Tools/telemetry_server.py).
    /// </summary>
    [CreateAssetMenu(menuName = "Telemetry Settings")]
    public class TelemetrySettings : ScriptableObject {
        [Tooltip("Record every game played on screen (a JSON line each, in the telemetry folder).")]
        public bool record = true;
        [Tooltip("Also record the headless AI games (tournaments, balance study, adventure simulation): thousands of games a run.")]
        public bool recordSimulations;
        [Tooltip("Where the records are sent: the collector's address, e.g. http://localhost:8787/v1/games. Empty: nothing is sent.")]
        public string endpoint = "";
        [Tooltip("Sent as the X-Api-Key header, if the collector asks for one.")]
        public string apiKey = "";
        [Tooltip("How often the records waiting are sent while the game runs, in minutes.")]
        [Min(1)] public int uploadEveryMinutes = 5;
        [Tooltip("Records a request, at most.")]
        [Min(1)] public int batchSize = 200;
    }

    /// <summary>
    /// The telemetry (the user, 2026-10-16): games recorded by <see cref="GameRecorder"/> are kept as JSON lines in
    /// <see cref="Folder"/>/pending, a file a day, until they're sent to the collector server (<see cref="TelemetrySettings.endpoint"/>);
    /// then they move to /sent. Saving works from any thread; sending runs in the background and retries what failed next
    /// time. Each record carries an anonymous install id and the game's version, nothing about the person playing.
    /// </summary>
    public static class Telemetry {
        public const string InstallIdKey = "TelemetryInstallId";
        private static readonly object gate = new object();
        private static TelemetrySettings settings;
        private static volatile bool uploading;
        private static readonly Encoding NoBom = new UTF8Encoding(false); // Plain JSON lines, no byte-order mark

        /// <summary>Games on screen are recorded.</summary>
        public static bool Recording { get; private set; } = true;
        /// <summary>Headless AI games are recorded too.</summary>
        public static bool RecordingSimulations { get; private set; }
        public static string Endpoint { get; private set; } = "";
        public static string InstallId { get; private set; } = "unknown";
        public static string Version { get; private set; } = "";
        /// <summary>Where the records are kept: the project's Logs/Telemetry in the editor, the app's data folder in a build.</summary>
        public static string Folder { get; private set; }
        public static string PendingFolder => Path.Combine(Folder, "pending");
        public static string SentFolder => Path.Combine(Folder, "sent");
        private static string OutboxFolder => Path.Combine(Folder, "outbox");
        /// <summary>What the last send did, for the menu and the log.</summary>
        public static string LastUpload { get; private set; } = "Nothing sent yet.";

        /// <summary>Reads the settings, on the main thread (Unity's own calls need it): at start, and before a tournament.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Load() {
            settings = Resources.Load<TelemetrySettings>("TelemetrySettings");
            Recording = settings == null || settings.record;
            RecordingSimulations = settings != null && settings.record && settings.recordSimulations;
            Endpoint = settings != null ? settings.endpoint?.Trim() ?? "" : "";
            Version = Application.version;
            Folder = Path.Combine(Application.isEditor ? Path.GetDirectoryName(Application.dataPath) : Application.persistentDataPath,
                Application.isEditor ? "Logs" : "", "Telemetry");
            if (!PlayerPrefs.HasKey(InstallIdKey)) {
                PlayerPrefs.SetString(InstallIdKey, Guid.NewGuid().ToString("N"));
                PlayerPrefs.Save();
            }
            InstallId = PlayerPrefs.GetString(InstallIdKey);
            if (Application.isPlaying && !string.IsNullOrEmpty(Endpoint)) TelemetryUploader.Ensure(settings.uploadEveryMinutes);
        }

        /// <summary>Keeps the record, as one JSON line in today's pending file. Any thread.</summary>
        public static void Save(GameRecord record) {
            if (record == null || Folder == null) return;
            string line = JsonUtility.ToJson(record);
            try {
                lock (gate) {
                    Directory.CreateDirectory(PendingFolder);
                    File.AppendAllText(Path.Combine(PendingFolder, $"games-{DateTime.UtcNow:yyyy-MM-dd}.jsonl"), line + "\n", NoBom);
                }
            }
            catch (IOException exception) {
                Debug.LogWarning($"Telemetry: the game couldn't be saved ({exception.Message})");
            }
        }

        /// <summary>The records waiting to be sent.</summary>
        public static int PendingCount {
            get {
                lock (gate) {
                    return new[] { PendingFolder, OutboxFolder }.Where(Directory.Exists)
                        .SelectMany(f => Directory.GetFiles(f, "*.jsonl")).Sum(f => File.ReadLines(f).Count(l => l.Trim().Length > 0));
                }
            }
        }

        /// <summary>
        /// Sends the records waiting to the endpoint, in batches, in the background; what's sent moves to /sent. Null when
        /// there's nothing to do (no endpoint, already sending). The task's text says what happened.
        /// </summary>
        public static Task<string> UploadAsync() {
            if (string.IsNullOrEmpty(Endpoint) || Folder == null || uploading) return null;
            uploading = true;
            string apiKey = settings != null ? settings.apiKey : "";
            int batch = settings != null ? Math.Max(1, settings.batchSize) : 200;
            string endpoint = Endpoint, installId = InstallId, version = Version;
            return Task.Run(async () => {
                try {
                    // What waits moves to the outbox first, so games saved meanwhile start a new file
                    lock (gate) {
                        Directory.CreateDirectory(OutboxFolder);
                        if (Directory.Exists(PendingFolder)) {
                            foreach (var file in Directory.GetFiles(PendingFolder, "*.jsonl")) {
                                File.Move(file, Path.Combine(OutboxFolder, $"{Path.GetFileNameWithoutExtension(file)}-{Guid.NewGuid():N}.jsonl"));
                            }
                        }
                    }
                    int sent = 0, files = 0;
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                    if (!string.IsNullOrEmpty(apiKey)) client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
                    foreach (var file in Directory.GetFiles(OutboxFolder, "*.jsonl").OrderBy(f => f)) {
                        var lines = File.ReadAllLines(file).Where(l => l.Trim().Length > 0).ToList();
                        for (int i = 0; i < lines.Count; i += batch) {
                            string body = $"{{\"installId\":\"{installId}\",\"version\":\"{version}\",\"records\":[{string.Join(",", lines.Skip(i).Take(batch))}]}}";
                            var response = await client.PostAsync(endpoint, new StringContent(body, NoBom, "application/json"));
                            if (!response.IsSuccessStatusCode) {
                                // What was sent of this file is dropped from it; the rest waits for the next try
                                File.WriteAllLines(file, lines.Skip(i));
                                return LastUpload = $"Sent {sent} games, then the collector answered {(int)response.StatusCode} {response.ReasonPhrase}: the rest waits.";
                            }
                            sent += Math.Min(batch, lines.Count - i);
                        }
                        Directory.CreateDirectory(SentFolder);
                        File.Move(file, Path.Combine(SentFolder, Path.GetFileName(file)));
                        files++;
                    }
                    return LastUpload = sent == 0 ? "Nothing waiting to be sent." : $"Sent {sent} games ({files} files) to {endpoint}.";
                }
                catch (Exception exception) {
                    return LastUpload = $"Couldn't reach the collector ({exception.GetBaseException().Message}): the games wait for the next try.";
                }
                finally {
                    uploading = false;
                }
            });
        }
    }

    /// <summary>Sends the records waiting every few minutes while the game runs, and once more when it quits.</summary>
    public class TelemetryUploader : MonoBehaviour {
        private static TelemetryUploader instance;
        private float every;

        public static void Ensure(int minutes) {
            if (instance != null) return;
            var go = new GameObject("Telemetry Uploader") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            instance = go.AddComponent<TelemetryUploader>();
            instance.every = Mathf.Max(1, minutes) * 60f;
            instance.StartCoroutine(instance.Loop());
        }

        private IEnumerator Loop() {
            while (true) {
                var task = Telemetry.UploadAsync();
                if (task != null) {
                    while (!task.IsCompleted) yield return null;
                    Debug.Log($"Telemetry: {task.Result}");
                }
                yield return new WaitForSecondsRealtime(every);
            }
        }

        private void OnApplicationQuit() => Telemetry.UploadAsync();
    }
}
