using UnityEngine;

public class Enemy : MonoBehaviour
{
    //[SerializeField] private ""scriptableObjetct"""

    private UnityEngine.AI.NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        agent.updateRotation = false;
        agent.updateUpAxis = false;     
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            Vector3 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            world.z = transform.position.z;
            agent.SetDestination(world);
        }
    }
}
