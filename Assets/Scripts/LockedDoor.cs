using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// LockedDoor — controla acesso por ShapeType tanto para o player como para NPCs.
///
/// Para o player: desativa o BoxCollider2D (passa fisicamente).
/// Para NPCs: desativa o NavMeshObstacle (o NavMesh recalcula e permite passage).
///
/// Setup no Inspector:
///   - _doorVisual: GameObject com BoxCollider2D + SpriteRenderer
///   - O mesmo GameObject (ou este) deve ter um NavMeshObstacle com Carve = true
///   - allowedShapes: shapes que têm acesso
/// </summary>
public class LockedDoor : MonoBehaviour
{
    [SerializeField] private List<ShapeType> allowedShapes = new List<ShapeType>();
    [SerializeField] private GameObject _doorVisual;

    private BoxCollider2D _doorCollider;
    private SpriteRenderer _doorRenderer;
    private NavMeshObstacle _navObstacle;

    // Conta separada por tipo de entidade — evita que o NPC feche a porta no player
    private int _playerEntries = 0;
    private int _npcEntries = 0;

    // Rastreia quais NPCs estão autorizados dentro do trigger
    // (para não decrementar em saídas de NPCs não autorizados)
    private HashSet<GameObject> _authorizedNPCsInside = new HashSet<GameObject>();

    private void Awake()
    {
        _doorCollider = _doorVisual.GetComponent<BoxCollider2D>();
        _doorRenderer = _doorVisual.GetComponent<SpriteRenderer>();

        // NavMeshObstacle pode estar no doorVisual ou neste GameObject
        _navObstacle = _doorVisual.GetComponent<NavMeshObstacle>();
        if (_navObstacle == null)
            _navObstacle = GetComponent<NavMeshObstacle>();

        if (_navObstacle == null)
            Debug.LogWarning($"[LockedDoor] {gameObject.name}: sem NavMeshObstacle — NPCs não serão bloqueados pelo NavMesh.");
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
            // É um NPC
            if (isAllowed)
            {
                _npcEntries++;
                _authorizedNPCsInside.Add(other.gameObject);
                RefreshDoorState();
            }
            // NPC não autorizado — a porta/obstáculo continua ativo, o NavMesh impede-o
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
            // Só decrementa se este NPC estava realmente autorizado dentro
            if (_authorizedNPCsInside.Remove(other.gameObject))
            {
                _npcEntries = Mathf.Max(0, _npcEntries - 1);
                RefreshDoorState();
            }
        }
    }

    /// <summary>
    /// Atualiza o estado da porta com base em quem está dentro do trigger.
    /// A porta abre se houver pelo menos um player OU NPC autorizado dentro.
    /// </summary>
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

        // NavMesh — NPCs passam
        if (_navObstacle != null)
            _navObstacle.enabled = false;

        // Visual
        SetDoorAlpha(0.5f);
    }

    private void CloseDoor()
    {
        // Física
        if (_doorCollider != null)
            _doorCollider.enabled = true;

        // NavMesh
        if (_navObstacle != null)
            _navObstacle.enabled = true;

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