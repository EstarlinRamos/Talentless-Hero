using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

/// <summary>
/// Handles zone transitions by updating the Cinemachine confiner boundary,
/// nudging the player position, and notifying the audio system of zone changes.
/// </summary>
public class MapTransition : MonoBehaviour
{
    [SerializeField] PolygonCollider2D mapboundary;
    CinemachineConfiner2D confiner;

    [SerializeField] Direction direction;
    [SerializeField] float additivePos = 2f;

    [Header("Zone Music")]
    [Tooltip("Zone name for this boundary (e.g. T1, V1, P1, C1). Must match ZoneMusicConfig.")]
    [SerializeField] private string zoneName = "";

    enum Direction
    {
        Up,
        Down,
        Left,
        Right
    }

    private void Awake()
    {
        confiner = FindFirstObjectByType<CinemachineConfiner2D>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            confiner.BoundingShape2D = mapboundary;
            UpdatePlayerPosition(collision.transform);

            if (!string.IsNullOrEmpty(zoneName) && AudioManager.Instance != null)
                AudioManager.Instance.EnterZone(zoneName);
        }
    }

    private void UpdatePlayerPosition(Transform player)
    {
        Vector3 newPos = player.position;
        switch (direction)
        {
            case Direction.Up:    newPos.y += additivePos; break;
            case Direction.Down:  newPos.y -= additivePos; break;
            case Direction.Left:  newPos.x -= additivePos; break;
            case Direction.Right: newPos.x += additivePos; break;
        }
        player.position = newPos;
    }
}
