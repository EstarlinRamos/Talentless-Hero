using UnityEngine;
using TMPro;

/// <summary>
/// Attach to your combat results panel.
/// Shows exact EXP earned and progress toward next level.
/// Example output: "+45 EXP" and "320 / 500 to next level"
/// </summary>
public class CombatEXPDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI expEarnedText;
    [SerializeField] private TextMeshProUGUI expProgressText;
    [SerializeField] private TextMeshProUGUI levelUpText;

    private void OnEnable()
    {
        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnEXPAwarded += ShowEXPGain;
            EXPRewardSystem.Instance.OnLevelUp += ShowLevelUp;
        }

        if (levelUpText != null)
            levelUpText.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (EXPRewardSystem.Instance != null)
        {
            EXPRewardSystem.Instance.OnEXPAwarded -= ShowEXPGain;
            EXPRewardSystem.Instance.OnLevelUp -= ShowLevelUp;
        }
    }

    private void ShowEXPGain(EXPRewardEntry entry, int currentEXP, int required)
    {
        if (expEarnedText != null)
            expEarnedText.text = $"+{entry.amount} EXP";

        if (expProgressText != null)
            expProgressText.text = $"{currentEXP} / {required} to next level";
    }

    private void ShowLevelUp(int newLevel)
    {
        if (levelUpText != null)
        {
            levelUpText.gameObject.SetActive(true);
            levelUpText.text = $"LEVEL UP! Level {newLevel}";
        }
    }
}
