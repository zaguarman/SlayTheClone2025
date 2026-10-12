using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// The order a turn resolves in: by kind of action, then by speed, with effects triggered along the way
    /// resolving in the same turn. Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class ResolutionOrderTests {
        private TestGame game;
        private Player p1;
        private Player p2;

        [SetUp]
        public void SetUp() {
            game = new TestGame();
            p1 = game.Player1;
            p2 = game.Player2;
        }

        [TearDown]
        public void TearDown() => game.Dispose();

        [Test]
        public void ActionsResolveByKind_SummonsMovesPlaysCombatDamageDiscardsThenDraws() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            var walker = game.Summon(p1, game.Creature("Walker", 1, 3), 0);
            var mover = game.Summon(p1, game.Creature("Mover", 1, 3), 2); // A creature has one intent a Day, so another one moves
            var played = game.GiveCard(p1, game.Creature("Played", 1, 1));
            var summoned = (ICreature)CardFactory.CreateCard(game.Creature("Summoned", 1, 1));
            game.History.Clear();

            // Queued in the opposite order
            game.ActionsQueue.AddAction(new DrawCardsAction(p1, 1));
            game.ActionsQueue.AddAction(new DiscardHandAction(p2));
            game.ActionsQueue.AddAction(new DamagePlayerAction(p2, 1));
            game.Declare(walker, p2.Battlefield[4]);
            game.ActionsQueue.AddAction(new PlayCardAction(played, p1, p1.Battlefield[3]));
            game.ActionsQueue.AddAction(new MoveCreatureAction(mover, p1.Battlefield[2], p1.Battlefield[1], p1));
            game.ActionsQueue.AddAction(new SummonCreatureAction(summoned, p2, p2.Battlefield[0]));
            game.Resolve();

            CollectionAssert.AreEqual(new[] {
                "SummonCreature", "MoveCreature",
                "PlayCard", "SummonCreature", // The play summons its creature right away
                "BattlefieldCombat",
                "DamagePlayer", "DamagePlayer", // The damage queued directly, then the attack's
                "DiscardHand", "DrawCards"
            }, game.History.ExecutedKinds());
        }

        /// <summary>A 2-health guard is attacked for 2 the same turn its side gets +2 attack or armor, from a creature entering or a spell.</summary>
        [Test]
        public void HelpPlayedThisTurn_LandsBeforeThisTurnsAttacks() {
            var buff = Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 2, TargetType.FriendlyCreatures);
            var armor = Effect(EffectTrigger.OnPlay, ActionType.Armor, 2, TargetType.FriendlyCreatures);
            foreach (var (help, isSpell, what) in new[] { (buff, false, "attack"), (armor, false, "armor"), (armor, true, "armor") }) {
                using (var g = new TestGame()) {
                    var guard = g.Summon(g.Player1, g.Creature("Guard", 0, 2), 0);
                    var brute = g.Summon(g.Player2, g.Creature("Brute", 2, 3), 0);
                    var helper = g.GiveCard(g.Player1, isSpell
                        ? g.Spell("Shield", TargetType.FriendlyCreatures, help)
                        : g.Creature("Helper", 0, 3, 1, help));

                    g.Declare(brute, g.Player1.Battlefield[0]);
                    g.ActionsQueue.AddAction(new PlayCardAction(helper, g.Player1, g.Player1.Battlefield[isSpell ? 0 : 1]));
                    g.Resolve();

                    Assert.IsFalse(guard.IsDead, $"The {(isSpell ? "spell" : "entering creature")}'s +2 {what} should land before the brute's attack");
                }
            }
        }

        [Test]
        public void EveryKindOfAction_HasAPlaceInTheResolutionOrder() {
            var actionTypes = typeof(IGameAction).Assembly.GetTypes()
                .Where(t => typeof(IGameAction).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in actionTypes) {
                Assert.DoesNotThrow(() => ActionsQueue.PriorityOf(type), $"{type.Name} would be queued with no priority");
            }
        }
    }
}
