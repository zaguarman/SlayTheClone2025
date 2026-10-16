using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.DebugLogger;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>An adventure's relics (Slay the Spire's): kept for the whole run, each one rule that's always on. New ones go last.</summary>
    public enum Relic {
        None,
        FirstAidKit,      // After each fight won, heal 6 Health
        DoubleHeadedCoin, // Each fight won pays 5 more
        CreditCard,       // Shop prices a quarter off
        Backpack,         // Card packs show 4 cards instead of 3
        AirFryer,         // Each Dawn, your most hurt creature heals 2
        Taser,            // A creature that attacks one of yours takes 1 damage
        Coffee,           // Your first creature each fight arrives ready
        Kevlar,           // Your creatures enter with 2 Armor
        ProteinShake,     // Your creatures enter with +1 Health
        MemoNote          // One more spell in your opening hand
    }

    /// <summary>
    /// What the relics do and say (the user, 2026-10-16, from Slay the Spire: elites and treasure rooms give
    /// them). Each is a single rule, checked where it applies: in the run (rewards, shops, Health) or in the fight (the
    /// carrier's <see cref="IPlayer.Relics"/>). They're plain everyday things from the Brainstorm List's Neutral Resources, faction-free, like the potions.
    /// </summary>
    public static class Relics {
        public const int FirstAidHeal = 6, CoinPay = 5, AirFryerHeal = 2, TaserDamage = 1, KevlarArmor = 2;
        public const float CreditDiscount = 0.75f;

        public static readonly Relic[] All = System.Enum.GetValues(typeof(Relic)).Cast<Relic>().Where(r => r != Relic.None).ToArray();

        public static string NameOf(Relic relic) => relic switch {
            Relic.FirstAidKit => "First Aid Kit",
            Relic.DoubleHeadedCoin => "Double Headed Coin",
            Relic.CreditCard => "Credit Card",
            Relic.Backpack => "Backpack",
            Relic.AirFryer => "Air Fryer",
            Relic.Taser => "Taser",
            Relic.Coffee => "Coffee",
            Relic.Kevlar => "Kevlar",
            Relic.ProteinShake => "Protein Shake",
            Relic.MemoNote => "Memo Note",
            _ => relic.ToString()
        };

        public static string Describe(Relic relic) => relic switch {
            Relic.FirstAidKit => $"After each fight you win, heal {FirstAidHeal} Health.",
            Relic.DoubleHeadedCoin => $"Each fight you win pays {CoinPay} more.",
            Relic.CreditCard => "Shop prices are a quarter off.",
            Relic.Backpack => "Card packs show 4 cards instead of 3.",
            Relic.AirFryer => $"Each Dawn, your most hurt creature heals {AirFryerHeal}.",
            Relic.Taser => $"A creature that attacks one of yours takes {TaserDamage} damage.",
            Relic.Coffee => "Your first creature each fight arrives ready.",
            Relic.Kevlar => $"Your creatures enter with {KevlarArmor} Armor.",
            Relic.ProteinShake => "Your creatures enter with +1 Health.",
            Relic.MemoNote => "One more spell in your opening hand.",
            _ => ""
        };

        /// <summary>One line for a list: the name in bold, then what it does.</summary>
        public static string Line(Relic relic) => $"<b>{NameOf(relic)}</b>: {Describe(relic)}";

        /// <summary>A creature entered play on its side: Coffee, Kevlar, Protein Shake.</summary>
        public static void OnEnter(ICreature creature, ActionExecutionContext context) {
            var side = creature?.Owner;
            if (side == null || side.Relics.Count == 0 || creature.IsToken) return;
            if (side.Relics.Contains(Relic.Coffee) && !creature.IsOperative && side.UsedOncePerCombat.Add("relic#Coffee")) {
                creature.MakeOperative();
                context.GameMediator.NotifyCreatureStatsChanged(creature);
                Log($"{side.Name}'s Coffee: {creature.Name} arrives ready", LogTag.Effects);
            }
            if (side.Relics.Contains(Relic.Kevlar)) context.ActionsQueue.AddAction(new ModifyArmorAction(creature, KevlarArmor));
            if (side.Relics.Contains(Relic.ProteinShake)) {
                context.ActionsQueue.AddAction(new ModifyAction(creature, 1, false, true, false, durationInTurns: 0, source: null, controller: side));
            }
        }

        /// <summary>At Dawn: Air Fryer heals each carrier's most hurt creature.</summary>
        public static void ProcessDawn(IEnumerable<IPlayer> players, IActionsQueue queue) {
            foreach (var player in players.Where(p => p.Relics.Contains(Relic.AirFryer))) {
                var hurt = player.Battlefield.Select(s => s.OccupyingCreature)
                    .Where(c => c != null && !c.IsDead && c.Health < c.MaxHealth)
                    .OrderByDescending(c => c.MaxHealth - c.Health).FirstOrDefault();
                if (hurt != null) queue.AddAction(new HealCreatureAction(hurt, AirFryerHeal));
            }
        }
    }
}
