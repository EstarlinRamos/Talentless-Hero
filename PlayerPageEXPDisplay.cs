using UnityEngine;
using TMPro;

/// <summary>
/// Player page UI showing level and EXP progress as a percentage.
/// </summary>
public class PlayerPageEXPDisplay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI expPercentText;

    private void OnEnable()
    {
        RefreshDisplay();

        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnEXPAwarded += HandleEXPAwarded;
            EXPRewardSystem.Instance.OnLevelUp += HandleLevelUp;
        }
    }

    private void OnDisable()
    {
        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnEXPAwarded -= HandleEXPAwarded;
            EXPRewardSystem.Instance.OnLevelUp -= HandleLevelUp;
        }
    }

    private void HandleEXPAwarded(EXPRewardEntry entry, int currentEXP, int required)
    {
        RefreshDisplay();
    }

    private void HandleLevelUp(int newLevel)
    {
        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        EXPRewardSystem exp = EXPRewardSystem.Instance;
        if (exp == null) return;

        if (levelText != null)
            levelText.text = $"Lv. {exp.GetCurrentLevel()}";

        if (expPercentText != null)
            expPercentText.text = exp.GetLevelProgressString();
    }
}
