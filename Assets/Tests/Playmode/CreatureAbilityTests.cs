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
            var far = With(CreatureAbility.None, p2, 3);
            game.Attack(sprayer, p2.Battlefield[1]);
            Assert.AreEqual(new[] { 8, 8, 8, 10 }, new[] { left.Health, middle.Health, right.Health, far.Health },
                "Spray and Pray: the target and the creatures on both sides, a cone of three");

            var tapper = With(CreatureAbility.DoubleTap, p1, 3, attack: 2);
            game.Attack(tapper, p2.Battlefield[3]);
            Assert.AreEqual(6, far.Health, "Double Tap: it hits twice");

            var chainer = With(CreatureAbility.ChainMail, p1, 0, attack: 4);
            game.Attack(chainer, p2.Battlefield[0]);
            Assert.AreEqual(new[] { 4, 6, 7 }, new[] { left.Health, middle.Health, right.Health },
                "Chain Mail: 4, then the next creatures in line for half each jump (2, then 1)");
        }

        [Test]
        public void OtherCreatures_Copied_Stolen_Swapped_Guarded_AndAbilitiesSpread() {
            game.SetDecks(game.Fillers(5), game.Fillers(5));
            With(CreatureAbility.HiveMind, p1, 0, attack: 0, health: 30);
            var attacker = With(CreatureAbility.PennyStock, p2, 0, attack: 2, health: 2);
            game.Attack(attacker, p1.Battlefield[0]);
            Assert.AreEqual(CreatureAbility.HiveMind, attacker.Ability, "Hive Mind spreads to its attacker, replacing Penny Stock");
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
            var first = With(CreatureAbility.None, p2, 2);
            var second = With(CreatureAbility.None, p2, 3, attack: 5);
            game.Run(new UseAbilityAction(shell, p2.Battlefield[2]));
            Assert.AreEqual((second, first), (p2.Battlefield[2].OccupyingCreature, p2.Battlefield[3].OccupyingCreature),
                "Shell Game: the target trades places with the one to its right");

            // The same effects on spells: a spell works between its target and the creature facing it
            var mimic = Effect(EffectTrigger.OnPlay, ActionType.CopyStats, 0, TargetType.AllCreatures);
            game.Play(game.GiveCard(p1, game.Spell("Mimetic Theory", TargetType.AllCreatures, mimic)), p1, p1.Battlefield[2]);
            var grant = Effect(EffectTrigger.OnPlay, ActionType.GrantAbility, 0, TargetType.FriendlyCreatures);
            grant.actions[0].ability = CreatureAbility.DoubleTap;
            game.Play(game.GiveCard(p1, game.Spell("One for the Road", TargetType.FriendlyCreatures, grant)), p1, p1.Battlefield[2]);
            game.Resolve();
            Assert.AreEqual(5, shell.Attack, "A spell copies the creature facing its target");
            Assert.AreEqual(CreatureAbility.DoubleTap, shell.Ability, "Any ability can come from a spell");

            Fresh();
            var dome = With(CreatureAbility.Bodyguard, p1, 0);
            var vip = With(CreatureAbility.None, p1, 1, health: 5);
            var raider = With(CreatureAbility.None, p2, 1, attack: 4, health: 10);
            var safe = With(CreatureAbility.SafeSpace, p1, 3);
            var huddled = With(CreatureAbility.None, p1, 4);
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
