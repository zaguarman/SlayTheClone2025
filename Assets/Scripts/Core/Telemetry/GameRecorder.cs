using System;
using System.Collections.Generic;
using System.Linq;

namespace Zeitgeist {
    /// <summary>
    /// Records one game for the telemetry (the user, 2026-10-16: "a logger that saves data from all games played"): it
    /// watches the game's session from its opening hands to its end, then saves a <see cref="GameRecord"/> (see
    /// <see cref="Telemetry"/>). The board is noted at the end of every Day; what each card did comes from the balance
    /// study's own tally (<see cref="CardLedger"/>), so the two count the same way. A game left before its end is saved too,
    /// marked as not ended. It touches nothing of Unity's, so headless games on other threads can be recorded as well.
    /// </summary>
    public sealed class GameRecorder {
        /// <summary>Who plays a seat and with what, as the recorder notes it at the start.</summary>
        public struct Seat {
            public string Name, Deck, PlayedBy, Relics;
            public IEnumerable<CardData> Cards;
        }

        private readonly GameSession session;
        private readonly GameMediator mediator;
        private readonly CardLedger ledger = new CardLedger();
        private readonly CardLedger.Watch watch;
        private readonly GameRecord record;
        private readonly string[] seatKeys;
        private readonly Func<IPlayer, string> playedBy;
        private bool done;

        /// <summary>Starts recording a game whose opening hands are dealt. Null when the telemetry is off.</summary>
        /// <param name="playedBy">Who plays a seat now ("human", "AI Hard"), asked again at the end: the AI can be switched on mid-game.</param>
        public static GameRecorder Begin(GameSession session, GameMediator mediator, string mode, int seed, Seat first, Seat second,
            AdventureRecord adventure = null, Func<IPlayer, string> playedBy = null) {
            if (!Telemetry.Recording || session == null || mediator == null) return null;
            return new GameRecorder(session, mediator, mode, seed, first, second, adventure, playedBy);
        }

        private GameRecorder(GameSession session, GameMediator mediator, string mode, int seed, Seat first, Seat second,
            AdventureRecord adventure, Func<IPlayer, string> playedBy) {
            this.session = session;
            this.mediator = mediator;
            this.playedBy = playedBy;
            record = new GameRecord {
                id = Guid.NewGuid().ToString("N"),
                installId = Telemetry.InstallId,
                version = Telemetry.Version,
                mode = mode,
                startedUtc = DateTime.UtcNow.ToString("o"),
                seed = seed,
                adventure = adventure
            };
            Fill(record.player1, first, session.Player1);
            Fill(record.player2, second, session.Player2);
            seatKeys = new[] { "1: " + first.Name, "2: " + second.Name }; // Distinct even when both seats are the same AI
            watch = ledger.Begin(session, mediator, seatKeys[0], seatKeys[1], first.Deck, second.Deck);
            mediator.TurnEnded += OnTurnEnded;
            mediator.GameOver += OnGameOver;
        }

        private static void Fill(SideRecord side, Seat seat, IPlayer player) {
            side.name = seat.Name;
            side.deck = seat.Deck;
            side.playedBy = seat.PlayedBy;
            side.relics = seat.Relics ?? "";
            side.startHealth = player.Health;
            var cards = (seat.Cards ?? Enumerable.Empty<CardData>()).Where(c => c != null).ToList();
            side.deckList = cards.Select(c => c.cardName).ToList();
            side.factions = string.Join(" + ", cards.Select(c => c.faction).Where(f => f != Enums.Faction.None).Distinct()
                .OrderBy(f => f).Select(CardTextFormatter.FactionName));
        }

        private void OnTurnEnded(int day) {
            var p1 = session.Player1;
            var p2 = session.Player2;
            record.timeline.Add(new DayRecord {
                day = day,
                health1 = p1.Health, health2 = p2.Health,
                creatures1 = p1.Battlefield.Count(s => s.OccupyingCreature is { IsDead: false }),
                creatures2 = p2.Battlefield.Count(s => s.OccupyingCreature is { IsDead: false }),
                hand1 = p1.Hand.Count, hand2 = p2.Hand.Count
            });
        }

        private void OnGameOver(IPlayer winner) =>
            Finish(winner == session.Player1 ? 1 : winner == session.Player2 ? 2 : 0, ended: true);

        /// <summary>The game was left before its end (back to the menu, the game closed): saved as not ended.</summary>
        public void Abandon() => Finish(0, ended: false);

        /// <summary>Ends a game played to its end without a winner (a headless game called off).</summary>
        public void End(int winner) => Finish(winner, ended: true);

        private void Finish(int winner, bool ended) {
            if (done) return;
            done = true;
            mediator.TurnEnded -= OnTurnEnded;
            mediator.GameOver -= OnGameOver;
            watch.End(winner);
            watch.Detach();

            record.ended = ended;
            record.winner = winner;
            record.days = session.Clock.TurnNumber;
            record.endedUtc = DateTime.UtcNow.ToString("o");
            record.player1.endHealth = session.Player1.Health;
            record.player2.endHealth = session.Player2.Health;
            if (playedBy != null) {
                string now1 = playedBy(session.Player1), now2 = playedBy(session.Player2);
                record.player1.aiTookOver = now1 != record.player1.playedBy;
                record.player2.aiTookOver = now2 != record.player2.playedBy;
            }
            foreach (var entry in ledger.Entries) {
                int side = entry.AI == seatKeys[0] ? 1 : 2;
                foreach (var name in entry.Plays.Keys.OrderBy(n => n)) {
                    var stats = ledger.ForAI(entry.AI, name);
                    if (stats == null) continue;
                    record.cards.Add(new CardRecord {
                        side = side, name = name, faction = CardTextFormatter.FactionName(stats.Faction), creature = stats.IsCreature,
                        plays = stats.Plays, faceDamage = stats.FaceDamage, creatureDamage = stats.CreatureDamage, kills = stats.Kills,
                        assistDamage = stats.AssistDamage, assistKills = stats.AssistKills, openedDamage = stats.OpenedDamage,
                        draws = stats.Draws, sentFor = stats.SentFor, healed = stats.Healed, denied = stats.Denied, friendlyDamage = stats.FriendlyDamage
                    });
                }
            }
            Telemetry.Save(record);
        }
    }
}
