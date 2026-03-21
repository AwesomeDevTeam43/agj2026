using System;
using UnityEngine;

public class Teasure : MonoBehaviour
{
    public enum RoomLocale
    {
        StaffRoom,
        VipRoom,
        OfficeRoom
    }

    [SerializeField] private RoomLocale _locale;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Check if the object colliding is the player
        if (collision.CompareTag("Player"))
        {
            // Tell the GameManager we collected this specific treasure
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectTreasure(_locale);
            }

            // Destroy the treasure so it disappears from the map
            Destroy(gameObject);
        }
    }
}
