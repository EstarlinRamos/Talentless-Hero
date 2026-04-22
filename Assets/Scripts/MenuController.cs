using UnityEngine;
using UnityEngine.InputSystem;
 
/// <summary>
/// Toggles the menu canvas and locks player movement while open.
/// Works with the TabController for page switching.
///
/// Controls:
///   [Tab]        — Toggle menu open/close
///   [Escape]     — Close menu
///   [Left/Right] — Switch tabs (forwarded to TabController)
/// </summary>

public class MenuController : MonoBehaviour
{
    [Header("References")]
    public GameObject menuCanvas;
    public TabController tabController;
    public PlayerMovement playerMovement;
    public Rigidbody2D playerRigidbody;
 
    private bool _isOpen = false;
    private int _currentTab = 0;
    private int _tabCount = 3; // Player, Inventory, Settings
 
    public bool IsOpen => _isOpen;
 
    void Start()
    {
        menuCanvas.SetActive(false);
        _isOpen = false;
    }
 
    void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
 
        // Toggle
        if (kb.tabKey.wasPressedThisFrame)
        {
            if (_isOpen) Close();
            else Open();
            return;
        }
 
        // Close with Escape
        if (_isOpen && kb.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }
 
        // Tab navigation
        if (_isOpen)
        {
            if (kb.rightArrowKey.wasPressedThisFrame)
                SwitchTab(_currentTab + 1);
            else if (kb.leftArrowKey.wasPressedThisFrame)
                SwitchTab(_currentTab - 1);
        }
    }
 
    private void Open()
{
    if (DialogueManager.IsInDialogue) return;
    
        _isOpen = true;
        menuCanvas.SetActive(true);
 
        // Lock player
        if (playerMovement != null)
            playerMovement.enabled = false;
        if (playerRigidbody != null)
            playerRigidbody.linearVelocity = Vector2.zero;
 
        // Reset to first tab
        _currentTab = 0;
        if (tabController != null)
            tabController.ActivateTab(0);
    }
 
    private void Close()
    {
        _isOpen = false;
        menuCanvas.SetActive(false);
 
        // Unlock player
        if (playerMovement != null)
            playerMovement.enabled = true;
    }
 
    private void SwitchTab(int index)
    {
        // Wrap around
        if (index < 0) index = _tabCount - 1;
        if (index >= _tabCount) index = 0;
 
        _currentTab = index;
 
        if (tabController != null)
            tabController.ActivateTab(index);
    }
}