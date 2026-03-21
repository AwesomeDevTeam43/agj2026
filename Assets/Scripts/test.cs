using UnityEngine;

public class Test : MonoBehaviour, IInteractable
{
    [SerializeField] private ShapeVisualizer playerController;
    [SerializeField] private ShapeData shapeData;

    void Awake()
    {
        
    }

    public void OnClick()
    {
        playerController.ApplyShape(shapeData);
        Debug.Log("Interact with blue square");
    }
}
