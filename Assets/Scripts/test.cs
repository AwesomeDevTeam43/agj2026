using UnityEngine;

public class Test : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;
    private ShapeVisualizer shapeVisualizer;
    [SerializeField] private ShapeData shapeData;

    void Awake()
    {
        shapeVisualizer = GetComponent<ShapeVisualizer>();
    }

    void Start()
    {
        if (shapeData != null)
        {
            shapeVisualizer.ApplyShape(shapeData);
        }
    }

    public void OnClick()
    {
        playerController.ApplyShape(shapeData);
        Debug.Log("Interact with blue square");
    }
}
