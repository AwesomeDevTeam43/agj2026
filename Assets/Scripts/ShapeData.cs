using UnityEngine;

public enum ShapeType
{
  Square,           // green
  Circle,         // red
  Triangle,       // orange
  Hexagon,    // blue
  Husk     
}

[CreateAssetMenu(fileName = "Shape_Data", menuName = "Shapes/Shape_Data", order = 0)]
public class ShapeData : ScriptableObject
{
   public ShapeType type; 
   public Color color;
   public float baseSpeed;

}
