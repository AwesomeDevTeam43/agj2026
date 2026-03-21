using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class Test : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;
    private ShapeVisualizer shapeVisualizer;
    [SerializeField] private ShapeData shapeData;
    [SerializeField] private SuspicionDetector suspicionDetector;
    private Coroutine _stealCoroutine;
    private bool _isStealing = false;

    [SerializeField] private float minStealDistance = 0.8f;
    [SerializeField] private float maxStealDistance = 3f;

    private Queue<float> killHistory = new Queue<float>();


    [Header("Kill Overload Settings")]
    [Tooltip("How many kills can be committed within the time window before suspicion is maxed?")]
    [SerializeField] private int killThreshold = 3;
    [SerializeField] private float killWindow = 10f;

    public static bool GlobalArmActive = false;

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
        if (_isStealing)
        {
            Debug.Log("is stealing right now");
            return; 
        }
        if (IsWithinGoldilocksZone())
        {
            Debug.Log("start stealing");
            _isStealing = true;
            _stealCoroutine = StartCoroutine(CommenceSteal());
        }
        else
        {
            Debug.Log("not in steal range");
            return;
        }
    }

    void Update()
    {
        if (_isStealing && !IsWithinGoldilocksZone())
        {
            Debug.Log("move out of rande");
            CancelSteal();
        }
        while (killHistory.Count > 0 && Time.time - killHistory.Peek() > killWindow)
        {
            killHistory.Dequeue();
        }
    }

    //too close, raise suspicion. too far, cancel stealing process and reset to husk
    private bool IsWithinGoldilocksZone()
    {
        float distance = Vector2.Distance(playerController.transform.position, transform.position);
        return distance > minStealDistance && distance < maxStealDistance; 
        
    }

    private void CancelSteal()
{
    if (_stealCoroutine != null)
    {
        StopCoroutine(_stealCoroutine);
        _stealCoroutine = null;
    }
    _isStealing = false;
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
        killHistory.Enqueue(Time.time);
        //set tag to Draggable
        gameObject.tag = "Draggable";
        _isStealing = false;

        if (killHistory.Count >= killThreshold)
        {
            Debug.Log("Kill threshold exceeded, maxing out suspicion!");
            suspicionDetector.RaiseGlobalAlarm();
        }
    }



    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxStealDistance);
    }
}
