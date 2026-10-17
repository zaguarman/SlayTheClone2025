using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// Keeps this rect centred on another one, whatever the layout does to it: the world behind the title ZEITGEIST (the user,
    /// 2026-10-17) sits out of the title screen's layout and follows the title as the menu builds around it.
    /// </summary>
    [ExecuteAlways]
    public class FollowRect : MonoBehaviour {
        public RectTransform Target;

        private void LateUpdate() {
            if (Target == null) return;
            transform.position = Target.TransformPoint(Target.rect.center);
        }
    }
}
