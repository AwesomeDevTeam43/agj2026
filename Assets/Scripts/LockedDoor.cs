using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LockedDoor — controla acesso visual e colisão física apenas para o Player.
/// Os NPCs são agora controlados nativamente pelas NavMesh Area Masks!
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [SerializeField] private List<ShapeType> allowedShapes = new List<ShapeType>();
    [SerializeField] private GameObject _doorVisual;

    private BoxCollider2D _doorCollider;
    private SpriteRenderer _doorRenderer;

    private int _playerEntries = 0;
    private int _npcEntries = 0;

    private HashSet<GameObject> _authorizedNPCsInside = new HashSet<GameObject>();

    private void Awake()
    {
        _doorCollider = _doorVisual.GetComponent<BoxCollider2D>();
        _doorRenderer = _doorVisual.GetComponent<SpriteRenderer>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        ShapeVisualizer sv = other.GetComponent<ShapeVisualizer>();
        if (sv == null || sv.shapeData == null) return;

        bool isAllowed = allowedShapes.Contains(sv.shapeData.type);
        bool isPlayer = other.CompareTag("Player");

        if (isPlayer)
        {
            if (isAllowed)
            {
                _playerEntries++;
                RefreshDoorState();
                AudioManager.Instance?.Play("opendoor");
            }
            else
            {
                AudioManager.Instance?.Play("wrongdoor");
            }
        }
        else
        {
            // É um NPC - se o NavMesh o trouxe até aqui, ele tem permissão, 
            // mas validamos na mesma para abrir a porta visualmente.
            if (isAllowed)
            {
                _npcEntries++;
                _authorizedNPCsInside.Add(other.gameObject);
                RefreshDoorState();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        ShapeVisualizer sv = other.GetComponent<ShapeVisualizer>();
        if (sv == null || sv.shapeData == null) return;

        bool isAllowed = allowedShapes.Contains(sv.shapeData.type);
        bool isPlayer = other.CompareTag("Player");

        if (isPlayer)
        {
            if (isAllowed)
            {
                _playerEntries = Mathf.Max(0, _playerEntries - 1);
                RefreshDoorState();
            }
        }
        else
        {
            if (_authorizedNPCsInside.Remove(other.gameObject))
            {
                _npcEntries = Mathf.Max(0, _npcEntries - 1);
                RefreshDoorState();
            }
        }
    }

    private void RefreshDoorState()
    {
        bool anyoneAuthorizedInside = (_playerEntries + _npcEntries) > 0;

        if (anyoneAuthorizedInside)
            OpenDoor();
        else
            CloseDoor();
    }

    private void OpenDoor()
    {
        // Física — player passa
        if (_doorCollider != null)
            _doorCollider.enabled = false;

        // Visual
        SetDoorAlpha(0.5f);
    }

    private void CloseDoor()
    {
        // Física
        if (_doorCollider != null)
            _doorCollider.enabled = true;

        // Visual
        SetDoorAlpha(1f);
    }

    private void SetDoorAlpha(float alpha)
    {
        if (_doorRenderer == null) return;
        Color c = _doorRenderer.color;
        _doorRenderer.color = new Color(c.r, c.g, c.b, alpha);
    }

    private void OnDrawGizmos()
    {
        if (_doorVisual == null) return;
        Gizmos.color = allowedShapes.Count > 0
            ? new Color(0f, 1f, 0f, 0.2f)
            : new Color(1f, 0f, 0f, 0.2f);
        var col = _doorVisual.GetComponent<BoxCollider2D>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}