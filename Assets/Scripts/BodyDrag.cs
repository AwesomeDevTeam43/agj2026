using UnityEngine;
using UnityEngine.InputSystem.Interactions;

public class BodyDrag : MonoBehaviour
{
    [Header("Controls and Settings (using unity's new input system)")]
    //player input
    private InputHandler input;
    [SerializeField] private float dragRadius = 2.0f;

    [SerializeField]private Transform _dragPoint;
    private GameObject _draggedBody = null;
    private Collider2D _draggedCollider = null;
    [SerializeField] private float tooltipRange = 2.0f;

    void Awake()
    {
        input = GetComponent<InputHandler>();
    }

    void Update()
    {
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
            TooltipManager.Instance.ShowTooltip("Hold F to Drag\n (Tip: Hide them in a dumpster)", closestBody.transform.position);
        }
        else
        {
            TooltipManager.Instance.HideTooltip();
        }
        //hold input to drag body, release to drop
        if (input.DragInput)
        {
            if (_draggedBody == null)
            {
                TryGrabBody();
            }

            if (_draggedBody != null)
            {
                _draggedBody.transform.position = Vector3.Lerp(_draggedBody.transform.position, _dragPoint.position, Time.deltaTime * 10f);
            }
        }
        else
        {
            // Stop dragging
            if (_draggedBody != null)
            {
                    DropBody();
            }
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
