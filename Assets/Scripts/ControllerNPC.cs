using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// ControllerNPC — IA de baile de máscaras com navegação orgânica.
///
/// Delega todo o movimento ao componente HumanNavigation (mesmo GameObject).
/// Não usa nenhum waypoint manual — os percursos são gerados proceduralmente.
///
/// Tipos:
///   Circle  (Convidado) — passeia, admira quadros, bebe, socializa
///   Hexagon (Segurança) — patrulha a zona inicial, investiga incidentes
///   Triangle (VIP)      — quartos privados, socializa longamente, pouco movimento
///   Square  (Staff)     — maioritariamente estático, circula ocasionalmente
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HumanNavigation))]
public class ControllerNPC : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // State machine
    // ──────────────────────────────────────────────────────────────────────────

    public enum NPCState
    {
        Idle,           // A decidir o próximo passo
        Walking,        // Em trânsito (gerido pelo HumanNavigation)
        Observing,      // A admirar um quadro / objeto
        Drinking,       // A beber
        Socializing,    // A conversar numa zona social
        Resting,        // Sentado / a descansar
        Patrolling,     // Segurança: a gerar rondas na área
        Watching,       // Guarda estático: look-around no posto
        Investigating,  // Segurança: a ir a um incidente
        Returning       // A regressar ao posto
    }

    public NPCState currentState = NPCState.Idle;

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector
    // ──────────────────────────────────────────────────────────────────────────

    [Header("References")]
    public ShapeData shapeData;

    [Tooltip("Posição de 'posto' — onde o NPC começa e regressa. Se null usa spawn.")]
    public Transform initialPosition;

    [Header("Interest Points")]
    [Tooltip("Vazio = o NPC descobre automaticamente todos os InterestPoint da cena")]
    public InterestPoint[] interestPoints;

    [Header("Vision")]
    public float visionRange = 5f;

    [Header("Behavior Timing")]
    public float minActivityTime = 3f;
    public float maxActivityTime = 12f;

    [Tooltip("Intervalo (segundos) entre decisões no estado Idle")]
    public float idleDecisionDelay = 1.5f;

    [Header("Patrol (Hexagon only)")]
    [Tooltip("Guarda estático — fica no posto a observar, só se move se alertado")]
    public bool isStationary = false;

    [Tooltip("Raio de patrulha em torno do posto (ignorado se isStationary = true)")]
    public float patrolRadius = 6f;

    [Tooltip("Quantos pontos de patrulha gerar dinamicamente")]
    [Range(2, 8)]
    public int patrolPointCount = 4;

    [Tooltip("Ângulo do look-around no posto, para cada lado (graus)")]
    [Range(10f, 90f)]
    public float stationaryLookAngle = 45f;

    [Tooltip("Tempo entre cada rotação do look-around (segundos)")]
    public float stationaryLookInterval = 3f;

    [Header("Allowed Rooms")]
    public List<GameObject> allowedRooms;

    // ──────────────────────────────────────────────────────────────────────────
    // Runtime privado
    // ──────────────────────────────────────────────────────────────────────────

    private NavMeshAgent    agent;
    private HumanNavigation nav;

    private Vector3         homePosition;
    private InterestPoint   currentInterestPoint;

    private float           stateTimer  = 0f;
    private float           idleTimer   = 0f;
    private bool            isDeciding  = false;
    private float           lookTimer   = 0f;   // timer do look-around estático
    private int             lookDir     = 1;     // direção atual do look-around

    // Patrulha procedural — lista de pontos gerados em runtime
    private List<Vector3>   patrolRoute = new List<Vector3>();
    private int             patrolIndex = 0;

    private Transform       playerTransform;

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        nav   = GetComponent<HumanNavigation>();

        agent.updateRotation = false;
        agent.updateUpAxis   = false;
        agent.speed          = shapeData.baseSpeed;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null && shapeData != null)
            sr.color = shapeData.color;

        homePosition = initialPosition != null
            ? initialPosition.position
            : transform.position;

        if (interestPoints == null || interestPoints.Length == 0)
            interestPoints = FindObjectsByType<InterestPoint>(FindObjectsSortMode.None);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;

        // Gera a rota de patrulha apenas para Hexagon não estáticos
        if (shapeData.type == ShapeType.Hexagon && !isStationary)
            GeneratePatrolRoute();

        // Stagger inicial para os NPCs não decidirem todos ao mesmo tempo
        idleTimer = Random.Range(0f, idleDecisionDelay * 3f);
    }

    void Update()
    {
        switch (currentState)
        {
            case NPCState.Idle:          UpdateIdle();          break;
            case NPCState.Walking:       /* gerido por HumanNavigation + callback */ break;
            case NPCState.Observing:
            case NPCState.Drinking:
            case NPCState.Socializing:
            case NPCState.Resting:       UpdateTimedActivity(); break;
            case NPCState.Patrolling:    UpdatePatrolling();    break;
            case NPCState.Watching:      UpdateWatching();      break;
            case NPCState.Investigating: /* gerido por callback */               break;
            case NPCState.Returning:     /* gerido por callback */               break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // State handlers
    // ──────────────────────────────────────────────────────────────────────────

    void UpdateIdle()
    {
        idleTimer -= Time.deltaTime;
        if (idleTimer <= 0f && !isDeciding)
        {
            isDeciding = true;
            StartCoroutine(DecideNextBehavior());
        }
    }

    void UpdateTimedActivity()
    {
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
            FinishActivity();
    }

    /// <summary>
    /// Patrulha procedural: percorre a lista de pontos gerados
    /// com perfil Guard. Cada chegada agenda o próximo ponto.
    /// </summary>
    void UpdatePatrolling()
    {
        if (patrolRoute.Count == 0)
        {
            EnterIdle();
            return;
        }

        // Só lança movimento se não estiver já em trânsito
        if (!nav.IsMoving)
        {
            Vector3 nextPoint = patrolRoute[patrolIndex];
            patrolIndex = (patrolIndex + 1) % patrolRoute.Count;

            nav.StartJourney(nextPoint, HumanNavigation.MovementProfile.Guard, () =>
            {
                // Pequena pausa de sentinela antes de avançar
                EnterIdle(Random.Range(1.5f, 4f));
            });
        }
    }

    /// <summary>Guarda estático: roda lentamente de um lado para o outro no posto.</summary>
    void UpdateWatching()
    {
        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            lookDir    = -lookDir; // alterna esquerda/direita
            lookTimer  = stationaryLookInterval + Random.Range(-0.5f, 0.5f);
            float targetAngle = lookDir * stationaryLookAngle;
            StartCoroutine(SmoothRotateToAngle(targetAngle, stationaryLookInterval * 0.6f));
        }
    }

    IEnumerator SmoothRotateToAngle(float angleDeg, float duration)
    {
        Quaternion from = transform.rotation;
        // Em 2D rotação no eixo Z a partir da rotação base (homeRotation)
        Quaternion to   = Quaternion.Euler(0f, 0f, angleDeg);
        float elapsed   = 0f;

        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = to;
    }



    IEnumerator DecideNextBehavior()
    {
        yield return new WaitForSeconds(Random.Range(0.2f, idleDecisionDelay));

        switch (shapeData.type)
        {
            case ShapeType.Circle:   DecideCircle();   break;
            case ShapeType.Hexagon:  DecideHexagon();  break;
            case ShapeType.Triangle: DecideTriangle(); break;
            case ShapeType.Square:   DecideSquare();   break;
        }

        isDeciding = false;
    }

    // ── Circle (Convidado) ────────────────────────────────────────────────────
    void DecideCircle()
    {
        float roll = Random.value;

        if      (roll < 0.30f) TryGoToInterestPoint(InterestPoint.PointType.Painting,   HumanNavigation.MovementProfile.Casual);
        else if (roll < 0.50f) TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Casual);
        else if (roll < 0.65f) TryGoToInterestPoint(InterestPoint.PointType.SocialArea, HumanNavigation.MovementProfile.Casual);
        else if (roll < 0.80f) WalkToRandomNearby(5f, HumanNavigation.MovementProfile.Casual);
        else                   EnterIdle(Random.Range(4f, 10f)); // fica a conversar
    }

    // ── Hexagon (Segurança) ───────────────────────────────────────────────────
    void DecideHexagon()
    {
        if (isStationary)
            currentState = NPCState.Watching;
        else
            currentState = NPCState.Patrolling;
    }

    // ── Triangle (VIP) ────────────────────────────────────────────────────────
    void DecideTriangle()
    {
        float roll = Random.value;

        if      (roll < 0.35f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.PrivateRoom, HumanNavigation.MovementProfile.Purposeful))
                TryGoToInterestPoint(InterestPoint.PointType.Seating, HumanNavigation.MovementProfile.Purposeful);
        }
        else if (roll < 0.55f) TryGoToInterestPoint(InterestPoint.PointType.SocialArea, HumanNavigation.MovementProfile.Purposeful);
        else if (roll < 0.70f) TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Purposeful);
        else if (roll < 0.80f) TryGoToInterestPoint(InterestPoint.PointType.Painting,   HumanNavigation.MovementProfile.Purposeful);
        else                   EnterIdle(Random.Range(8f, 20f)); // VIPs não se apressam
    }

    // ── Square (Staff) ────────────────────────────────────────────────────────
    void DecideSquare()
    {
        float roll = Random.value;

        if      (roll < 0.75f) EnterIdle(Random.Range(5f, 15f));
        else if (roll < 0.90f) WalkToRandomNearby(3f, HumanNavigation.MovementProfile.Worker);
        else                   TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Worker);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Navigation helpers
    // ──────────────────────────────────────────────────────────────────────────

    bool TryGoToInterestPoint(InterestPoint.PointType type, HumanNavigation.MovementProfile profile)
    {
        List<InterestPoint> candidates = new List<InterestPoint>();
        foreach (var pt in interestPoints)
        {
            if (pt.pointType != type)                                  continue;
            if (pt.vipOnly && shapeData.type != ShapeType.Triangle)   continue;
            if (!pt.IsAvailable())                                     continue;
            candidates.Add(pt);
        }

        if (candidates.Count == 0) return false;

        // Peso por distância — pontos mais perto têm mais probabilidade
        // (evita que todos os NPCs vão sempre ao mesmo sítio)
        InterestPoint chosen = PickWeightedByDistance(candidates);

        ReleaseCurrentInterestPoint();
        if (!chosen.Occupy()) return false;

        currentInterestPoint = chosen;
        currentState = NPCState.Walking;

        nav.StartJourney(chosen.transform.position, profile, OnArrivedAtInterestPoint);
        return true;
    }

    void WalkToRandomNearby(float radius, HumanNavigation.MovementProfile profile)
    {
        ReleaseCurrentInterestPoint();

        Vector3 randomDir = Random.insideUnitCircle;
        // 2D — offset no plano XY
        Vector3 offset    = new Vector3(randomDir.x, randomDir.y, 0f) * radius;
        Vector3 candidate = transform.position + offset;

        Vector3 dest = homePosition; // fallback
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            dest = hit.position;

        currentState = NPCState.Walking;
        nav.StartJourney(dest, profile, () => EnterIdle());
    }

    void ReturnHome(System.Action onArrived = null)
    {
        ReleaseCurrentInterestPoint();
        currentState = NPCState.Returning;

        HumanNavigation.MovementProfile profile = shapeData.type == ShapeType.Hexagon
            ? HumanNavigation.MovementProfile.Guard
            : HumanNavigation.MovementProfile.Purposeful;

        nav.StartJourney(homePosition, profile, () =>
        {
            onArrived?.Invoke();
            if (shapeData.type == ShapeType.Hexagon)
                currentState = isStationary ? NPCState.Watching : NPCState.Patrolling;
            else
                EnterIdle();
        });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Arrival callback
    // ──────────────────────────────────────────────────────────────────────────

    void OnArrivedAtInterestPoint()
    {
        if (currentInterestPoint == null) { EnterIdle(); return; }

        // Rotação suave para o ponto de interesse
        if (currentInterestPoint.facingDirection != Vector3.zero)
            StartCoroutine(SmoothFace(currentInterestPoint.facingDirection));

        NPCState activityState = currentInterestPoint.pointType switch
        {
            InterestPoint.PointType.Painting    => NPCState.Observing,
            InterestPoint.PointType.DrinkTable  => NPCState.Drinking,
            InterestPoint.PointType.SocialArea  => NPCState.Socializing,
            InterestPoint.PointType.Seating     => NPCState.Resting,
            InterestPoint.PointType.PrivateRoom => NPCState.Resting,
            _                                   => NPCState.Idle
        };

        float minT = minActivityTime;
        float maxT = maxActivityTime;

        if (shapeData.type == ShapeType.Triangle) { minT *= 2f; maxT *= 3f; }
        if (shapeData.type == ShapeType.Hexagon)  { minT = 1f; maxT = 3f; }

        stateTimer   = Random.Range(minT, maxT);
        currentState = activityState;
    }

    void FinishActivity()
    {
        ReleaseCurrentInterestPoint();

        bool returnToPost = shapeData.type == ShapeType.Hexagon
                         || shapeData.type == ShapeType.Square;

        if (returnToPost) ReturnHome();
        else              EnterIdle();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Patrol generation — cria rota circular no NavMesh em torno do posto
    // ──────────────────────────────────────────────────────────────────────────

    void GeneratePatrolRoute()
    {
        patrolRoute.Clear();

        for (int i = 0; i < patrolPointCount; i++)
        {
            // Distribui ângulos uniformemente + pequeno jitter
            float angle     = (360f / patrolPointCount) * i + Random.Range(-15f, 15f);
            float distance  = Random.Range(patrolRadius * 0.5f, patrolRadius);
            float rad       = angle * Mathf.Deg2Rad;

            // 2D — movimento no plano XY, não XZ
            Vector3 candidate = homePosition + new Vector3(
                Mathf.Cos(rad) * distance,
                Mathf.Sin(rad) * distance,
                0f
            );

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
                patrolRoute.Add(hit.position);
        }

        // Se não conseguiu nenhum ponto, usa só a posição inicial
        if (patrolRoute.Count == 0)
            patrolRoute.Add(homePosition);

        // Começa num ponto aleatório da rota (evita todos saírem do mesmo lado)
        patrolIndex = Random.Range(0, patrolRoute.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Envia um Hexagon investigar uma posição. Pode ser chamado externamente.</summary>
    public void InvestigatePosition(Vector3 position)
    {
        if (shapeData.type != ShapeType.Hexagon)
        {
            Debug.Log($"[NPC] {gameObject.name} ignorou InvestigatePosition — não é Hexagon (é {shapeData.type})");
            return;
        }

        Debug.Log($"[NPC] {gameObject.name} a investigar {position}");

        nav.StopJourney();
        ReleaseCurrentInterestPoint();
        currentState = NPCState.Investigating;

        nav.StartJourney(position, HumanNavigation.MovementProfile.Urgent, () =>
        {
            Debug.Log($"[NPC] {gameObject.name} chegou ao local, a regressar ao posto");
            ReturnHome();
        });
    }

    /// <summary>Alerta genérico — delega para InvestigatePosition se for segurança.</summary>
    public void Alert(Vector3 incidentPosition) => InvestigatePosition(incidentPosition);

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    void EnterIdle(float delay = -1f)
    {
        currentState = NPCState.Idle;
        idleTimer    = delay > 0f ? delay : Random.Range(0.5f, idleDecisionDelay);
        isDeciding   = false;
    }

    void ReleaseCurrentInterestPoint()
    {
        currentInterestPoint?.Vacate();
        currentInterestPoint = null;
    }

    /// <summary>Escolhe um InterestPoint com probabilidade inversamente proporcional à distância.</summary>
    InterestPoint PickWeightedByDistance(List<InterestPoint> candidates)
    {
        float totalWeight = 0f;
        float[] weights   = new float[candidates.Count];

        for (int i = 0; i < candidates.Count; i++)
        {
            float dist  = Vector3.Distance(transform.position, candidates[i].transform.position);
            weights[i]  = 1f / (dist + 0.1f); // inverso da distância
            totalWeight += weights[i];
        }

        float rand = Random.Range(0f, totalWeight);
        float cumulative = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            cumulative += weights[i];
            if (rand <= cumulative) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    IEnumerator SmoothFace(Vector3 direction)
    {
        float elapsed  = 0f;
        float duration = 0.5f;
        Quaternion from = transform.rotation;

        // Em 2D a rotação é no eixo Z
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion to = Quaternion.Euler(0f, 0f, angle);

        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = to;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Gizmos
    // ──────────────────────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        // Posto / home
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, 0.4f);

        // Visão
        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, visionRange);

        // Rota de patrulha (Hexagon)
        if (patrolRoute != null && patrolRoute.Count > 1)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.6f);
            for (int i = 0; i < patrolRoute.Count; i++)
            {
                Vector3 a = patrolRoute[i];
                Vector3 b = patrolRoute[(i + 1) % patrolRoute.Count];
                Gizmos.DrawLine(a, b);
                Gizmos.DrawWireSphere(a, 0.2f);
            }
        }

        // Raio de patrulha
        if (shapeData != null && shapeData.type == ShapeType.Hexagon)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.1f);
            Gizmos.DrawWireSphere(homePosition, patrolRadius);
        }

        // Label de estado
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.4f,
            $"{(shapeData != null ? shapeData.type.ToString() : "?")} [{currentState}]"
        );
        #endif
    }
}