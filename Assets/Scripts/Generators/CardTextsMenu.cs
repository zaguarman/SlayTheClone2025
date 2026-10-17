#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Zeitgeist {
    /// <summary>
    /// Cards > Dump Card Texts writes every card as it prints (the formatter's rules text, links stripped) to
    /// Assets/Documentation/Card Texts.txt, and every keyword with its explanation after them: the card text audit and the
    /// keyword review (the dossier, 2026-10-17) read it instead of clicking through the gallery.
    /// </summary>
    public static class CardTextsMenu {
        public const string Path = "Assets/Documentation/Card Texts.txt";

        [MenuItem("Cards/Dump Card Texts")]
        public static void Dump() {
            var cards = AssetDatabase.FindAssets("t:CardDataScriptableObject", new[] { "Assets/Scriptables/Cards" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<CardDataScriptableObject>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(c => c != null).OrderBy(c => Umbrellas.Order(c.faction)).ThenBy(c => c is CreatureCardScriptableObject ? 0 : 1).ThenBy(c => c.cardName).ToList();
            var text = new StringBuilder();
            text.AppendLine($"CARD TEXTS ({cards.Count} cards, as printed; written by Cards > Dump Card Texts on {System.DateTime.Now:yyyy-MM-dd})");
            text.AppendLine("The style rule: one plain sentence a clause, a mechanic's name only as a sparse label, no arrows, no timing in brackets.");
            foreach (var group in cards.GroupBy(c => c.faction)) {
                text.AppendLine();
                text.AppendLine($"== {CardTextFormatter.FactionName(group.Key)} ({group.Count()})");
                foreach (var card in group) {
                    var data = card.ToCardData();
                    string head = card is CreatureCardScriptableObject creature
                        ? $"{creature.attack}/{creature.health}/{creature.speed}, deploy {creature.turnsUntilOperative}" : "spell";
                    string rules = Keywords.Plain(CardTextFormatter.GetRulesText(data)).Replace("\n", " | ");
                    text.AppendLine($"{card.cardName} [{head}]: {rules}");
                    if (!string.IsNullOrEmpty(card.description)) text.AppendLine($"    \"{card.description}\"");
                }
            }
            text.AppendLine();
            text.AppendLine($"KEYWORDS ({Keywords.All.Count()}), by group");
            foreach (var group in Keywords.All.GroupBy(k => k.Group)) {
                text.AppendLine();
                text.AppendLine($"== {group.Key} ({group.Count()})");
                foreach (var entry in group) text.AppendLine($"{entry.Title}: {Keywords.Plain(entry.Text)}");
            }
            File.WriteAllText(Path, text.ToString(), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(Path);
            Debug.Log($"Wrote {cards.Count} card texts to {Path}");
        }
    }
}
#endif
