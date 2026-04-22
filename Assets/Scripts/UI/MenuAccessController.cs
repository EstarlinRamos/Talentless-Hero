using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls access to the Player Page and Inventory via the Tab key.
/// Blocks Tab during combat — inventory access during combat will be
/// handled by a dedicated combat UI button if added later.
///
/// Also blocks Tab during dialogue to prevent UI conflicts.
///
/// SETUP:
///   1. Attach to a persistent GameObject (e.g., the Player or a UI manager)
///   2. Assign the player page panel and settings panel references
///   3. Tab toggles the player page on/off
///   4. Escape closes any open panel
/// </summary>
public class MenuAccessController : MonoBehaviour
{
    [Header("Panels")]
    [Tooltip("The Player Page / Inventory panel toggled by Tab.")]
    [SerializeField] private GameObject playerPagePanel;

    [Tooltip("The Settings panel toggled by Escape.")]
    [SerializeField] private GameObject settingsPanel;

    private void Update()
    {
        if (Keyboard.current == null) return;

        // TAB — Toggle Player Page (blocked during combat and dialogue)
        if (Keyboard.current.tabKey.wasPressedThisFrame)
        {
            if (CombatUIManager.IsInCombat)
            {
                Debug.Log("[MenuAccess] Tab blocked during combat.");
                return;
            }

            if (DialogueManager.IsInDialogue)
            {
                Debug.Log("[MenuAccess] Tab blocked during dialogue.");
                return;
            }

            TogglePlayerPage();
        }

        // ESCAPE — Close any open panel, or open Settings
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // If player page is open, close it first
            if (playerPagePanel != null && playerPagePanel.activeSelf)
            {
                playerPagePanel.SetActive(false);
                return;
            }

            // If settings is open, close it
            if (settingsPanel != null && settingsPanel.activeSelf)
            {
                settingsPanel.SetActive(false);
                return;
            }

            // Otherwise, open settings (if not in combat or dialogue)
            if (!CombatUIManager.IsInCombat && !DialogueManager.IsInDialogue)
            {
                if (settingsPanel != null)
                    settingsPanel.SetActive(true);
            }
        }
    }

    private void TogglePlayerPage()
    {
        if (playerPagePanel == null) return;

        bool opening = !playerPagePanel.activeSelf;
        playerPagePanel.SetActive(opening);

        // Close settings if opening player page
        if (opening && settingsPanel != null && settingsPanel.activeSelf)
            settingsPanel.SetActive(false);
    }
}
