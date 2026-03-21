using UnityEngine;

public class Test : MonoBehaviour, IInteractable
{
    public void OnClick()
    {
        Debug.Log("Interact with blue square");
    }
}
