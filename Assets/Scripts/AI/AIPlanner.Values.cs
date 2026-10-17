using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>The AI: what each action, status, Trend and creature is worth to it (a part of AIPlanner).</summary>
    public partial class AIPlanner {

        #region Values
        /// <summary>
        /// Roughly how much good one effect of a spell does for this side, played at this aim: what it does to each target it
        /// lands on, counted for us on our side and against us on theirs (a random target counts as a share of each it may
        /// pick), each kind of good weighed by the style.
        /// </summary>
        private float Value(EffectAction action, Spell spell, ITarget aim) {
            // Good for whoever plays them, whichever side they look at
            if (action.actionType == ActionType.SetTrend) {
                // A Trend is the same for both sides: worth what it does for us less what it does for them, over its Days
                int days = System.Math.Max(1, action.statusDuration);
                switch (action.trend) {
                    case Trend.SurvivalOfTheFittest: { // The weakest creature on the board is culled: better be theirs
                        var weakest = view.All.OrderBy(c => c.Health).FirstOrDefault();
                        if (weakest == null) return 0.2f;
                        return (weakest.Ours ? -1 : 1) * TrendSystem.CullDamage * days * 0.8f * Style.Removal;
                    }
                    case Trend.VolcanoLair: { // Each side's boss hides: ours is worth hiding if it's the bigger one
                        float Hidden(IEnumerable<SeenCreature> side) => side.Count() >= 2 ? side.Max(c => c.Worth) * 0.4f : 0;
                        return (Hidden(view.Mine) - Hidden(view.Theirs)) * days * Style.Protection + 0.2f;
                    }
                    case Trend.Zeitgeist: // The side with fewer creatures draws more
                        return (view.Mine.Count() < view.Theirs.Count() ? 1.5f : view.Mine.Count() > view.Theirs.Count() ? -1.5f : 0) * days * Style.Cards + 0.2f;
                    case Trend.Casino: // The house's weather: the same for both sides, a gamble; a little worth for the tempo
                        return 0.3f * days;
                    case Trend.RushHour: { // The slowest act first: good for whoever is slower
                        float Speed(IEnumerable<SeenCreature> side) => side.Any() ? (float)side.Average(c => c.Known ? c.Creature.Speed : 1) : 0;
                        return (Speed(view.Theirs) - Speed(view.Mine)) * days + 0.2f;
                    }
                    case Trend.LiminalSpace: // Attack and Health trade places for everyone: good for whoever has the taller creatures
                        return view.All.Sum(c => (c.Ours ? 1 : -1) * (HealthLeft(c) - AttackOf(c))) * 0.4f + 0.2f;
                    default:
                        return 1;
                }
            }
            // Grace: worth a lot when the next hits could finish us, little otherwise
            if (action.actionType == ActionType.Grace) {
                if (view.Me.GraceUntilDay != null) return 0;
                int danger = view.Theirs.Sum(c => AttackOf(c));
                return (view.Me.Health <= danger + 3 ? 6f : 0.3f) * Style.OwnHealth;
            }
            // An Earthquake hits every other slot, on whichever side the zigzag puts it: each creature has an even chance
            if (action.actionType == ActionType.Earthquake) {
                return view.All.Sum(c => 0.5f * (c.Ours ? -1 : 1) * (HealthLeft(c) <= action.value ? c.Worth + HealthLeft(c) : action.value)) * Style.Removal + 0.2f;
            }
            if (action.actionType == ActionType.CreateWatchdog) return Style.Intel;
            if (action.actionType == ActionType.SwapSlotEffects) return SwapValue();
            // Shuffle (a Maneuver): scrambling their formation is worth a little; ours, rarely
            if (action.actionType is ActionType.Shuffle or ActionType.Spin) {
                var side = action.targetType switch {
                    TargetType.Enemy or TargetType.EnemyCreatures => view.Them, TargetType.Player or TargetType.FriendlyCreatures => view.Me,
                    _ => aim as IPlayer ?? (aim as BoardSlot)?.Owner ?? (aim as ICreature)?.Owner ?? view.Them
                };
                int creatures = (side == view.Them ? view.Theirs : view.Mine).Count();
                return creatures < 2 ? 0 : side == view.Them ? 0.4f * creatures * Style.Removal : -0.3f * creatures;
            }
            // Send For: the right card next Day (it picks the best one when it resolves)
            if (action.actionType == ActionType.SendFor) return SendFor.Choices(view.Me, action).Count > 0 ? 2.5f * Style.Cards : 0;
            // GoldenEye: each of their Watchdogs gone is a trap we no longer walk into
            if (action.actionType == ActionType.WipeWatchdogs) return queue.Watchdogs.Count(w => w.Controller == view.Them) * 1.5f * Style.Intel;
            if (action.actionType == ActionType.Reveal) {
                if (!EffectResolver.UsesChosenTarget(action)) return 0.5f * Style.Intel; // A side's Watchdogs
                var seen = CreatureAt(aim);
                float look = (seen != null && !seen.Ours && !seen.Known ? 1 : 0.2f) * Style.Intel; // Or what may lie in the slot
                return seen != null && !seen.Ours ? look - RevealCost(seen) : look;
            }

            var targets = TargetsOf(action, aim);
            bool random = action.targetModifier.HasFlag(TargetModifier.Random);
            float share = random && targets.Count > 0 ? 1f / targets.Count : 1f;
            bool area = !random && targets.Count(t => t is SeenCreature) > 1;
            float value = 0;
            foreach (var target in targets) {
                float good = target switch {
                    SeenSlot slot => SlotGoodness(action, slot),
                    SeenCreature creature => Goodness(action, creature, spell, area),
                    IPlayer player => PlayerGoodness(player, action.actionType, action.value),
                    _ => 0
                };
                if (target is SeenCreature { Planted: true }) continue; // Our Gift on their side: let it work
                bool ours = IsOurs(target);
                value += share * Weight(action, target, ours, good) * (ours ? good : -good);
            }
            return value;
        }

        // How much the style cares for this kind of good (or harm) on this target
        private float Weight(EffectAction action, object target, bool ours, float good) {
            switch (action.actionType) {
                case ActionType.Damage:
                    return target is IPlayer ? ours ? Style.OwnHealth : Style.Face : ours ? Style.Board : Style.Removal;
                case ActionType.Heal or ActionType.Armor or ActionType.Cleanse or ActionType.CleanseSlot:
                    return ours ? Style.Protection : 1;
                case ActionType.ModifyStat:
                    return ours ? Style.Board : good < 0 ? Style.Statuses : 1;
                case ActionType.ApplyStatus:
                    if (action.statusEffectToApply is StatusEffectType.Concealed or StatusEffectType.Encrypted or StatusEffectType.Revealed) return Style.Intel;
                    return ours ? good > 0 ? Style.Protection : Style.Board : Style.Statuses;
                case ActionType.Stun or ActionType.DelayDeployment or ActionType.Rush:
                    return ours ? Style.Board : Style.Statuses;
                case ActionType.PlaceSlotEffect:
                    return ours && good > 0 ? Style.Protection : Style.Statuses;
                case ActionType.Draw or ActionType.CutDraws:
                    return Style.Cards;
                case ActionType.Summon or ActionType.SummonDecoys:
                    return Style.Board;
                default:
                    return 1;
            }
        }

        // What the effect lands on, as EffectResolver picks it (see its SelectTargets), as this side sees it
        private List<object> TargetsOf(EffectAction action, ITarget aim) {
            var targets = new List<object>();
            if (action.targetType == TargetType.Self) return targets; // A spell has no self
            // A side hazard or its sweeper: every slot of the side
            if (action.actionType is ActionType.PlaceSlotEffect or ActionType.CleanseSlot && action.targetModifier.HasFlag(TargetModifier.AllSameTypeTargets)) {
                if (action.targetType is TargetType.FriendlyCreatures or TargetType.Player or TargetType.AllCreatures) targets.AddRange(view.MySlots);
                if (action.targetType is TargetType.EnemyCreatures or TargetType.Enemy or TargetType.AllCreatures) targets.AddRange(view.TheirSlots);
                return targets;
            }
            if (action.targetModifier.HasFlag(TargetModifier.DroppedSide)) { // The creatures of the side it's played on
                bool ours = aim is IPlayer player ? player == view.Me : view.Of(SlotOf(aim))?.Ours ?? true;
                foreach (var slot in ours ? view.MySlots : view.TheirSlots) {
                    if (slot.Creature != null && (action.requires == TargetRequisite.None || Lands(action, slot.Creature))) targets.Add(slot.Creature);
                }
                return targets;
            }
            if (action.targetModifier.HasFlag(TargetModifier.AlliesOfTarget)) { // The other creatures on the aimed side
                var aimed = view.Of(SlotOf(aim));
                if (aimed == null) return targets;
                foreach (var other in aimed.Ours ? view.MySlots : view.TheirSlots) {
                    if (other != aimed && other.Creature != null) targets.Add(other.Creature);
                }
                return targets;
            }
            if (EffectResolver.UsesChosenTarget(action)) {
                var slot = view.Of(SlotOf(aim));
                if (slot == null) return targets;
                if (EffectResolver.IsSlotAction(action.actionType)) {
                    targets.Add(slot);
                    return targets;
                }
                bool beside = action.targetModifier.HasFlag(TargetModifier.Beside); // Its neighbours only
                if (!beside && CreatureAt(aim) is SeenCreature creature) targets.Add(creature);
                else if (EffectResolver.CanAffectPlayers(action.actionType)) targets.Add(slot.Ours ? view.Me : view.Them);
                if (action.targetModifier.HasFlag(TargetModifier.WithAdjacent) || beside) {
                    var row = slot.Ours ? view.MySlots : view.TheirSlots;
                    foreach (int i in Formation.Neighbours(slot.Index)) {
                        if (i >= 0 && i < row.Count && row[i].Creature != null) targets.Add(row[i].Creature);
                    }
                }
                return targets;
            }

            foreach (var target in TargetingSystem.GetValidTargets(view.Me, action.targetType)) {
                if (target is IPlayer player) targets.Add(player);
                else if (view.Of(target) is SeenCreature seen) targets.Add(seen);
            }
            if (action.targetModifier.HasFlag(TargetModifier.Strongest)) {
                var strongest = targets.OfType<SeenCreature>().OrderByDescending(AttackOf).FirstOrDefault();
                return strongest != null ? new List<object> { strongest } : new List<object>();
            }
            // Hard knows Heat draws random effects: random damage goes to the hottest creature it can see, on either side
            // (Heat it gives today counts too: a combo)
            if (Hard && action.targetModifier.HasFlag(TargetModifier.Random)) {
                var pool = action.actionType == ActionType.Damage ? view.All : targets.OfType<SeenCreature>();
                var hottest = pool.Where(c => HeatOf(c) > 0).OrderByDescending(HeatOf).FirstOrDefault();
                if (hottest != null) return new List<object> { hottest };
            }
            return targets;
        }

        // How good the effect is for the creature it lands on (Hard: as its ability and statuses bend it)
        private float Goodness(EffectAction action, SeenCreature creature, Spell spell, bool area) {
            if (Hard && area && creature.Ability == CreatureAbility.TooMainstream) return 0;
            if (action.actionType == ActionType.Damage && action.requires != TargetRequisite.None && !Lands(action, creature)) return 0;
            switch (action.actionType) {
                case ActionType.ModifyStat:
                    return StatChangeGoodness(action, creature);
                case ActionType.Destroy:
                    if (!Lands(action, creature)) return 0;
                    // Hard: a Fall Guy of theirs takes it instead
                    var patsy = Hard && !creature.Ours ? view.Theirs.FirstOrDefault(c => c != creature && c.Ability == CreatureAbility.FallGuy) : null;
                    if (patsy != null) creature = patsy;
                    if (HealthLeft(creature) <= 0) return -0.1f; // Already dead to what's planned: wasted
                    if (creature.Has(StatusEffectType.Blessed)) return -1f; // Its Blessing takes it: one stack off
                    // One of ours with a Last Will (Dead Man's Switch): its death does something for us
                    return -(creature.Worth + HealthLeft(creature)) + (Hard && creature.Ours ? LastWillValue(creature) : 0);
                case ActionType.TakeControl: {
                    if (!Lands(action, creature) || creature.Ours || HealthLeft(creature) <= 0) return 0;
                    // They lose it and we gain it; whatever of ours stands facing it is discarded
                    var facing = view.MySlots[creature.Slot.Index].Creature;
                    return -2 * creature.Worth + (facing?.Worth ?? 0);
                }
                case ActionType.ApplyStatus:
                    if (Hard && !creature.Ours && (creature.Ability is CreatureAbility.NoU or CreatureAbility.ReturnToSender)) {
                        return -StatusGoodness(action, creature); // It comes back to one of ours
                    }
                    if (Hard && creature.Has(StatusEffectType.Blessed)) return -0.3f; // Negated, and it loses a stack of Blessed
                    if (Hard && creature.Has(StatusEffectType.Doomed)) return 0; // Blocks every other status
                    // Revealing one that burns in the light, or strikes back at whoever reveals it
                    float revealed = action.statusEffectToApply == StatusEffectType.Revealed && !creature.Ours ? RevealCost(creature) : 0;
                    return StatusGoodness(action, creature) + revealed;
                case ActionType.Cleanse: // Every status goes: the harmful ones are a relief, the good ones a loss
                    return 2 * (HarmfulStatuses.Count(creature.Has) - GoodStatuses.Count(creature.Has));
                case ActionType.ReturnToHand or ActionType.FakeDeath or ActionType.Vanish: {
                    // Off the board: worth it to save one that would die (and for its Last Will); otherwise a lost Day
                    if (!creature.Ours) return 0;
                    float saved = Lost(creature) ? creature.Worth * 0.8f : -1.5f;
                    // Very Hard: only a fresh start lifts a Doomed or Brainwashed creature's fate; Borrowed Time is lifted by leaving too
                    if (VeryHard && action.actionType == ActionType.FakeDeath && creature.Has(StatusEffectType.Brainwashed)) saved += 0.5f;
                    if (action.actionType == ActionType.FakeDeath && Hard) saved += LastWillValue(creature);
                    if (action.actionType == ActionType.ReturnToHand && creature.Has(StatusEffectType.Revealed)) saved += 0.5f * Style.Intel;
                    return saved;
                }
                case ActionType.Resurrect: { // Ours dies now and comes back Days later: worth it for one that's lost anyway
                    if (!creature.Ours) return 0;
                    float back = Lost(creature) ? creature.Worth * 0.7f : -creature.Worth * 0.5f;
                    return back + (Hard ? LastWillValue(creature) : 0);
                }
                case ActionType.Consolidate: { // The strongest takes Attack from the rest: our gains and their losses count
                    var group = action.targetType == TargetType.AllCreatures ? view.All : creature.Ours ? view.Mine : view.Theirs;
                    float swing = 0;
                    foreach (var giver in group.Where(c => c != creature)) {
                        int amount = System.Math.Min(action.value, AttackOf(giver));
                        swing += (creature.Ours ? amount : -amount) + (giver.Ours ? -amount : amount);
                    }
                    return swing;
                }
                case ActionType.Funnel: // Spread out raises gathered into one creature that can kill with them
                    var others = (creature.Ours ? view.Mine : view.Theirs).Where(c => c != creature).Sum(c => System.Math.Max(0, c.StatRaises));
                    return others * 0.5f;
                case ActionType.StealStatChanges or ActionType.PassHarmfulStatuses or ActionType.StealGoodStatuses when spell != null: {
                    // A spell works between its target and the creature facing it
                    var facing = (creature.Ours ? view.TheirSlots : view.MySlots)[creature.Slot.Index].Creature;
                    if (facing == null) return 0;
                    return action.actionType switch {
                        ActionType.StealStatChanges => 2 * System.Math.Max(0, facing.StatRaises), // They lose them, it gains them
                        ActionType.PassHarmfulStatuses when VeryHard && (creature.Has(StatusEffectType.Doomed) || creature.Has(StatusEffectType.Brainwashed)) =>
                            creature.Worth + facing.Worth, // Ours saved, theirs condemned
                        ActionType.PassHarmfulStatuses => 2 * (HarmfulStatuses.Count(creature.Has) - GoodStatuses.Count(creature.Has)), // All of them go
                        _ => 2 * (GoodStatuses.Count(facing.Has) - HarmfulStatuses.Count(facing.Has)) // All of them come
                    };
                }
                case ActionType.CopyStatuses: { // A copy of the facing creature's statuses: its good ones help, its harmful ones hurt
                    var model = (creature.Ours ? view.TheirSlots : view.MySlots)[creature.Slot.Index].Creature;
                    return model == null ? 0 : 2 * (GoodStatuses.Count(model.Has) - HarmfulStatuses.Count(model.Has));
                }
                case ActionType.Equip: // A second ability on one of ours: more on a strong one; none on theirs
                    return creature.Ours ? (creature.Creature?.Gear == CreatureAbility.None ? 1.5f + AttackOf(creature) * 0.2f : 0.5f) : -1f;
                case ActionType.Push or ActionType.Pull: // Maneuvers: theirs into reach or off their bonuses; a hurt one of ours out of the front
                    return PositionValue(creature, action.actionType == ActionType.Pull);
                case ActionType.DoubleDown: // 3rd Parallel Job: a big friendly creature with Health to spare
                    return creature.Ours && creature.Operative ? AttackOf(creature) - HealthLeft(creature) * 0.25f : -2;
                case ActionType.Mimic: { // Monkey See, Monkey Do: a free copy of one of theirs, over whatever stands facing it on our side
                    if (creature.Ours) return -1;
                    var replaced = Facing(creature);
                    return creature.Worth + 1f - (replaced?.Worth ?? 0f) * 0.8f;
                }
                case ActionType.TradeStats: { // Freaky Friday: the target and the creature facing it trade Attack and Health
                    var other = Facing(creature);
                    if (other == null) return 0;
                    var (ours, theirs) = creature.Ours ? (creature, other) : (other, creature);
                    return (AttackOf(theirs) - AttackOf(ours)) * 0.8f + (HealthLeft(theirs) - HealthLeft(ours)) * 0.4f;
                }
                case ActionType.SplitPain: { // Parabiosis: the two share their Health out
                    var other = Facing(creature);
                    if (other == null) return 0;
                    var (ours, theirs) = creature.Ours ? (creature, other) : (other, creature);
                    return (HealthLeft(theirs) - HealthLeft(ours)) * 0.5f;
                }
                case ActionType.CopyStats or ActionType.StealStatChanges or ActionType.SwapPositions or ActionType.GrantAbility or ActionType.SwapAbilities:
                    return IntendedFor(spell, creature.Ours ? view.Me : view.Them) ? 1 : -1; // Hard to weigh: done where the card means it
                case ActionType.Armor or ActionType.Heal when Hard && creature.Ours:
                    return Protection(creature, action.actionType, action.value);
                default:
                    return CreatureGoodness(creature, action.actionType, action.value);
            }
        }

        private float CreatureGoodness(SeenCreature creature, ActionType type, int amount) => type switch {
            ActionType.Damage when IsUntouchable(creature) => 0,
            ActionType.Damage => DamageGoodness(creature, amount + (IsExposed(creature) ? 1 : 0)),
            ActionType.Heal => creature.Known ? System.Math.Min(amount, creature.MaxHealth - creature.Health) : 0,
            ActionType.Armor => amount * 0.75f,
            ActionType.Stun => creature.Operative && !IsStunned(creature) ? -(AttackOf(creature) + 1) : -0.5f,
            ActionType.DelayDeployment => creature.Operative ? 0 : -1.5f * amount, // Only a creature still deploying waits longer
            // Overtime: a Day of it now, for the creature afterwards; worth it only for a strong one that would wait long
            ActionType.Rush => creature.Operative ? 0 : AttackOf(creature) * 1.5f - creature.Worth * 0.6f,
            // Attack and Health trade places: the Attack it gains (or loses), and a kill when it had none
            ActionType.SwapStats => AttackOf(creature) <= 0 ? -(creature.Worth + HealthLeft(creature)) : (HealthLeft(creature) - AttackOf(creature)) * 0.6f,
            _ => 0
        };

        private float PlayerGoodness(IPlayer player, ActionType type, int amount) => type switch {
            ActionType.Damage => amount + (player == view.Them ? plannedFace : 0) >= player.Health ? -Lethal : -amount,
            ActionType.Heal => amount, // No ceiling: every point counts
            ActionType.Draw => amount,
            ActionType.Summon => 2 * amount,
            ActionType.SummonDecoys => 2.5f * amount, // Bodies that soak their attacks and kills
            ActionType.CutDraws => -amount, // Cards they won't have
            ActionType.Seal => -2.5f, // The DMCA: their second copies wait
            _ => 0
        };

        private float DamageGoodness(SeenCreature creature, int damage) {
            int left = HealthLeft(creature);
            if (left <= 0 || Condemned(creature)) return -0.1f; // Already dead to what's planned (or condemned): wasted
            return damage >= left ? -(creature.Worth + left) : -damage;
        }

        // Whether it lands: what it requires holds (as far as we can see), and nothing makes a Destroy's target Untouchable
        private bool Lands(EffectAction action, SeenCreature creature) {
            if (action.actionType == ActionType.Destroy && IsUntouchable(creature)) return false;
            switch (action.requires) {
                case TargetRequisite.Revealed: return creature.Has(StatusEffectType.Revealed) || revealedNow.Contains(creature);
                case TargetRequisite.NextToEmptySlot:
                    var row = creature.Slot.Ours ? view.MySlots : view.TheirSlots;
                    return Formation.Neighbours(creature.Slot.Index).Any(i => i >= 0 && i < row.Count && row[i].IsEmpty);
                case TargetRequisite.Outnumbered:
                    int mine = view.Mine.Count(), theirs = view.Theirs.Count();
                    return creature.Ours ? mine < theirs : theirs < mine;
                case TargetRequisite.AttackAtMost: return AttackOf(creature) <= action.value;
                case TargetRequisite.Deploying: return !creature.Operative;
                case TargetRequisite.Online: return creature.Creature?.SourceData?.faction == Faction.DigitalSociety;
                case TargetRequisite.OutsideHeadquarters: return Formation.PositionOf(creature.Slot.Index) != Formation.Position.Headquarters;
                default: return true;
            }
        }

        // Revealing one of theirs, for them: it strikes back at us (When Revealed), less what the light does to it (Allergic to Light)
        private float RevealCost(SeenCreature theirs) =>
            theirs.RevealBacklash * 0.8f * Style.OwnHealth - (theirs.Ability == CreatureAbility.AllergicToLight ? Abilities.LightDamage : 0);

        // Hard: what one of our creatures' Last Will does for us when it dies (Dead Man's Switch destroying their strongest)
        private float LastWillValue(SeenCreature ours) {
            if (weighingLastWill) return 0; // A Last Will that kills another of ours with one: counted once
            weighingLastWill = true;
            try {
                return ours.Creature.Effects.Where(e => e.trigger == EffectTrigger.OnDeath).SelectMany(e => e.actions)
                    .Sum(a => a.targetType == TargetType.Self ? 0 : Value(a, null, view.Me));
            }
            finally {
                weighingLastWill = false;
            }
        }
        private bool weighingLastWill;

        // Hard: the change as the creature's ability bends it: Contrarian reverses it, Extremist doubles it
        private int BentByAbility(int change, SeenCreature creature) {
            if (creature.Ability == CreatureAbility.Contrarian) return -change;
            return creature.Ability == CreatureAbility.Extremist ? change * 2 : change;
        }

        private float StatChangeGoodness(EffectAction action, SeenCreature creature) {
            int change = action.value;
            if (Hard) {
                bool enemyLowers = change < 0 && !creature.Ours;
                if (enemyLowers && creature.Ability is CreatureAbility.Unbothered or CreatureAbility.Unpatchable) return 0;
                if (enemyLowers && creature.Ability == CreatureAbility.NoU) return -change * 2; // Bounces to one of ours
                change = BentByAbility(change, creature);
            }
            if (action.modifyHealth && change < 0 && -change >= HealthLeft(creature)) return -(creature.Worth + HealthLeft(creature)); // Its Health drops to 0
            float good = change * ((action.modifyAttack ? 1 : 0) + (action.modifyHealth ? 1 : 0) + (action.modifySpeed ? 0.5f : 0));
            if (Hard && change < 0 && creature.Ability == CreatureAbility.Spite) good += 2; // It gets +2 Attack out of it
            return good;
        }

        private static readonly StatusEffectType[] GoodStatuses = {
            StatusEffectType.Blessed, StatusEffectType.Concealed, StatusEffectType.Encrypted, StatusEffectType.Untouchable
        };

        private static readonly StatusEffectType[] HarmfulStatuses = {
            StatusEffectType.Burned, StatusEffectType.Exhausted, StatusEffectType.Doomed, StatusEffectType.HeatSignature,
            StatusEffectType.Revealed, StatusEffectType.Cooldown
        };

        private float StatusGoodness(EffectAction action, SeenCreature creature) {
            int days = System.Math.Max(1, action.statusDuration);
            return action.statusEffectToApply switch {
                // Hormesis: what would burn it heals it
                StatusEffectType.Burned when creature.Ability == CreatureAbility.Hormesis => BurnTotal(action, days) * 0.5f,
                StatusEffectType.Burned => -BurnTotal(action, days),
                // Martyrdom: Doomed doubles what it deals until it dies
                StatusEffectType.Doomed when creature.Ability == CreatureAbility.Martyrdom && creature.Ours => AttackOf(creature) * 1.5f - creature.Worth / 4f,
                StatusEffectType.Exhausted => creature.Operative && !IsStunned(creature) ? -(AttackOf(creature) + 1) : -0.5f,
                StatusEffectType.Doomed => -creature.Worth / 2f,
                StatusEffectType.Blessed => System.Math.Max(1, action.statusPotency),
                StatusEffectType.HeatSignature => -0.5f,
                // A combo: with a "destroy it if it's Revealed" spell in hand, revealing a creature of theirs is nearly its death
                StatusEffectType.Revealed when holdsKillIfRevealed && !creature.Ours && !IsUntouchable(creature) && HealthLeft(creature) > 0 =>
                    -(creature.Worth + HealthLeft(creature)) * 0.8f,
                StatusEffectType.Revealed => creature.Has(StatusEffectType.Concealed) || creature.Ability is CreatureAbility.Undercover or CreatureAbility.Ambush ? -2 : -0.5f,
                StatusEffectType.Concealed or StatusEffectType.Encrypted => 1,
                StatusEffectType.Untouchable => Hard && creature.Ours ? Protection(creature, ActionType.Stun, 0) : 3,
                StatusEffectType.Shielded => creature.Has(StatusEffectType.Shielded) ? 0 : 2,
                StatusEffectType.Cooldown => -1,
                // A delayed death they can't see coming, unless they happen to cleanse it
                StatusEffectType.KillOrder when VeryHard && !creature.Ours => -(creature.Worth + HealthLeft(creature)) * 0.9f * SlowKillShare(creature),
                StatusEffectType.KillOrder => creature.Has(StatusEffectType.KillOrder) ? 0 : -(creature.Worth + HealthLeft(creature)) * 0.7f,
                StatusEffectType.Burnout => creature.Has(StatusEffectType.Burnout) ? 0 : -(creature.Worth + HealthLeft(creature)) * 0.7f,
                // A defection: they lose it and we gain it, later; sooner when it's Revealed
                StatusEffectType.Brainwashed when VeryHard && !creature.Ours => -(creature.Worth * 2) * (days <= 1 ? 0.85f : 0.7f) * SlowKillShare(creature),
                StatusEffectType.Brainwashed => creature.Ours ? -creature.Worth : -(creature.Worth * 2) * (days <= 1 ? 0.75f : 0.6f),
                // Can't die until tomorrow's Dusk: worth the creature when it would die now, a little otherwise
                StatusEffectType.BorrowedTime => creature.Has(StatusEffectType.BorrowedTime) ? 0 : WouldDie(creature) ? creature.Worth * 0.7f : 0.3f,
                // Its next attack kills: worth the best kill it can make
                StatusEffectType.Licensed => creature.Has(StatusEffectType.Licensed) || !CanAttack(creature) ? 0
                    : view.Theirs.Where(Hittable).Select(t => (t.Worth + HealthLeft(t)) * 0.6f).DefaultIfEmpty(0.5f).Max(),
                StatusEffectType.Composed => creature.Has(StatusEffectType.Exhausted) ? AttackOf(creature) + 1 : 0.5f,
                // One more hit survived, only while unhurt: worth most on a healthy creature that's about to be hit
                StatusEffectType.Survivor => creature.Has(StatusEffectType.Survivor) || creature.Health < creature.MaxHealth ? 0
                    : WouldDie(creature) ? creature.Worth * 0.6f : 0.5f,
                // Provoked: on theirs, its attack goes where we choose to stand; on ours, it loses its aim
                StatusEffectType.Provoked => creature.Operative && CanAttack(creature) ? -1.5f : -0.3f,
                // Groundhog Day: stuck repeating today's plan (ours loses its choices; theirs, we see every Day coming)
                StatusEffectType.Looping => creature.Operative ? -(1f + AttackOf(creature) * 0.5f) * System.Math.Max(1, days) : -0.3f,
                StatusEffectType.Stalled => creature.Operative && CanAttack(creature) ? -(0.5f + AttackOf(creature) * 0.4f) : 0, // Filibuster: its hit lands after ours
                StatusEffectType.Rooted => creature.Operative ? -0.9f : -0.3f, // Curfew: it can't reposition
                StatusEffectType.Deniable => 0.8f, // Plausible Deniability: area effects pass it by
                StatusEffectType.Leeched => -(System.Math.Max(1, action.statusPotency) * days) * 0.9f, // Tithe: a slow bleed that heals us
                StatusEffectType.MutuallyAssured => creature.Ours ? creature.Worth * 0.4f + 0.5f : -0.5f, // Destiny Bond: on ours
                StatusEffectType.Blackout => creature.Ability != CreatureAbility.None || creature.Retaliation > 0 ? -1.5f : -0.5f,
                _ => 0
            };
        }

        // What a Burn deals over its Days
        private static float BurnTotal(EffectAction action, int days) => System.Math.Max(1, action.statusPotency) * days;

        // Hard: help for one of ours is worth what it saves: the whole creature if it would otherwise die next Day
        private float Protection(SeenCreature ours, ActionType type, int amount) {
            if (IsUntouchable(ours)) return 0.1f; // Safe already
            int threat = ThreatOn(ours);
            int toughness = HealthLeft(ours);
            switch (type) {
                case ActionType.Armor:
                    return threat >= toughness && threat < toughness + amount ? ours.Worth * 0.8f : amount * 0.4f;
                case ActionType.Heal:
                    int healed = ours.Known ? System.Math.Min(amount, ours.MaxHealth - ours.Health) : 0;
                    return threat >= toughness && threat < toughness + healed ? ours.Worth * 0.6f : healed;
                default: // Untouchable, or a Shield
                    return threat <= 0 ? 0.3f : threat >= toughness ? ours.Worth : threat * 0.7f;
            }
        }

        // A Slot Effect, for the side whose slot it is
        private float SlotGoodness(EffectAction action, SeenSlot slot) {
            switch (action.actionType) {
                case ActionType.PlaceSlotEffect:
                    bool occupied = slot.Creature != null || slot.Incoming != null;
                    return action.slotEffect switch {
                        SlotEffectType.Landmine => occupied ? -0.5f : -action.value * 0.6f, // Waits for the next creature to enter
                        SlotEffectType.BarbedSnare => occupied ? -action.value * 0.6f : -0.5f, // Waits for one to leave
                        SlotEffectType.Shield when Hard && slot.Ours && (slot.Creature ?? view.IncomingTo(slot)) is SeenCreature ours =>
                            Protection(ours, ActionType.Stun, 0),
                        SlotEffectType.Shield => occupied ? 2 : 0.25f,
                        SlotEffectType.ScorchedEarth => occupied ? -1.5f : -0.5f,
                        SlotEffectType.Checkpoint => -action.value * (occupied ? 0.5f : 0.8f), // Every creature that comes after pays
                        _ => 0
                    };
                case ActionType.CleanseSlot:
                    return -ValueOfEntering(slot);
                default:
                    return 0;
            }
        }

        // Which side the card is meant for: the one its default target names (all creatures: either)
        // A step back or forward (Push, Pull): theirs pulled out of Headquarters into reach, or pushed off the Vanguard's Armor and
        // the Flanks' bonus; ours, a hurt one sent back, a healthy one brought forward
        private float PositionValue(SeenCreature creature, bool forward) {
            int depth = Formation.Depth(creature.Slot.Index);
            if (forward ? depth == 0 : depth == 2) return -0.2f; // It can't go further
            if (!creature.Ours) return forward ? (depth == 2 ? 1.5f : 0.4f) : (depth == 0 ? 1f : 0.6f);
            bool hurt = HealthLeft(creature) <= 3;
            return forward ? (hurt ? -1f : 0.6f) : (hurt ? 1.2f : -0.4f);
        }

        private bool IntendedFor(Spell spell, IPlayer side) => spell == null || spell.DefaultTargetType switch {
            TargetType.Enemy or TargetType.EnemyCreatures => side != view.Me,
            TargetType.Player or TargetType.FriendlyCreatures or TargetType.Self => side == view.Me,
            _ => true
        };
        #endregion
    }
}
