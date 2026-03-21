using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// HumanNavigation — Módulo de movimento orgânico para NPCs.
///
/// Funciona por cima do NavMeshAgent: dado um destino final, este componente
/// gera pontos intermédios no NavMesh para criar um percurso que parece humano,
/// com velocidade variável (Perlin noise), micro-pausas espontâneas e rotação
/// suave. Não precisa de nenhum waypoint manual no Inspector.
///
/// Uso:
///   nav.StartJourney(destination, profile, onArrived);
///   nav.StopJourney();
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class HumanNavigation : MonoBehaviour
{
    // ──────────────────────────────────────────────────────────────────────────
    // Movement Profiles — definem a "personalidade" do andar
    // ──────────────────────────────────────────────────────────────────────────

    public enum MovementProfile
    {
        Casual,     // Convidado — passeia devagar, para às vezes, desvia-se
        Purposeful, // VIP a ir para um sítio específico — mais direto mas ainda humano
        Guard,      // Segurança — passo firme, poucas pausas, mais reto
        Worker,     // Funcionário — andar funcional, ligeiramente apressado
        Urgent      // Alerta / emergência — quase a correr, sem desvios
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector tunables
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Path Generation")]
    [Tooltip("Quantos pontos intermédios gerar entre origem e destino")]
    [Range(0, 4)]
    public int waypointCount = 2;

    [Tooltip("Amplitude máxima do desvio lateral dos waypoints intermédios (metros)")]
    public float deviationRadius = 1.8f;

    [Tooltip("Raio de amostragem no NavMesh para waypoints intermédios")]
    public float navSampleRadius = 3f;

    [Tooltip("Distância mínima às paredes — pontos mais perto que isto são descartados. Deve ser >= Agent Radius do NavMeshAgent.")]
    public float wallClearance = 0.5f;

    [Header("Speed Variation")]
    [Tooltip("Amplitude do noise de velocidade (0 = velocidade constante)")]
    [Range(0f, 0.6f)]
    public float speedNoiseAmplitude = 0.28f;

    [Tooltip("Frequência do noise (valores maiores = variação mais rápida)")]
    [Range(0.1f, 2f)]
    public float speedNoiseFrequency = 0.4f;

    [Header("Micro-pauses")]
    [Tooltip("Probabilidade (0-1) de fazer uma micro-pausa num waypoint intermédio")]
    [Range(0f, 1f)]
    public float micropauseProbability = 0.35f;

    [Tooltip("Duração mínima de uma micro-pausa (segundos)")]
    public float micropauseMin = 0.4f;

    [Tooltip("Duração máxima de uma micro-pausa (segundos)")]
    public float micropauseMax = 2.2f;

    [Header("Look-Around (ao parar)")]
    [Tooltip("O NPC olha ligeiramente para os lados quando faz micro-pausas")]
    public bool lookAroundOnPause = true;

    [Tooltip("Ângulo máximo de rotação ao olhar em redor (graus)")]
    [Range(10f, 60f)]
    public float lookAroundAngle = 30f;

    // ──────────────────────────────────────────────────────────────────────────
    // Private runtime
    // ──────────────────────────────────────────────────────────────────────────

    private NavMeshAgent        agent;
    private Coroutine           journeyCoroutine;
    private float               baseSpeed;
    private float               noiseOffset;        // offset único por NPC no Perlin

    // Perfil ativo — ajusta os parâmetros em runtime
    private MovementProfile     activeProfile = MovementProfile.Casual;

    // Callback ao chegar
    private System.Action       onArrivedCallback;

    public bool IsMoving { get; private set; }

    // ──────────────────────────────────────────────────────────────────────────
    // Init
    // ──────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        agent       = GetComponent<NavMeshAgent>();
        noiseOffset = Random.Range(0f, 100f); // cada NPC tem o seu próprio "ritmo"
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Inicia uma viagem até <destination> com o perfil dado.
    /// Quando chegar (ou se já lá estiver), invoca <onArrived>.
    /// </summary>
    public void StartJourney(Vector3 destination, MovementProfile profile, System.Action onArrived = null)
    {
        StopJourney();
        activeProfile    = profile;
        onArrivedCallback = onArrived;
        baseSpeed        = agent.speed;

        ApplyProfileDefaults(profile);

        journeyCoroutine = StartCoroutine(JourneyRoutine(destination));
    }

    /// <summary>Cancela a viagem em curso imediatamente.</summary>
    public void StopJourney()
    {
        if (journeyCoroutine != null)
        {
            StopCoroutine(journeyCoroutine);
            journeyCoroutine = null;
        }
        IsMoving = false;
        agent.ResetPath();
        agent.speed = baseSpeed > 0 ? baseSpeed : agent.speed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Core journey coroutine
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator JourneyRoutine(Vector3 finalDestination)
    {
        IsMoving = true;

        // 1. Gera a lista de waypoints intermédios + destino final
        List<Vector3> path = BuildPath(transform.position, finalDestination);

        // 2. Percorre cada waypoint
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 waypoint = path[i];
            bool    isFinal  = (i == path.Count - 1);

            // Define destino no agente
            agent.SetDestination(waypoint);

            // Aguarda que o path seja calculado
            yield return new WaitUntil(() => !agent.pathPending);

            // Caminha até ao waypoint com velocidade orgânica
            yield return StartCoroutine(WalkToPoint(waypoint, isFinal));

            // Micro-pausa (apenas em waypoints intermédios)
            if (!isFinal && Random.value < micropauseProbability)
            {
                float pauseDuration = Random.Range(micropauseMin, micropauseMax);
                yield return StartCoroutine(MicroPause(pauseDuration));
            }
        }

        IsMoving = false;
        agent.ResetPath();
        onArrivedCallback?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Walk to a single point with organic speed
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator WalkToPoint(Vector3 target, bool isFinal)
    {
        // Garante que o destino está definido
        if (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid)
            agent.SetDestination(target);

        while (true)
        {
            // Velocidade orgânica via Perlin noise
            float noise      = Mathf.PerlinNoise(Time.time * speedNoiseFrequency + noiseOffset, 0f);
            float speedMult  = 1f + (noise - 0.5f) * 2f * speedNoiseAmplitude;
            agent.speed      = baseSpeed * Mathf.Clamp(speedMult, 0.5f, 1.5f);

            // Verifica chegada
            if (HasArrivedAt(target))
                break;

            // Destino perdido (obstáculo dinâmico) — recalcula
            if (!agent.hasPath && !agent.pathPending)
                agent.SetDestination(target);

            yield return null;
        }

        agent.speed = baseSpeed;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Micro-pause with optional look-around
    // ──────────────────────────────────────────────────────────────────────────

    IEnumerator MicroPause(float duration)
    {
        agent.ResetPath();
        agent.speed = 0f;

        if (lookAroundOnPause)
            yield return StartCoroutine(LookAround(duration));
        else
            yield return new WaitForSeconds(duration);

        agent.speed = baseSpeed;
    }

    IEnumerator LookAround(float duration)
    {
        float elapsed   = 0f;
        Quaternion baseRot = transform.rotation;

        // Escolhe um ângulo aleatório para "olhar" — para um lado e depois volta
        float targetAngle = Random.Range(-lookAroundAngle, lookAroundAngle);
        float halfDuration = duration * 0.45f;

        // Vira para o lado — em 2D a rotação é no eixo Z
        Quaternion lookRot = baseRot * Quaternion.Euler(0f, 0f, targetAngle);
        while (elapsed < halfDuration)
        {
            transform.rotation = Quaternion.Slerp(baseRot, lookRot, elapsed / halfDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Volta à frente
        float elapsed2 = 0f;
        while (elapsed2 < halfDuration)
        {
            transform.rotation = Quaternion.Slerp(lookRot, baseRot, elapsed2 / halfDuration);
            elapsed2 += Time.deltaTime;
            yield return null;
        }

        // Pausa restante
        float remaining = duration - elapsed - elapsed2;
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Path building — gera waypoints intermédios no NavMesh
    // ──────────────────────────────────────────────────────────────────────────

    List<Vector3> BuildPath(Vector3 origin, Vector3 destination)
    {
        List<Vector3> path = new List<Vector3>();

        if (waypointCount <= 0)
        {
            path.Add(destination);
            return path;
        }

        // Em 2D o movimento é no plano XY — perpendicular é no eixo X/Y
        Vector3 dir      = (destination - origin);
        // Perpendicular no plano XY (2D): roda 90° no plano
        Vector3 perpXY   = new Vector3(-dir.y, dir.x, 0f).normalized;

        for (int i = 1; i <= waypointCount; i++)
        {
            float   t         = (float)i / (waypointCount + 1);
            Vector3 linePoint = Vector3.Lerp(origin, destination, t);

            // Desvio lateral suave via Perlin noise
            float   noiseVal  = Mathf.PerlinNoise(noiseOffset + t * 3f, noiseOffset) * 2f - 1f;
            float   deviation = noiseVal * deviationRadius;
            Vector3 candidate = linePoint + perpXY * deviation;

            // Em 2D mantém o Z original do agente (o NavMesh está nesse plano)
            candidate.z = origin.z;

            // Afasta do centro para evitar colar em paredes —
            // amostra numa área maior e rejeita pontos demasiado perto de obstáculos
            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, navSampleRadius, NavMesh.AllAreas))
            {
                // Verifica se o ponto amostrado não está colado a uma borda
                // fazendo um segundo sample com raio menor — se falhar é porque
                // está muito perto de uma parede
                if (NavMesh.SamplePosition(hit.position, out _, wallClearance, NavMesh.AllAreas))
                    path.Add(hit.position);
                else
                    path.Add(linePoint); // muito perto da parede, vai pelo centro
            }
            else
            {
                path.Add(linePoint);
            }
        }

        path.Add(destination);
        return path;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Profile presets — ajusta parâmetros consoante o tipo de movimento
    // ──────────────────────────────────────────────────────────────────────────

    void ApplyProfileDefaults(MovementProfile profile)
    {
        switch (profile)
        {
            case MovementProfile.Casual:
                waypointCount          = Random.Range(1, 3);
                deviationRadius        = 1.8f;
                speedNoiseAmplitude    = 0.30f;
                micropauseProbability  = 0.40f;
                micropauseMin          = 0.5f;
                micropauseMax          = 2.5f;
                lookAroundOnPause      = true;
                break;

            case MovementProfile.Purposeful:
                waypointCount          = 1;
                deviationRadius        = 0.8f;
                speedNoiseAmplitude    = 0.15f;
                micropauseProbability  = 0.15f;
                micropauseMin          = 0.3f;
                micropauseMax          = 1.0f;
                lookAroundOnPause      = false;
                break;

            case MovementProfile.Guard:
                waypointCount          = 0;        // vai direto
                deviationRadius        = 0f;
                speedNoiseAmplitude    = 0.08f;
                micropauseProbability  = 0.05f;
                micropauseMin          = 0.3f;
                micropauseMax          = 0.6f;
                lookAroundOnPause      = true;
                break;

            case MovementProfile.Worker:
                waypointCount          = 1;
                deviationRadius        = 0.6f;
                speedNoiseAmplitude    = 0.12f;
                micropauseProbability  = 0.08f;
                micropauseMin          = 0.2f;
                micropauseMax          = 0.5f;
                lookAroundOnPause      = false;
                break;

            case MovementProfile.Urgent:
                waypointCount          = 0;
                deviationRadius        = 0f;
                speedNoiseAmplitude    = 0.05f;
                micropauseProbability  = 0f;
                lookAroundOnPause      = false;
                agent.speed           *= 1.8f;     // aumenta velocidade
                break;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    bool HasArrivedAt(Vector3 target)
    {
        if (agent.pathPending) return false;
        // Usa stoppingDistance com pequena margem extra
        float threshold = agent.stoppingDistance + 0.15f;
        return agent.remainingDistance <= threshold
            && (!agent.hasPath || agent.velocity.sqrMagnitude < 0.02f);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Gizmos — mostra o path gerado no editor
    // ──────────────────────────────────────────────────────────────────────────

    #if UNITY_EDITOR
    // Apenas para debug no editor — guarda último path gerado
    private List<Vector3> debugPath = new List<Vector3>();

    void OnDrawGizmosSelected()
    {
        if (debugPath == null || debugPath.Count == 0) return;

        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        Vector3 prev = transform.position;
        foreach (var pt in debugPath)
        {
            Gizmos.DrawLine(prev, pt);
            Gizmos.DrawWireSphere(pt, 0.15f);
            prev = pt;
        }
    }
    #endif
}