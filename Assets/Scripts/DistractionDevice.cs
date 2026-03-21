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
            if (hitCollider.CompareTag("Guard"))
            {
                // GONKI ESTA LOGICA E PARA TU IMPLEMENTARES PARA OS GUARDAS
                // Exemplo: hitCollider.GetComponent<GuardAI>().Investigate(transform.position);
                Debug.Log($"Guard {hitCollider.name} is investigating the distraction!");
            }
        }
    }
}