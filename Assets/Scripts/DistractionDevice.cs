using UnityEngine;

public class DistractionDevice : MonoBehaviour
{
    [Header("Device Settings")]
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] private float _distractionRadius = 5f;

    void Start()
    {
        Destroy(gameObject, _lifetime);


        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, _distractionRadius);


        foreach (Collider2D hitCollider in hitColliders)
        {

            ControllerNPC npc = hitCollider.GetComponent<ControllerNPC>();
            if (npc != null)
            {
                npc.InvestigatePosition(transform.position);
            }
            else
            {
                Debug.Log($"[DistractionDevice]   -> Sem ControllerNPC neste objeto");
            }
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, _distractionRadius);
    }
}