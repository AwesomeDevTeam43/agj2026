using UnityEngine;

/// <summary>
/// Marca um ponto de interesse no mapa que os NPCs podem visitar.
/// Coloca este componente em objetos como quadros, mesas de bebidas, cadeiras, etc.
/// </summary>
public class InterestPoint : MonoBehaviour
{
    public enum PointType
    {
        Painting,       // Quadros / obras de arte
        DrinkTable,     // Mesa de bebidas
        Seating,        // Cadeiras / sofás
        Entrance,       // Entradas / saídas de salas
        PrivateRoom,    // Quartos privados (só VIPs)
        SocialArea,     // Zonas de socialização
        Generic         // Genérico
    }

    [Header("Point Configuration")]
    public PointType pointType = PointType.Generic;

    [Tooltip("Quantos NPCs podem usar este ponto ao mesmo tempo")]
    public int maxOccupants = 1;

    [Tooltip("Só NPCs VIP (Triangle) podem usar este ponto")]
    public bool vipOnly = false;

    [Tooltip("Direção para onde o NPC olha quando está neste ponto (deixar (0,0,0) para ignorar)")]
    public Vector3 facingDirection = Vector3.zero;

    // Runtime
    private int currentOccupants = 0;

    public bool IsAvailable() => currentOccupants < maxOccupants;

    public bool Occupy()
    {
        if (!IsAvailable()) return false;
        currentOccupants++;
        return true;
    }

    public void Vacate()
    {
        currentOccupants = Mathf.Max(0, currentOccupants - 1);
    }

    void OnDrawGizmos()
    {
        Color gizmoColor = pointType switch
        {
            PointType.Painting     => Color.cyan,
            PointType.DrinkTable   => Color.yellow,
            PointType.Seating      => Color.green,
            PointType.Entrance     => Color.white,
            PointType.PrivateRoom  => new Color(0.8f, 0.2f, 0.8f),
            PointType.SocialArea   => Color.blue,
            _                      => Color.gray
        };

        Gizmos.color = gizmoColor;
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.4f);

        if (facingDirection != Vector3.zero)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawRay(transform.position, facingDirection.normalized * 0.8f);
        }
    }
}