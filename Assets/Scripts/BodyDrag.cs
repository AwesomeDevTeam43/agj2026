using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public class BodyDrag : MonoBehaviour
{
    [Header("Controls and Settings (using unity's new input system)")]
    //player input
    private InputHandler input;
    [SerializeField] private float dragRadius = 2.0f;

    [SerializeField] private Transform _dragPoint;
    private GameObject _draggedBody = null;
    private Collider2D _draggedCollider = null;
    [SerializeField] private float tooltipRange = 2.0f;
    public bool IsDragging => _draggedBody != null;

    // NEW: We track the previous frame's input to detect a single press
    private bool _previousDragInput = false;

    void Awake()
    {
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
        HandleTooltip();

        // 1. Detect if the button was JUST pressed this frame (Toggle trigger)
        bool dragInputDown = input.DragInput && !_previousDragInput;

        if (dragInputDown)
        {
            if (_draggedBody == null)
            {
                TryGrabBody();
            }
            else
            {
                DropBody();
            }
        }

        // 2. Keep the body following the player if we are currently holding one
        if (_draggedBody != null)
        {
            _draggedBody.transform.position = Vector3.Lerp(_draggedBody.transform.position, _dragPoint.position, Time.deltaTime * 10f);
        }

        // 3. Save the input state for the next frame
        _previousDragInput = input.DragInput;
    }

    private void HandleTooltip()
    {
        // Don't show the prompt if we are already carrying a body
        if (_draggedBody != null)
        {
            TooltipManager.Instance.HideTooltip();
            return;
        }

        GameObject closestBody = null;
        float minDistance = tooltipRange;

        foreach (GameObject body in GameObject.FindGameObjectsWithTag("Draggable"))
        {
            float distance = Vector3.Distance(transform.position, body.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestBody = body;
            }
        }

        if (closestBody != null)
        {
            TooltipManager.Instance.ShowTooltip("Press F to Drag\n (Tip: Hide them in a dumpster)", closestBody.transform.position);
        }
        else
        {
            TooltipManager.Instance.HideTooltip();
        }
    }

    void TryGrabBody()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, dragRadius);
        foreach (Collider2D hit in hits)
        {
            if (hit.CompareTag("Draggable"))
            {
                _draggedBody = hit.gameObject;
                _draggedCollider = hit;
                if (_draggedCollider != null)
                {
                    _draggedCollider.enabled = false;
                }
                break;
            }
        }
    }

    void DropBody()
    {
        if (_draggedBody == null) return;

        if (_draggedCollider != null)
        {
            _draggedCollider.enabled = true;
        }

        _draggedBody = null;
        _draggedCollider = null;
    }
}