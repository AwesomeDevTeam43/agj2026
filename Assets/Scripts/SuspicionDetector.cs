using System.Collections;
using UnityEngine;

public class SuspicionDetector : MonoBehaviour
{

    [Header("Security Rules")]
    [Tooltip("Which shapes are allowed in this area?")]
    public ShapeType[] allowedShapes;
    public bool isRestrictedToAll = false;
    [SerializeField] Collider2D detectionArea;
    [SerializeField] private PolygonCollider2D visionCone;
    [Header("Suspicion Settings")]
    [Tooltip("How quickly does suspicion increase when a disallowed shape is present?")]
    public float suspicionIncreaseRate = 10f;
    public float suspicionDecreaseRate = 5f;

    private float _currentSuspicion = 0f;
    private const float MAX_SUSPICION = 100f;
    private Coroutine _reduceRoutine;

    void Awake()
    {
        
         visionCone = GetComponent<PolygonCollider2D>();
    }
    //suspicioun reducement coroutine
    IEnumerator ReduceSuspicion()
    {
        //if player reenters the area again, stop the coroutine and resume increasing from where it left off

        //wait 3 seconds before starting to reduce suspicion
        yield return new WaitForSeconds(3f);
        while (_currentSuspicion > 0f)
        {
            _currentSuspicion -= suspicionDecreaseRate * Time.deltaTime;
            _currentSuspicion = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);
            Debug.Log($"Suspicion decreased: {_currentSuspicion}");
            yield return null;
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            Debug.Log("Player left detection area, resetting suspicion.");

            _reduceRoutine = StartCoroutine(ReduceSuspicion());
        }

    }
    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && _reduceRoutine != null)
        {
            Debug.Log("Player entered detection area, evaluating shape.");
            StopCoroutine(_reduceRoutine);
        }
    }
    void OnTriggerStay2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ShapeVisualizer shapeVisualizer = other.GetComponent<ShapeVisualizer>();
            if (shapeVisualizer != null)
            {
                ShapeType currentShape = shapeVisualizer.shapeData.type;
                bool isAllowed = isRestrictedToAll ? false : System.Array.Exists(allowedShapes, shape => shape == currentShape);

                if (!isAllowed)
                {
                    _currentSuspicion += suspicionIncreaseRate * Time.deltaTime;
                    _currentSuspicion = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);
                    Debug.Log($"Suspicion increased: {_currentSuspicion}");
                }
                else
                {
                    _currentSuspicion -= suspicionDecreaseRate * Time.deltaTime;
                    _currentSuspicion = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);
                    Debug.Log($"Suspicion decreased: {_currentSuspicion}");
                }
            }
        }
    }

    void OnDrawGizmos()
    {
        if (detectionArea != null)
        {
            Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
            Gizmos.DrawCube(detectionArea.bounds.center, detectionArea.bounds.size);
        }

    }
}