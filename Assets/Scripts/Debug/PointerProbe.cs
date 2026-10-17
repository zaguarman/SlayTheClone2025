#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Zeitgeist {
    /// <summary>
    /// Logs every click in the game (the user's unresponsive slots): what's on top under the pointer, the slot and card it
    /// falls in, and whether it reached them or something else took it, with a running count of the clicks that went
    /// through. Editor only; on while playing. Each line starts with "[Click]" in the console.
    /// </summary>
    public class PointerProbe : MonoBehaviour {
        public static int Clicks { get; private set; }
        public static int WentThrough { get; private set; }
        public static readonly List<string> Lines = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start() {
            Clicks = WentThrough = 0;
            Lines.Clear();
            var probe = new GameObject("PointerProbe") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(probe);
            probe.AddComponent<PointerProbe>();
        }

        private void Update() {
            if (!GameInput.ClickedThisFrame || EventSystem.current == null) return;
            var position = GameInput.PointerPosition;
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            var top = hits.Count > 0 ? hits[0].gameObject : null;

            // The slot the pointer is over, whatever covers it: by the slots' own rectangles
            BattlefieldSlot under = null;
            foreach (var slot in FindObjectsByType<BattlefieldSlot>(FindObjectsSortMode.None)) {
                if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, position, CanvasUtility.GetEventCamera(slot))) under = slot;
            }
            if (under == null && top == null) return; // Off the table
            Clicks++;
            var card = top != null ? top.GetComponentInParent<CardController>() : null;
            bool through = under == null ? top != null : top != null && (top.transform.IsChildOf(under.transform) || top == under.gameObject);
            if (through) WentThrough++;
            string slotName = under == null ? "no slot" : $"{under.Model?.Owner?.Name} slot {under.Model?.Index}";
            string what = top == null ? "nothing" : $"{top.name} (in {top.transform.parent?.name})";
            string line = $"[Click] #{Clicks} at {position}: {slotName}; on top {what}; card {(card != null ? card.GetLinkedCardInstance()?.Name ?? card.name : "none")}; "
                          + (through ? "went through" : "TAKEN BY SOMETHING ELSE") + $" ({WentThrough}/{Clicks} so far)";
            Lines.Add(line);
            Debug.Log(line);
        }
    }
}
#endif
