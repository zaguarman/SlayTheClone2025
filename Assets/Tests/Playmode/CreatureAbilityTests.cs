using System.Linq;
using NUnit.Framework;
using static SlayTheClone.Enums;
using static SlayTheClone.Tests.TestCards;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Creature abilities ('Abilities Compendium'): one per creature, switched off (not removed) while abilities are
    /// disabled, and the stat-change Resolution Order (bypass, block, reflect, transform, apply, react).
    /// Runs the real rules without the scene (see <see cref="TestGame"/>).
    /// </summary>
    public class CreatureAbilityTests {
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

        private ICreature With(CreatureAbility ability, Player owner, int slot, int attack = 3, int health = 10, params CardEffect[] effects) {
            var data = game.Creature($"{ability} {owner.Name} {slot}", attack, health, 1, effects);
            data.ability = ability;
            return game.Summon(owner, data, slot);
        }

        // A stat change from one of `by`'s effects (null: a spell from `controller`'s hand)
        private void ChangeAttack(ICreature target, int amount, IPlayer controller, ICreature by = null) =>
            game.Run(new ModifyAction(target, amount, true, false, false, source: by, controller: controller));

        #region Changing how stat changes affect it
        [Test]
        public void Contrarian_ReversesStatChanges_FromEitherSide() {
            var contrarian = With(CreatureAbility.Contrarian, p1, 0);
            ChangeAttack(contrarian, 2, p1);
            Assert.AreEqual(1, contrarian.Attack, "Its owner's buff hurts it");
            ChangeAttack(contrarian, -2, p2);
            Assert.AreEqual(3, contrarian.Attack, "The enemy's debuff helps it");
        }

        [Test]
        public void Extremist_DoublesStatChanges() {
            var extremist = With(CreatureAbility.Extremist, p1, 0);
            ChangeAttack(extremist, 2, p1);
            Assert.AreEqual(7, extremist.Attack);
            ChangeAttack(extremist, -1, p2);
            Assert.AreEqual(5, extremist.Attack);
        }

        [Test]
        public void Unbothered_StopsEnemyDebuffsOnly_AndHealthLossIsntADebuff() {
            var calm = With(CreatureAbility.Unbothered, p1, 0);
            ChangeAttack(calm, -2, p2);
            Assert.AreEqual(3, calm.Attack, "The enemy can't lower its stats");
            ChangeAttack(calm, -1, p1);
            Assert.AreEqual(2, calm.Attack, "Its owner can");
            ChangeAttack(calm, 1, p2);
            Assert.AreEqual(3, calm.Attack, "Enemy buffs still apply");

            game.Run(new ModifyAction(calm, -2, false, true, false, controller: p2));
            Assert.AreEqual(8, calm.Health, "Health Loss isn't a stat change: it goes through");
        }

        [Test]
        public void NoU_BouncesEnemyDebuffs_ToTheirSource_OrTheCastersWeakestCreature_OnlyOnce() {
            var mirror = With(CreatureAbility.NoU, p1, 0);
            var shrinker = With(CreatureAbility.None, p2, 0, health: 10);
            var weakling = With(CreatureAbility.None, p2, 1, health: 2);

            ChangeAttack(mirror, -1, p2, by: shrinker);
            Assert.AreEqual(3, mirror.Attack);
            Assert.AreEqual(2, shrinker.Attack, "Back to the creature that caused it");

            ChangeAttack(mirror, -1, p2); // A spell from hand
            Assert.AreEqual(2, weakling.Attack, "No source creature: the caster's lowest Health creature");

            var otherMirror = With(CreatureAbility.NoU, p2, 2);
            ChangeAttack(mirror, -1, p2, by: otherMirror);
            Assert.AreEqual(2, otherMirror.Attack, "A bounced effect can't bounce again");
            Assert.AreEqual(3, mirror.Attack);
        }

        [Test]
        public void Spite_GainsTwoAttack_WhenAnEnemyEffectLowersItsStats() {
            var spiteful = With(CreatureAbility.Spite, p1, 0);
            ChangeAttack(spiteful, -1, p2);
            Assert.AreEqual(4, spiteful.Attack, "-1, then +2");
            ChangeAttack(spiteful, -1, p1);
            Assert.AreEqual(3, spiteful.Attack, "Not from its own side");
        }

        [Test]
        public void ZeroDay_IgnoresDefensiveAbilities_ButNotUnpatchable() {
            var calm = With(CreatureAbility.Unbothered, p1, 0);
            var unpatchable = With(CreatureAbility.Unpatchable, p1, 1);
            var hacker = With(CreatureAbility.ZeroDay, p2, 0);

            ChangeAttack(calm, -2, p2, by: hacker);
            Assert.AreEqual(1, calm.Attack, "Zero-Day bypasses Unbothered");
            ChangeAttack(unpatchable, -2, p2, by: hacker);
            Assert.AreEqual(3, unpatchable.Attack, "...but not Unpatchable");
        }
        #endregion

        #region Combat and damage
        [Test]
        public void Underground_IgnoresTheAttackersBuffs_WhenAttacked() {
            var mole = With(CreatureAbility.Underground, p1, 0, attack: 0);
            var brute = With(CreatureAbility.None, p2, 0, attack: 2);
            ChangeAttack(brute, 3, p2);
            game.ModifierManager.ChangeBaseStats(brute, 1, 0, 0); // A Persistent Stat Modification still counts

            game.Attack(brute, p1.Battlefield[0]);

            Assert.AreEqual(7, mole.Health, "Base 2 + persistent 1; the +3 buff is ignored");
        }

        [Test]
        public void Technician_DoublesHitsOf3OrLess() {
            var tech = With(CreatureAbility.Technician, p1, 0, attack: 3);
            var target = With(CreatureAbility.None, p2, 0, health: 20);
            game.Attack(tech, p2.Battlefield[0]);
            Assert.AreEqual(14, target.Health, "3 becomes 6");

            ChangeAttack(tech, 1, p1);
            game.Attack(tech, p2.Battlefield[0]);
            Assert.AreEqual(10, target.Health, "4 is too much to double");
        }

        [Test]
        public void InsiderJob_IgnoresArmor() {
            var insider = With(CreatureAbility.InsiderJob, p1, 0, attack: 3);
            var guard = With(CreatureAbility.None, p2, 0, health: 10);
            game.Run(new ModifyArmorAction(guard, 5));

            game.Attack(insider, p2.Battlefield[0]);

            Assert.AreEqual(7, guard.Health);
            Assert.AreEqual(5, guard.CurrentArmorPool, "The Armor is untouched");
        }

        [Test]
        public void TooMainstream_IgnoresEffectsOnSeveralCreatures_ButNotOnesAimedAtItAlone() {
            var hipster = With(CreatureAbility.TooMainstream, p2, 0);
            var normie = With(CreatureAbility.None, p2, 1);
            var bomb = game.GiveCard(p1, game.Spell("Bomb", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            game.Play(bomb, p1, null);
            game.Resolve();
            Assert.AreEqual(10, hipster.Health, "Every enemy creature: too mainstream");
            Assert.AreEqual(8, normie.Health);

            var zap = game.GiveCard(p1, game.Spell("Zap", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures)));
            game.Play(zap, p1, p2.Battlefield[0]);
            game.Resolve();
            Assert.AreEqual(8, hipster.Health, "Aimed at it alone");
        }
        #endregion

        #region Statuses, copying and switching off
        [Test]
        public void ReturnToSender_BouncesEnemyStatuses_BackToTheCreatureThatSentThem() {
            var bouncer = With(CreatureAbility.ReturnToSender, p1, 0);
            var stunner = With(CreatureAbility.None, p2, 0);

            game.Run(new ApplyStatusEffectAction(bouncer, StatusEffectType.Exhausted, 1, 0, source: stunner, controller: p2));

            Assert.IsFalse(game.ModifierManager.HasStatusEffect(bouncer, StatusEffectType.Exhausted));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(stunner, StatusEffectType.Exhausted));

            game.Run(new ApplyStatusEffectAction(bouncer, StatusEffectType.Burned, 1, 1, controller: p1));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(bouncer, StatusEffectType.Burned), "Friendly statuses apply normally");
        }

        [Test]
        public void HiveMind_SpreadsToWhoeverAttacksIt_AndStaysOnTheCardUntilTheReshuffle() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            With(CreatureAbility.HiveMind, p1, 0, attack: 0, health: 30);
            var attacker = With(CreatureAbility.Technician, p2, 0, attack: 2, health: 2);

            game.Attack(attacker, p1.Battlefield[0]);
            Assert.AreEqual(CreatureAbility.HiveMind, attacker.Ability, "It lost Technician");

            game.Run(new DestroyCreatureAction(attacker, unconditional: true));
            var discarded = (ICreature)p2.Deck.GetDiscardPilePreview().Single(c => c.Name == attacker.Name);
            Assert.AreEqual(CreatureAbility.HiveMind, discarded.Ability, "A changed ability stays on the card");
            game.CardDealingService.RecycleDiscardPile(p2);
            var reshuffled = (ICreature)p2.Deck.GetDeckPreview().Single(c => c.Name == attacker.Name);
            Assert.AreEqual(CreatureAbility.Technician, reshuffled.Ability, "Reshuffling gives it its printed ability back");
        }

        [Test]
        public void SignalJammer_SwitchesOffTheAbilityOfTheEnemyOpposite_WhileBothStayInTheLane() {
            var calm = With(CreatureAbility.Unbothered, p1, 0);
            With(CreatureAbility.SignalJammer, p2, 0);
            Assert.IsFalse(Abilities.IsActive(calm));
            Assert.AreEqual(CreatureAbility.Unbothered, calm.Ability, "Switched off, not removed");

            ChangeAttack(calm, -1, p2);
            Assert.AreEqual(2, calm.Attack, "Its Unbothered is off");

            game.Run(new MoveCreatureAction(calm, p1.Battlefield[0], p1.Battlefield[1], p1));
            ChangeAttack(calm, -1, p2);
            Assert.AreEqual(2, calm.Attack, "Out of the lane, it's back on");
        }

        [Test]
        public void Exhausted_SwitchesAbilitiesOff_WhileItLasts() {
            var contrarian = With(CreatureAbility.Contrarian, p1, 0);
            game.Run(new ApplyStatusEffectAction(contrarian, StatusEffectType.Exhausted, 1, 0));
            ChangeAttack(contrarian, 2, p1);
            Assert.AreEqual(5, contrarian.Attack, "Abilities Disabled: the buff isn't reversed");
        }

        [Test]
        public void Copypasta_CopiesTheAbilityOfTheCreatureOpposite_WhenReady_OrAtALaterDawn() {
            var copycat = With(CreatureAbility.Copypasta, p1, 0);
            Assert.AreEqual(CreatureAbility.Copypasta, copycat.Ability, "Nobody opposite yet");

            With(CreatureAbility.Extremist, p2, 0);
            game.StartTurn();
            Assert.AreEqual(CreatureAbility.Extremist, copycat.Ability, "Copied at Dawn, replacing Copypasta");
        }

        [Test]
        public void AbilitiesArePrintedOnTheCard() {
            var data = game.Creature("Contrarian Guy", 1, 1);
            data.ability = CreatureAbility.Contrarian;
            StringAssert.StartsWith("<b>Contrarian</b> (stat changes on it are reversed).", CardTextFormatter.GetRulesText(data));
        }
        #endregion
    }
}
