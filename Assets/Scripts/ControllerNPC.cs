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
///   Circle  (Convidado) — passeia de um lado para o outro, bebe, admira, socializa
///   Hexagon (Segurança) — estático a vigiar OU patrulha, investiga incidentes
///   Triangle (VIP)      — vigilante, zonas restritas, quartos privados, pouco movimento
///   Square  (Staff)     — maioritariamente estático no posto, circula raramente
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(HumanNavigation))]
public class ControllerNPC : MonoBehaviour, ISeesPlayerActions
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
        Returning,      // A regressar ao posto
        CallingGuard,    // Convidado/VIP: em pânico a chamar um segurança
        Dead            // NPC morto (usado para corpos no chão, não tem comportamento)
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
    private Quaternion      homeRotation;           // rotação no spawn — base do look-around
    private InterestPoint   currentInterestPoint;

    private float           stateTimer     = 0f;
    private float           idleTimer      = 0f;
    private bool            isDeciding     = false;
    private float           lookTimer      = 0f;
    private int             lookDir        = 1;
    private float           bodyCheckTimer = 0f;

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

        homePosition = initialPosition != null ? initialPosition.position : transform.position;
        homeRotation = transform.rotation; // base para o look-around do guarda estático

        if (interestPoints == null || interestPoints.Length == 0)
            interestPoints = FindObjectsByType<InterestPoint>(FindObjectsSortMode.None);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            playerTransform = playerObj.transform;

        if (shapeData.type == ShapeType.Hexagon && !isStationary)
            GeneratePatrolRoute();

        // Stagger inicial — NPCs não decidem todos ao mesmo tempo
        idleTimer = Random.Range(0f, idleDecisionDelay * 3f);
        // lookTimer começa cheio — guarda não roda logo no primeiro frame
        lookTimer = stationaryLookInterval;
    }

    void Update()
    {
        if (nav == null || shapeData == null) return;

        // Deteção de corpos — todos exceto Husks, e não durante ações prioritárias
        if (shapeData.type != ShapeType.Husk &&
            currentState != NPCState.CallingGuard &&
            currentState != NPCState.Investigating)
        {
            bodyCheckTimer -= Time.deltaTime;
            if (bodyCheckTimer <= 0f)
            {
                bodyCheckTimer = 0.5f;
                CheckForDeadBodies();
            }
        }

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
            case NPCState.Investigating: UpdateInvestigating(); break;
            case NPCState.Returning:     /* gerido por callback */ break;
            case NPCState.CallingGuard:  /* gerido por callback */ break;
            case NPCState.Dead:         EnsureIsDead(); break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // State handlers
    // ──────────────────────────────────────────────────────────────────────────

    void EnsureIsDead()
    {
        if (currentState == NPCState.Dead)
        {
            currentState = NPCState.Dead;
            nav.StopJourney();
            ReleaseCurrentInterestPoint();
        }
    }

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

    void UpdatePatrolling()
    {
        if (patrolRoute.Count == 0) { EnterIdle(); return; }

        if (!nav.IsMoving)
        {
            Vector3 nextPoint = patrolRoute[patrolIndex];
            patrolIndex = (patrolIndex + 1) % patrolRoute.Count;

            nav.StartJourney(nextPoint, HumanNavigation.MovementProfile.Guard, () =>
            {
                EnterIdle(Random.Range(1.5f, 4f));
            });
        }
    }

    void UpdateWatching()
    {
        lookTimer -= Time.deltaTime;
        if (lookTimer <= 0f)
        {
            lookDir   = -lookDir;
            lookTimer = stationaryLookInterval + Random.Range(-0.5f, 0.5f);
            StartCoroutine(SmoothRotateToAngle(lookDir * stationaryLookAngle, stationaryLookInterval * 0.6f));
        }
    }

    void UpdateInvestigating()
    {
        if (shapeData.type != ShapeType.Hexagon) return;

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null && Vector2.Distance(transform.position, playerObj.transform.position) < 1.5f)
        {
            Debug.Log("Guard caught the player!");
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Dead body detection
    // ──────────────────────────────────────────────────────────────────────────
    public void OnPlayerPickedUpTreasure(GameObject player)
    {
        if (CanSeePlayer(player))
        {
            if (shapeData.type == ShapeType.Hexagon)
            {
                Debug.Log($"[NPC] {gameObject.name} viu o jogador a roubar o tesouro!");
                Alert(player.transform.position);
            }
            else
            {
                Debug.Log($"[NPC] {gameObject.name} viu o jogador a roubar o tesouro e entrou em pânico!");
                PanicAndFindGuard(player.transform.position);
            }
        }
    }
    public bool CanSeePlayer(GameObject player)
    {
      if (playerTransform == null) return false;

        Vector2 dirToPlayer = playerTransform.position - transform.position;
        if (dirToPlayer.magnitude > visionRange) return false;

        // Draws a red line in the Scene view so you can visually verify the line of sight!
        Debug.DrawRay(transform.position, dirToPlayer.normalized * visionRange, Color.red, 2f);

        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, dirToPlayer.normalized, visionRange);
        
        foreach (var hit in hits)
        
        {
            // 1. Ignore the NPC itself
            if (hit.collider.gameObject == gameObject) continue;
            
            // 2. DID WE HIT THE PLAYER? Check by tag to avoid child-object / trigger issues
            if (hit.collider.CompareTag("Player")) 
            {
                Debug.Log($"[Vision] {gameObject.name} clearly sees the Player!");
                return true;
            }

            // 3. Ignore non-player triggers (like the Treasure zone itself)
            if (hit.collider.isTrigger) continue;

            // 4. If we hit a solid wall before finding the player, vision is blocked
            Debug.Log($"[Vision] {gameObject.name}'s vision blocked by {hit.collider.name}");
            return false; 
        }

        return false; 
    }

    void CheckForDeadBodies()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, visionRange);
        foreach (var hit in hits)
        {
            var sv = hit.GetComponent<ShapeVisualizer>();
            if (sv != null && sv.shapeData != null && sv.shapeData.type == ShapeType.Husk)
            {
                PanicAndFindGuard(hit.transform.position);
                break;
            }
        }
    }

