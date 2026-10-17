#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>
    /// The card files (cards as data, the user, 2026-10-17): Assets/Cards/&lt;Subfaction&gt;.json, read by Cards > Generate Faction
    /// Decks. Cards > Export Cards To Files writes them back from the card assets (the one-time move out of the generator's code,
    /// and a way to re-sort a file: cards keep the order the file had, new ones go last).
    /// </summary>
    public static class CardFilesMenu {
        private const string CardsRoot = "Assets/Scriptables/Cards";

        /// <summary>The file a subfaction's cards are in; Potions for the neutral ones.</summary>
        public static string PathOf(Faction faction) => $"{CardFiles.Folder}/{(faction == Faction.None ? "Potions" : faction.ToString())}.json";

        /// <summary>The cards of a subfaction's file (Potions for None), in the file's order.</summary>
        public static List<CardFiles.CardSpec> Load(Faction faction) {
            string path = PathOf(faction);
            if (!File.Exists(path)) throw new FileNotFoundException($"No card file at {path}");
            return CardFiles.Read(File.ReadAllText(path)).cards;
        }

        [MenuItem("Cards/Export Cards To Files")]
        public static void Export() {
            Directory.CreateDirectory(CardFiles.Folder);
            int count = 0;
            foreach (var folder in AssetDatabase.GetSubFolders(CardsRoot)) {
                string name = Path.GetFileName(folder);
                if (!System.Enum.TryParse(name, out Faction faction)) continue;
                var assets = AssetDatabase.FindAssets("t:CardDataScriptableObject", new[] { folder })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(AssetDatabase.GUIDToAssetPath(guid))).Where(a => a != null).ToList();
                var order = Order(faction);
                int At(CardDataScriptableObject a) => order.TryGetValue(a.cardName, out int i) ? i : int.MaxValue;
                var file = new CardFiles.CardFile {
                    mod = faction == Faction.None ? "Potions" : CardTextFormatter.FactionName(faction),
                    cards = assets.OrderBy(At).ThenBy(a => a.cardName).Select(a => CardFiles.ToSpec(a, faction)).ToList()
                };
                File.WriteAllText(PathOf(faction), CardFiles.Write(file), new System.Text.UTF8Encoding(false));
                count += file.cards.Count;
            }
            AssetDatabase.Refresh();
            Debug.Log($"Wrote {count} cards to {CardFiles.Folder}");
        }

        // The order the cards had: the file's, if there is one; otherwise the generator source's definitions (the first export)
        private static Dictionary<string, int> Order(Faction faction) {
            var order = new Dictionary<string, int>();
            string path = PathOf(faction);
            if (File.Exists(path)) {
                var names = CardFiles.Read(File.ReadAllText(path)).cards.Select(c => c.name).ToList();
                for (int i = 0; i < names.Count; i++) order[names[i]] = i;
                return order;
            }
            string source = Path.Combine(Application.dataPath, "Scripts", "Generators", "FactionDecksGenerator.cs");
            if (!File.Exists(source)) return order;
            var defined = System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(source), @"(?:Creature|Spell|GambleSpell)\(""((?:[^""\\]|\\.)+)""")
                .Cast<System.Text.RegularExpressions.Match>().Select(m => m.Groups[1].Value.Replace("\\\"", "\"")).ToList();
            for (int i = 0; i < defined.Count; i++) if (!order.ContainsKey(defined[i])) order[defined[i]] = i;
            return order;
        }
    }
}
#endif
