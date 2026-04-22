using UnityEngine;

/// <summary>
/// Holds a dedicated side-view combat sprite for a combatant.
/// Attach alongside PlayerStats or EnemyStats.
///
/// The overworld uses top-down sprites with directional animation.
/// Combat uses side-view sprites facing each other horizontally,
/// similar to FGO or side-scroller RPGs.
///
/// CombatPlate reads from this component to display the correct sprite.
/// If not found, falls back to a colored placeholder.
///
/// SETUP:
///   1. Attach to the Player or Enemy GameObject
///   2. Assign the side-view combat sprite
///   3. Optionally assign an idle animation controller for combat
///   4. The plate will automatically flip the sprite based on side
///      (allies face right, enemies face left)
/// </summary>
public class CombatSpriteHolder : MonoBehaviour
{
    [Header("Combat Sprite")]
    [Tooltip("Side-view sprite used in plate combat. " +
             "Should face RIGHT by default — enemies are auto-flipped to face left.")]
    [SerializeField] private Sprite combatSprite;

    [Header("Optional Combat Animation")]
    [Tooltip("Animator controller for combat idle/attack animations. " +
             "Leave empty if using a static sprite.")]
    [SerializeField] private RuntimeAnimatorController combatAnimatorController;

    /// <summary>The side-view combat sprite (facing right by default).</summary>
    public Sprite CombatSprite => combatSprite;

    /// <summary>Optional animator controller for combat animations.</summary>
    public RuntimeAnimatorController CombatAnimator => combatAnimatorController;
}
