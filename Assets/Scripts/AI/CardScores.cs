using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Every card's score in the latest AI balance study, as a tier (the user, 2026-10-17: filter the deck builder by Tier S, A,
    /// B, C, D "depending on the scores we have on the latest balancing runs"). The study writes them to
    /// Resources/CardScores.json each time it runs (Debug > AI Balance Report). Spells are scored by their edge (how often the
    /// side that played them more won); creatures by what they deal over their life, per play (damage to the player, a third of
    /// their damage to creatures, kills at 2): a side whose creatures keep dying plays more of them, so their edge says less.
    /// Each kind is ranked apart, like the adventure's tiers: the top 10% S, then 20% A, 25% B, 25% C, the last 20% D. Cards
    /// played in too few games are unrated.
    /// </summary>
    public static class CardScores {
        public const string ResourcePath = "CardScores";
        public static readonly string[] Tiers = { "S", "A", "B", "C", "D" };
        private static readonly float[] TierTops = { 0.2f, 0.45f, 0.7f, 0.9f, 1f }; // D, C, B, A, S from the bottom
        public const int MinSpellEdgeGames = 60, MinCreatureGames = 20;

        [System.Serializable]
        public class Entry {
            public string card;
            public string tier;  // S to D
            public float score;  // Edge for a spell, value per play for a creature
            public bool creature;
            public int games;
        }

        [System.Serializable]
        public class File {
            public string writtenUtc;
            public int gamesPlayed;
            public List<Entry> cards = new List<Entry>();
        }

        private static Dictionary<string, Entry> byName;
        private static File loaded;

        /// <summary>The latest scores, as the study left them (null: none yet).</summary>
        public static File Latest {
            get {
                if (loaded == null) {
                    var asset = Resources.Load<TextAsset>(ResourcePath);
                    loaded = asset != null ? JsonUtility.FromJson<File>(asset.text) : new File();
                    byName = loaded.cards.Where(e => e?.card != null).GroupBy(e => e.card).ToDictionary(g => g.Key, g => g.First());
                }
                return loaded;
            }
        }

        public static bool HasScores => Latest.cards.Count > 0;

        /// <summary>The card's tier in the latest study, "S" to "D"; null when it wasn't played enough to say.</summary>
        public static string TierOf(string cardName) {
            _ = Latest;
            return cardName != null && byName.TryGetValue(cardName, out var entry) ? entry.tier : null;
        }

        /// <summary>Forget what was loaded (after the study writes new scores).</summary>
        public static void Reload() => loaded = null;

        /// <summary>The tiers, from the study's card records.</summary>
        public static File From(IEnumerable<CardLedger.Record> records, int gamesPlayed) {
            var file = new File { writtenUtc = System.DateTime.UtcNow.ToString("u"), gamesPlayed = gamesPlayed };
            var list = records.ToList();
            void Rank(List<(CardLedger.Record r, float score)> scored, bool creature) {
                var ranked = scored.OrderBy(s => s.score).ThenBy(s => s.r.Name).ToList();
                for (int i = 0; i < ranked.Count; i++) {
                    float share = (i + 0.5f) / ranked.Count;
                    int tier = 0;
                    while (tier < TierTops.Length - 1 && share > TierTops[tier]) tier++;
                    file.cards.Add(new Entry {
                        card = ranked[i].r.Name, tier = Tiers[Tiers.Length - 1 - tier], score = ranked[i].score, creature = creature,
                        games = creature ? ranked[i].r.Games : ranked[i].r.EdgeGames
                    });
                }
            }
            Rank(list.Where(r => !r.IsCreature && r.EdgeGames >= MinSpellEdgeGames).Select(r => (r, r.Edge)).ToList(), false);
            Rank(list.Where(r => r.IsCreature && r.Games >= MinCreatureGames).Select(r => (r, ValuePerPlay(r))).ToList(), true);
            return file;
        }

        /// <summary>A creature's value per play, as the report ranks them: damage to the player, a third of its damage to creatures, kills at 2.</summary>
        public static float ValuePerPlay(CardLedger.Record r) =>
            r.FacePerPlay + r.CreatureDamage / (float)System.Math.Max(1, r.Plays) / 3f + r.KillsPerPlay * 2f;
    }
}
