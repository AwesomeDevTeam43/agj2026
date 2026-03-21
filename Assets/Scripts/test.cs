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

    [SerializeField] private float maxStealDistance = 3f;
    [SerializeField] private float highSuspicionOnStealRange = 1.5f;
    private Coroutine _closeSuspicionRoutine;

    private Queue<float> killHistory = new Queue<float>();


    [Header("Kill Overload Settings")]
    [Tooltip("How many kills can be committed within the time window before suspicion is maxed?")]
    [SerializeField] private int killThreshold = 3;
    [SerializeField] private float killWindow = 10f;

    public static bool GlobalArmActive = false;

    void Awake()
    {
        shapeVisualizer = GetComponent<ShapeVisualizer>();
        suspicionDetector = GetComponentInParent<SuspicionDetector>();
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
        /*f (_isStealing && !IsWithinGoldilocksZone())
        {
            Debug.Log("move out of rande");
            CancelSteal();
        }*/
        Steal();
    }

    void Steal()
    {
        if (!_isStealing) return;

        float distance = Vector2.Distance(playerController.transform.position, transform.position);

        if (distance >= maxStealDistance)
        {
            Debug.Log("too far, steal canceled");
            CancelSteal();
            return;
        }

        if (distance < highSuspicionOnStealRange)
        {
            if (_closeSuspicionRoutine == null && suspicionDetector != null)
            {
                Debug.Log("Entered high suspicion zone during steal");
                _closeSuspicionRoutine = suspicionDetector.StartCoroutine(suspicionDetector.RaiseSuspicion());
            }
        }
        else
        {
            if (_closeSuspicionRoutine != null)
            {
                Debug.Log("Left high suspicion zone during steal");
                suspicionDetector.StopCoroutine(_closeSuspicionRoutine);
                _closeSuspicionRoutine = null;
            }
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
        return distance < maxStealDistance;
    }

    private void CancelSteal()
    {
        if (_stealCoroutine != null)
        {
            StopCoroutine(_stealCoroutine);
            _stealCoroutine = null;
        }
        if (_closeSuspicionRoutine != null)
        {
            suspicionDetector.StopCoroutine(_closeSuspicionRoutine);
            _closeSuspicionRoutine = null;
        }
        _isStealing = false;
    }

    IEnumerator CommenceSteal()
    {
        // Placeholder for any animation or delay during the stealing process
        yield return new WaitForSeconds(3f);
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
        gameObject.tag = "Draggable";
        _isStealing = false;
        CancelSteal();
    }



    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxStealDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, highSuspicionOnStealRange);
    }
}
