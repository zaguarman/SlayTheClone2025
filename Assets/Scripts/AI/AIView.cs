using System.Collections.Generic;
using System.Linq;
using static SlayTheClone.Enums;

namespace SlayTheClone {
    /// <summary>
    /// A creature as one side sees it. Its own creatures it knows in full. Of the other side's it knows what their cards show:
    /// a creature it knows (Fog of War, GDD 2.5) shows its stats, ability, statuses and card text; one it doesn't is a card
    /// back in a slot, and everything about it is a guess.
    /// </summary>
    public sealed class SeenCreature {
        // What an unknown creature is taken to be
        public const int GuessedAttack = 2;
        public const int GuessedHealth = 3;

        /// <summary>Only for aiming at it and telling creatures apart: the AI reads nothing else from it.</summary>
        public ICreature Creature { get; }
        public SeenSlot Slot { get; }
        public bool Ours { get; }
        public bool Known { get; }
        /// <summary>Our Gift, planted on their side: theirs to control, ours to keep.</summary>
        public bool Planted { get; }
        /// <summary>On our side, but a Gift from them: it works against us.</summary>
        public bool GiftFromThem { get; }
        public int Attack { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public bool Operative { get; }
        /// <summary>Its ability while switched on; None when it has none, it's off, or it isn't known.</summary>
        public CreatureAbility Ability { get; }
        public int Heat { get; }
        public bool Exposed { get; }
        /// <summary>Damage it answers an attack with (Counterattack, Retaliate), from its card text.</summary>
        public int Retaliation { get; }
        /// <summary>Ambush and still unseen: its damage is doubled (known for ours, and for theirs once we know them).</summary>
        public bool DealsDouble { get; }
        /// <summary>How far its Attack and Health are above its printed ones (what Credit Grab steals).</summary>
        public int StatRaises { get; }
        /// <summary>Damage it deals whoever reveals it (When Revealed), from its card text.</summary>
        public int RevealBacklash { get; }
        private readonly HashSet<StatusEffectType> statuses;

        /// <param name="ours">Ours even before it has an owner: a creature of ours queued to be summoned.</param>
        internal SeenCreature(ICreature creature, SeenSlot slot, IPlayer viewer, IModifierManager modifiers, bool ours = false) {
            Creature = creature;
            Slot = slot;
            Ours = ours || creature.Owner == viewer;
            Known = Ours || Intel.KnowsWhatItIs(viewer, creature);
            Planted = !Ours && creature.DeckOwner == viewer;
            GiftFromThem = Ours && creature.DeckOwner != null && creature.DeckOwner != viewer;
            statuses = new HashSet<StatusEffectType>();
            if (!Known) {
                var disguise = Ours ? null : creature.Disguise; // A decoy shows its fake card's printed numbers
                Attack = disguise?.attack ?? GuessedAttack;
                Health = MaxHealth = disguise?.health ?? GuessedHealth;
                Operative = true;
                return;
            }
            Attack = creature.Attack;
            Health = creature.Health;
            MaxHealth = creature.MaxHealth;
            Armor = creature.CurrentArmorPool;
            Operative = creature.IsOperative;
            Ability = Abilities.IsActive(creature) ? creature.Ability : CreatureAbility.None;
            Heat = creature.Heat;
            Exposed = modifiers.IsExposed(creature);
            foreach (StatusEffectType status in System.Enum.GetValues(typeof(StatusEffectType))) {
                if (status != StatusEffectType.None && modifiers.HasStatusEffect(creature, status) && Intel.CanSeeStatus(viewer, creature, status)) statuses.Add(status);
            }
            StatRaises = creature.Attack - creature.BaseAttack + creature.Health - creature.BaseHealth;
            DealsDouble = slot != null && creature.Slot != null && (Abilities.IsAmbushing(creature, modifiers) || Abilities.IsOnTheBrink(creature));
            RevealBacklash = creature.Effects.Where(e => e.trigger == EffectTrigger.OnRevealed).SelectMany(e => e.actions)
                .Where(a => a.actionType == ActionType.Damage && a.targetType == TargetType.Enemy).Sum(a => a.value);
            Retaliation = creature.Effects.Where(e => e.trigger == EffectTrigger.OnDamage).SelectMany(e => e.actions)
                .Where(a => a.actionType == ActionType.Damage && a.targetType == TargetType.Enemy && a.targetModifier == TargetModifier.None)
                .Sum(a => a.value > 0 ? a.value : creature.Attack);
        }

        public bool Has(StatusEffectType status) => statuses.Contains(status);
        public float Worth => Attack + Health + Armor;

        public override string ToString() => Known ? Creature.Name : "???";
    }

