using UnityEngine;

/// <summary>
/// Soul Absorb — Wraith's primary offensive skill.
/// Deals magic damage and heals the Wraith for 20% of damage dealt.
/// </summary>
[CreateAssetMenu(fileName = "SoulAbsorb", menuName = "Talentless Hero/Enemy Skills/Soul Absorb")]
public class SkillSoulAbsorb : EnemySkill
{
    [Header("Soul Absorb Settings")]
    [SerializeField] private float damageMultiplier = 1.2f;
    [SerializeField] private float healPercent = 0.20f;

    public override SkillResult Execute(EnemyStats self, PlayerStats player,
                                        StatusEffectHandler selfEffects,
                                        StatusEffectHandler playerEffects,
                                        CombatTurnManager turnManager)
    {
        float rawDamage = self.MagicDamage * damageMultiplier;
        float reducedDamage = Mathf.Max(rawDamage - player.MagicDefense, 1f);
        int finalDamage = Mathf.RoundToInt(reducedDamage);

        bool hits = selfEffects.HasGuaranteedHit || self.RollHit(player.DodgeChance);

        if (!hits)
        {
            StartCooldown();
            return new SkillResult
            {
                SkillName = skillName,
                LogMessage = $"{self.EnemyName} used {skillName} but missed!",
                DamageDealt = 0,
                HealingDone = 0,
                AppliedEffect = false
            };
        }

        int actualDamage = player.TakeDamage(finalDamage);

        int healAmount = Mathf.RoundToInt(actualDamage * healPercent);
        self.Heal(healAmount);

        StartCooldown();

        string log = $"{self.EnemyName} used {skillName}! Dealt {actualDamage} magic damage, healed for {healAmount} HP.";
        return new SkillResult
        {
            SkillName = skillName,
            LogMessage = log,
            DamageDealt = actualDamage,
            HealingDone = healAmount,
            AppliedEffect = false
        };
    }
}
