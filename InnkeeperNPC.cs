using UnityEngine;

/// <summary>
/// Innkeeper NPC — triggers the rest mechanic when the player interacts.
/// Attach to the Innkeeper GameObject with a Collider2D set to "Is Trigger".
/// </summary>
public class InnkeeperNPC : MonoBehaviour
{
    [Tooltip("The key the player presses to interact.")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;

    private bool _playerInRange = false;
    private PlayerStats _playerStats;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = true;
            _playerStats = other.GetComponent<PlayerStats>();
            Debug.Log("[Innkeeper] Press E to rest.");
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            _playerInRange = false;
            _playerStats = null;
        }
    }

    private void Update()
    {
        if (_playerInRange && Input.GetKeyDown(interactKey) && _playerStats != null)
        {
            _playerStats.Rest();
            Debug.Log("[Innkeeper] Sweet dreams! HP and MP fully restored. Enemies respawned.");
        }
    }
}
