using UnityEngine;

public enum ShapeType
{
  Square,         // worker
  Circle,         // normal npc
  Triangle,       // vip
  Hexagon,        // security
  Husk            
}

[CreateAssetMenu(fileName = "Shape_Data", menuName = "Shapes/Shape_Data", order = 0)]
public class ShapeData : ScriptableObject
{
  public ShapeType type;
  public Color color;
  public float baseSpeed;

}
