using System.Collections;
using UnityEngine;

public class Test : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;
    private ShapeVisualizer shapeVisualizer;
    [SerializeField] private ShapeData shapeData;
    private IEnumerator _stealCoroutine;
    void Awake()
    {
        shapeVisualizer = GetComponent<ShapeVisualizer>();
    }

    void Start()
    {
        if (shapeData != null)
        {
            shapeVisualizer.ApplyShape(shapeData);
        }
        _stealCoroutine = CommenceSteal();
    }
    //when clicked, steals NPC's shape and applies to player
    //currently leaving them as empty husks but its instantaneous
    
    //todo: implementing a goldilocks zone where the player has to remain close while 
    // it transfers the shape, staying too close alerts and raises suspicion
    // get too far and you lose it and risk getting caught trying to steal it again
    public void OnClick()
    {
        if (shapeData == null)
        {
            Debug.LogWarning("No Shape Data assigned to Test interactable");
            return;
        }
        else if (shapeData.type != ShapeType.Husk)
        {
            //goldilock zone
            if (!IsWithinGoldilocksZone())
            {
                StopCoroutine(_stealCoroutine);
                Debug.Log("Not within goldilocks zone, cannot steal shape");
                return;
            }
            else
            {
                Debug.Log("Within goldilocks zone, commencing steal");
                StartCoroutine(CommenceSteal());
            }
        }
        else
        {
            Debug.LogWarning("Husk shape type is not interactable");
        }
        
    }

    //too close, raise suspicion. too far, cancel stealing process and reset to husk
    private bool IsWithinGoldilocksZone()
    {
        float distance = Vector2.Distance(playerController.transform.position, transform.position);
        return distance > 1f && distance < 3f; // Example thresholds for goldilocks zone
        
    }

    IEnumerator CommenceSteal()
    {
        // Placeholder for any animation or delay during the stealing process
        yield return new WaitForSeconds(3f); // Simulate time taken to steal
        playerController.ApplyShape(shapeData);
        shapeData = Resources.Load<ShapeData>("HuskData");
        if (shapeData == null)
        {
            Debug.LogError("Failed to load HuskData ShapeData from Resources");
            yield break;
        }
        shapeVisualizer.ApplyShape(shapeData);
    }
}
