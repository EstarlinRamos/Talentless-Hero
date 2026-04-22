using UnityEngine;

/// <summary>
/// Silence — Prevents the player from using skills/spells for 1 turn.
/// AI won't use this if the player is already silenced.
/// </summary>
[CreateAssetMenu(fileName = "Silence", menuName = "Talentless Hero/Enemy Skills/Silence")]
public class SkillSilence : EnemySkill
{
    [Header("Silence Settings")]
    [SerializeField] private int duration = 1;

    public override bool CanUse(EnemyStats self, PlayerStats player,
                                StatusEffectHandler selfEffects,
                                StatusEffectHandler playerEffects)
    {
        return IsReady && !playerEffects.IsSilenced;
    }

    public override SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager)
    {
        StatusEffect effect = new StatusEffect(
            "Silence",
            StatusEffect.EffectType.Silence,
            duration,
            0f,
            StatType.Strength,
            self,
            false
        );

        playerEffects.ApplyEffect(effect);
        StartCooldown();

        string log = $"{self.EnemyName} used {skillName}! Player is silenced for {duration} turn(s).";
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
