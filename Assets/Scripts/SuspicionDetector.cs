using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SuspicionDetector — Vive no VisionCone (filho do guarda).
///
/// Avalia o contexto do que o guarda vê (usando Field of View matemático em vez de triggers)
/// e aumenta/diminui suspicion consoante várias condições.
/// </summary>
public class SuspicionDetector : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────────────────────────────────

    [Header("FOV Settings")]
    [Tooltip("Distância máxima de visão")]
    public float visionRange = 6f;
    
    [Tooltip("Ângulo total do cone de visão (ex: 90 = 45º para cada lado)")]
    [Range(10f, 360f)]
    public float visionAngle = 90f;

    [Header("Zone Rules")]
    [Tooltip("Shapes permitidos nesta zona. Vazio = ninguém é suspeito por shape.")]
    public ShapeType[] allowedShapes;

    [Tooltip("Zona restrita a todos — qualquer shape aumenta suspeição")]
    public bool isRestrictedToAll = false;

    [Header("Suspicion Settings")]
    [Tooltip("Taxa base de aumento de suspeição por segundo")]
    public float suspicionIncreaseRate = 10f;

    [Tooltip("Taxa de diminuição de suspeição por segundo (após delay)")]
    public float suspicionDecreaseRate = 5f;

    [Tooltip("Segundos antes de começar a diminuir suspeição após o player sair do cone")]
    public float decreaseDelay = 3f;

    [Header("Context Multipliers")]
    [Tooltip("Multiplicador quando o player está a correr (acima de runSpeedThreshold)")]
    public float runningMultiplier = 2.5f;

    [Tooltip("Velocidade a partir da qual o player é considerado a correr")]
    public float runSpeedThreshold = 3f;

    [Tooltip("Multiplicador quando o player está parado demasiado tempo num local suspeito")]
    public float loiteringMultiplier = 1.8f;

    [Tooltip("Segundos parado no cone até ser considerado loitering")]
    public float loiteringTime = 5f;

    [Header("Debug")]
    public bool showDebugLog = false;

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime privado
    // ──────────────────────────────────────────────────────────────────────────

    private const float MAX_SUSPICION = 100f;

    private float       _currentSuspicion   = 0f;
    private bool        _playerInCone       = false;
    private float       _loiterTimer        = 0f;   

    private Coroutine   _reduceRoutine;

    // Referências
    private ControllerNPC       _guardNPC;      
    private Transform           _playerTransform;
    private PlayerController    _playerController;
    private ShapeVisualizer     _playerVisualizer;
    private Rigidbody2D         _playerRb;      

    private static Texture2D _bgTexture;
    private static Texture2D _fillTexture;

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        _guardNPC = GetComponentInParent<ControllerNPC>();

        if (_guardNPC == null)
            Debug.LogWarning($"[SuspicionDetector] {gameObject.name}: não encontrou ControllerNPC no pai!");

        // Remove the PolygonCollider2D if one still exists on this object to prevent physics overhead
        var oldCollider = GetComponent<PolygonCollider2D>();
        if (oldCollider != null)
        {
            Destroy(oldCollider);
        }
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            _playerTransform = playerObj.transform;
            _playerController = playerObj.GetComponent<PlayerController>();
            _playerVisualizer = playerObj.GetComponent<ShapeVisualizer>();
            _playerRb = playerObj.GetComponent<Rigidbody2D>();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update — avalia o FOV matemático todos os frames
    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (_playerTransform == null) return;

        // --- THE ZOMBIE FIX ---
        // Se o guarda estiver morto ou transformado num Husk, desliga a suspeita e limpa a UI!
        if (_guardNPC != null)
        {
            if (_guardNPC.currentState == ControllerNPC.NPCState.Dead || 
               (_guardNPC.shapeData != null && _guardNPC.shapeData.type == ShapeType.Husk))
            {
                _currentSuspicion = 0f; // Zera a barra para ela desaparecer da UI
                return;                 // Aborta o Update para que ele não veja mais nada
            }
        }
        // ----------------------

        bool isCurrentlyInFOV = IsPlayerInFOV();

        // 1. STATE CHANGE: Player acabou de entrar na visão
        if (isCurrentlyInFOV && !_playerInCone)
        {
            _playerInCone = true;
            _loiterTimer = 0f;
            
            if (_reduceRoutine != null)
            {
                StopCoroutine(_reduceRoutine);
                _reduceRoutine = null;
            }
            if (showDebugLog) Debug.Log($"[SuspicionDetector] Player entrou na visão matemática!");
        }
        // 2. STATE CHANGE: Player acabou de sair da visão
        else if (!isCurrentlyInFOV && _playerInCone)
        {
            _playerInCone = false;
            _loiterTimer = 0f;
            _reduceRoutine = StartCoroutine(ReduceSuspicionRoutine());
            if (showDebugLog) Debug.Log($"[SuspicionDetector] Player saiu da visão matemática.");
        }

        // 3. CONTINUOUS LOGIC: Enquanto o player está na visão
        if (_playerInCone)
        {
            // Acumula loiter timer se estiver quase parado
            if (_playerRb != null && _playerRb.linearVelocity.magnitude < 0.5f)
                _loiterTimer += Time.deltaTime;
            else
                _loiterTimer = 0f;

            EvaluateAndApplySuspicion();
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Field of View Core Logic
    // ──────────────────────────────────────────────────────────────────────────

    private bool IsPlayerInFOV()
    {
        Vector2 dirToPlayer = _playerTransform.position - transform.position;
        float distanceToPlayer = dirToPlayer.magnitude;

        // 1. DISTANCE CHECK
        if (distanceToPlayer > visionRange) return false;

        // 2. ANGLE CHECK
        // Using _guardNPC.transform.right because HumanNavigation rotates the parent NPC
        if (Vector2.Angle(_guardNPC.transform.right, dirToPlayer.normalized) > visionAngle / 2f)
        {
            return false;
        }

        // 3. WALL CHECK (Line of Sight)
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, dirToPlayer.normalized, distanceToPlayer);
        
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject == gameObject || hit.collider.gameObject == _guardNPC.gameObject) continue; 
            if (hit.collider.isTrigger) continue; 

            if (hit.collider.transform == _playerTransform) return true; 

            // Hit a solid wall before the player
            return false; 
        }

        return false;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Avaliação de contexto
    // ──────────────────────────────────────────────────────────────────────────

    void EvaluateAndApplySuspicion()
    {
        if (_playerVisualizer == null) return;

        ShapeType playerShape = _playerVisualizer.shapeData.type;
        
        bool isSuspiciousShape = IsSuspicious(playerShape);
        bool isDoingSuspiciousAction = false;
        
        if (_playerRb != null && _playerRb.linearVelocity.magnitude > runSpeedThreshold) isDoingSuspiciousAction = true;
        if (_loiterTimer >= loiteringTime) isDoingSuspiciousAction = true;

        if (isSuspiciousShape || isDoingSuspiciousAction)
        {
            // Aumenta suspicion diretamente no Update em vez de usar uma Coroutine complexa
            float multiplier = CalculateMultiplier();
            _currentSuspicion += suspicionIncreaseRate * multiplier * Time.deltaTime;
            _currentSuspicion = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);

            if (showDebugLog)
                Debug.Log($"[SuspicionDetector] suspicion={_currentSuspicion:F1} (x{multiplier:F2})");

            if (_currentSuspicion >= MAX_SUSPICION)
            {
                TriggerAlarm();
            }
        }
        else if (_currentSuspicion > 0 && _reduceRoutine == null)
        {
            // Shape permitido e sem ação suspeita — começa a reduzir
            _reduceRoutine = StartCoroutine(ReduceSuspicionRoutine());
        }
    }

    bool IsSuspicious(ShapeType playerShape)
    {
        if (isRestrictedToAll) return true;
        
        if (allowedShapes == null || allowedShapes.Length == 0) return false;

        return !System.Array.Exists(allowedShapes, s => s == playerShape);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Coroutines & Multipliers
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator ReduceSuspicionRoutine()
    {
        yield return new WaitForSeconds(decreaseDelay);

        while (_currentSuspicion > 0f && !_playerInCone) // Only reduce if player is STILL out of sight
        {
            _currentSuspicion -= suspicionDecreaseRate * Time.deltaTime;
            _currentSuspicion  = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);
            yield return null;
        }

        _reduceRoutine = null;
    }

    float CalculateMultiplier()
    {
        float multiplier = 1f;

        if (_playerRb != null && _playerRb.linearVelocity.magnitude > runSpeedThreshold)
            multiplier *= runningMultiplier;

        if (_loiterTimer >= loiteringTime)
            multiplier *= loiteringMultiplier;

        return multiplier;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Alarm & UI
    // ──────────────────────────────────────────────────────────────────────────

    void TriggerAlarm()
    {
        if (_playerTransform == null) return;

        Vector3 playerPos = _playerTransform.position;
        Debug.Log($"[SuspicionDetector] ALARME! {_guardNPC?.name} alertou todos!");

        ControllerNPC[] allGuards = FindObjectsByType<ControllerNPC>(FindObjectsSortMode.None);
        foreach (var guard in allGuards)
        {
            guard.Alert(playerPos);
        }

        Debug.Log("MAX SUSPICION REACHED! GAME OVER!");
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        if (_currentSuspicion > 0.1f && _guardNPC != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_guardNPC.transform.position + Vector3.up * 1.5f);
            
            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y; 
                
                float width = 80f;
                float height = 15f;
                Rect bgRect = new Rect(screenPos.x - width / 2, screenPos.y - height, width, height);
                Rect fillRect = new Rect(bgRect.x, bgRect.y, width * SuspicionNormalized, height);

                if (_bgTexture == null)
                {
                    _bgTexture = new Texture2D(1, 1);
                    _bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                    _bgTexture.Apply();
                }
                GUI.DrawTexture(bgRect, _bgTexture);

                Color barColor = Color.Lerp(Color.yellow, Color.red, SuspicionNormalized);
                if (_fillTexture == null) _fillTexture = new Texture2D(1, 1);
                
                _fillTexture.SetPixel(0, 0, barColor);
                _fillTexture.Apply();
                GUI.DrawTexture(fillRect, _fillTexture);

                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                
                Rect shadowRect = new Rect(bgRect.x + 1, bgRect.y + 1, bgRect.width, bgRect.height);
                GUIStyle shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = Color.black;
                GUI.Label(shadowRect, "Suspicion", shadowStyle);
                
                GUI.Label(bgRect, "Suspicion", style);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API & Gizmos
    // ──────────────────────────────────────────────────────────────────────────

    public void RaiseGlobalAlarm()
    {
        _currentSuspicion = MAX_SUSPICION;
        TriggerAlarm();
    }

    public float CurrentSuspicion => _currentSuspicion;
    public float SuspicionNormalized => _currentSuspicion / MAX_SUSPICION;

    public IEnumerator RaiseSuspicion()
    {
        while (_currentSuspicion < MAX_SUSPICION)
        {
            _currentSuspicion += suspicionIncreaseRate * Time.deltaTime;
            _currentSuspicion  = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);
            yield return null;
        }
        if (_currentSuspicion >= MAX_SUSPICION)
            TriggerAlarm();
    }

    void OnDrawGizmos()
    {
        // Draw the math FOV instead of the PolygonCollider
        Transform drawTransform = _guardNPC != null ? _guardNPC.transform : transform;

        float t = _currentSuspicion / MAX_SUSPICION;
        Gizmos.color = new Color(1f, 1f - t, 0f, 0.25f + t * 0.4f);

        // Draw Left FOV Limit
        Vector3 leftDir = Quaternion.Euler(0, 0, visionAngle / 2f) * drawTransform.right;
        Gizmos.DrawRay(transform.position, leftDir * visionRange);

        // Draw Right FOV Limit
        Vector3 rightDir = Quaternion.Euler(0, 0, -visionAngle / 2f) * drawTransform.right;
        Gizmos.DrawRay(transform.position, rightDir * visionRange);

        // Draw front arc (approximate)
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}