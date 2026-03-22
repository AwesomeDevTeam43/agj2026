using UnityEngine;

public class ShapeVisualizer : MonoBehaviour {
  private SpriteRenderer _renderer;
  public ShapeData shapeData {get ; private set;}
    
  [Tooltip("Square = 0, Circle = 1, Triangle = 2, Hexagon = 3")]
  [SerializeField] private Sprite[] _shapeSprites;

  [SerializeField] private BoxCollider2D _colliderBox;
  [SerializeField] private CircleCollider2D _colliderCircle;
  [SerializeField] private PolygonCollider2D _colliderPolygon;

  void Awake()
  {
    _renderer = GetComponent<SpriteRenderer>();
  
  }

  public void ApplyShape(ShapeData newShape)
  {
      if (newShape == null)
      {
        Debug.LogWarning("No Shape Data assigned");
        return;
      }

      shapeData = newShape;
      
      _renderer.color = shapeData.color;
      if (shapeData.type != ShapeType.Husk)
      {
      _colliderBox.enabled = false;
      _colliderCircle.enabled = false;
      _colliderPolygon.enabled = false;
      }

      switch (newShape.type)
      {
        case ShapeType.Square:
          _renderer.sprite = _shapeSprites[0];
            _colliderBox.enabled = true;
          break;
        case ShapeType.Circle:
          _renderer.sprite = _shapeSprites[1];
            _colliderCircle.enabled = true;
          break;
        case ShapeType.Triangle:
          _renderer.sprite = _shapeSprites[2];
            _colliderPolygon.enabled = true;
          break;
        case ShapeType.Hexagon:
          _renderer.sprite = _shapeSprites[3];
            _colliderCircle.enabled = true;
          break;
        case ShapeType.Husk:
          _renderer.color = shapeData.color;
          break;
        default:
          Debug.LogWarning("Unknown shape type");
          break;
      }
  }
}
