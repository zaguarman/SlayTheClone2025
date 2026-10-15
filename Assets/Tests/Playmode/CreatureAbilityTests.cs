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

        // An empty board for the next part of a test
        private void Fresh() {
            game.Dispose();
            SetUp();
        }

        private ICreature With(CreatureAbility ability, Player owner, int slot, int attack = 3, int health = 10, params CardEffect[] effects) {
            var data = game.Creature($"{ability} {owner.Name} {slot}", attack, health, 1, effects);
            data.ability = ability;
            return game.Summon(owner, data, slot);
        }

        // A stat change from one of `by`'s effects (null: a spell from `controller`'s hand)
        private void ChangeAttack(ICreature target, int amount, IPlayer controller, ICreature by = null) =>
            game.Run(new ModifyAction(target, amount, true, false, false, source: by, controller: controller));

        [Test]
        public void StatChangeAbilities_Reverse_Double_Block_OrReact_UntilSwitchedOff() {
            var contrarian = With(CreatureAbility.Contrarian, p1, 0);
            ChangeAttack(contrarian, 2, p1);
            Assert.AreEqual(1, contrarian.Attack, "Contrarian: its owner's buff hurts it");
            ChangeAttack(contrarian, -2, p2);
            Assert.AreEqual(3, contrarian.Attack, "...and the enemy's debuff helps it");

            var extremist = With(CreatureAbility.Extremist, p1, 1);
            ChangeAttack(extremist, 2, p1);
            ChangeAttack(extremist, -1, p2);
            Assert.AreEqual(5, extremist.Attack, "Extremist: +4, then -2");

            var calm = With(CreatureAbility.Unbothered, p1, 2);
            ChangeAttack(calm, -2, p2);
            Assert.AreEqual(3, calm.Attack, "Unbothered: the enemy can't lower its stats");
            ChangeAttack(calm, -1, p1);
            ChangeAttack(calm, 1, p2);
            Assert.AreEqual(3, calm.Attack, "Its owner can, and enemy buffs still apply");
            game.Run(new ModifyAction(calm, -2, false, true, false, controller: p2));
            Assert.AreEqual(8, calm.Health, "Health Loss isn't a stat change: it goes through");

            var spiteful = With(CreatureAbility.Spite, p1, 3);
            ChangeAttack(spiteful, -1, p2);
            Assert.AreEqual(4, spiteful.Attack, "Spite: -1, then +2");
            ChangeAttack(spiteful, -1, p1);
            Assert.AreEqual(5, spiteful.Attack, "From its own side too: -1, then +2");

            var unpatchable = With(CreatureAbility.Unpatchable, p1, 4);
            var hacker = With(CreatureAbility.ZeroDay, p2, 0);
            ChangeAttack(calm, -2, p2, by: hacker);
            Assert.AreEqual(1, calm.Attack, "Zero-Day bypasses Unbothered");
            ChangeAttack(unpatchable, -2, p2, by: hacker);
            Assert.AreEqual(3, unpatchable.Attack, "...but not Unpatchable");

            game.Run(new ApplyStatusEffectAction(contrarian, StatusEffectType.Exhausted, 1, 0));
            ChangeAttack(contrarian, 2, p1);
            Assert.AreEqual(5, contrarian.Attack, "Exhausted switches abilities off: the buff isn't reversed");
            Assert.AreEqual(CreatureAbility.Contrarian, contrarian.Ability, "Switched off, not removed");

            var printed = game.Creature("Contrarian Guy", 1, 1);
            printed.ability = CreatureAbility.Contrarian;
            string text = CardTextFormatter.GetRulesText(printed);
            Assert.AreEqual("Contrarian", Keywords.Plain(text), "The card prints the ability's name...");
            CollectionAssert.Contains(Keywords.LinksIn(text), "Contrarian", "...as a keyword whose tooltip explains it");
        }

        [Test]
        public void NoU_DeflectsEnemyEffectsAtRandom_ReturnToSender_BouncesStatusesToTheirSource() {
            var mirror = With(CreatureAbility.NoU, p1, 0);
            var shrinker = With(CreatureAbility.None, p2, 0, health: 10);
            var weakling = With(CreatureAbility.None, p2, 1, health: 2);
            game.Run(new ApplyStatusEffectAction(weakling, StatusEffectType.HeatSignature, 1, 1)); // Draws the random pick

            ChangeAttack(mirror, -1, p2, by: shrinker);
            Assert.AreEqual(3, mirror.Attack);
            Assert.AreEqual(3, shrinker.Attack, "No U deflects it to a random enemy creature, not to its source...");
            Assert.AreEqual(2, weakling.Attack, "...the one with the most Heat Signature first");
            ChangeAttack(mirror, 2, p2, by: shrinker);
            Assert.AreEqual(3, mirror.Attack, "Raises too: every enemy stat change");
            Assert.AreEqual(4, weakling.Attack);
            game.Run(new ApplyStatusEffectAction(mirror, StatusEffectType.Exhausted, 1, 0, source: shrinker, controller: p2));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(mirror, StatusEffectType.Exhausted));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(weakling, StatusEffectType.Exhausted), "...and statuses");
            var otherMirror = With(CreatureAbility.NoU, p2, 2);
            game.Run(new ApplyStatusEffectAction(otherMirror, StatusEffectType.HeatSignature, 1, 5));
            ChangeAttack(mirror, -1, p2, by: otherMirror);
            Assert.AreEqual(2, otherMirror.Attack, "A deflected effect can't be deflected again");
            Assert.AreEqual(3, mirror.Attack);

            var bouncer = With(CreatureAbility.ReturnToSender, p1, 1);
            game.Run(new ApplyStatusEffectAction(bouncer, StatusEffectType.Exhausted, 1, 0, source: shrinker, controller: p2));
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(bouncer, StatusEffectType.Exhausted));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(shrinker, StatusEffectType.Exhausted), "Return to Sender: the status bounces");
            game.Run(new ApplyStatusEffectAction(bouncer, StatusEffectType.Revealed, 1, 0, controller: p2)); // A spell from hand
            Assert.IsFalse(game.ModifierManager.HasStatusEffect(bouncer, StatusEffectType.Revealed));
            Assert.AreEqual(1, p2.Battlefield.Count(s => s.OccupyingCreature != null && game.ModifierManager.HasStatusEffect(s.OccupyingCreature, StatusEffectType.Revealed)),
                "From a spell, it bounces to a random enemy creature");
            game.Run(new ApplyStatusEffectAction(bouncer, StatusEffectType.Burned, 1, 1, controller: p1));
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(bouncer, StatusEffectType.Burned), "Friendly statuses apply normally");
        }

        [Test]
        public void CombatAbilities_BendTheDamage_SpreadTheHit_OrHandItOver() {
            // False Flag Operation: the creature it destroys rallies the others of its side only (+2 Attack)
            using (var g = new TestGame()) {
                var victim = g.Summon(g.Player2, g.Creature("Victim", 1, 3), 0);
                var ally = g.Summon(g.Player2, g.Creature("Ally", 1, 3), 2);
                var mine = g.Summon(g.Player1, g.Creature("Mine", 1, 3), 1);
                var destroy = TestCards.Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.AllCreatures);
                var rally = TestCards.Effect(EffectTrigger.OnPlay, ActionType.ModifyStat, 2, TargetType.AllCreatures, TargetModifier.AlliesOfTarget);
                rally.actions[0].modifyAttack = true;
                rally.actions[0].modifyHealth = false;
                var flagData = g.Spell("False Flag", TargetType.AllCreatures, destroy, rally);
                StringAssert.Contains("The other creatures on its side +2 Attack", Keywords.Plain(CardTextFormatter.GetRulesText(flagData)));
                g.Run(new PlayCardAction(g.GiveCard(g.Player1, flagData), g.Player1, g.Player2.Battlefield[0]));
                Assert.IsTrue(victim.IsDead, "Destroyed...");
                Assert.AreEqual(3, ally.Attack, "...and the others on its side rally...");
                Assert.AreEqual(1, mine.Attack, "...not the other side");
            }

            var mole = With(CreatureAbility.Underground, p1, 0, attack: 0);
            var brute = With(CreatureAbility.None, p2, 0, attack: 2);
            ChangeAttack(brute, 3, p2);
            game.ModifierManager.ChangeBaseStats(brute, 1, 0, 0); // A Persistent Stat Modification still counts
            game.Attack(brute, p1.Battlefield[0]);
            Assert.AreEqual(7, mole.Health, "Underground, attacked: base 2 + persistent 1; the +3 buff is ignored");

            var digger = With(CreatureAbility.Underground, p1, 1, attack: 3);
            var buffed = With(CreatureAbility.None, p2, 1, health: 10);
            game.Run(new ModifyAction(buffed, 4, false, true, false, controller: p2)); // 14/14: the buff would soak the hit
            game.Attack(digger, p2.Battlefield[1]);
            Assert.AreEqual(7, buffed.Health, "Underground, attacking: as if the Health buff weren't there");

            var tech = With(CreatureAbility.PennyStock, p1, 2, attack: 3);
            var target = With(CreatureAbility.None, p2, 2, health: 20);
            game.Attack(tech, p2.Battlefield[2]);
            Assert.AreEqual(14, target.Health, "Penny Stock: 3 becomes 6");
            ChangeAttack(tech, 1, p1);
            game.Attack(tech, p2.Battlefield[2]);
            Assert.AreEqual(10, target.Health, "4 is too much to double");

            var insider = With(CreatureAbility.InsiderJob, p1, 3, attack: 3);
            var guard = With(CreatureAbility.None, p2, 3, health: 10);
            game.Run(new ModifyArmorAction(guard, 5));
            game.Attack(insider, p2.Battlefield[3]);
            Assert.AreEqual(7, guard.Health, "Insider Job ignores Armor...");
            Assert.AreEqual(5, guard.CurrentArmorPool, "...and leaves it untouched");

            // Ambush: it arrives as ???, and hits double until the other side knows what it is
            var sleeper = With(CreatureAbility.Ambush, p1, 3, attack: 3);
            var mark = With(CreatureAbility.None, p2, 3, health: 20);
            Assert.IsFalse(Intel.KnowsWhatItIs(p2, sleeper), "Ambush: it arrives unknown");
            game.Attack(sleeper, p2.Battlefield[3]);
            Assert.AreEqual(14, mark.Health, "...and while they don't know it, 3 becomes 6");
            game.Run(new ApplyStatusEffectAction(sleeper, StatusEffectType.Revealed, 1, 0, controller: p2));
            game.Attack(sleeper, p2.Battlefield[3]);
            Assert.AreEqual(11, mark.Health, "Revealed, it's known for good: plain damage");

            // Destroy kills outright, Health and Armor or not; this one only a Revealed creature, and Untouchable saves it
            game.Run(new ModifyArmorAction(mark, 5));
            game.Run(new ApplyStatusEffectAction(mark, StatusEffectType.Revealed, 1, 0, controller: p1));
            var hit = game.GiveCard(p1, game.Spell("Hit", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures)));
            ((Spell)hit).Effects[0].actions[0].requires = TargetRequisite.Revealed;
            game.Run(new ApplyStatusEffectAction(mark, StatusEffectType.Untouchable, 1, 0, controller: p2));
            game.Play(hit, p1, p2.Battlefield[3]);
            game.Resolve();
            Assert.IsFalse(mark.IsDead, "Destroy: Untouchable saves it");
            var another = game.Summon(p2, game.Creature("Wall", 0, 30), 3);
            var hitAgain = game.GiveCard(p1, game.Spell("Hit", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures)));
            ((Spell)hitAgain).Effects[0].actions[0].requires = TargetRequisite.Revealed;
            game.Play(hitAgain, p1, p2.Battlefield[3]);
            game.Resolve();
            Assert.IsFalse(another.IsDead, "...and it needs its requisite: this one isn't Revealed");
            game.Run(new ApplyStatusEffectAction(another, StatusEffectType.Revealed, 1, 0, controller: p1));
            var hitThird = game.GiveCard(p1, game.Spell("Hit", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures)));
            ((Spell)hitThird).Effects[0].actions[0].requires = TargetRequisite.Revealed;
            game.Play(hitThird, p1, p2.Battlefield[3]);
            game.Resolve();
            Assert.IsTrue(another.IsDead, "Revealed, its 30 Health don't matter");

            // Extreme Prejudice: its attack destroys what it hits, and then it's gone too
            var hitman = With(CreatureAbility.ExtremePrejudice, p1, 3, attack: 1);
            var bunker = With(CreatureAbility.None, p2, 3, health: 25);
            game.Run(new ModifyArmorAction(bunker, 5));
            game.Attack(hitman, p2.Battlefield[3]);
            Assert.IsTrue(bunker.IsDead, "Extreme Prejudice: Health and Armor don't save it...");
            Assert.IsTrue(hitman.IsDead, "...and the assassin is gone too");
            var guarded = With(CreatureAbility.None, p2, 3, health: 2);
            game.Run(new ApplyStatusEffectAction(guarded, StatusEffectType.Untouchable, 1, 0, controller: p2));
            var second = With(CreatureAbility.ExtremePrejudice, p1, 3, attack: 1);
            game.Attack(second, p2.Battlefield[3]);
            Assert.IsFalse(guarded.IsDead, "Untouchable stops the hit");
            Assert.IsFalse(second.IsDead, "...and the assassin lives to try again");

            var hipster = With(CreatureAbility.TooMainstream, p2, 4);
            var bomb = game.GiveCard(p1, game.Spell("Bomb", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.AllSameTypeTargets)));
            game.Play(bomb, p1, null);
            game.Resolve();
            Assert.AreEqual(10, hipster.Health, "Too Mainstream: every enemy creature is too mainstream");
            Assert.AreEqual(8, target.Health);
            var zap = game.GiveCard(p1, game.Spell("Zap", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Damage, 2, TargetType.EnemyCreatures)));
            game.Play(zap, p1, p2.Battlefield[4]);
            game.Resolve();
            Assert.AreEqual(8, hipster.Health, "Aimed at it alone, it lands");

            var smoker = With(CreatureAbility.Recoil, p1, 4, attack: 2, health: 5);
            game.Attack(smoker, p2.Battlefield[4]);
            Assert.AreEqual(4, hipster.Health, "Recoil: its attack hits for 2 more...");
            Assert.AreEqual(4, smoker.Health, "...and costs it 1 damage");

            var shift = With(CreatureAbility.Handover, p1, 4, attack: 1, health: 4);
            ChangeAttack(shift, 2, p1);
            game.ModifierManager.ChangeBaseStats(shift, 0, 1, 0);
            game.Run(new ApplyStatusEffectAction(shift, StatusEffectType.Burned, 2, 1, controller: p2));
            var nextShift = With(CreatureAbility.None, p1, 4, attack: 1, health: 4);
            Assert.AreEqual(3, nextShift.Attack, "Handover: the creature that replaces it takes over its stat changes...");
            Assert.AreEqual(5, nextShift.MaxHealth, "...its Persistent Stat Modifications...");
            Assert.IsTrue(game.ModifierManager.HasStatusEffect(nextShift, StatusEffectType.Burned), "...and its statuses");
            Assert.AreEqual(4, shift.BaseHealth, "Passed on: its own card is back to what it prints");

            Fresh();
            var sprayer = With(CreatureAbility.SprayAndPray, p1, 1, attack: 2);
            var left = With(CreatureAbility.None, p2, 0);
            var middle = With(CreatureAbility.None, p2, 1);
            var right = With(CreatureAbility.None, p2, 2);
            var far = With(CreatureAbility.None, p2, 4);
            game.Attack(sprayer, p2.Battlefield[1]);
            Assert.AreEqual(new[] { 8, 8, 8, 10 }, new[] { left.Health, middle.Health, right.Health, far.Health },
                "Spray and Pray: the target (the left Flank) and the creatures a step away (the Vanguard, the right Flank), not the far Headquarters");

            var tapper = With(CreatureAbility.DoubleTap, p1, 3, attack: 2);
            game.Attack(tapper, p2.Battlefield[4]);
            Assert.AreEqual(6, far.Health, "Double Tap: it hits twice");

            var chainer = With(CreatureAbility.ChainMail, p1, 0, attack: 4);
            game.Attack(chainer, p2.Battlefield[0]);
            // From the Vanguard the chain jumps to the Flank that leads on further (the right one, beside the far Headquarters),
            // then to one of the two a step from there, for 1
            Assert.AreEqual((4, 6), (left.Health, right.Health), "Chain Mail: 4, then the next creature a step away for half (2)...");
            Assert.AreEqual(8 + 6 - 1, middle.Health + far.Health, "...then the next one for half again (1)");

            // On the Brink (Utopia's accelerationism): at 2 Health or less, everything it deals is doubled
            var daredevil = With(CreatureAbility.Brink, p1, 4, attack: 3, health: 5);
            var dummy = With(CreatureAbility.None, p2, 4, health: 30);
            game.Attack(daredevil, p2.Battlefield[4]);
            Assert.AreEqual(27, dummy.Health, "On the Brink, still healthy: plain damage");
            game.Run(new DamageCreatureAction(daredevil, 3));
            game.Attack(daredevil, p2.Battlefield[4]);
            Assert.AreEqual(21, dummy.Health, "...at 2 Health, 3 becomes 6");

            // Collateral Damage: what its attack has left once the creature's Armor and Health are gone hits that creature's player
            var executive = With(CreatureAbility.CollateralDamage, p1, 0, attack: 7);
            game.Run(new ModifyArmorAction(left, 1));
            int before = p2.Health;
            game.Attack(executive, p2.Battlefield[0]);
            Assert.IsTrue(left.IsDead, "Collateral Damage: 1 Armor and 4 Health take 5 of its 7...");
            Assert.AreEqual(before - 2, p2.Health, "...and the 2 left go on to their player");

            // False Flag Operation: one of yours dies, the rest grow. Its Last Will still happens: Dead Man's Switch destroys
            // their strongest, whatever killed it
            Fresh();
            var deadMan = game.Summon(p1, game.Creature("Dead Man", 1, 2, 1,
                Effect(EffectTrigger.OnDeath, ActionType.Destroy, 0, TargetType.EnemyCreatures, TargetModifier.Strongest)), 0);
            var buddy = With(CreatureAbility.None, p1, 1, attack: 1, health: 1);
            var small = With(CreatureAbility.None, p2, 0, attack: 2);
            var big = With(CreatureAbility.None, p2, 1, attack: 6);
            var flag = game.Spell("False Flag", TargetType.FriendlyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.FriendlyCreatures));
            flag.effects[0].actions.Add(new EffectAction { actionType = ActionType.ModifyStat, value = 3, targetType = TargetType.FriendlyCreatures,
                targetModifier = TargetModifier.AllSameTypeTargets, modifyAttack = true, modifyHealth = true });
            game.Play(game.GiveCard(p1, flag), p1, p1.Battlefield[0]);
            game.Resolve();
            Assert.IsTrue(deadMan.IsDead, "False Flag: the target is destroyed...");
            Assert.AreEqual((4, 4), (buddy.Attack, buddy.MaxHealth), "...and the others get +3/+3");
            Assert.IsTrue(big.IsDead, "Dead Man's Switch: its Last Will destroys the strongest enemy creature");
            Assert.IsFalse(small.IsDead);

            // Fell Out of a Window: the strongest dies only if a slot beside it is empty
            var wall = With(CreatureAbility.None, p2, 1, attack: 5);
            var filler = With(CreatureAbility.None, p2, 2, attack: 1);
            With(CreatureAbility.None, p2, 3, attack: 1); // The left Flank's neighbours: the Vanguard, the right Flank, the left Headquarters
            var window = game.Spell("Window", TargetType.EnemyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures, TargetModifier.Strongest));
            window.effects[0].actions[0].requires = TargetRequisite.NextToEmptySlot;
            game.Play(game.GiveCard(p1, window), p1, null);
            game.Resolve();
            Assert.IsFalse(wall.IsDead, "No empty slot beside the strongest: no window");
            game.Run(new DestroyCreatureAction(filler, unconditional: true));
            game.Play(game.GiveCard(p1, window), p1, null);
            game.Resolve();
            Assert.IsTrue(wall.IsDead, "A slot beside it is free: out it goes");

            // MK Ultra Test Subject: each Dawn it attacks a random creature, on either side, never itself
            var subject = game.Summon(p1, game.Creature("Subject", 4, 3, 1,
                Effect(EffectTrigger.StartOfTurn, ActionType.Attack, 0, TargetType.AllCreatures, TargetModifier.Random)), 2);
            game.StartTurn();
            Assert.AreEqual(4, (4 - buddy.Health) + (10 - small.Health), "At Dawn it hits one of the others for its Attack");
            Assert.AreEqual(3, subject.Health);

            // When Revealed, it strikes back at whoever revealed it; Allergic to Light, it burns each time
            Fresh();
            var keeper = With(CreatureAbility.AllergicToLight, p2, 0, attack: 3, health: 4,
                Effect(EffectTrigger.OnRevealed, ActionType.Damage, 4, TargetType.Enemy));
            var snoop = With(CreatureAbility.None, p1, 0, attack: 1, health: 10);
            game.Run(new ApplyStatusEffectAction(keeper, StatusEffectType.Revealed, 2, 0, snoop, p1));
            Assert.AreEqual(2, keeper.Health, "Allergic to Light: Revealed, it takes 2 damage");
            Assert.AreEqual(6, snoop.Health, "When Revealed: 4 damage to the creature that revealed it");
            int p1Health = p1.Health;
            game.Run(new RevealAction(p2.Battlefield[0], p1));
            Assert.AreEqual(p1Health - 4, p1.Health, "A Reveal from a spell: its player takes it");
            Assert.IsTrue(keeper.IsDead, "...and the light finishes it");

            // Fall Guy: an enemy reveal or destroy meant for another of its side lands on it instead
            var patsy = With(CreatureAbility.FallGuy, p2, 1, attack: 1, health: 3);
            var boss = With(CreatureAbility.None, p2, 2, attack: 5);
            game.Run(new ApplyStatusEffectAction(boss, StatusEffectType.Revealed, 2, 0, controller: p1));
            Assert.IsTrue(HasStatus(patsy, StatusEffectType.Revealed) && !HasStatus(boss, StatusEffectType.Revealed), "The Lone Gunman is Revealed instead");
            var kill = game.GiveCard(p1, game.Spell("Kill", TargetType.EnemyCreatures, Effect(EffectTrigger.OnPlay, ActionType.Destroy, 0, TargetType.EnemyCreatures)));
            game.Play(kill, p1, p2.Battlefield[2]);
            game.Resolve();
            Assert.IsTrue(patsy.IsDead && !boss.IsDead, "...and destroyed instead");

            // Licensed (Licence to Kill): its next attack on a creature destroys it, and the licence is spent
            Fresh();
            var agent = With(CreatureAbility.None, p1, 0, attack: 1, health: 5);
            var tank = With(CreatureAbility.None, p2, 0, attack: 1, health: 30);
            var henchman = With(CreatureAbility.None, p2, 1, attack: 1, health: 30);
            game.Run(new ApplyStatusEffectAction(agent, StatusEffectType.Licensed, 1, 0));
            game.Attack(agent, p2.Battlefield[0]);
            Assert.IsTrue(tank.IsDead, "Its 30 Health don't matter");
            Assert.IsFalse(HasStatus(agent, StatusEffectType.Licensed), "The licence is spent");
            game.StartTurn();
            game.Attack(agent, p2.Battlefield[1]);
            Assert.AreEqual(29, henchman.Health, "The next attack is a plain one");

            // With Double Tap both hits carry the licence: two Blessings, or one Blessing and the creature
            var doubleO = With(CreatureAbility.DoubleTap, p1, 2, attack: 1, health: 5);
            var saint = With(CreatureAbility.None, p2, 2, attack: 1, health: 30);
            var martyr = With(CreatureAbility.None, p2, 3, attack: 1, health: 30);
            game.Run(new ApplyStatusEffectAction(saint, StatusEffectType.Blessed, 1, 2));
            game.Run(new ApplyStatusEffectAction(martyr, StatusEffectType.Blessed, 1, 1));
            game.Run(new ApplyStatusEffectAction(doubleO, StatusEffectType.Licensed, 1, 0));
            game.Attack(doubleO, p2.Battlefield[2]);
            Assert.IsFalse(saint.IsDead, "Two Blessings take both hits");
            Assert.IsFalse(HasStatus(saint, StatusEffectType.Blessed));
            game.StartTurn();
            var third = With(CreatureAbility.DoubleTap, p1, 3, attack: 1, health: 5);
            game.Run(new ApplyStatusEffectAction(third, StatusEffectType.Licensed, 1, 0));
            game.Attack(third, p2.Battlefield[3]);
            Assert.IsTrue(martyr.IsDead, "One Blessing takes the first hit; the second kills");

            // Femme Fatale: each Dusk, 2 damage to the creature facing her, and she heals 1
            var fatale = game.Summon(p1, game.Creature("Fatale", 2, 4, 1,
                Effect(EffectTrigger.EndOfTurn, ActionType.Damage, 2, TargetType.EnemyCreatures, TargetModifier.Opposite),
                Effect(EffectTrigger.EndOfTurn, ActionType.Heal, 1, TargetType.Self)), 1);
            game.Run(TargetedActions.Damage(fatale, 2));
            game.StartTurn();
            Assert.AreEqual(27, henchman.Health, "Squeezed: the one facing her");
            Assert.AreEqual(3, fatale.Health);
        }

        [Test]
        public void OtherCreatures_Copied_Stolen_Swapped_Guarded_AndAbilitiesSpread() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            With(CreatureAbility.HiveMind, p1, 0, attack: 0, health: 30);
            var attacker = With(CreatureAbility.PennyStock, p2, 0, attack: 2, health: 2);
            game.Attack(attacker, p1.Battlefield[0]);
            Assert.AreEqual(CreatureAbility.HiveMind, attacker.Ability, "Hive Mind spreads to its attacker, replacing PennyStock");
            game.Run(new DestroyCreatureAction(attacker, unconditional: true));
            var discarded = (ICreature)p2.Deck.GetDiscardPilePreview().Single(c => c.Name == attacker.Name);
            Assert.AreEqual(CreatureAbility.HiveMind, discarded.Ability, "A changed ability stays on the card");
            game.CardDealingService.RecycleDiscardPile(p2);
            var reshuffled = (ICreature)p2.Deck.GetDeckPreview().Single(c => c.Name == attacker.Name);
            Assert.AreEqual(CreatureAbility.PennyStock, reshuffled.Ability, "Reshuffling gives it its printed ability back");

            var thief = With(CreatureAbility.IdentityTheft, p1, 1);
            var mark = With(CreatureAbility.Unpatchable, p2, 2);
            game.Declare(thief, p2.Battlefield[1]); // Planned an attack first
            game.ActionsQueue.AddAction(new UseAbilityAction(thief, p2.Battlefield[2])); // Replaces it: one action a Day
            game.Resolve();
            Assert.AreEqual(CreatureAbility.Unpatchable, thief.Ability, "Identity Theft swaps abilities");
            Assert.AreEqual(CreatureAbility.IdentityTheft, mark.Ability, "The target gets Identity Theft and can use it in turn");
            Assert.AreEqual(Player.MaxHealth, p2.Health, "Using it replaced the planned attack");

            var calm = With(CreatureAbility.Unbothered, p1, 3);
            With(CreatureAbility.SignalJammer, p2, 3);
            ChangeAttack(calm, -1, p2);
            Assert.AreEqual(2, calm.Attack, "Signal Jammer: the ability of the enemy opposite is off");
            game.Run(new MoveCreatureAction(calm, p1.Battlefield[3], p1.Battlefield[4], p1));
            ChangeAttack(calm, -1, p2);
            Assert.AreEqual(2, calm.Attack, "Out of the lane, it's back on");

            Fresh();
            var copycat = With(CreatureAbility.Copypasta, p1, 0);
            Assert.AreEqual(CreatureAbility.Copypasta, copycat.Ability, "Copypasta: nobody opposite yet");
            With(CreatureAbility.Extremist, p2, 0);
            game.StartTurn();
            Assert.AreEqual(CreatureAbility.Extremist, copycat.Ability, "Copied at Dawn, replacing Copypasta");

            Fresh();
            var wannabe = With(CreatureAbility.Wannabe, p1, 0, attack: 1, health: 3);
            var star = With(CreatureAbility.None, p2, 0, attack: 6, health: 9);
            game.Run(new DamageCreatureAction(star, 2));
            game.Run(new UseAbilityAction(wannabe, p2.Battlefield[0]));
            Assert.AreEqual((6, 9, 7), (wannabe.Attack, wannabe.MaxHealth, wannabe.Health), "Wannabe: a copy of its Attack and Health, damage included");

            var grabber = With(CreatureAbility.CreditGrab, p1, 1, attack: 1, health: 3);
            var victim = With(CreatureAbility.None, p2, 1, attack: 2, health: 5);
            ChangeAttack(victim, 3, p2);
            game.ModifierManager.ChangeBaseStats(victim, 0, 2, 0);
            ChangeAttack(victim, -1, p1);
            game.Run(new UseAbilityAction(grabber, p2.Battlefield[1]));
            Assert.AreEqual((4, 5), (grabber.Attack, grabber.MaxHealth), "Credit Grab: the +3 Attack and the +2 Health for good move over");
            Assert.AreEqual((1, 5), (victim.Attack, victim.MaxHealth), "The victim keeps only its lowered stat");
            ChangeAttack(victim, 2, p2);
            game.Run(new UseAbilityAction(grabber, p2.Battlefield[1]));
            Assert.AreEqual(4, grabber.Attack, "Once per combat");

            var shell = With(CreatureAbility.ShellGame, p1, 2);
            var first = With(CreatureAbility.None, p2, 3);
            var second = With(CreatureAbility.None, p2, 4, attack: 5);
            game.Run(new UseAbilityAction(shell, p2.Battlefield[3]));
            Assert.AreEqual((second, first), (p2.Battlefield[3].OccupyingCreature, p2.Battlefield[4].OccupyingCreature),
                "Shell Game: the target trades places with its partner in the formation (the other Headquarters slot)");

            // The same effects on spells: a spell works between its target and the creature facing it
            With(CreatureAbility.None, p2, 2, attack: 5); // Facing the shell, now that the swap took the others to Headquarters
            var mimic = Effect(EffectTrigger.OnPlay, ActionType.CopyStats, 0, TargetType.AllCreatures);
            game.Play(game.GiveCard(p1, game.Spell("Mimetic Theory", TargetType.AllCreatures, mimic)), p1, p1.Battlefield[2]);
            var grant = Effect(EffectTrigger.OnPlay, ActionType.GrantAbility, 0, TargetType.FriendlyCreatures);
            grant.actions[0].ability = CreatureAbility.DoubleTap;
            game.Play(game.GiveCard(p1, game.Spell("One for the Road", TargetType.FriendlyCreatures, grant)), p1, p1.Battlefield[2]);
            game.Resolve();
            Assert.AreEqual(5, shell.Attack, "A spell copies the creature facing its target");
            Assert.AreEqual(CreatureAbility.DoubleTap, shell.Ability, "Any ability can come from a spell");

            // Never Let Them Know Your Next Move: the others' stat raises all go to the target
            Fresh();
            var hitman = With(CreatureAbility.None, p1, 0, attack: 1, health: 5);
            var helperA = With(CreatureAbility.None, p1, 1, attack: 1, health: 5);
            var helperB = With(CreatureAbility.None, p1, 2, attack: 1, health: 5);
            ChangeAttack(helperA, 3, p1);
            ChangeAttack(helperB, 2, p1);
            game.Play(game.GiveCard(p1, game.Spell("Funnel", TargetType.FriendlyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Funnel, 0, TargetType.FriendlyCreatures))), p1, p1.Battlefield[0]);
            game.Resolve();
            Assert.AreEqual((6, 1, 1), (hitman.Attack, helperA.Attack, helperB.Attack), "Funnel: every raise on its side goes to the target");

            // Frame Job: the target's harmful statuses go to the enemy creature facing it, with their Days
            var patsy = With(CreatureAbility.None, p2, 0);
            game.Run(new ApplyStatusEffectAction(hitman, StatusEffectType.Revealed, 2, 0, controller: p2));
            game.Run(new ApplyStatusEffectAction(hitman, StatusEffectType.Burned, 2, 1, controller: p2));
            game.Play(game.GiveCard(p1, game.Spell("Frame", TargetType.FriendlyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.PassHarmfulStatuses, 0, TargetType.FriendlyCreatures))), p1, p1.Battlefield[0]);
            game.Resolve();
            Assert.IsFalse(HasStatus(hitman, StatusEffectType.Revealed) || HasStatus(hitman, StatusEffectType.Burned), "Frame Job: the evidence leaves it...");
            Assert.IsTrue(HasStatus(patsy, StatusEffectType.Revealed) && HasStatus(patsy, StatusEffectType.Burned), "...for the creature facing it");

            // Devil's Breath: the target takes the stat raises and every status of the creature facing it (none is good or bad by itself)
            game.Run(new ApplyStatusEffectAction(patsy, StatusEffectType.Blessed, 1, 2, controller: p2));
            ChangeAttack(patsy, 2, p2);
            var breath = game.Spell("Breath", TargetType.FriendlyCreatures, Effect(EffectTrigger.OnPlay, ActionType.StealStatChanges, 0, TargetType.FriendlyCreatures));
            breath.effects[0].actions.Add(new EffectAction { actionType = ActionType.StealGoodStatuses, targetType = TargetType.FriendlyCreatures });
            game.Play(game.GiveCard(p1, breath), p1, p1.Battlefield[0]);
            game.Resolve();
            Assert.AreEqual((8, 3), (hitman.Attack, patsy.Attack), "Devil's Breath: its raises...");
            Assert.IsTrue(HasStatus(hitman, StatusEffectType.Blessed) && !HasStatus(patsy, StatusEffectType.Blessed), "...and its statuses...");
            Assert.IsTrue(HasStatus(hitman, StatusEffectType.Burned) && !HasStatus(patsy, StatusEffectType.Burned), "...all of them, the Burn too");

            // Reflect: the target copies every status of the creature facing it, good and harmful, and that one keeps them
            var mirror = With(CreatureAbility.None, p1, 1);
            var model = With(CreatureAbility.None, p2, 1);
            game.Run(new ApplyStatusEffectAction(model, StatusEffectType.Concealed, 2, 0, controller: p2));
            game.Run(new ApplyStatusEffectAction(model, StatusEffectType.Burned, 2, 1, controller: p1));
            game.Play(game.GiveCard(p1, game.Spell("Reflect", TargetType.AllCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.CopyStatuses, 0, TargetType.AllCreatures))), p1, p1.Battlefield[1]);
            game.Resolve();
            Assert.IsTrue(HasStatus(mirror, StatusEffectType.Concealed) && HasStatus(mirror, StatusEffectType.Burned), "Reflect: both statuses copied...");
            Assert.IsTrue(HasStatus(model, StatusEffectType.Concealed) && HasStatus(model, StatusEffectType.Burned), "...and kept by the one it copied");

            // The strongest takes from the rest: Laissez-Faire on the whole board (theirs is the strongest here), The
            // Invisible Hand on its own side. Each gives what it has, up to 1; Too Mainstream keeps its own
            Fresh();
            var tycoon = With(CreatureAbility.None, p2, 0, attack: 5);
            var hipster = With(CreatureAbility.TooMainstream, p2, 1, attack: 3);
            var worker = With(CreatureAbility.None, p1, 0, attack: 2);
            var broke = With(CreatureAbility.None, p1, 1, attack: 0);
            var foreman = With(CreatureAbility.None, p1, 2, attack: 4);
            game.Play(game.GiveCard(p1, game.Spell("Laissez-Faire", TargetType.AllCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Consolidate, 1, TargetType.AllCreatures, TargetModifier.Strongest))), p1, null);
            game.Resolve();
            Assert.AreEqual((7, 3, 1, 0, 3), (tycoon.Attack, hipster.Attack, worker.Attack, broke.Attack, foreman.Attack), "Laissez-Faire: the strongest on the board takes 1 from each other creature");
            game.Play(game.GiveCard(p1, game.Spell("Invisible Hand", TargetType.FriendlyCreatures,
                Effect(EffectTrigger.OnPlay, ActionType.Consolidate, 1, TargetType.FriendlyCreatures, TargetModifier.Strongest))), p1, null);
            game.Resolve();
            Assert.AreEqual((7, 0, 4), (tycoon.Attack, worker.Attack, foreman.Attack), "On its own side: your strongest takes from yours alone");

            Fresh();
            var dome = With(CreatureAbility.Bodyguard, p1, 0);
            var vip = With(CreatureAbility.None, p1, 1, health: 5);
            var raider = With(CreatureAbility.None, p2, 1, attack: 4, health: 10);
            var safe = With(CreatureAbility.SafeSpace, p1, 4); // The right Headquarters: beside it, the other Headquarters slot and the right Flank, not the guarded one
            var huddled = With(CreatureAbility.None, p1, 3);
            game.Declare(dome, p2.Battlefield[0]);
            Assert.IsNull(game.ActionsQueue.GetActiveAction(dome.TargetId), "Bodyguard: it can't attack");
            game.Declare(raider, p1.Battlefield[1]);
            game.ActionsQueue.AddAction(new UseAbilityAction(dome, p1.Battlefield[1]));
            game.Resolve();
            Assert.AreEqual(5, vip.Health, "Untouchable: no damage. The guard lands with the spells, before the attacks");
            game.StartTurn();
            Assert.IsFalse(HasStatus(vip, StatusEffectType.Untouchable), "Untouchable 1 is this Day only");
            game.Run(new UseAbilityAction(dome, p1.Battlefield[1]));
            Assert.IsFalse(HasStatus(vip, StatusEffectType.Untouchable), "Cooldown: it rests a Day");
            Assert.IsTrue(HasStatus(safe, StatusEffectType.Blessed) && HasStatus(huddled, StatusEffectType.Blessed),
                "Safe Space: at Dawn it and the friendly creatures beside it gain Bless");
            game.StartTurn();
            game.Run(new UseAbilityAction(dome, p1.Battlefield[1]));
            Assert.IsTrue(HasStatus(vip, StatusEffectType.Untouchable), "Rested, it guards again");
        }

        private bool HasStatus(ICreature creature, StatusEffectType status) => game.ModifierManager.HasStatusEffect(creature, status);
    }
}
