using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>How well the AI plays (see <see cref="AIPlanner"/>).</summary>
    public enum AILevel { Easy, Medium, Hard, VeryHard }

    /// <summary>
    /// An AI that plans one side's Day (GDD 3.6 Planning) the way a player does: it queues the same plays, attacks, moves and
    /// Active abilities that dragging cards queues, so it can sit in either seat, or both. It looks at the game only through
    /// an <see cref="AIView"/>, so it knows what a player in that seat knows and nothing more (GDD 2.5 Fog of War).
    /// <list type="bullet">
    /// <item>Easy plays everything: creatures to random free slots, spells at a random target on the side the card names,
    /// each creature attacking the slot in front of it.</item>
    /// <item>Medium scores every choice by a rough value of what it does (see <see cref="Value"/>): each spell where it does the
    /// most good, if anywhere; each attack where it kills the most, else at the player through an empty slot.</item>
    /// <item>Hard also reads what the cards it knows say: abilities that bend effects (Contrarian, No U, Too Mainstream...),
    /// Blessed, Doomed, Untouchable, Heat, Shields, Counterattacks; it protects the creatures the other side can kill,
    /// races or holds the board, kills the biggest threats with the smallest attacker that does it, and moves idle creatures
    /// out of bad slots. It plays combinations: it forecasts what its spells will have done by the time attacks land (see
    /// <see cref="Forecast"/>) and picks each next spell knowing the ones before. Its <see cref="AIStyle"/> says what it
    /// values most: the same planner goes face, controls, goes wide, hexes, turtles, spies or draws.</item>
    /// <item>Very Hard is Hard that also plays the long game, the cards that pay off on a later Day: it saves the creatures
    /// already lost to a slow death (Doomed, Brainwashed, Borrowed Time running out at 0 Health) with Extraction, Witness
    /// Protection or a faked death, frames the enemy for them, keeps its slow kills (Kill Order, Brainwashed) for what its
    /// attacks can't kill, and doesn't waste attacks or damage on enemy creatures already condemned (a Kill Order it gave,
    /// one Brainwashed to come over to its side).</item>
    /// </list>
    /// </summary>
    public partial class AIPlanner {
        private const float Lethal = 100f; // A player at 0 ends the combat: nothing else counts as much

        public AILevel Level { get; }
        public AIStyle Style { get; }
        private readonly IActionsQueue queue;
        private readonly IBattlefieldCombatHandler combat;
        private readonly System.Random random; // Easy's choices; its own, so the game's shuffles stay the same
        private IModifierManager Modifiers => queue.Modifiers;
        private bool Hard => Level >= AILevel.Hard;
        private bool VeryHard => Level == AILevel.VeryHard; // Delayed strategies

        private AIView view;
        // Damage already planned today, so two hits don't both kill the same creature
        private readonly Dictionary<SeenCreature, int> plannedDamage = new Dictionary<SeenCreature, int>();
        private int plannedFace;

        /// <summary>
        /// Hard: what today's spells will have done to a creature by the time the attacks land (spells resolve before them,
        /// GDD 3.7). Spells resolve among themselves in random order, so one spell counts on another only half the time.
        /// </summary>
        private sealed class Forecast {
            public int Attack, Health, Damage, Heat;
            public bool Exhausted, Untouchable;
        }
        private readonly Dictionary<SeenCreature, Forecast> forecasts = new Dictionary<SeenCreature, Forecast>();
        // Revealed by an instant spell played today (Tapped Communications): it's so now, whatever the order of the rest
        private readonly HashSet<SeenCreature> revealedNow = new HashSet<SeenCreature>();
        // Hard: a "destroy it if it's Revealed" spell waits in the hand, so revealing a big creature sets up its death
        private bool holdsKillIfRevealed;
        private bool seeForecast; // Whether what's being weighed lands after today's spells

        public AIPlanner(IActionsQueue queue, IBattlefieldCombatHandler combat, AILevel level = AILevel.Medium, int seed = 0, AIStyle style = null) {
            this.queue = queue ?? throw new System.ArgumentNullException(nameof(queue));
            this.combat = combat ?? throw new System.ArgumentNullException(nameof(combat));
            Level = level;
            Style = level >= AILevel.Hard ? style ?? AIStyle.Balanced : AIStyle.Balanced;
            random = new System.Random(seed);
        }

        public override string ToString() => new AIEntrant(Level, Style).Name;

        /// <summary>
        /// Plans the player's Day: creatures to slots, then the spells worth playing, then each creature's ability, attack or
        /// move. What's already planned stays, so planning again the same Day only adds what's left.
        /// </summary>
        public void PlanDay(IPlayer player) {
            if (player?.Opponent == null || Grace.IsDown(player) || Grace.IsDown(player.Opponent)) return;
            view = new AIView(player, queue);
            plannedDamage.Clear();
            forecasts.Clear();
            revealedNow.Clear();
            holdsKillIfRevealed = Hard && player.Hand.OfType<Spell>().Any(s => !IsQueued(s)
                && s.Effects.SelectMany(e => e.actions).Any(a => a.actionType == ActionType.Destroy && a.requires == TargetRequisite.Revealed));
            plannedFace = 0;
            seeForecast = false;
            Log($"AI ({this}) plans {player.Name}'s Day", LogTag.Turns);

            PlayCreatures();
            PlaySpells();
            seeForecast = true; // Abilities and attacks land after every spell
            var ready = view.Mine.Where(c => !queue.HasActiveAction(c.Creature.TargetId) && Modifiers.CanAct(c.Creature))
                .OrderByDescending(c => c.Attack).ToList();
            ready.RemoveAll(UseAbility);
            var idle = Level switch {
                AILevel.Easy => AttackInFront(ready),
                AILevel.Medium => AttackGreedily(ready),
                _ => AttackByThreat(ready)
            };
            if (Hard) idle.ForEach(MoveOutOfHarm);
        }

        #region Creatures
        // The best creatures first, each to the free slot that suits it best (Easy: any); a much better one replaces a weak one
        private void PlayCreatures() {
            var hand = view.Me.Hand.OfType<ICreature>().Where(c => !IsQueued(c) && PlayRestrictions.Why(c, view.Me, queue.Today) == null).ToList(); // The DMCA
            hand = Level == AILevel.Easy ? hand.OrderBy(_ => random.Next()).ToList() : hand.OrderByDescending(c => c.Attack + c.Health).ToList();
            var planted = new HashSet<SeenSlot>();
            foreach (var creature in hand) {
                if (creature.IsGift) {
                    // Planted in one of their empty slots (Easy: any)
                    var open = view.TheirSlots.Where(s => s.IsEmpty && !planted.Contains(s)).ToList();
                    if (open.Count == 0) continue;
                    var into = Level == AILevel.Easy ? open[random.Next(open.Count)] : open[0];
                    Play(creature, into.Slot);
                    planted.Add(into);
                    continue;
                }
                // A Gift they planted on our side is the first thing to summon over
                var poisoned = Level == AILevel.Easy ? null : view.MySlots.FirstOrDefault(s => s.Creature is { GiftFromThem: true } && s.Incoming == null
                    && Abilities.CanBeReplaced(s.Creature.Creature)); // A Turncoat can't be summoned over
                if (poisoned != null) {
                    Play(creature, poisoned.Slot);
                    poisoned.Incoming = creature;
                    continue;
                }
                var free = view.MySlots.Where(s => s.IsFree).ToList();
                var slot = Level == AILevel.Easy
                    ? free.Count > 0 ? free[random.Next(free.Count)] : null
                    : free.OrderByDescending(s => ValueOfEntering(s) + (Hard ? FormationValue(creature, s) : 0)).FirstOrDefault() // Hard: where it reaches, and survives deploying
                      ?? view.MySlots.Where(s => s.Creature != null && s.Incoming == null && !queue.HasActiveAction(s.Creature.Creature.TargetId)
                                                 && Abilities.CanBeReplaced(s.Creature.Creature)
                                                 && creature.Attack + creature.Health >= s.Creature.Worth + 4 / Style.Board)
                          .OrderBy(s => s.Creature.Worth).FirstOrDefault();
                if (slot == null) continue;
                Play(creature, slot.Slot);
                slot.Incoming = creature;
            }
        }

        // What landing in this slot does to a creature, as far as this side can see
        private static float ValueOfEntering(SeenSlot slot) {
            var effect = slot.VisibleEffect;
            if (effect == null) return 0;
            return effect.Type switch {
                SlotEffectType.Landmine => -effect.Value,
                SlotEffectType.BarbedSnare => -effect.Value / 2f,
                SlotEffectType.Shield => 2,
                SlotEffectType.ScorchedEarth => -2,
                SlotEffectType.Checkpoint => -effect.Value,
                _ => 0
            };
        }

        // Projection: the Slot Effects each side can see trade places; good for us when the worse ones are on our side now
        private float SwapValue() {
            float For(SlotEffect effect) => effect == null || !Intel.CanSee(view.Me, effect) ? 0 : effect.Type switch { // What this side can see
                SlotEffectType.Landmine or SlotEffectType.Checkpoint => -effect.Value,
                SlotEffectType.BarbedSnare => -effect.Value / 2f,
                SlotEffectType.ScorchedEarth => -1.5f,
                SlotEffectType.Shield => 2,
                _ => 0
            };
            var mine = view.Me.Battlefield;
            var theirs = view.Them.Battlefield;
            float gain = 0;
            for (int i = 0; i < System.Math.Min(mine.Count, theirs.Count); i++) gain += 2 * (For(theirs[i].Effect) - For(mine[i].Effect));
            return gain * Style.Protection;
        }
        #endregion

        #region Abilities
        // An Active ability, when using it beats attacking: Bodyguard (which can't attack) always guards someone
        private bool UseAbility(SeenCreature user) {
            if (!Abilities.CanUseActive(user.Creature, Modifiers)) return false;
            var others = view.All.Where(c => c != user).ToList();
            var friends = others.Where(c => c.Ours && !IsUntouchable(c)).ToList();
            SeenCreature target = user.Creature.Ability switch {
                CreatureAbility.Bodyguard when Level == AILevel.Easy => friends.Count > 0 ? friends[random.Next(friends.Count)] : null,
                CreatureAbility.Bodyguard when Hard => friends.OrderByDescending(c => WouldDie(c) ? c.Worth + 10 : c.Worth).FirstOrDefault(),
                CreatureAbility.Bodyguard => friends.OrderByDescending(c => c.Worth).FirstOrDefault(),
                _ when Level == AILevel.Easy => null,
                CreatureAbility.Wannabe => others.Where(c => c.Known && c.Worth >= user.Worth + 2).OrderByDescending(c => c.Worth).FirstOrDefault(),
                CreatureAbility.IdentityTheft => others.Where(c => !c.Ours && c.Ability != CreatureAbility.None && user.Attack <= 2).FirstOrDefault(),
                CreatureAbility.CreditGrab => others.Where(c => !c.Ours && c.StatRaises >= 2).OrderByDescending(c => c.StatRaises).FirstOrDefault(),
                _ => null
            };
            if (target == null) return false;
            Log($"AI: {user} uses {Abilities.NameOf(user.Creature.Ability)} on {target}", LogTag.Turns);
            queue.AddAction(new UseAbilityAction(user.Creature, target.Slot.Slot));
            return true;
        }
        #endregion

        #region Moves
        // Hard: a creature with nothing to do leaves a slot that harms it, or takes cover on a Shield of ours
        private void MoveOutOfHarm(SeenCreature creature) {
            bool harmed = creature.Slot.VisibleEffect?.Type == SlotEffectType.ScorchedEarth;
            bool exposed = WouldDie(creature) && !creature.Slot.Shielded && !creature.Has(StatusEffectType.Shielded);
            if (!harmed && !exposed) return;
            var to = view.MySlots.Where(s => s.IsFree && ValueOfEntering(s) >= 0 && (harmed || s.Shielded))
                .OrderByDescending(ValueOfEntering).FirstOrDefault(s => !MovementRules.Evaluate(creature.Creature, s.Slot, queue).blocked);
            if (to == null) return;
            Log($"AI: {creature} moves to {to}", LogTag.Turns);
            queue.AddAction(new MoveCreatureAction(creature.Creature, creature.Slot.Slot, to.Slot, view.Me, MovementRules.KindOf(creature.Creature, to.Slot, queue)));
            to.Incoming = creature.Creature; // Taken
        }
        #endregion

        #region A creature as it will be when it counts (Hard: after today's spells)
        private Forecast ForecastOf(SeenCreature creature) {
            if (!forecasts.TryGetValue(creature, out var forecast)) forecasts[creature] = forecast = new Forecast();
            return forecast;
        }

        // Today's spells change it, if what's weighed lands after them
        private Forecast Expected(SeenCreature creature) => seeForecast && forecasts.TryGetValue(creature, out var forecast) ? forecast : null;

        private int AttackOf(SeenCreature creature) => System.Math.Max(0, creature.Attack + (Expected(creature)?.Attack ?? 0));

        private int HealthLeft(SeenCreature creature) {
            var expected = Expected(creature);
            return creature.Health + creature.Armor + (expected?.Health ?? 0) - (expected?.Damage ?? 0)
                - (plannedDamage.TryGetValue(creature, out int damage) ? damage : 0);
        }

        private bool IsStunned(SeenCreature creature) => creature.Has(StatusEffectType.Exhausted) || (Expected(creature)?.Exhausted ?? false);
        private bool IsExposed(SeenCreature creature) => creature.Exposed || (Expected(creature)?.Exhausted ?? false); // Exhausted exposes
        private bool IsUntouchable(SeenCreature creature) => creature.Has(StatusEffectType.Untouchable) || (Expected(creature)?.Untouchable ?? false)
            || creature.Ability == CreatureAbility.Immaculate; // Only a Burn hurts it
        private int HeatOf(SeenCreature creature) => creature.Heat + (Expected(creature)?.Heat ?? 0);
        #endregion

        #region Helpers
        // The creature facing this one, across the table, if any
        private SeenCreature Facing(SeenCreature creature) {
            int index = creature?.Slot?.Index ?? -1;
            if (index < 0) return null;
            return (creature.Ours ? view.TheirSlots : view.MySlots).FirstOrDefault(s => s.Index == index)?.Creature;
        }

        private void AddDamage(SeenCreature creature, int damage) => plannedDamage[creature] = (plannedDamage.TryGetValue(creature, out int done) ? done : 0) + damage;

        private bool IsOurs(object target) => target switch {
            IPlayer player => player == view.Me,
            SeenCreature creature => creature.Ours,
            SeenSlot slot => slot.Ours,
            BoardSlot slot => slot.Owner == view.Me,
            _ => false
        };

        private bool IsQueued(ICard card) => queue.GetPendingActions().OfType<PlayCardAction>().Any(p => p.GetCard() == card);

        private static BoardSlot SlotOf(ITarget aim) => aim switch {
            HomingTarget homing => homing.AimedSlot,
            BoardSlot slot => slot,
            _ => null
        };

        // The creature an aim lands on, as we see it: the one of ours it homes on (maybe on its way), or the one in the slot
        private SeenCreature CreatureAt(ITarget aim) {
            if (aim is HomingTarget homing) return view.Of(homing.Creature) ?? view.IncomingTo(view.Of(homing.AimedSlot));
            return view.Of(SlotOf(aim))?.Creature;
        }

        private void Play(ICard card, ITarget aim) {
            if (!view.Me.Hand.Contains(card)) return; // Given up to pay for another card
            Log($"AI: {view.Me.Name} plays {card.Name} on {aim}", LogTag.Turns);
            // A Send For: the AI picks its card now, so no one is asked on screen
            if (SendFor.Of(card) is EffectAction sending && SendFor.Best(view.Me, SendFor.Choices(view.Me, sending)) is ICard wanted) view.Me.SendForPicks[card] = wanted;
            queue.AddAction(new PlayCardAction(card, view.Me, aim));
        }
        #endregion
    }
}
