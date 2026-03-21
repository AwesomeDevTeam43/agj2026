using UnityEngine;

public class DistractionDevice : MonoBehaviour
{
    [Header("Device Setting")]
    [SerializeField] private float _lifetime = 3f;
    [SerializeField] private float _distractionRadius;


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
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, _distractionRadius);
    }
}