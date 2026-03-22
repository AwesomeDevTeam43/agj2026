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

    [SerializeField] private LineRenderer line;
    [SerializeField] private float stealCoyoteTime = 1f;
    [SerializeField] private float coyoteBlinkSpeed = 10f;
    private float _lastTimeWithinStealRange = float.NegativeInfinity;
    private bool _isInStealCoyoteTime = false;

    public static Queue<float> killHistory = new Queue<float>();


    [Header("Kill Overload Settings")]
    [Tooltip("How many kills can be committed within the time window before suspicion is maxed?")]
    [SerializeField] private int killThreshold = 3;
    [SerializeField] private float killWindow = 40f;

    public static bool GlobalArmActive = false;

    void Awake()
    {
        shapeVisualizer = GetComponent<ShapeVisualizer>();
        suspicionDetector = GetComponentInParent<SuspicionDetector>();
        
        // Ensure LineRenderer exists
        line = GetComponent<LineRenderer>();
        if (line == null)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.material = new Material(Shader.Find("Sprites/Default")); // Gives it a basic solid color material
        }

        if (line != null)
        {
            line.widthCurve = new AnimationCurve(new Keyframe(0, 0.05f), new Keyframe(1, 0.05f));
            line.positionCount = 2;
            line.useWorldSpace = true;
            line.enabled = false;
        }

        // Find the player automatically if not assigned
        if (playerController == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                playerController = playerObj.GetComponent<ShapeVisualizer>();
            }
            else
            {
                // Fallback in case the Player tag isn't set, find the PlayerController script
                PlayerController pc = Object.FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    playerController = pc.GetComponent<ShapeVisualizer>();
                }
            }

            if (playerController == null)
            {
                Debug.LogWarning("PlayerController (ShapeVisualizer) could not be found automatically by Test.cs!");
            }
        }
    }

    void Start()
    {
        if (shapeData != null)
        {
            shapeVisualizer.ApplyShape(shapeData);
        }
    }

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
        Steal();
        UpdateStealLine();
    }

    void Steal()
    {
        if (!_isStealing) return;

        float distance = Vector2.Distance(playerController.transform.position, transform.position);
        
        if (distance >= maxStealDistance)
        {
            // If we've exceeded the coyote time limit, cancel the steal
            if (Time.time - _lastTimeWithinStealRange > stealCoyoteTime)
            {
                Debug.Log("too far and coyote time expired, steal canceled");
                CancelSteal();
                return;
            }
            else
            {
                // We are out of range but still within coyote time
                _isInStealCoyoteTime = true;
            }
        }
        else
        {
            // We are within range, reset the coyote timer tracker and flag
            _lastTimeWithinStealRange = Time.time;
            _isInStealCoyoteTime = false;
        }

        // --- Keep the rest of your Steal() logic (suspicion ranges & kill history) ---
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
            AudioManager.Instance.Stop("hack");
            _stealCoroutine = null;
        }
        if (_closeSuspicionRoutine != null)
        {
            suspicionDetector.StopCoroutine(_closeSuspicionRoutine);
            _closeSuspicionRoutine = null;
        }
        _isStealing = false;
        _isInStealCoyoteTime = false; // Add this reset

        if (line != null) line.enabled = false;
    }

    private void UpdateStealLine()
    {
        if (line == null) return;
        if (!_isStealing || playerController == null)
        {
            line.enabled = false;
            return;
        }

        Color baseColor = playerController.shapeData.color;

        // Make the line blink if in coyote time
        if (_isInStealCoyoteTime)
        {
            float blinkValue = Mathf.PingPong(Time.time * coyoteBlinkSpeed, 1f);
            Color blinkColor = Color.Lerp(baseColor, Color.clear, blinkValue);
            line.startColor = blinkColor;
            line.endColor = blinkColor;
        }
        else
        {
            line.startColor = baseColor;
            line.endColor = baseColor;
        }

        Vector3 a = playerController.transform.position;
        Vector3 b = transform.position;

        a.z = -0.1f;
        b.z = -0.1f;

        line.enabled = true;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
    }

    IEnumerator CommenceSteal()
    {
        AudioManager.Instance.Play("hack");
        // Placeholder for any animation or delay during the stealing process
        if (shapeData.type != ShapeType.Husk)
        {
            float stealDuration = 3f;
            float currentStealTime = 0f;

            // Loop until the required time is met
            while (currentStealTime < stealDuration)
            {
                // Only increment the timer if we are NOT in coyote time
                if (!_isInStealCoyoteTime)
                {
                    currentStealTime += Time.deltaTime;
                }

                // Wait for the next frame
                yield return null;
            }

            playerController.ApplyShape(shapeData);
            PlayerController pc = playerController.GetComponent<PlayerController>();
            pc.SetSpeed(shapeData.baseSpeed);
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
            // Disable NPC logic so it stops moving
            var controller = GetComponent<ControllerNPC>();
            if (controller != null) controller.enabled = false;

            gameObject.tag = "Draggable";
            _isStealing = false;
            CancelSteal();
        }
        else
        {
            Debug.LogWarning("Attempted to steal from a husk, which is not allowed.");
            _isStealing = false;
            yield break;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, maxStealDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, highSuspicionOnStealRange);
    }
}
