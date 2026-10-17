using System.Collections.Generic;
using System.Linq;
using static Zeitgeist.DebugLogger;
using static Zeitgeist.Enums;

namespace Zeitgeist {
    /// <summary>The AI: which spells it plays, where (a part of AIPlanner).</summary>
    public partial class AIPlanner {

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
        // Creatures not played today are discarded at Dusk anyway: they're the cheapest to give up
        private List<ICard> LeastUseful(List<ICard> cards, int count) =>
            cards.OrderBy(c => (c is Spell s ? AimsFor(s).Select(a => SpellValue(s, a)).DefaultIfEmpty(0).Max()
                : c is ICreature creature ? (creature.Attack + creature.Health) / 2f * 0.3f : 0)
                - (LaidOff.Has(c) ? LaidOffGain : 0)).Take(count).ToList(); // A Laid Off card does something when given up

        // How much good a spell must do today to be played rather than kept for a better Day (spells stay in the hand)
        private const float HoldBar = 1f;
        private const float RipeningWait = 1.5f;
        private const float LaidOffGain = 1.5f;
        private const float PolicyWorth = 2.5f; // A Policy happens again each Dawn: about this many times its first go

        private void PlayEachSpell(List<Spell> spells) {
            if (Level == AILevel.Easy) {
                foreach (var spell in spells) {
                    var aims = AimsFor(spell).ToList();
                    if (aims.Count > 0) Play(spell, EasyAim(spell, aims));
                }
                return;
            }

            // Spells stay in the hand (5 at most): one that does little today waits, unless the hand is full and new ones would be lost
            bool full = Player.SpellsIn(view.Me.Hand) >= Player.MaxSpellsInHand;
            float bar = full ? 0f : HoldBar;
            // A Ripening spell that can still ripen asks for more today: tomorrow it's better (unless the hand is full)
            float Ripens(Spell spell) => !full && Ripening.Has(spell) && spell.Ripeness < Ripening.Most ? RipeningWait : 0;
            while (spells.Count > 0) {
                (Spell spell, ITarget aim, float value) best = (null, null, bar);
                foreach (var spell in Hard ? spells : spells.Take(1).ToList()) {
                    foreach (var aim in AimsFor(spell)) {
                        float value = SpellValue(spell, aim) - Ripens(spell);
                        if (value > best.value) best = (spell, aim, value);
                    }
                }
                if (best.spell == null) {
                    if (Hard) break; // Nothing left does any good
                    spells.RemoveAt(0);
                    continue;
                }
                // Hard: a kill worth more on the creature of theirs that is ready next Day waits for it (the look ahead)
                if (!full && BetterTomorrow(best.spell, best.aim, best.value)) {
                    Log($"AI: {view.Me.Name} keeps {best.spell.Name} for tomorrow", LogTag.Turns);
                    spells.Remove(best.spell);
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
            // A Policy happens again each Dawn: worth several times what it does now (the sooner, the more)
            if (Policy.Has(spell)) return PolicyWorth * System.Math.Max(0.5f, ValueNow());
            // Combo: worth more with each other spell of ours today (its numbers grow by 1 for each)
            if (Combo.Has(spell)) return ValueNow() * (1f + 0.5f * Combo.OthersToday(spell, view.Me, queue));
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
    }
}
