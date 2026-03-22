using UnityEngine;
using TMPro;

public class KillCounterUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI counterText;
    
    // Just for visual reference in the UI, make sure this matches your NPC settings
    [SerializeField] private int threshold = 3; 

    void Update()
    {
        if (counterText != null)
        {
            // Reads the static queue from your Test script
            int currentKills = Test.killHistory.Count;
            
            counterText.text = $"Recent Hacks: {currentKills} / {threshold}";

            // Optional: Turn text red if you are one hack away from an alarm
            if (currentKills >= threshold - 1)
                counterText.color = Color.red;
            else
                counterText.color = Color.white;
        }
    }
}