using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SuspicionDetector — Vive no VisionCone (filho do guarda).
///
/// Avalia o contexto do que o guarda vê e aumenta/diminui suspicion
/// consoante várias condições:
///   - Shape do player não permitido na zona
///   - Player a mover-se demasiado rápido
///   - Player parado demasiado tempo num local suspeito
///
/// Hierarquia esperada:
///   Guarda (ControllerNPC)
///   └── VisionCone (este GameObject)
///       ├── PolygonCollider2D (trigger)
///       └── SuspicionDetector
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class SuspicionDetector : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────────────────────────────────

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
    private float       _loiterTimer        = 0f;   // tempo que o player está parado no cone

    private Coroutine   _raiseRoutine;
    private Coroutine   _reduceRoutine;

    // Referências
    private ControllerNPC       _guardNPC;      // guarda pai
    private PlayerController    _player;        // player detectado
    private Rigidbody2D         _playerRb;      // para ler velocidade

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Sobe na hierarquia para encontrar o ControllerNPC do guarda pai
        _guardNPC = GetComponentInParent<ControllerNPC>();

        if (_guardNPC == null)
            Debug.LogWarning($"[SuspicionDetector] {gameObject.name}: não encontrou ControllerNPC no pai!");

        // Garante que o collider é trigger
        var col = GetComponent<PolygonCollider2D>();
        col.isTrigger = true;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update — avalia contexto enquanto o player está no cone
    // ──────────────────────────────────────────────────────────────────────────

    void Update()
    {
        if (!_playerInCone || _player == null) return;

        // Acumula loiter timer se o player estiver quase parado
        if (_playerRb != null && _playerRb.linearVelocity.magnitude < 0.5f)
            _loiterTimer += Time.deltaTime;
        else
            _loiterTimer = 0f;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Trigger events
    // ──────────────────────────────────────────────────────────────────────────

    void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Para redução se estava a decorrer
        if (_reduceRoutine != null)
        {
            StopCoroutine(_reduceRoutine);
            _reduceRoutine = null;
        }

        _player      = other.GetComponent<PlayerController>();
        _playerRb    = other.GetComponent<Rigidbody2D>();
        _loiterTimer = 0f;
        _playerInCone = true;

        // Avalia se deve começar a aumentar suspicion
        EvaluateAndStartRaising(other);
    }

    void OnTriggerStay2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        // Reavalia continuamente — o shape do player pode ter mudado
        // (ex: roubou identidade dentro do cone)
        if (_raiseRoutine == null)
            EvaluateAndStartRaising(other);
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        _playerInCone = false;
        _loiterTimer  = 0f;

        // Para aumento
        if (_raiseRoutine != null)
        {
            StopCoroutine(_raiseRoutine);
            _raiseRoutine = null;
        }

        // Inicia redução com delay
        _reduceRoutine = StartCoroutine(ReduceSuspicionRoutine());

        if (showDebugLog)
            Debug.Log($"[SuspicionDetector] {_guardNPC?.name}: player saiu do cone. Suspicion={_currentSuspicion:F1}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Avaliação de contexto
    // ──────────────────────────────────────────────────────────────────────────

    void EvaluateAndStartRaising(Collider2D playerCollider)
    {
        ShapeVisualizer sv = playerCollider.GetComponent<ShapeVisualizer>();
        if (sv == null) return;

        ShapeType playerShape = sv.shapeData.type;
        
        bool isSuspiciousShape = IsSuspicious(playerShape);
        bool isDoingSuspiciousAction = false;
        
        // Also check if running or loitering!
        if (_playerRb != null && _playerRb.linearVelocity.magnitude > runSpeedThreshold) isDoingSuspiciousAction = true;
        if (_loiterTimer >= loiteringTime) isDoingSuspiciousAction = true;

        if (isSuspiciousShape || isDoingSuspiciousAction)
        {
            if (_raiseRoutine == null)
                _raiseRoutine = StartCoroutine(RaiseSuspicionRoutine(playerCollider));
        }
        else
        {
            // Shape permitido e sem ação suspeita — para de aumentar se estava a aumentar
            if (_raiseRoutine != null)
            {
                StopCoroutine(_raiseRoutine);
                _raiseRoutine  = null;
                _reduceRoutine = StartCoroutine(ReduceSuspicionRoutine());
            }
        }
    }

    bool IsSuspicious(ShapeType playerShape)
    {
        if (isRestrictedToAll) return true;
        
        // If they haven't set up the allowed shapes, let's warn them in case it's misconfigured
        if (allowedShapes == null || allowedShapes.Length == 0)
        {
            if (showDebugLog) Debug.LogWarning($"[SuspicionDetector] {gameObject.name} has no allowedShapes! Everyone is allowed by default. Increase isRestrictedToAll if it's a restricted area.");
            return false;
        }

        return !System.Array.Exists(allowedShapes, s => s == playerShape);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Coroutines
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator RaiseSuspicionRoutine(Collider2D playerCollider)
    {
        while (_currentSuspicion < MAX_SUSPICION)
        {
            float multiplier = CalculateMultiplier();
            _currentSuspicion += suspicionIncreaseRate * multiplier * Time.deltaTime;
            _currentSuspicion  = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);

            if (showDebugLog)
                Debug.Log($"[SuspicionDetector] {_guardNPC?.name}: suspicion={_currentSuspicion:F1} (x{multiplier:F2})");

            // Reavalia context a cada frame — se mudou de identidade para o shape
            // correto ou parou de correr, para de aumentar
            ShapeVisualizer sv = playerCollider.GetComponent<ShapeVisualizer>();
            bool stillSuspiciousShape = sv != null && IsSuspicious(sv.shapeData.type);
            bool stillDoingAction = false;
            if (_playerRb != null && _playerRb.linearVelocity.magnitude > runSpeedThreshold) stillDoingAction = true;
            if (_loiterTimer >= loiteringTime) stillDoingAction = true;

            if (!stillSuspiciousShape && !stillDoingAction)
            {
                if (showDebugLog)
                    Debug.Log($"[SuspicionDetector] Player parou de ser suspeito — a parar aumento");
                break;
            }

            yield return null;
        }

        _raiseRoutine = null;

        if (_currentSuspicion >= MAX_SUSPICION)
            TriggerAlarm();
    }

    IEnumerator ReduceSuspicionRoutine()
    {
        yield return new WaitForSeconds(decreaseDelay);

        while (_currentSuspicion > 0f)
        {
            _currentSuspicion -= suspicionDecreaseRate * Time.deltaTime;
            _currentSuspicion  = Mathf.Clamp(_currentSuspicion, 0f, MAX_SUSPICION);

            if (showDebugLog)
                Debug.Log($"[SuspicionDetector] {_guardNPC?.name}: suspicion a baixar={_currentSuspicion:F1}");

            yield return null;
        }

        _reduceRoutine = null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multiplier — combina todos os fatores de contexto
    // ──────────────────────────────────────────────────────────────────────────

    float CalculateMultiplier()
    {
        float multiplier = 1f;

        // Player a correr
        if (_playerRb != null && _playerRb.linearVelocity.magnitude > runSpeedThreshold)
        {
            multiplier *= runningMultiplier;
            if (showDebugLog) Debug.Log("[SuspicionDetector] Contexto: a correr");
        }

        // Player parado demasiado tempo (loitering)
        if (_loiterTimer >= loiteringTime)
        {
            multiplier *= loiteringMultiplier;
            if (showDebugLog) Debug.Log("[SuspicionDetector] Contexto: loitering");
        }

        return multiplier;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Alarm — alerta todos os guardas na cena
    // ──────────────────────────────────────────────────────────────────────────

    void TriggerAlarm()
    {
        if (_player == null) return;

        Vector3 playerPos = _player.transform.position;

        Debug.Log($"[SuspicionDetector] ALARME! {_guardNPC?.name} alertou todos os guardas para {playerPos}");

        ControllerNPC[] allGuards = FindObjectsByType<ControllerNPC>(FindObjectsSortMode.None);
        foreach (var guard in allGuards)
        {
            guard.Alert(playerPos);
        }

        // Make suspicion truly impactful! Instant game over if they catch you doing something max level suspicious
        Debug.Log("MAX SUSPICION REACHED! GAME OVER!");
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }

    private static Texture2D _bgTexture;
    private static Texture2D _fillTexture;

    void OnGUI()
    {
        if (Event.current.type != EventType.Repaint) return;

        if (_currentSuspicion > 0.1f && _guardNPC != null)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(_guardNPC.transform.position + Vector3.up * 1.5f);
            
            // Only draw if the guard is on screen (z > 0 in WorldToScreenPoint)
            if (screenPos.z > 0)
            {
                screenPos.y = Screen.height - screenPos.y; // Flip Y for GUI
                
                float width = 80f;
                float height = 15f;
                Rect bgRect = new Rect(screenPos.x - width / 2, screenPos.y - height, width, height);
                Rect fillRect = new Rect(bgRect.x, bgRect.y, width * SuspicionNormalized, height);

                // Draw solid background
                if (_bgTexture == null)
                {
                    _bgTexture = new Texture2D(1, 1);
                    _bgTexture.SetPixel(0, 0, new Color(0.1f, 0.1f, 0.1f, 0.8f));
                    _bgTexture.Apply();
                }
                GUI.DrawTexture(bgRect, _bgTexture);

                // Draw filled bar based on suspicion
                Color barColor = Color.Lerp(Color.yellow, Color.red, SuspicionNormalized);
                if (_fillTexture == null)
                {
                    _fillTexture = new Texture2D(1, 1);
                }
                _fillTexture.SetPixel(0, 0, barColor);
                _fillTexture.Apply();
                GUI.DrawTexture(fillRect, _fillTexture);

                // Draw text
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.alignment = TextAnchor.MiddleCenter;
                style.fontSize = 12;
                style.fontStyle = FontStyle.Bold;
                style.normal.textColor = Color.white;
                
                // Add a small drop shadow for the text
                Rect shadowRect = new Rect(bgRect.x + 1, bgRect.y + 1, bgRect.width, bgRect.height);
                GUIStyle shadowStyle = new GUIStyle(style);
                shadowStyle.normal.textColor = Color.black;
                GUI.Label(shadowRect, "Suspicion", shadowStyle);
                
                GUI.Label(bgRect, "Suspicion", style);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Força suspicion máxima imediatamente (ex: distraction device especial).</summary>
    public void RaiseGlobalAlarm()
    {
        _currentSuspicion = MAX_SUSPICION;
        TriggerAlarm();
    }

    /// <summary>Valor atual de suspicion (0-100). Útil para UI.</summary>
    public float CurrentSuspicion => _currentSuspicion;

    /// <summary>Suspicion normalizada (0-1). Útil para UI.</summary>
    public float SuspicionNormalized => _currentSuspicion / MAX_SUSPICION;

    /// <summary>
    /// Aumenta suspicion externamente (ex: roubo de identidade em zona de alta suspeição).
    /// Compatível com StartCoroutine externo — para quando já não for necessário.
    /// </summary>
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

    // ──────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // Cor do cone muda consoante nível de suspeição
        float t = _currentSuspicion / MAX_SUSPICION;
        Gizmos.color = new Color(1f, 1f - t, 0f, 0.25f + t * 0.4f);

        PolygonCollider2D cone = GetComponent<PolygonCollider2D>();
        if (cone == null) return;

        // Desenha o perímetro do cone
        Vector2[] points = cone.points;
        for (int i = 0; i < points.Length; i++)
        {
            Vector3 a = transform.TransformPoint(points[i]);
            Vector3 b = transform.TransformPoint(points[(i + 1) % points.Length]);
            Gizmos.DrawLine(a, b);
        }
    }
}