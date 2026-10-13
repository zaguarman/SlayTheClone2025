using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A simple AI that plans one side's Day (GDD 3.6 Planning) the way a player does: it queues the same plays, attacks and
    /// Active abilities that dragging cards queues, so it can sit in either seat, or both. It plays under Fog of War: of the
    /// other side it only uses what that side shows (a creature it doesn't know is guessed at), never their hand or plans.
    /// A proof of concept: it doesn't move creatures or look ahead. Each choice is scored by a rough value of what it does
    /// (see <see cref="Value"/>) and the best one is taken, so it plays any card made of the effects the game knows.
    /// </summary>
    public class AIPlanner {
        // What an unknown enemy creature is taken to be
        private const int GuessedAttack = 2;
        private const int GuessedHealth = 3;
        private const float Lethal = 100f; // A player at 0 ends the combat: nothing else counts as much

        private readonly IActionsQueue queue;
        private readonly IBattlefieldCombatHandler combat;
        private IModifierManager Modifiers => queue.Modifiers;

        private IPlayer me;
        // Damage already planned today, so two attackers don't both kill the same creature
        private readonly Dictionary<ITarget, int> plannedDamage = new Dictionary<ITarget, int>();

        public AIPlanner(IActionsQueue queue, IBattlefieldCombatHandler combat) {
            this.queue = queue ?? throw new System.ArgumentNullException(nameof(queue));
            this.combat = combat ?? throw new System.ArgumentNullException(nameof(combat));
        }

        /// <summary>
        /// Plans the player's Day: creatures to free slots, then the spells worth playing, then each creature's ability or
        /// attack. What's already planned stays, so planning again the same Day only adds what's left.
        /// </summary>
        public void PlanDay(IPlayer player) {
            if (player?.Opponent == null || player.Health <= 0 || player.Opponent.Health <= 0) return;
            me = player;
            plannedDamage.Clear();
            Log($"AI plans {me.Name}'s Day", LogTag.Turns);
            PlayCreatures();
            PlaySpells();
            foreach (var creature in Creatures(me).OrderByDescending(c => c.Attack).ToList()) {
                if (queue.HasActiveAction(creature.TargetId) || !Modifiers.CanAct(creature)) continue;
                if (!UseAbility(creature)) Attack(creature);
            }
        }

        #region Creatures
        // The best creatures first, each to the free slot that suits it best; a much better one replaces a weak one when the row is full
        private void PlayCreatures() {
            foreach (var creature in me.Hand.OfType<ICreature>().Where(c => !IsQueued(c)).OrderByDescending(Worth).ToList()) {
                var slot = me.Battlefield.Where(IsFree).OrderByDescending(s => ValueOfEntering(s)).FirstOrDefault()
                    ?? me.Battlefield.Where(s => s.IsOccupied() && EffectResolver.IncomingCreature(s, queue) == null
                                                 && !queue.HasActiveAction(s.OccupyingCreature.TargetId)
                                                 && Worth(creature) >= Worth(s.OccupyingCreature) + 4)
                        .OrderBy(s => Worth(s.OccupyingCreature)).FirstOrDefault();
                if (slot != null) Play(creature, slot);
            }
        }

        private bool IsFree(BoardSlot slot) => !slot.IsOccupied() && EffectResolver.IncomingCreature(slot, queue) == null;

        // What landing in this slot does to a creature, as far as this side can see
        private float ValueOfEntering(BoardSlot slot) {
            var effect = slot.Effect;
            if (effect == null || !Intel.CanSee(me, effect)) return 0;
            return effect.Type switch {
                SlotEffectType.Landmine => -effect.Value,
                SlotEffectType.BarbedSnare => -effect.Value / 2f,
                SlotEffectType.TemporalShield => 2,
                SlotEffectType.ScorchedEarth => -2,
                _ => 0
            };
        }
        #endregion

        #region Spells
        // Each spell goes where it does the most good, if anywhere it does some
        private void PlaySpells() {
            foreach (var spell in me.Hand.OfType<Spell>().Where(s => !IsQueued(s)).ToList()) {
                ITarget bestAim = null;
                float best = 0;
                foreach (var aim in AimsFor(spell)) {
                    float value = spell.Effects.SelectMany(e => e.actions).Sum(a => Value(a, spell, aim));
                    if (value > best) (bestAim, best) = (aim, value);
                }
                if (bestAim == null) continue;
                Play(spell, bestAim);
                foreach (var damage in spell.Effects.SelectMany(e => e.actions).Where(a => a.actionType == ActionType.Damage && EffectResolver.UsesChosenTarget(a))) {
                    if (CreatureAt(bestAim) is ICreature hit) AddDamage(hit, damage.value);
                }
            }
        }

        // Where it can be dropped: any slot for an aimed spell (on this side, homing on the creature that will be there),
        // otherwise the side it does something to (its caster's when it does something to both)
        private IEnumerable<ITarget> AimsFor(Spell spell) {
            if (EffectResolver.IsAimed(spell)) {
                foreach (var slot in me.Battlefield) yield return EffectResolver.AimAt(spell, me, slot, queue);
                foreach (var slot in me.Opponent.Battlefield) yield return slot;
            }
            else if (EffectResolver.Reaches(spell, me, me)) yield return me;
            else if (EffectResolver.Reaches(spell, me, me.Opponent)) yield return me.Opponent;
        }
        #endregion

        #region Abilities and attacks
        // An Active ability, when using it beats attacking: Bodyguard (which can't attack) always guards someone
        private bool UseAbility(ICreature creature) {
            if (!Abilities.CanUseActive(creature, Modifiers)) return false;
            var others = AllCreatures().Where(c => c != creature).ToList();
            ICreature target = creature.Ability switch {
                CreatureAbility.Bodyguard => others.Where(c => c.Owner == me && !Modifiers.HasStatusEffect(c, StatusEffectType.Untouchable))
                    .OrderByDescending(Worth).FirstOrDefault(),
                CreatureAbility.Wannabe => others.Where(c => Worth(c) >= Worth(creature) + 2).OrderByDescending(Worth).FirstOrDefault(),
                CreatureAbility.IdentityTheft => others.Where(c => c.Owner != me && Knows(c) && c.Ability != CreatureAbility.None && creature.Attack <= 2)
                    .FirstOrDefault(),
                CreatureAbility.CreditGrab => others.Where(c => c.Owner != me && Knows(c) && c.Attack - c.BaseAttack + c.Health - c.BaseHealth >= 2)
                    .FirstOrDefault(),
                _ => null
            };
            if (target?.Slot == null) return false;
            Log($"AI: {creature.Name} uses {Abilities.NameOf(creature.Ability)} on {target.Name}", LogTag.Turns);
            queue.AddAction(new UseAbilityAction(creature, target.Slot));
            return true;
        }

        // The enemy slot where its hit is worth the most: a kill, else the player through an empty slot, else the softest creature
        private void Attack(ICreature attacker) {
            if (!Abilities.CanAttack(attacker) || attacker.Attack <= 0) return;
            int damage = attacker.Attack + Abilities.AttackBonus(attacker);
            var target = me.Opponent.Battlefield.OrderByDescending(s => ValueOfHitting(s, damage)).First();
            Log($"AI: {attacker.Name} attacks {target}", LogTag.Turns);
            combat.HandleCreatureCombat(attacker, target);
            AddDamage(target.OccupyingCreature ?? (ITarget)me.Opponent, damage);
        }

        private float ValueOfHitting(BoardSlot slot, int damage) {
            if (slot.OccupyingCreature is ICreature creature) return -Goodness(ActionType.Damage, damage, creature) * 0.9f; // A tie goes to the player
            return -Goodness(ActionType.Damage, damage, me.Opponent);
        }
        #endregion

        #region Values
        /// <summary>
        /// Roughly how much good one effect of a spell does for this side, played at this aim: what it does to each target it
        /// lands on, counted for us on our side and against us on theirs (a random target counts as a share of each it may pick).
        /// </summary>
        private float Value(EffectAction action, Spell spell, ITarget aim) {
            // Good for whoever plays them, whichever side they look at
            if (action.actionType is ActionType.CreateWatchdog or ActionType.SetTrend) return 1;
            if (action.actionType == ActionType.Reveal) {
                return CreatureAt(aim) is ICreature hidden && Intel.HidesSomethingFrom(me, hidden, queue) ? 1 : 0;
            }

            var targets = TargetsOf(action, aim);
            float share = action.targetModifier.HasFlag(TargetModifier.Random) && targets.Count > 0 ? 1f / targets.Count : 1f;
            float value = 0;
            foreach (var target in targets) {
                float good = target is BoardSlot slot ? GoodnessOfSlot(action, slot) : Goodness(action, target, spell);
                value += share * (IsOurs(target) ? good : -good);
            }
            return value;
        }

        // What the effect lands on, as EffectResolver picks it (see its SelectTargets)
        private List<ITarget> TargetsOf(EffectAction action, ITarget aim) {
            if (action.targetType == TargetType.Self) return new List<ITarget>(); // A spell has no self
            if (EffectResolver.UsesChosenTarget(action)) {
                var slot = SlotOf(aim);
                if (EffectResolver.IsSlotAction(action.actionType)) return slot != null ? new List<ITarget> { slot } : new List<ITarget>();
                var targets = new List<ITarget>();
                if (CreatureAt(aim) is ICreature creature) targets.Add(creature);
                else if (EffectResolver.CanAffectPlayers(action.actionType) && slot != null) targets.Add(slot.Owner);
                if (action.targetModifier.HasFlag(TargetModifier.WithAdjacent) && slot != null) {
                    var row = slot.Owner.Battlefield;
                    foreach (int i in new[] { slot.Index - 1, slot.Index + 1 }) {
                        if (i >= 0 && i < row.Count && row[i].OccupyingCreature != null) targets.Add(row[i].OccupyingCreature);
                    }
                }
                return targets;
            }
            return TargetingSystem.GetValidTargets(me, action.targetType);
        }

        // How good the effect is for whoever it lands on: a player or a creature
        private float Goodness(EffectAction action, ITarget target, Spell spell) {
            if (target is ICreature creature) {
                switch (action.actionType) {
                    case ActionType.ModifyStat when action.modifyHealth && action.value < 0 && -action.value >= HealthLeft(creature):
                        return -(Worth(creature) + HealthLeft(creature)); // Its Health drops to 0
                    case ActionType.ModifyStat:
                        return action.value * ((action.modifyAttack ? 1 : 0) + (action.modifyHealth ? 1 : 0) + (action.modifySpeed ? 0.5f : 0));
                    case ActionType.ApplyStatus:
                        return GoodnessOfStatus(action, creature);
                    case ActionType.Cleanse:
                        return HarmfulStatuses.Any(s => Modifiers.HasStatusEffect(creature, s)) ? 2 : 0;
                    case ActionType.CopyStats or ActionType.StealStatChanges or ActionType.SwapPositions or ActionType.GrantAbility:
                        return IntendedFor(spell, creature.Owner) ? 1 : -1; // Hard to weigh: done where the card means it
                }
            }
            return Goodness(action.actionType, action.value, target);
        }

        private float Goodness(ActionType type, int amount, ITarget target) {
            switch (target) {
                case IPlayer player:
                    return type switch {
                        ActionType.Damage => amount + Planned(player) >= player.Health ? -Lethal : -amount,
                        ActionType.Heal => System.Math.Min(amount, Player.MaxHealth - player.Health),
                        ActionType.Draw => amount,
                        ActionType.Summon => 2 * amount,
                        _ => 0
                    };
                case ICreature creature:
                    bool operative = creature.IsOperative;
                    return type switch {
                        ActionType.Damage when Modifiers.HasStatusEffect(creature, StatusEffectType.Untouchable) => 0,
                        ActionType.Damage => DamageGoodness(creature, amount + (Modifiers.IsExposed(creature) ? 1 : 0)),
                        ActionType.Heal => Knows(creature) ? System.Math.Min(amount, creature.MaxHealth - creature.Health) : 0,
                        ActionType.Armor => amount * 0.75f,
                        ActionType.Stun => operative ? -(AttackOf(creature) + 1) : -0.5f,
                        ActionType.DelayDeployment => -1.5f * amount,
                        _ => 0
                    };
                default:
                    return 0;
            }
        }

        private float DamageGoodness(ICreature creature, int damage) {
            int left = HealthLeft(creature);
            if (left <= 0) return -0.1f; // Already dead to what's planned: wasted
            return damage >= left ? -(Worth(creature) + left) : -damage;
        }

        private static readonly StatusEffectType[] HarmfulStatuses = {
            StatusEffectType.Burned, StatusEffectType.Exhausted, StatusEffectType.Doomed, StatusEffectType.HeatSignature,
            StatusEffectType.Revealed, StatusEffectType.Cooldown
        };

        private float GoodnessOfStatus(EffectAction action, ICreature creature) {
            int days = System.Math.Max(1, action.statusDuration);
            return action.statusEffectToApply switch {
                StatusEffectType.Burned => -System.Math.Max(1, action.statusPotency) * days,
                StatusEffectType.Exhausted => creature.IsOperative ? -(AttackOf(creature) + 1) : -0.5f,
                StatusEffectType.Doomed => -Worth(creature) / 2f,
                StatusEffectType.Blessed => System.Math.Max(1, action.statusPotency),
                StatusEffectType.HeatSignature => -0.5f,
                StatusEffectType.Revealed => Intel.IsConcealed(creature, Modifiers) ? -2 : -0.5f,
                StatusEffectType.Concealed or StatusEffectType.Encrypted => 1,
                StatusEffectType.Untouchable => 3,
                StatusEffectType.Cooldown => -1,
                _ => 0
            };
        }

        // A slot effect, for the side whose slot it is
        private float GoodnessOfSlot(EffectAction action, BoardSlot slot) {
            switch (action.actionType) {
                case ActionType.PlaceSlotEffect:
                    bool occupied = EffectResolver.IncomingCreature(slot, queue) != null || slot.IsOccupied();
                    return action.slotEffect switch {
                        SlotEffectType.Landmine => occupied ? -0.5f : -action.value * 0.6f, // Waits for the next creature to enter
                        SlotEffectType.BarbedSnare => occupied ? -action.value * 0.6f : -0.5f, // Waits for one to leave
                        SlotEffectType.TemporalShield => occupied ? 2 : 0.25f,
                        SlotEffectType.ScorchedEarth => occupied ? -1.5f : -0.5f,
                        _ => 0
                    };
                case ActionType.CleanseSlot:
                    return slot.Effect != null && Intel.CanSee(me, slot.Effect) ? -ValueOfEntering(slot) : 0;
                default:
                    return 0;
            }
        }

        // Which side the card is meant for: the one its default target names (all creatures: either)
        private bool IntendedFor(Spell spell, IPlayer side) => spell == null || spell.DefaultTargetType switch {
            TargetType.Enemy or TargetType.EnemyCreatures => side != me,
            TargetType.Player or TargetType.FriendlyCreatures or TargetType.Self => side == me,
            _ => true
        };
        #endregion

        #region What this side sees
        private bool Knows(ICreature creature) => Intel.KnowsWhatItIs(me, creature);
        private int AttackOf(ICreature creature) => Knows(creature) ? creature.Attack : GuessedAttack;
        private int HealthOf(ICreature creature) => Knows(creature) ? creature.Health + creature.CurrentArmorPool : GuessedHealth;
        private int HealthLeft(ICreature creature) => HealthOf(creature) - Planned(creature);
        private float Worth(ICreature creature) => AttackOf(creature) + HealthOf(creature);

        private int Planned(ITarget target) => plannedDamage.TryGetValue(target, out int damage) ? damage : 0;
        private void AddDamage(ITarget target, int damage) => plannedDamage[target] = Planned(target) + damage;

        private bool IsOurs(ITarget target) => target switch {
            IPlayer player => player == me,
            ICreature creature => creature.Owner == me,
            BoardSlot slot => slot.Owner == me,
            _ => false
        };

        private static IEnumerable<ICreature> Creatures(IPlayer side) =>
            side.Battlefield.Where(s => s.IsOccupied()).Select(s => s.OccupyingCreature);

        private IEnumerable<ICreature> AllCreatures() => Creatures(me).Concat(Creatures(me.Opponent));

        private bool IsQueued(ICard card) => queue.GetPendingActions().OfType<PlayCardAction>().Any(p => p.GetCard() == card);

        private static BoardSlot SlotOf(ITarget aim) => aim switch {
            HomingTarget homing => homing.AimedSlot,
            BoardSlot slot => slot,
            _ => null
        };

        // The creature the aim lands on: the one homed on, or the one in the slot
        private static ICreature CreatureAt(ITarget aim) => aim switch {
            HomingTarget homing => homing.Creature,
            BoardSlot slot => slot.OccupyingCreature,
            _ => null
        };
        #endregion

        private void Play(ICard card, ITarget aim) {
            Log($"AI: {me.Name} plays {card.Name} on {aim}", LogTag.Turns);
            queue.AddAction(new PlayCardAction(card, me, aim));
        }
    }
}
