using UnityEngine;

/// <summary>
/// Wraith's Third Eye — Self-buff: all attacks guaranteed to hit for 2 turns.
/// </summary>
[CreateAssetMenu(fileName = "WraithsThirdEye", menuName = "Talentless Hero/Enemy Skills/Wraiths Third Eye")]
public class SkillWraithsThirdEye : EnemySkill
{
    [Header("Third Eye Settings")]
    [SerializeField] private int duration = 2;

    public override bool CanUse(EnemyStats self, PlayerStats player,
                                StatusEffectHandler selfEffects,
                                StatusEffectHandler playerEffects)
    {
        return IsReady && !selfEffects.HasGuaranteedHit;
    }

    public override SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager)
    {
        StatusEffect effect = new StatusEffect(
            "Wraith's Third Eye",
            StatusEffect.EffectType.GuaranteedHit,
            duration,
            0f,
            StatType.Strength,
            self,
            false
        );

        selfEffects.ApplyEffect(effect);
        StartCooldown();

        string log = $"{self.EnemyName} activated {skillName}! All attacks will hit for {duration} turns.";
        return new SkillResult
        {
            SkillName = skillName,
            LogMessage = log,
            DamageDealt = 0,
            HealingDone = 0,
            AppliedEffect = true
        };
    }
}