void PanicAndFindGuard(Vector3 bodyPosition)
    {
        Debug.Log($"[NPC] {gameObject.name} ({shapeData.type}) viu um corpo e precisa de ajuda!");

        if (shapeData.type == ShapeType.Hexagon)
        {
            GetComponentInChildren<SuspicionDetector>()?.RaiseGlobalAlarm();
            InvestigatePosition(bodyPosition);
            return;
        }

        nav.StopJourney();
        ReleaseCurrentInterestPoint();
        currentState = NPCState.CallingGuard;

        // 1. Try to find a Guard first
        ControllerNPC target = FindReachableNPC(new List<ShapeType> { ShapeType.Hexagon });

        // 2. If no Guard is reachable, look for a Staff member or VIP to pass the message to!
        if (target == null)
        {
            Debug.Log($"[NPC] {gameObject.name} cannot reach a Guard. Looking for Staff/VIP!");
            target = FindReachableNPC(new List<ShapeType> { ShapeType.Square, ShapeType.Triangle });
        }

        // 3. Run to whoever we found
        if (target != null)
        {
            nav.StartJourney(target.transform.position, HumanNavigation.MovementProfile.Urgent, () =>
            {
                Debug.Log($"[NPC] {gameObject.name} pediu ajuda a {target.name}!");
                target.ReceivePanic(bodyPosition); // Pass the panic to the proxy
                EnterIdle(5f); // The guest rests/cowers while the proxy handles it
            });
        }
        else
        {
            // FALLBACK: Completely trapped alone in a room. 
            Debug.LogWarning($"[NPC] {gameObject.name} is trapped with a body! Cowering.");
            EnterIdle(5f); 
        }
    }

    // Called when another NPC runs up to this one and asks for help
    // Helper method to find the closest reachable NPC of specific Shape Types
public void ReceivePanic(Vector3 bodyPosition)
    {
        if (!enabled || currentState == NPCState.Dead) return;

        if (shapeData.type == ShapeType.Hexagon)
        {
            InvestigatePosition(bodyPosition);
        }
        else if (currentState != NPCState.CallingGuard)
        {
            Debug.Log($"[NPC] {gameObject.name} foi avisado do corpo! A procurar um guarda!");
            PanicAndFindGuard(bodyPosition);
        }
    }
