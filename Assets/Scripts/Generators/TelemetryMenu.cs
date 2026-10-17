#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Zeitgeist {
    /// <summary>
    /// Debug > Telemetry: the settings (made if missing), the folder where the games are kept, sending what waits now, and
    /// how much waits. See <see cref="Telemetry"/>.
    /// </summary>
    public static class TelemetryMenu {
        private const string SettingsPath = "Assets/Resources/TelemetrySettings.asset";

        [MenuItem("Debug/Telemetry/Settings")]
        public static void SelectSettings() {
            var settings = AssetDatabase.LoadAssetAtPath<TelemetrySettings>(SettingsPath);
            if (settings == null) {
                settings = ScriptableObject.CreateInstance<TelemetrySettings>();
                AssetDatabase.CreateAsset(settings, SettingsPath);
                AssetDatabase.SaveAssets();
            }
            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Debug/Telemetry/Open Folder")]
        public static void OpenFolder() {
            Telemetry.Load();
            Directory.CreateDirectory(Telemetry.PendingFolder);
            EditorUtility.RevealInFinder(Telemetry.PendingFolder);
        }

        [MenuItem("Debug/Telemetry/Send Now")]
        public static void SendNow() {
            Telemetry.Load();
            if (string.IsNullOrEmpty(Telemetry.Endpoint)) {
                Debug.LogWarning("Telemetry: no endpoint set (Debug > Telemetry > Settings): nothing is sent. The games are kept in " + Telemetry.PendingFolder);
                return;
            }
            var task = Telemetry.UploadAsync();
            if (task == null) {
                Debug.Log("Telemetry: already sending.");
                return;
            }
            task.ContinueWith(t => Debug.Log($"Telemetry: {t.Result}"));
        }

        [MenuItem("Debug/Telemetry/Status")]
        public static void Status() {
            Telemetry.Load();
            Debug.Log($"Telemetry: recording {(Telemetry.Recording ? "on" : "off")}, simulations {(Telemetry.RecordingSimulations ? "on" : "off")}; "
                      + $"{Telemetry.PendingCount} games waiting in {Telemetry.Folder}; endpoint: {(string.IsNullOrEmpty(Telemetry.Endpoint) ? "none" : Telemetry.Endpoint)}. "
                      + Telemetry.LastUpload);
        }
    }
}
#endif
