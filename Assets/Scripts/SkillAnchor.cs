using UnityEngine;

/// <summary>
/// Anchor — Reduces the player's AGI by 25% for 2 turns.
/// Stacks multiplicatively with itself.
/// </summary>
[CreateAssetMenu(fileName = "Anchor", menuName = "Talentless Hero/Enemy Skills/Anchor")]
public class SkillAnchor : EnemySkill
{
    [Header("Anchor Settings")]
    [SerializeField] private float agiReduction = 0.25f;
    [SerializeField] private int duration = 2;

    public override SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager)
    {
        StatusEffect effect = new StatusEffect(
            "Anchor",
            StatusEffect.EffectType.StatDebuff,
            duration,
            agiReduction,
            StatType.Agility,
            self,
            true
        );

        playerEffects.ApplyEffect(effect);
        StartCooldown();

        string log = $"{self.EnemyName} used {skillName}! Player's AGI reduced by {agiReduction:P0} for {duration} turns.";
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
