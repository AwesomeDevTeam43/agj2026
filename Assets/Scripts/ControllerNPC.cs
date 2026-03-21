using System.Data.Common;
using UnityEngine;

public class ControllerNPC : MonoBehaviour
{
    public ShapeData shapeData;
    public enum NPCState { Idle, Moving, Chasing, Returning }
    public NPCState currentState = NPCState.Idle;

    [Header("Configuration")]
    public Transform[] waypoints;
    public Transform initialPosition;
    public float visionRange = 5f;

    private int currentWaypointIndex = 0;
    private Vector2 targetPosition;
    private Vector2 homePosition;
    private Rigidbody2D rb;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer != null && shapeData != null)
        {
            spriteRenderer.color = shapeData.color;
        }

        switch (shapeData.type)
        {
            case ShapeType.Square:
                currentState = NPCState.Idle;
                break;

            case ShapeType.Circle:
                currentState = NPCState.Idle;
                break;

            case ShapeType.Triangle:
                currentState = NPCState.Idle;
                break;

            case ShapeType.Hexagon:
                currentState = NPCState.Idle;
                break;
        }

        homePosition = initialPosition.position;
    }

    void Update()
    {
        switch (currentState)
        {
            case NPCState.Idle:
                HandleBehavior();
                break;

            case NPCState.Moving:
                MoveToWaypoint();
                CheckArrival();
                break;

            case NPCState.Chasing:
                //ChasePlayer();
                break;

            case NPCState.Returning:
                ReturnToInitialPosition();
                break;
        }
    }

    void HandleBehavior()
    {
        /*funcionamento dos npc
        circle npc
        apenas vão andar de um lado para o outro e podemos fazer com que eles se o player der muita cana esse npc pode ir alertar um segurança
        hexagon security
        vão estar maioritariamente parados nas entradas e saídas das salas a vigiar o ambiente e caso aconteça algo vai até ao local e depois volta a posição inicial
        triangle vip
        npc que tanto podem só andar de um lado para o outro como podem ser bem mais vigilantes que o npc normal e vão estar em zonas de acesso restrito
        square worker
        estes vão estar atrás de espécies de balcões estáticos a “atender” clientes e uma vez ou outra podem andar pelo mapa*/
        float chance = Random.Range(0f, 100f);

        switch (shapeData.type)
        {
            case ShapeType.Circle:
                if (waypoints.Length > 0)
                {
                    targetPosition = waypoints[currentWaypointIndex].position;
                    currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
                }
                else
                {
                    SetNewRandomWaipoint();
                }
                currentState = NPCState.Moving;
                break;
            case ShapeType.Hexagon:
                if (chance < .5f)
                {
                    SetNewRandomWaipoint();
                    currentState = NPCState.Moving;
                }
                break;
            case ShapeType.Triangle:
                SetNewRandomWaipoint();
                currentState = NPCState.Moving;
                break;
            case ShapeType.Square:
                if (chance < .02f)
                {
                    SetNewRandomWaipoint();
                    currentState = NPCState.Moving;
                }
                break;
        }
    }

    void CheckArrival()
    {
        float dist = Vector2.Distance(transform.position, targetPosition);

        if (dist < 0.2f)
        {
            rb.linearVelocity = Vector2.zero;
            if (shapeData.type == ShapeType.Hexagon || shapeData.type == ShapeType.Square)
            {
                currentState = NPCState.Returning;
            }
            else
            {
                currentState = NPCState.Idle;
            }
        }
    }

    void ReturnToInitialPosition()
    {
        Vector2 direction = (homePosition - (Vector2)transform.position).normalized;
        rb.linearVelocity = direction * shapeData.baseSpeed;

        if (Vector2.Distance(transform.position, homePosition) < 0.1f)
        {
            rb.linearVelocity = Vector2.zero; // Para o boneco
            currentState = NPCState.Idle;
        }
    }

    void MoveToWaypoint()
    {
        Vector2 offset = targetPosition - (Vector2)transform.position;
        Vector2 direction = offset.normalized;
        rb.linearVelocity = direction * shapeData.baseSpeed;
    }

    void SetNewRandomWaipoint()
    {
        targetPosition = homePosition + (Random.insideUnitCircle * 5f);
    }

    void OnDrawGizmos()
    {
        // Desenha uma linha até ao destino atual (alvo)
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetPosition);

        // Desenha uma esfera no ponto inicial (home)
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(homePosition, 0.5f);

        // Desenha o alcance de visão
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}
