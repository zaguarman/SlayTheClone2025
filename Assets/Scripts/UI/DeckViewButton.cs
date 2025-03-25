using UnityEngine;
using UnityEngine.UI;
using static DebugLogger;

public class DeckViewButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private DeckViewUI deckViewUI;
    
    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
        }
    }
    
    private void Start()
    {
        if (deckViewUI == null)
        {
            // Get the DeckViewUI from GameReferences
            deckViewUI = GameReferences.Instance?.GetDeckViewUI();
            
            if (deckViewUI == null)
            {
                LogError("DeckViewUI reference not found", LogTag.UI | LogTag.Initialization);
            }
        }
    }
    
    private void OnButtonClick()
    {
        if (deckViewUI != null)
        {
            deckViewUI.ShowDeckView();
            Log("Deck view button clicked", LogTag.UI);
        }
        else
        {
            LogError("Cannot show deck view - DeckViewUI reference is missing", LogTag.UI);
        }
    }
    
    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}
