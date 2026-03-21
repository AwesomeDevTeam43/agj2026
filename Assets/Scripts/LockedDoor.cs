using Unity.VisualScripting;
using UnityEngine;

public class LockedDoor : MonoBehaviour
{
    [SerializeField] private ShapeType allowedShape;

    [SerializeField] private GameObject _doorVisual;

    private BoxCollider2D _doorCollider;
    private SpriteRenderer _doorRenderer;

    private int _authorizedEntries = 0;

    private void Awake()
    {
        _doorCollider = _doorVisual.GetComponent<BoxCollider2D>();
        _doorRenderer = _doorVisual.GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ShapeVisualizer playerShape = other.GetComponent<ShapeVisualizer>();
            if (playerShape != null && playerShape.shapeData != null && playerShape.shapeData.type == allowedShape)
            {
                _authorizedEntries++;
                OpenDoor();
            }
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            ShapeVisualizer playerShape = other.GetComponent<ShapeVisualizer>();
            if (playerShape != null && playerShape.shapeData != null && playerShape.shapeData.type == allowedShape)
            {
                _authorizedEntries = Mathf.Max(0, _authorizedEntries - 1);
                if (_authorizedEntries == 0)
                {
                    CloseDoor();
                }
            }
        }
    }

    private void OpenDoor()
    {
        _doorCollider.enabled = false;
        Color tempColor = _doorRenderer.color;
        _doorRenderer.color = new Color(tempColor.r, tempColor.g, tempColor.b, 0.5f);
    }

    private void CloseDoor()
    {
        _doorCollider.enabled = true;
        Color tempColor = _doorRenderer.color;
        _doorRenderer.color = new Color(tempColor.r, tempColor.g, tempColor.b, 1f);
    }
}