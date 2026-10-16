using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
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
    public class AIPlanner {
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
            var hand = view.Me.Hand.OfType<ICreature>().Where(c => !IsQueued(c)).ToList();
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
                var poisoned = Level == AILevel.Easy ? null : view.MySlots.FirstOrDefault(s => s.Creature is { GiftFromThem: true } && s.Incoming == null);
                if (poisoned != null) {
                    Play(creature, poisoned.Slot);
                    poisoned.Incoming = creature;
                    continue;
                }
                var free = view.MySlots.Where(s => s.IsFree).ToList();
                var slot = Level == AILevel.Easy
                    ? free.Count > 0 ? free[random.Next(free.Count)] : null
                    : free.OrderByDescending(ValueOfEntering).FirstOrDefault()
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
                _ => 0
            };
        }
        #endregion

        #region Spells
        // Easy plays each spell somewhere on its side. Medium plays each one, in hand order, where it does the most good, if
        // anywhere. Hard plays first whichever does the most now, then looks again: each spell is weighed knowing the ones before
        private void PlaySpells() {
            var spells = view.Me.Hand.OfType<Spell>().Where(s => !IsQueued(s)).ToList();
            // A discard cost is paid at random from the rest of the hand: play those last, with what's left over
            var costly = spells.Where(s => DiscardCost.Of(s) > 0).ToList();
            if (Level != AILevel.Easy) spells.RemoveAll(costly.Contains);
            PlayEachSpell(spells);
            if (Level != AILevel.Easy) PlayCostly(costly);
        }

        // Each one whose value is worth it, if the leftovers can pay for it
        private void PlayCostly(List<Spell> costly) {
            foreach (var spell in costly.OrderByDescending(s => s.Effects.SelectMany(e => e.actions).Where(a => a.actionType == ActionType.Draw).Sum(a => a.value))) {
                if (!view.Me.Hand.Contains(spell) || !DiscardCost.CanPay(spell, view.Me, queue)) continue; // Given up for another
                var aim = AimsFor(spell).Select(a => (aim: a, value: SpellValue(spell, a))).OrderByDescending(a => a.value).FirstOrDefault();
                if (aim.aim == null || aim.value <= 0) continue;
                if (DiscardCost.IsChosen(spell)) view.Me.DiscardPicks[spell] = LeastUseful(DiscardCost.Payable(spell, view.Me, queue), DiscardCost.Of(spell));
                Play(spell, aim.aim);
            }
        }

        // A chosen discard cost: the cards it would miss least (a spell by the most good it can do now, a creature by its stats)
        private List<ICard> LeastUseful(List<ICard> cards, int count) =>
            cards.OrderBy(c => c is Spell s ? AimsFor(s).Select(a => SpellValue(s, a)).DefaultIfEmpty(0).Max()
                : c is ICreature creature ? (creature.Attack + creature.Health) / 2f : 0).Take(count).ToList();

        private void PlayEachSpell(List<Spell> spells) {
            if (Level == AILevel.Easy) {
                foreach (var spell in spells) {
                    var aims = AimsFor(spell).ToList();
                    if (aims.Count > 0) Play(spell, EasyAim(spell, aims));
                }
                return;
            }

            while (spells.Count > 0) {
                (Spell spell, ITarget aim, float value) best = (null, null, 0);
                foreach (var spell in Hard ? spells : spells.Take(1).ToList()) {
                    foreach (var aim in AimsFor(spell)) {
                        float value = SpellValue(spell, aim);
                        if (value > best.value) best = (spell, aim, value);
                    }
                }
                if (best.spell == null) {
                    if (Hard) break; // Nothing left does any good
                    spells.RemoveAt(0);
                    continue;
                }
                Play(best.spell, best.aim);
                Expect(best.spell, best.aim);
                spells.Remove(best.spell);
            }
        }

        // What the spell does played there. Hard weighs it both ways when today's other spells could change it: they may
        // resolve before it or after (random order), so each way counts half
        private float SpellValue(Spell spell, ITarget aim) {
            if (!Sacrifice.CanPay(spell, view.Me) || !DoomCost.CanPay(spell, view.Me, Modifiers)) return -100; // Nobody to pay with: it can't be played
            // A Gamble is worth its outcomes on average
            float ValueNow() => (spell.SourceData?.gamble == true && spell.Effects.Count > 0
                ? spell.Effects.Average(e => e.actions.Sum(a => PartValue(a, spell, aim)))
                : spell.Effects.SelectMany(e => e.actions).Sum(a => PartValue(a, spell, aim))) + SacrificeValue(spell);
            // A Prophecy comes true later: worth less, and what it will hit may have changed by then
            if (Prophecy.DaysOf(spell) > 0) return 0.7f * ValueNow();
            if (!Hard || forecasts.Count == 0) return ValueNow();
            seeForecast = true;
            float after = ValueNow();
            seeForecast = false;
            return (after + ValueNow()) / 2f;
        }

        // A part that waits (Dead Cat Bounce's crash) is worth less, like a Prophecy: what it hits may have changed by then
        private float PartValue(EffectAction action, Spell spell, ITarget aim) => Value(action, spell, aim) * (action.delayDays > 0 ? 0.7f : 1f);

        // Remembers what a spell just planned will do: the damage it deals (so nothing is killed twice), and for Hard what it
        // changes before the attacks: stats, stuns, Exposed, Heat, Untouchable
        // What the creatures it costs are worth to us (negative), less what their Last Will and a Revive give back
        private float SacrificeValue(ICard card) {
            float value = 0;
            // Doom: the creature fights on harder for a while, then dies
            var condemned = view.Mine.FirstOrDefault(c => c.Creature == DoomCost.Condemned(card, view.Me, Modifiers));
            if (condemned != null) value -= condemned.Worth / 2f;
            foreach (var victim in Sacrifice.Victims(card, view.Me)) {
                var ours = view.Mine.FirstOrDefault(c => c.Creature == victim);
                if (ours == null) continue;
                bool rises = victim.Effects.Any(e => e.trigger == EffectTrigger.OnDeath && e.actions.Any(a => a.actionType == ActionType.Revive));
                value += -(ours.Worth + HealthLeft(ours)) * (rises ? 0.3f : 1f) + (Hard ? LastWillValue(ours) : 0);
            }
            return value;
        }

        private void Expect(Spell spell, ITarget aim) {
            if (Prophecy.DaysOf(spell) > 0) return; // Nothing happens today
            bool instant = EffectResolver.IsInstant(spell);
            foreach (var action in spell.Effects.SelectMany(e => e.actions)) {
                if (action.targetModifier.HasFlag(TargetModifier.Random)) continue; // Where it lands is luck
                if (action.delayDays > 0) continue; // It lands another Day
                foreach (var creature in TargetsOf(action, aim).OfType<SeenCreature>()) {
                    if (instant && action.actionType == ActionType.ApplyStatus && action.statusEffectToApply == StatusEffectType.Revealed) {
                        revealedNow.Add(creature);
                    }
                    bool gone = action.actionType is ActionType.Destroy or ActionType.TakeControl && Lands(action, creature);
                    if (!Hard) {
                        if (action.actionType == ActionType.Damage) AddDamage(creature, action.value);
                        if (gone) AddDamage(creature, 999);
                        continue;
                    }
                    var forecast = ForecastOf(creature);
                    switch (action.actionType) {
                        case ActionType.Damage:
                            forecast.Damage += action.value;
                            break;
                        case ActionType.Destroy or ActionType.TakeControl when gone:
                            forecast.Damage += 999; // Gone from where it stood before the attacks
                            break;
                        case ActionType.ModifyStat:
                            int change = BentByAbility(action.value, creature);
                            if (action.modifyAttack) forecast.Attack += change;
                            if (action.modifyHealth) forecast.Health += change;
                            break;
                        case ActionType.Stun:
                            forecast.Exhausted = true;
                            break;
                        case ActionType.ApplyStatus when action.statusEffectToApply == StatusEffectType.Exhausted:
                            forecast.Exhausted = true;
                            break;
                        case ActionType.ApplyStatus when action.statusEffectToApply == StatusEffectType.HeatSignature:
                            forecast.Heat += System.Math.Max(1, action.statusPotency);
                            break;
                        case ActionType.ApplyStatus when action.statusEffectToApply == StatusEffectType.Untouchable:
                            forecast.Untouchable = true;
                            break;
                    }
                }
            }
        }

        // Where it can be dropped: any slot for an aimed spell (on our side, homing on the creature that will be there),
        // otherwise the side it does something to (ours when it does something to both)
        private IEnumerable<ITarget> AimsFor(Spell spell) {
            if (EffectResolver.IsAimed(spell)) {
                foreach (var slot in view.MySlots) yield return EffectResolver.AimAt(spell, view.Me, slot.Slot, queue);
                foreach (var slot in view.TheirSlots) yield return slot.Slot;
            }
            else {
                // A spell for whichever side it's played on (Team Building Exercise) is weighed on both
                bool eitherSide = spell.Effects.SelectMany(e => e.actions).Any(a => a.targetModifier.HasFlag(TargetModifier.DroppedSide));
                bool mine = EffectResolver.Reaches(spell, view.Me, view.Me);
                if (mine) yield return view.Me;
                if ((!mine || eitherSide) && EffectResolver.Reaches(spell, view.Me, view.Them)) yield return view.Them;
            }
        }

        // A creature on the side the card names, at random; a slot there if it has none
        private ITarget EasyAim(Spell spell, List<ITarget> aims) {
            if (aims.Count == 1) return aims[0];
            var side = aims.Where(a => IsOurs(SlotOf(a)) == IntendedFor(spell, view.Me)).ToList();
            var withCreature = side.Where(a => CreatureAt(a) != null).ToList();
            var pick = withCreature.Count > 0 ? withCreature : side.Count > 0 ? side : aims;
            return pick[random.Next(pick.Count)];
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

        #region Attacks
        private bool CanAttack(SeenCreature attacker) => Abilities.CanAttack(attacker.Creature) && AttackOf(attacker) > 0 && !IsStunned(attacker);

        // Easy: the slot in front of it
        private List<SeenCreature> AttackInFront(List<SeenCreature> ready) {
            foreach (var attacker in ready.Where(CanAttack)) AttackSlot(attacker, view.TheirSlots[attacker.Slot.Index], attacker.Attack);
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
                var target = view.TheirSlots.OrderByDescending(HitValue).First();
                AttackSlot(attacker, target, Assassin(attacker) && target.Creature != null ? 999 : damage);
            }
            return ready.Where(c => !CanAttack(c)).ToList();
        }

        private float ValueOfHitting(SeenSlot slot, int damage) {
            if (slot.Creature is SeenCreature creature) return -CreatureGoodness(creature, ActionType.Damage, damage) * 0.9f; // A tie goes to the player
            return -PlayerGoodness(view.Them, ActionType.Damage, damage);
        }

        /// <summary>
        /// Hard: lethal if it has it. Otherwise the race: ahead (we'd bring them to 0 in fewer Days than they would us, as
        /// eager as its style is), it only stops real threats; behind, it kills the most threatening creatures, each with the
        /// smallest attacker (or two, for a big threat or when it could die next Day) that does it. The rest go at the player.
        /// It doesn't waste an attack on what can't be hurt (Untouchable, a Shield) and weighs Counterattacks.
        /// </summary>
        private List<SeenCreature> AttackByThreat(List<SeenCreature> ready) {
            var left = ready.Where(CanAttack).ToList();
            var open = view.TheirSlots.FirstOrDefault(s => s.IsEmpty);
            if (open != null && left.Sum(DamageOf) >= view.TheirHealth - plannedFace) {
                foreach (var attacker in left) AttackSlot(attacker, open, DamageOf(attacker));
                return ready.Where(c => !CanAttack(c)).ToList();
            }

            bool defending = TheirPotentialDamage() >= view.MyHealth;
            int ours = System.Math.Max(1, view.Mine.Where(c => c.Operative && CanAttack(c)).Sum(DamageOf));
            int theirs = System.Math.Max(1, TheirPotentialDamage());
            float daysToWin = (view.TheirHealth - plannedFace + ours - 1) / ours, daysToLose = (view.MyHealth + theirs - 1) / theirs;
            bool ahead = open != null && !defending && daysToWin < daysToLose * Style.Aggression;
            foreach (var target in view.Theirs.Where(t => Hittable(t) && (defending || !Condemned(t))).OrderByDescending(Threat).ToList()) {
                var killer = left.Where(a => Hit(a, target) >= HealthLeft(target) && (!Assassin(a) || target.Worth >= 6 || Threat(target) >= 6))
                    .OrderBy(a => Assassin(a) ? 100 : DamageOf(a)).FirstOrDefault();
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
                            where DamageOf(a) <= DamageOf(b) && a != b && Hit(a, target) + Hit(b, target) >= HealthLeft(target)
                            orderby DamageOf(a) + DamageOf(b) select (a, b)).FirstOrDefault();
                if (pair.a == null) continue;
                foreach (var attacker in new[] { pair.a, pair.b }) {
                    AttackSlot(attacker, target.Slot, Hit(attacker, target));
                    left.Remove(attacker);
                }
            }

            // The rest at the player; with no way through, at the creature where the damage counts most
            foreach (var attacker in left.ToList()) {
                var chip = view.Theirs.Where(t => Hittable(t) && !Condemned(t)).OrderByDescending(t => ChipValue(attacker, t)).FirstOrDefault();
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
        private int Hit(SeenCreature attacker, SeenCreature target) => Assassin(attacker) ? 999 :
            DamageOf(attacker) + (IsExposed(target) ? attacker.Ability == CreatureAbility.DoubleTap ? 2 : 1 : 0)
            + (attacker.Ability == CreatureAbility.InsiderJob ? target.Armor : 0);

        private bool Hittable(SeenCreature target) => !IsUntouchable(target) && !target.Slot.Shielded && !target.Planted;

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

        // The biggest hit one of their creatures could land on this one of ours
        private int ThreatOn(SeenCreature ours) {
            int biggest = TheirAttackers().Select(AttackOf).DefaultIfEmpty(0).Max();
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

        #region Moves
        // Hard: a creature with nothing to do leaves a slot that harms it, or takes cover on a Shield of ours
        private void MoveOutOfHarm(SeenCreature creature) {
            bool harmed = creature.Slot.VisibleEffect?.Type == SlotEffectType.ScorchedEarth;
            bool exposed = WouldDie(creature) && !creature.Slot.Shielded;
            if (!harmed && !exposed) return;
            var to = view.MySlots.Where(s => s.IsFree && ValueOfEntering(s) >= 0 && (harmed || s.Shielded))
                .OrderByDescending(ValueOfEntering).FirstOrDefault(s => !MovementRules.Evaluate(creature.Creature, s.Slot, queue).blocked);
            if (to == null) return;
            Log($"AI: {creature} moves to {to}", LogTag.Turns);
            queue.AddAction(new MoveCreatureAction(creature.Creature, creature.Slot.Slot, to.Slot, view.Me));
            to.Incoming = creature.Creature; // Taken
        }
        #endregion

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
                case ActionType.CopyStats or ActionType.StealStatChanges or ActionType.SwapPositions or ActionType.GrantAbility:
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
            _ => 0
        };

        private float PlayerGoodness(IPlayer player, ActionType type, int amount) => type switch {
            ActionType.Damage => amount + (player == view.Them ? plannedFace : 0) >= player.Health ? -Lethal : -amount,
            ActionType.Heal => System.Math.Min(amount, Player.MaxHealth - player.Health),
            ActionType.Draw => amount,
            ActionType.Summon => 2 * amount,
            ActionType.SummonDecoys => 2.5f * amount, // Bodies that soak their attacks and kills
            ActionType.CutDraws => -amount, // Cards they won't have
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
                StatusEffectType.Burned => -System.Math.Max(1, action.statusPotency) * days,
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
                StatusEffectType.Blackout => creature.Ability != CreatureAbility.None || creature.Retaliation > 0 ? -1.5f : -0.5f,
                _ => 0
            };
        }

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
                        _ => 0
                    };
                case ActionType.CleanseSlot:
                    return -ValueOfEntering(slot);
                default:
                    return 0;
            }
        }

        // Which side the card is meant for: the one its default target names (all creatures: either)
        private bool IntendedFor(Spell spell, IPlayer side) => spell == null || spell.DefaultTargetType switch {
            TargetType.Enemy or TargetType.EnemyCreatures => side != view.Me,
            TargetType.Player or TargetType.FriendlyCreatures or TargetType.Self => side == view.Me,
            _ => true
        };
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
        private bool IsUntouchable(SeenCreature creature) => creature.Has(StatusEffectType.Untouchable) || (Expected(creature)?.Untouchable ?? false);
        private int HeatOf(SeenCreature creature) => creature.Heat + (Expected(creature)?.Heat ?? 0);
        #endregion

        #region Helpers
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
