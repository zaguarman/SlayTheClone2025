using UnityEngine;

namespace SlayTheClone {
    /// <summary>
    /// A screen opened from the title screen in its place (the card gallery, the deck builder, the keyword glossary). It's
    /// built anew each time it opens and thrown away when it closes, so it always opens as new: on its first entry,
    /// scrolled to the top. Escape goes back to the title screen. Built with <see cref="MenuWidgets"/>.
    /// </summary>
    public abstract class MenuScreen : MonoBehaviour {
        private GameObject titleScreen;
        private bool wasTyping;

        protected DeckCatalog Catalog { get; private set; }
        protected GameObject Panel { get; private set; }
        public bool IsOpen => Panel != null && Panel.activeSelf;

        public void Setup(DeckCatalog deckCatalog, GameObject title) {
            Catalog = deckCatalog;
            titleScreen = title;
        }

        public void Show() {
            if (Catalog == null || Catalog.CardPrefab == null) {
                Debug.LogWarning($"{GetType().Name} needs a deck catalog with a card prefab (Cards > Generate Faction Decks sets it)");
                return;
            }
            Close();
            Panel = Build();
            if (titleScreen != null) titleScreen.SetActive(false);
            Opened();
        }

        public void Hide() {
            Close();
            if (titleScreen != null) titleScreen.SetActive(true);
        }

        protected virtual void Update() {
            if (MenuWidgets.BackPressed(ref wasTyping) && IsOpen) Hide();
        }

        /// <summary>Builds the screen's panel, under this object.</summary>
        protected abstract GameObject Build();

        /// <summary>Shows the first entry, once built.</summary>
        protected abstract void Opened();

        /// <summary>Lets go of everything in the panel, which is about to be destroyed.</summary>
        protected abstract void Forget();

        private void Close() {
            Tooltip.Existing?.HideTooltip();
            if (Panel == null) return;
            Forget();
            Panel.SetActive(false);
            Panel.transform.SetParent(null, false); // Gone from the menu at once; destroyed at the end of the frame
            Destroy(Panel);
            Panel = null;
        }
    }
}
