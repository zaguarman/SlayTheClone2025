using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>The AI: whom its creatures attack (a part of AIPlanner).</summary>
    public partial class AIPlanner {

        #region Attacks
        private bool CanAttack(SeenCreature attacker) => Abilities.CanAttack(attacker.Creature) && AttackOf(attacker) > 0 && !IsStunned(attacker);

        // Any creature attacks any enemy slot (the formation's reach went 2026-10-17); kept as the one place to say so
        private static bool Reaches(SeenCreature attacker, SeenSlot slot) => true;

        private IEnumerable<SeenSlot> InReach(SeenCreature attacker) => view.TheirSlots;

        // An empty slot of theirs it can hit the player through, if any
        private SeenSlot Open(SeenCreature attacker) => InReach(attacker).FirstOrDefault(s => s.IsEmpty);

        // Easy: the slot in front of it, or the nearest it reaches
        private List<SeenCreature> AttackInFront(List<SeenCreature> ready) {
            foreach (var attacker in ready.Where(CanAttack)) {
                var facing = view.TheirSlots[attacker.Slot.Index];
                var target = Reaches(attacker, facing) ? facing : InReach(attacker).FirstOrDefault();
                if (target != null) AttackSlot(attacker, target, attacker.Attack);
            }
            return ready.Where(c => !CanAttack(c)).ToList();
        }

        // Medium: the strongest first, each where its hit is worth the most: a kill, else the player through an empty slot
        private List<SeenCreature> AttackGreedily(List<SeenCreature> ready) {
            foreach (var attacker in ready.Where(CanAttack)) {
                int damage = attacker.Attack + Abilities.AttackBonus(attacker.Creature);
                float HitValue(SeenSlot slot) {
                    if (slot.Creature is { Planted: true }) return float.MinValue;
                    if (!Assassin(attacker) || slot.Creature == null) return ValueOfHitting(slot, damage);
                    return Hittable(slot.Creature) && HealthLeft(slot.Creature) > 0 ? slot.Creature.Worth + HealthLeft(slot.Creature) - attacker.Worth : -1;
                }
                var target = InReach(attacker).OrderByDescending(HitValue).FirstOrDefault();
                if (target != null) AttackSlot(attacker, target, Assassin(attacker) && target.Creature != null ? 999 : damage);
            }
            return ready.Where(c => !CanAttack(c)).ToList();
        }

        private float ValueOfHitting(SeenSlot slot, int damage) {
            if (slot.Creature is SeenCreature creature) return -CreatureGoodness(creature, ActionType.Damage, damage) * 0.9f; // A tie goes to the player
            return -PlayerGoodness(view.Them, ActionType.Damage, damage);
        }

        /// <summary>
        /// Hard, rebuilt from Medium's core (AI Design.txt, "Hard loses to Medium", 2026-10-17): lethal if it has it; about to
        /// die next Day, it kills the threats (<see cref="AttackByThreat"/>); otherwise it attacks as Medium does, the strongest
        /// first where its hit is worth the most. The by-threat race ("the smallest attacker that kills, the rest at the player")
        /// cost Hard about 25 games in 500 against Medium in a meta where boards win; the levels run measures each piece.
        /// </summary>
        private List<SeenCreature> AttackHard(List<SeenCreature> ready) {
            var left = ready.Where(CanAttack).ToList();
            // Lethal: everyone with an empty slot in reach goes at the player (the user, 2026-10-17: each from where it stands)
            var through = left.Where(a => Open(a) != null).ToList();
            if (through.Count > 0 && through.Sum(DamageOf) >= view.TheirHealth - plannedFace) {
                foreach (var attacker in through) AttackSlot(attacker, Open(attacker), DamageOf(attacker));
                return ready.Where(c => !CanAttack(c)).ToList();
            }
            return TheirDamageNextDay() >= view.MyHealth ? AttackByThreat(ready) : AttackGreedily(ready);
        }

        /// <summary>
        /// The by-threat planner, now Hard's defending mode only: it kills the most threatening creatures, each with the
        /// smallest attacker (or two, for a big threat or when it could die next Day) that does it; the rest go at the player.
        /// It doesn't waste an attack on what can't be hurt (Untouchable, a Shield) and weighs Counterattacks.
        /// </summary>
        private List<SeenCreature> AttackByThreat(List<SeenCreature> ready) {
            var left = ready.Where(CanAttack).ToList();
            var through = left.Where(a => Open(a) != null).ToList();
            bool defending = TheirDamageNextDay() >= view.MyHealth; // Hard: with the creatures of theirs ready by then
            int ours = System.Math.Max(1, view.Mine.Where(c => c.Operative && CanAttack(c)).Sum(DamageOf));
            int theirs = System.Math.Max(1, TheirDamageNextDay());
            float daysToWin = (view.TheirHealth - plannedFace + ours - 1) / ours, daysToLose = (view.MyHealth + theirs - 1) / theirs;
            bool anyOpen = through.Count > 0;
            bool ahead = anyOpen && !defending && daysToWin < daysToLose * Style.Aggression;
            foreach (var target in view.Theirs.Where(t => Hittable(t) && (defending || !Condemned(t))).OrderByDescending(Threat).ToList()) {
                var killer = left.Where(a => Reaches(a, target.Slot) && Hit(a, target) >= HealthLeft(target) && (!Assassin(a) || target.Worth >= 6 || Threat(target) >= 6))
                    .OrderBy(a => Assassin(a) ? 100 : DamageOf(a)).FirstOrDefault();
                var open = killer != null ? Open(killer) : null;
                if (killer != null && open != null) {
                    float face = DamageOf(killer) * Style.Face;
                    // A harmless creature isn't worth an attack that could go at the player instead; racing, only a real threat is
                    if (!defending && (target.Worth + AttackOf(target)) * Style.Removal < face) continue;
                    if (ahead && AttackOf(target) * Style.Removal < face) continue;
                }
                if (killer != null) {
                    AttackSlot(killer, target.Slot, Hit(killer, target));
                    left.Remove(killer);
                    continue;
                }
                if (Threat(target) * Style.Removal < 6 && !defending) continue;
                var pair = (from a in left from b in left
                            where DamageOf(a) <= DamageOf(b) && a != b && Reaches(a, target.Slot) && Reaches(b, target.Slot)
                                  && Hit(a, target) + Hit(b, target) >= HealthLeft(target)
                            orderby DamageOf(a) + DamageOf(b) select (a, b)).FirstOrDefault();
                if (pair.a == null) continue;
                foreach (var attacker in new[] { pair.a, pair.b }) {
                    AttackSlot(attacker, target.Slot, Hit(attacker, target));
                    left.Remove(attacker);
                }
            }

            // The rest at the player; with no way through, at the creature in reach where the damage counts most
            foreach (var attacker in left.ToList()) {
                var chip = view.Theirs.Where(t => Hittable(t) && !Condemned(t) && Reaches(attacker, t.Slot)).OrderByDescending(t => ChipValue(attacker, t)).FirstOrDefault();
                var open = Open(attacker);
                if (open != null) AttackSlot(attacker, open, DamageOf(attacker));
                else if (chip != null && ChipValue(attacker, chip) > 0) AttackSlot(attacker, chip.Slot, Hit(attacker, chip));
                else continue;
                left.Remove(attacker);
            }
            return ready.Where(c => !CanAttack(c)).Concat(left).ToList();
        }

        // What its attack deals, with today's buffs: Recoil's +2, Penny Stock doubling small hits, Double Tap hitting twice
        private int DamageOf(SeenCreature attacker) {
            int damage = AttackOf(attacker) + (attacker.Ability == CreatureAbility.Recoil ? 2 : 0);
            if (attacker.Ability == CreatureAbility.PennyStock && damage <= 3) damage *= 2;
            if (attacker.DealsDouble) damage *= 2; // Ambush, still unseen
            return attacker.Ability == CreatureAbility.DoubleTap ? damage * 2 : damage;
        }

        // Extreme Prejudice: its attack destroys whatever it hits (and then it's gone)
        private static bool Assassin(SeenCreature attacker) => attacker.Ability == CreatureAbility.ExtremePrejudice || attacker.Has(StatusEffectType.Licensed);

        // Its damage to this creature: Exposed adds 1 a hit; Insider Job goes through Armor; Extreme Prejudice kills
        // Sucker Punch holds back against a creature that won't attack: one that can't today isn't worth punching
        private int Hit(SeenCreature attacker, SeenCreature target) => Assassin(attacker) ? 999 :
            attacker.Ability == CreatureAbility.SuckerPunch && !(target.Operative && CanAttack(target)) ? 0 :
            DamageOf(attacker) + (attacker.Ability == CreatureAbility.StripSearch && target.Statuses > 0 ? Abilities.StripBonus : 0) + (IsExposed(target) ? attacker.Ability == CreatureAbility.DoubleTap ? 2 : 1 : 0)
            + (attacker.Ability == CreatureAbility.InsiderJob ? target.Armor : 0);

        private bool Hittable(SeenCreature target) => !IsUntouchable(target) && !target.Slot.Shielded && !target.Has(StatusEffectType.Shielded) && !target.Planted;

        // A hit that doesn't kill: worth the damage to a threat, minus what answers it
        private float ChipValue(SeenCreature attacker, SeenCreature target) {
            float value = System.Math.Min(Hit(attacker, target), HealthLeft(target)) * (AttackOf(target) > 0 ? 0.8f : 0.3f) * Style.Removal;
            if (target.Retaliation > 0) value -= target.Retaliation >= attacker.Health + attacker.Armor ? attacker.Worth : target.Retaliation * 0.5f;
            if (target.Ability == CreatureAbility.HiveMind && attacker.Ability != CreatureAbility.None) value -= 1;
            return value;
        }

        // How dangerous a creature of theirs is: its Attack counts double when it can use it, a known ability adds
        private float Threat(SeenCreature creature) =>
            AttackOf(creature) * (creature.Operative && !IsStunned(creature) ? 2 : 1) + creature.Health * 0.5f
            + (creature.Ability != CreatureAbility.None ? 1.5f : 0);

        // Their creatures that can still hit us next Day, after what we planned today
        private IEnumerable<SeenCreature> TheirAttackers() => view.Theirs.Where(c => c.Operative && !IsStunned(c) && HealthLeft(c) > 0);

        // All their creatures that can act could hit us for this much next Day, if nothing stopped them
        private int TheirPotentialDamage() => TheirAttackers().Sum(AttackOf);

        // The biggest hit one of their creatures could land on this one of ours next Day. Hard: only from where its attackers reach
        // this slot (the formation's reach), the ones ready by then included; Very Hard: the two biggest, as two can gang up on it
        private int ThreatOn(SeenCreature ours) {
            var hits = (Hard ? TheirAttackersOn(ours.Slot.Index) : TheirAttackers()).Select(AttackOf).OrderByDescending(d => d).ToList();
            int biggest = VeryHard ? hits.Take(2).Sum() : hits.FirstOrDefault();
            return biggest > 0 && IsExposed(ours) ? biggest + 1 : biggest;
        }

        private bool WouldDie(SeenCreature ours) => ThreatOn(ours) >= HealthLeft(ours) && !IsUntouchable(ours);

        // Very Hard: one of ours that's gone without help, now or on a later Day: it would die to their attacks, it's Doomed,
        // Brainwashed (it goes over to them), or living on Borrowed Time at 0 Health
        private bool Lost(SeenCreature ours) => WouldDie(ours) || (VeryHard && (ours.Has(StatusEffectType.Doomed)
            || ours.Has(StatusEffectType.Brainwashed) || (ours.Has(StatusEffectType.BorrowedTime) && HealthLeft(ours) <= 0)));

        // Very Hard: one of theirs already on its way out (a Kill Order we gave) or over to us (Brainwashed): attacks and
        // damage on it are wasted
        private bool Condemned(SeenCreature theirs) => VeryHard && !theirs.Ours
            && (theirs.Has(StatusEffectType.KillOrder) || theirs.Has(StatusEffectType.Brainwashed));

        // Very Hard: how much a slow kill (Kill Order, Brainwashed) on this one is worth, as a share of the creature: none if
        // it's going already, little if today's attacks can kill it anyway, all of it if it's too tough for them
        private float SlowKillShare(SeenCreature theirs) {
            if (theirs.Has(StatusEffectType.Doomed) || theirs.Has(StatusEffectType.KillOrder) || theirs.Has(StatusEffectType.Brainwashed)) return 0;
            int reach = view.Mine.Where(c => c.Operative && CanAttack(c)).Select(DamageOf).DefaultIfEmpty(0).Max();
            return HealthLeft(theirs) <= reach ? 0.35f : 1f;
        }

        private void AttackSlot(SeenCreature attacker, SeenSlot target, int damage) {
            Log($"AI: {attacker} attacks {target}", LogTag.Turns);
            combat.HandleCreatureCombat(attacker.Creature, target.Slot);
            if (target.Creature != null) AddDamage(target.Creature, damage);
            else plannedFace += damage;
        }
        #endregion
    }
}
