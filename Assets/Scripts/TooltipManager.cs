using UnityEngine;
using TMPro;


public class TooltipManager : MonoBehaviour
{
    public static TooltipManager Instance { get; private set; }

    [SerializeField] private GameObject tooltipUI;
    [SerializeField] private TMP_Text tooltipText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        tooltipUI.SetActive(false);

        tooltipText = tooltipUI.GetComponentInChildren<TMP_Text>();
    }

    public void ShowTooltip(string text, Vector3 position)
    {
        if (tooltipUI == null) return;
        
        tooltipUI.SetActive(true);
        tooltipText.text = text;
        Vector2 screenPos = Camera.main.WorldToScreenPoint(position);
        tooltipUI.transform.position = screenPos;
    } 

    public void HideTooltip()
    {
        if (tooltipUI != null)
        {
            tooltipUI.SetActive(false);
        }
    }
}