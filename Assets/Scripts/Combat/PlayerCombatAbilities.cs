using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Holds the player's equipped combat abilities (skills and spells).
/// Attach to the Player GameObject.
///
/// CombatUIManager reads from this to populate the skill/spell sub-panels
/// at the start of each combat encounter.
///
/// SETUP:
///   1. Attach to the Player GameObject
///   2. Drag PlayerAbility assets into the skills and spells lists
///   3. CombatUIManager auto-discovers this via the Player tag
/// </summary>
public class PlayerCombatAbilities : MonoBehaviour
{
    [Header("Equipped Skills")]
    [Tooltip("Skills available in combat (physical/utility abilities).")]
    [SerializeField] private List<PlayerAbility> skills = new List<PlayerAbility>();

    [Header("Equipped Spells")]
    [Tooltip("Spells available in combat (magic abilities).")]
    [SerializeField] private List<PlayerAbility> spells = new List<PlayerAbility>();

    public IReadOnlyList<PlayerAbility> Skills => skills.AsReadOnly();
    public IReadOnlyList<PlayerAbility> Spells => spells.AsReadOnly();
}
