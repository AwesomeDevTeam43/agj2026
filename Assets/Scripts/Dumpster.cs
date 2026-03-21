using UnityEngine;

public class Dumpster : MonoBehaviour
{
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Draggable"))
        {
            Debug.Log("Body disposed in dumpster!");
            Destroy(other.gameObject);
        }
    }
}