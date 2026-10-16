using System;
using System.Collections.Generic;

namespace SlayTheClone {
    /// <summary>
    /// One game, as the telemetry keeps it (see <see cref="GameRecorder"/>): who played what, how it went Day by Day, how it
    /// ended, and what each card did. A plain serializable object, written as one JSON line (JsonUtility), the same shape
    /// the collector server stores. New fields go last; a field never changes meaning (old records stay readable).
    /// </summary>
    [Serializable]
    public class GameRecord {
        public int schema = 1;
        public string id;           // Unique per game
        public string installId;    // This install, anonymous (a random id kept in PlayerPrefs): nothing about the person
        public string version;      // The game's version
        public string mode;         // "match" (a game chosen in the menu), "adventure", "autopilot", "simulation" (headless AI games)
        public string startedUtc, endedUtc;
        public int seed;
        public bool ended;          // False: left before the end (the menu, a closed game)
        public int winner;          // 1 or 2; 0 for nobody (a draw, called off, or not ended)
        public int days;
        public SideRecord player1 = new SideRecord(), player2 = new SideRecord();
        public AdventureRecord adventure; // Only in an adventure's fight
        public List<DayRecord> timeline = new List<DayRecord>();
        public List<CardRecord> cards = new List<CardRecord>();
    }

    [Serializable]
    public class SideRecord {
        public string name;
        public string deck;
        public string factions;     // "Corporate" or "Corporate + Zeitgeist"
        public string playedBy;     // "human", or the AI that played it ("AI Hard")
        public bool aiTookOver;     // The AI was switched on or off for this seat during the game
        public int startHealth, endHealth;
        public string relics;       // An adventure's, comma-separated
        public List<string> deckList = new List<string>(); // Its cards' names, a copy each
    }

    [Serializable]
    public class AdventureRecord {
        public string runId;
        public string faction;
        public string room;         // Fight, Elite, Boss
        public int row;
        public int fightsWonBefore;
        public int ascension;
    }

    /// <summary>The board at the end of a Day.</summary>
    [Serializable]
    public class DayRecord {
        public int day;
        public int health1, health2;
        public int creatures1, creatures2;
        public int hand1, hand2;
    }

    /// <summary>What a card did in this game, on one side (only the cards played).</summary>
    [Serializable]
    public class CardRecord {
        public int side;
        public string name;
        public string faction;
        public bool creature;
        public int plays;
        public int faceDamage, creatureDamage, kills;
        public int assistDamage, assistKills, openedDamage;
        public int draws, sentFor, healed, denied, friendlyDamage;
    }
}
