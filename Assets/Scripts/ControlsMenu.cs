using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ControlsMenu : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private TextMeshProUGUI objectiveText;

    [Header("Conteúdo")]
    [SerializeField] private GameObject controlsText;   // o Text (TMP) com os controls
    [SerializeField] private GameObject mapImage;       // o Image com o mapa

    [Header("Botões")]
    [SerializeField] private Button controlsTabButton;
    [SerializeField] private Button mapTabButton;

    [Header("Tab Colors")]
    [SerializeField] private Color tabActiveColor   = new Color(1f, 1f, 1f, 0.4f);
    [SerializeField] private Color tabInactiveColor = new Color(0.3f, 0.3f, 0.3f, 0.3f);

    [Header("Input")]
    [SerializeField] private InputHandler inputHandler;

    // ──────────────────────────────────────────────────────────────────────────

    private void OnEnable()
    {
        if (inputHandler != null)
            inputHandler.OnShowControls += TogglePanel;
    }

    private void OnDisable()
    {
        if (inputHandler != null)
            inputHandler.OnShowControls -= TogglePanel;
    }

    private void Start()
    {
        if (controlsPanel != null)
            controlsPanel.SetActive(false);

        // Garante estado inicial correto
        ShowControls();
    }

    // ──────────────────────────────────────────────────────────────────────────

    private void TogglePanel()
    {
        if (controlsPanel == null) return;

        bool isActive = controlsPanel.activeSelf;
        controlsPanel.SetActive(!isActive);

        // Pausa o jogo quando o menu está aberto
        Time.timeScale = isActive ? 1f : 0f;

        if (objectiveText != null)
            objectiveText.gameObject.SetActive(isActive);

        if (!isActive)
            ShowControls();
    }

    // ── Públicos — liga no Inspector dos botões ───────────────────────────────

    public void ShowControls()
    {
        if (controlsText != null) controlsText.SetActive(true);
        if (mapImage != null)     mapImage.SetActive(false);

        SetTabButtonColor(controlsTabButton, true);
        SetTabButtonColor(mapTabButton,      false);
    }

    public void ShowMap()
    {
        if (controlsText != null) controlsText.SetActive(false);
        if (mapImage != null)     mapImage.SetActive(true);

        SetTabButtonColor(controlsTabButton, false);
        SetTabButtonColor(mapTabButton,      true);
    }

    // ──────────────────────────────────────────────────────────────────────────

    private void SetTabButtonColor(Button btn, bool isActive)
    {
        if (btn == null) return;
        Image img = btn.GetComponent<Image>();
        if (img != null)
            img.color = isActive ? tabActiveColor : tabInactiveColor;
    }
}