using UnityEngine;

/// <summary>
/// Invisible trigger zone that starts a cutscene when the player enters.
/// Fires once then disables itself so it can't re-trigger.
///
/// SETUP:
///   1. Create an empty GameObject where you want the trigger line
///   2. Add a BoxCollider2D, check "Is Trigger", size it as a thin strip
///   3. Attach this script
///   4. Assign the CutsceneDirector reference in the Inspector
///   5. The pink gizmo shows the trigger area in Scene view
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CutsceneTrigger : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CutsceneDirector director;

    [Header("Settings")]
    [Tooltip("If true, the trigger is removed after firing once.")]
    [SerializeField] private bool oneShot = true;

    private bool _hasFired = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_hasFired) return;
        if (!other.CompareTag("Player")) return;

        _hasFired = true;

        if (director != null)
        {
            director.StartCutscene();
            Debug.Log("[CutsceneTrigger] Goddess cutscene started.");
        }
        else
        {
            Debug.LogError("[CutsceneTrigger] No CutsceneDirector assigned!");
        }

        if (oneShot)
            gameObject.SetActive(false);
    }

    /// <summary>
    /// Pink gizmo so you can see the trigger zone in Scene view.
    /// </summary>
    private void OnDrawGizmos()
    {
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box == null) return;

        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.4f);
        Vector3 center = transform.position + (Vector3)box.offset;
        Vector3 size = box.size;
        Gizmos.DrawCube(center, size);

        Gizmos.color = new Color(1f, 0.4f, 0.7f, 0.8f);
        Gizmos.DrawWireCube(center, size);
    }
}