private ControllerNPC FindReachableNPC(List<ShapeType> allowedTypes)
    {
        ControllerNPC nearest = null;
        float minDist = float.MaxValue;
        UnityEngine.AI.NavMeshPath testPath = new UnityEngine.AI.NavMeshPath();

        foreach (var npc in FindObjectsByType<ControllerNPC>(FindObjectsSortMode.None))
        {
            // Ignore self, dead bodies, disabled NPCs, and people already panicking
            if (npc == this || !npc.enabled || npc.currentState == NPCState.Dead || npc.currentState == NPCState.CallingGuard) continue;
            
            // Only check them if they are the shape we are looking for
            if (!allowedTypes.Contains(npc.shapeData.type)) continue;

            // Check if the path is physically clear
            agent.CalculatePath(npc.transform.position, testPath);
            if (testPath.status != UnityEngine.AI.NavMeshPathStatus.PathComplete) continue;

            float d = Vector2.Distance(transform.position, npc.transform.position);
            if (d < minDist) 
            { 
                minDist = d; 
                nearest = npc; 
            }
        }
        return nearest;
    }
    // ──────────────────────────────────────────────────────────────────────────
    // Decision coroutine
    // ──────────────────────────────────────────────────────────────────────────

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

    // ── Circle (Convidado de festa) ───────────────────────────────────────────
    // Passeia de um lado para o outro, para a conversar, bebe, admira coisas.
    void DecideCircle()
    {
        float roll = Random.value;

        if (roll < 0.25f)
        {
            WalkToRandomNearby(5f, HumanNavigation.MovementProfile.Casual);
        }
        else if (roll < 0.45f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Casual))
                WalkToRandomNearby(4f, HumanNavigation.MovementProfile.Casual);
        }
        else if (roll < 0.60f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.Painting, HumanNavigation.MovementProfile.Casual))
                EnterIdle(Random.Range(3f, 7f));
        }
        else if (roll < 0.75f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.SocialArea, HumanNavigation.MovementProfile.Casual))
                WalkToRandomNearby(3f, HumanNavigation.MovementProfile.Casual);
        }
        else
        {
            EnterIdle(Random.Range(5f, 12f));
        }
    }

    // ── Hexagon (Segurança) ───────────────────────────────────────────────────
    // Estático: fica no posto a vigiar. Patrulha: rondas na área.
    void DecideHexagon()
    {
        currentState = isStationary ? NPCState.Watching : NPCState.Patrolling;
    }

    // ── Triangle (VIP) ────────────────────────────────────────────────────────
    // Mais vigilante, zonas restritas, muito tempo parado.
    void DecideTriangle()
    {
        float roll = Random.value;

        if (roll < 0.30f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.PrivateRoom, HumanNavigation.MovementProfile.Purposeful))
                TryGoToInterestPoint(InterestPoint.PointType.Seating, HumanNavigation.MovementProfile.Purposeful);
        }
        else if (roll < 0.50f)
        {
            EnterIdle(Random.Range(10f, 25f));
        }
        else if (roll < 0.65f)
        {
            WalkToRandomNearby(4f, HumanNavigation.MovementProfile.Purposeful);
        }
        else if (roll < 0.78f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.SocialArea, HumanNavigation.MovementProfile.Purposeful))
                EnterIdle(Random.Range(8f, 15f));
        }
        else if (roll < 0.88f)
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Purposeful))
                EnterIdle(Random.Range(5f, 10f));
        }
        else
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.Painting, HumanNavigation.MovementProfile.Purposeful))
                EnterIdle(Random.Range(5f, 10f));
        }
    }

    // ── Square (Funcionário) ──────────────────────────────────────────────────
    // Maioritariamente no posto a "atender", circula raramente.
    void DecideSquare()
    {
        float roll = Random.value;

        if (roll < 0.70f)
        {
            EnterIdle(Random.Range(8f, 20f));
        }
        else if (roll < 0.88f)
        {
            WalkToRandomNearby(3f, HumanNavigation.MovementProfile.Worker);
        }
        else
        {
            if (!TryGoToInterestPoint(InterestPoint.PointType.DrinkTable, HumanNavigation.MovementProfile.Worker))
                EnterIdle(Random.Range(5f, 12f));
        }
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

        InterestPoint chosen = candidates[Random.Range(0, candidates.Count)];
        ReleaseCurrentInterestPoint();
        if (!chosen.Occupy()) return false;

        currentInterestPoint = chosen;
        currentState = NPCState.Walking;

        Vector3 destination = chosen.transform.position;
        if (chosen.maxOccupants > 1)
        {
            Vector2 offset = Random.insideUnitCircle * 0.8f;
            destination += new Vector3(offset.x, offset.y, 0f);
        }

        nav.StartJourney(destination, profile, OnArrivedAtInterestPoint);
        return true;
    }

    void WalkToRandomNearby(float radius, HumanNavigation.MovementProfile profile)
    {
        ReleaseCurrentInterestPoint();

        Vector2 randomDir = Random.insideUnitCircle;
        Vector3 candidate = transform.position + new Vector3(randomDir.x, randomDir.y, 0f) * radius;

        Vector3 dest = homePosition;
        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, radius, NavMesh.AllAreas))
            dest = hit.position;

        currentState = NPCState.Walking;
        nav.StartJourney(dest, profile, () => EnterIdle());
    }

    void ReturnHome(System.Action onArrived = null)
    {
        ReleaseCurrentInterestPoint();
        currentState = NPCState.Returning;

        var profile = shapeData.type == ShapeType.Hexagon
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
        if (shapeData.type == ShapeType.Hexagon)  { minT = 1f;  maxT = 3f; }

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
    // Patrol generation
    // ──────────────────────────────────────────────────────────────────────────

    void GeneratePatrolRoute()
    {
        patrolRoute.Clear();

        for (int i = 0; i < patrolPointCount; i++)
        {
            float angle    = (360f / patrolPointCount) * i + Random.Range(-15f, 15f);
            float distance = Random.Range(patrolRadius * 0.5f, patrolRadius);
            float rad      = angle * Mathf.Deg2Rad;

            Vector3 candidate = homePosition + new Vector3(
                Mathf.Cos(rad) * distance,
                Mathf.Sin(rad) * distance,
                0f
            );

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
                patrolRoute.Add(hit.position);
        }

        if (patrolRoute.Count == 0)
            patrolRoute.Add(homePosition);

        patrolIndex = Random.Range(0, patrolRoute.Count);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>Envia este guarda investigar uma posição. Só funciona em Hexagon.</summary>
    public void InvestigatePosition(Vector3 position)
    {
        if (shapeData.type != ShapeType.Hexagon)
        {
            Debug.Log($"[NPC] {gameObject.name} ignorou InvestigatePosition — não é Hexagon ({shapeData.type})");
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

    /// <summary>Envia este NPC para uma posição e executa uma ação ao chegar.</summary>
    public void GoDoTask(Vector3 position, System.Action onArrived)
    {
        nav.StopJourney();
        ReleaseCurrentInterestPoint();
        currentState = NPCState.Walking;
        nav.StartJourney(position, HumanNavigation.MovementProfile.Worker, onArrived);
    }

    /// <summary>Retoma comportamento normal após uma tarefa externa.</summary>
    public void ResumeNormalBehavior() => ReturnHome();

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

    IEnumerator SmoothRotateToAngle(float angleDeg, float duration)
    {
        Quaternion from = transform.rotation;
        // RELATIVO à rotação do spawn — o guarda olha X graus para o lado
        // a partir da direção em que estava virado quando foi colocado na cena
        Quaternion to   = homeRotation * Quaternion.Euler(0f, 0f, angleDeg);
        float elapsed   = 0f;

        while (elapsed < duration)
        {
            transform.rotation = Quaternion.Slerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.rotation = to;
    }

    IEnumerator SmoothFace(Vector3 direction)
    {
        float elapsed   = 0f;
        float duration  = 0.5f;
        Quaternion from = transform.rotation;
        float angle     = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Quaternion to   = Quaternion.Euler(0f, 0f, angle);

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
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, 0.4f);

        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, visionRange);

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

        if (shapeData != null && shapeData.type == ShapeType.Hexagon)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.1f);
            Gizmos.DrawWireSphere(homePosition, patrolRadius);
        }

        #if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 1.4f,
            $"{(shapeData != null ? shapeData.type.ToString() : "?")} [{currentState}]"
        );
        #endif
    }
}