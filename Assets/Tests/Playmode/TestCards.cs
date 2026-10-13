using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static SlayTheClone.Enums;
using Object = UnityEngine.Object;

namespace SlayTheClone.Tests {
    /// <summary>
    /// Card definitions made up for a test, destroyed with it. Effects are built with <see cref="Effect"/> and <see cref="Status"/>.
    /// </summary>
    public sealed class TestCards : IDisposable {
        private readonly List<Object> created = new List<Object>();

        public CreatureData Creature(string name, int attack, int health, int speed = 1, params CardEffect[] effects) {
            var data = Track(ScriptableObject.CreateInstance<CreatureData>(), name, effects);
            data.attack = attack;
            data.health = health;
            data.speed = speed;
            return data;
        }

        public SpellData Spell(string name, TargetType defaultTarget, params CardEffect[] effects) {
            var data = Track(ScriptableObject.CreateInstance<SpellData>(), name, effects);
            data.defaultTargetType = defaultTarget;
            return data;
        }

        /// <summary>A deck of plain 1/1 creatures with no effects.</summary>
        public List<CardData> Fillers(int count, string name = "Filler") {
            var filler = Creature(name, 1, 1);
            return Enumerable.Repeat<CardData>(filler, count).ToList();
        }

        public static CardEffect Effect(EffectTrigger trigger, ActionType action, int value, TargetType target,
            TargetModifier modifier = TargetModifier.None) {
            return new CardEffect {
                trigger = trigger,
                actions = new List<EffectAction> {
                    new EffectAction { actionType = action, value = value, targetType = target, targetModifier = modifier }
                }
            };
        }

        /// <summary>A World card's effect: puts the World in play for this many Days (GDD 4.2).</summary>
        public static CardEffect WorldEffect(Enums.World world, int days) {
            var effect = Effect(EffectTrigger.OnPlay, ActionType.SetWorld, 0, TargetType.Player);
            effect.actions[0].world = world;
            effect.actions[0].statusDuration = days;
            return effect;
        }

        private T Track<T>(T data, string name, CardEffect[] effects) where T : CardData {
            data.name = name;
            data.cardName = name;
            data.effects = effects.ToList();
            created.Add(data);
            return data;
        }

        public void Dispose() {
            foreach (var data in created) Object.DestroyImmediate(data);
            created.Clear();
        }
    }
}
