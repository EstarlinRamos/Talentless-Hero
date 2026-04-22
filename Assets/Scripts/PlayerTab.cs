using UnityEngine;
using TMPro;

/// <summary>
/// Player tab — simple numbers only. No derived stats, no bars.
/// Matches the UI layout:
///   Hero Icon / Hero Name / Level / Points
///   HP / MP / STR [+] / INT [+] / AGI [+] / HIT [+] / LCK [+]
/// </summary>
public class PlayerTab : MonoBehaviour
{
    [Header("Player Data")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Header")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text pointsText;

    [Header("Stats")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text mpText;
    [SerializeField] private TMP_Text strText;
    [SerializeField] private TMP_Text intText;
    [SerializeField] private TMP_Text agiText;
    [SerializeField] private TMP_Text hitText;
    [SerializeField] private TMP_Text lckText;

    [Header("Allocate Buttons")]
    [SerializeField] private UnityEngine.UI.Button strButton;
    [SerializeField] private UnityEngine.UI.Button intButton;
    [SerializeField] private UnityEngine.UI.Button agiButton;
    [SerializeField] private UnityEngine.UI.Button hitButton;
    [SerializeField] private UnityEngine.UI.Button lckButton;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (playerStats == null) return;

        // Header
        if (levelText != null)
            levelText.text = playerStats.Level.ToString();

        if (pointsText != null)
            pointsText.text = playerStats.UnspentPoints.ToString();

        // HP / MP — just the numbers
        if (hpText != null)
            hpText.text = $"{playerStats.CurrentHP} / {playerStats.MaxHP}";

        if (mpText != null)
            mpText.text = $"{playerStats.CurrentMP} / {playerStats.MaxMP}";

        // Stats — just the total number
        if (strText != null) strText.text = playerStats.Strength.ToString();
        if (intText != null) intText.text = playerStats.Intelligence.ToString();
        if (agiText != null) agiText.text = playerStats.Agility.ToString();
        if (hitText != null) hitText.text = playerStats.Hit.ToString();
        if (lckText != null) lckText.text = playerStats.Luck.ToString();

        // Buttons — gray out when no points
        bool canAllocate = playerStats.UnspentPoints > 0;
        if (strButton != null) strButton.interactable = canAllocate;
        if (intButton != null) intButton.interactable = canAllocate;
        if (agiButton != null) agiButton.interactable = canAllocate;
        if (hitButton != null) hitButton.interactable = canAllocate;
        if (lckButton != null) lckButton.interactable = canAllocate;
    }

    // Wire each button's OnClick to one of these
    public void AllocateSTR() { Allocate(StatType.Strength); }
    public void AllocateINT() { Allocate(StatType.Intelligence); }
    public void AllocateAGI() { Allocate(StatType.Agility); }
    public void AllocateHIT() { Allocate(StatType.Hit); }
    public void AllocateLCK() { Allocate(StatType.Luck); }

    private void Allocate(StatType stat)
    {
        if (playerStats == null) return;
        if (playerStats.AllocatePoint(stat))
            Refresh();
    }
}
