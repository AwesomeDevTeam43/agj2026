using UnityEngine;
using UnityEngine.EventSystems;

public class ClickManager : MonoBehaviour
{
    [SerializeField] private LayerMask interactableLayer;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverUI())
            {
                return;
            }

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            RaycastHit2D hit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, interactableLayer);

            if (hit.collider != null)
            {
                IInteractable interactable = hit.collider.GetComponent<IInteractable>();
                interactable?.OnClick();
            }
        }
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        // Check mouse
        if (EventSystem.current.IsPointerOverGameObject())
            return true;

        // Check touches
        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                if (EventSystem.current.IsPointerOverGameObject(Input.touches[i].fingerId))
                    return true;
            }
        }

        return false;
    }
}