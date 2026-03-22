using System;
using UnityEngine;
using System.Linq;

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
        Debug.Log($"[Treasure] Something collided with {gameObject.name}: {collision.gameObject.name} (Tag: {collision.tag})");

        // Check if the object colliding is the player
        if (collision.CompareTag("Player"))
        {
            // Tell the GameManager we collected this specific treasure
            if (GameManager.Instance != null)
            {
                GameManager.Instance.CollectTreasure(_locale);
                foreach (var observer in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<ISeesPlayerActions>())
                {
                    observer.OnPlayerPickedUpTreasure(collision.gameObject);
                }
            }
            else
            {
                Debug.LogError("[Treasure] GameManager.Instance is NULL! Cannot collect.");
            }

            // Destroy the treasure so it disappears from the map
            Destroy(gameObject);
        }
    }
}