    /// <summary>A board slot as one side sees it: who's there, and its Slot Effect if that side can see it.</summary>
    public sealed class SeenSlot {
        /// <summary>Only for aiming at it: the AI reads nothing else from it.</summary>
        public BoardSlot Slot { get; }
        public bool Ours { get; }
        public int Index => Slot.Index;
        public SeenCreature Creature { get; internal set; }
        /// <summary>Its Slot Effect if this side can see it (hidden traps of the other side are not here).</summary>
        public SlotEffect VisibleEffect { get; }
        /// <summary>Our own creature queued to be summoned here this Day (we know our plans; theirs we don't look at).</summary>
        public ICreature Incoming { get; internal set; }

        internal SeenSlot(BoardSlot slot, IPlayer viewer) {
            Slot = slot;
            Ours = slot.Owner == viewer;
            VisibleEffect = slot.Effect != null && Intel.CanSee(viewer, slot.Effect) ? slot.Effect : null;
        }

        public bool IsEmpty => Creature == null;
        public bool IsFree => Creature == null && Incoming == null;
        public bool Shielded => VisibleEffect?.Type == SlotEffectType.Shield; // No combat damage to the creature here
        public override string ToString() => $"{(Ours ? "our" : "their")} slot {Index}";
    }

    /// <summary>
    /// The game as one side sees it, taken at the start of its planning: the only way the AI looks at the game, so it plays
    /// with the information a player in that seat has (GDD 2.5 Fog of War). Its own hand, board and plans in full; of the
    /// other side, the health, the board as its cards show it (see <see cref="SeenCreature"/>) and the Slot Effects it can see.
    /// Never the other hand, deck or plans: the AI plans at Dawn, as if the other side hadn't planned yet.
    /// </summary>
    public sealed class AIView {
        public IPlayer Me { get; }
        /// <summary>Only for aiming at that side: the AI reads nothing else from it but <see cref="TheirHealth"/>.</summary>
        public IPlayer Them { get; }
        public int MyHealth => Me.Health;
        public int TheirHealth => Them.Health;
        public IReadOnlyList<SeenSlot> MySlots { get; }
        public IReadOnlyList<SeenSlot> TheirSlots { get; }
        private readonly Dictionary<ICreature, SeenCreature> byCreature = new Dictionary<ICreature, SeenCreature>();
        private readonly IModifierManager modifiers;

        public AIView(IPlayer me, IActionsQueue queue) {
            Me = me;
            modifiers = queue.Modifiers;
            Them = me.Opponent;
            MySlots = See(me, queue);
            TheirSlots = See(Them, queue);
        }

        private List<SeenSlot> See(IPlayer side, IActionsQueue queue) {
            var slots = new List<SeenSlot>();
            foreach (var slot in side.Battlefield) {
                var seen = new SeenSlot(slot, Me);
                if (slot.OccupyingCreature != null) {
                    seen.Creature = new SeenCreature(slot.OccupyingCreature, seen, Me, queue.Modifiers);
                    byCreature[slot.OccupyingCreature] = seen.Creature;
                }
                if (seen.Ours) seen.Incoming = EffectResolver.IncomingCreature(slot, queue);
                slots.Add(seen);
            }
            return slots;
        }

        public IEnumerable<SeenCreature> Mine => MySlots.Where(s => s.Creature != null).Select(s => s.Creature);
        public IEnumerable<SeenCreature> Theirs => TheirSlots.Where(s => s.Creature != null).Select(s => s.Creature);
        public IEnumerable<SeenCreature> All => Mine.Concat(Theirs);
        public IEnumerable<SeenSlot> AllSlots => MySlots.Concat(TheirSlots);

        /// <summary>Our creature queued to be summoned into this slot of ours, as we know it; null if none.</summary>
        public SeenCreature IncomingTo(SeenSlot slot) {
            if (slot?.Incoming == null || !slot.Ours) return null;
            if (!byCreature.TryGetValue(slot.Incoming, out var seen) || seen.Slot != slot) {
                seen = new SeenCreature(slot.Incoming, slot, Me, modifiers, ours: true);
                byCreature[slot.Incoming] = seen;
            }
            return seen;
        }

        /// <summary>How this side sees a creature on the board (or one of ours on its way); null for one that isn't.</summary>
        public SeenCreature Of(ITarget creature) => creature is ICreature c && byCreature.TryGetValue(c, out var seen) ? seen : null;

        public SeenSlot Of(BoardSlot slot) => AllSlots.FirstOrDefault(s => s.Slot == slot);
    }
}
