using UnityEngine;

public class GameButton : MonoBehaviour, IInteractable
{
    public enum ButtonType
    {
        Restart,
        Quit
    }

    public ButtonType buttonType;

    public void OnClick()
    {
        if (GameManager.Instance == null) return;

        if (buttonType == ButtonType.Restart)
        {
            GameManager.Instance.RestartLevel();
        }
        else if (buttonType == ButtonType.Quit)
        {
            GameManager.Instance.QuitGame();
        }
    }
}
