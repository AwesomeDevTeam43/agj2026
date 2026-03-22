using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControlsMenu : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject controlsPanel; // Reference to the Panel containing the controls info
    [SerializeField] private TextMeshProUGUI controlsText; // Reference to the Text component
    [SerializeField] private TextMeshProUGUI objectiveText; // Reference to the text component showing the current objective

    [Header("Input References")]
    [SerializeField] private InputHandler inputHandler; // Reference to the script handling inputs

    private void OnEnable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnShowControls += ToggleControls;
        }
        UpdateControlsText();
        SetPanelBlack();
    }

    private void OnDisable()
    {
        if (inputHandler != null)
        {
            inputHandler.OnShowControls -= ToggleControls;
        }
    }

    private void Start()
    {
        // Ensure the menu is hidden at start
        if (controlsPanel != null)
        {
            controlsPanel.SetActive(false);
        }
    }

    private void ToggleControls()
    {
        if (controlsPanel != null)
        {
            bool isActive = controlsPanel.activeSelf;
            controlsPanel.SetActive(!isActive);
            objectiveText.gameObject.SetActive(isActive); // Show objective text when controls are hidden, hide it when controls are shown

            // Optional: Pause time when menu is open
            // Time.timeScale = isActive ? 1f : 0f; 
        }
    }

    private void UpdateControlsText()
    {
        if (controlsText != null)
        {
            // Melhorar a formatação e alinhamento
            controlsText.alignment = TextAlignmentOptions.Center;
            
            // Usar formatação rica do TMPro para tamanhos e cores se necessário
            controlsText.text = "<size=150%><b>CONTROLS</b></size>\n\n" +
                "Move ................. WASD\n" +
                "Steal Identity ....... Left Click\n" +
                "Interact ............. Left Click\n" +
                "Throw Distraction .... Right Click\n" +
                "Drag Body ............ Hold F\n" +
                "Peek Camera .......... Hold Shift\n" +
                "Show/Hide ............ Escape";
        }
    }

    private void SetPanelBlack()
    {
        if (controlsPanel != null)
        {
            Image panelImage = controlsPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = new Color(0, 0, 0, 0.9f); // Preto quase opaco para boa leitura
            }
        }
    }
}
