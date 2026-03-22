using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    [SerializeField] private InputHandler _input;
    public static GameManager Instance { get; private set; }
    [Header("UI Panels")]
    [SerializeField] private GameObject _winScreen;
    [SerializeField] private GameObject _loseScreen;

    [Header("Game Object References")]
    private bool staffRoomTreasure = false;
    private bool vipRoomTreasure = false;
    private bool officeRoomTreasure = false;
    
    [Header("Checklist UI")]
    [SerializeField] private TextMeshProUGUI _checklistText;
    [SerializeField] private string checkBox = "[X]";
    [SerializeField] private string uncheckBox = "[ ]";

    [Header("Suspicion Bar")]
    [SerializeField] private Slider _suspicionBar;
    [SerializeField] private TextMeshProUGUI _suspicionText;

    private bool _isGameOver = false;

    public void GetPlayerActionObservers(System.Collections.Generic.List<ISeesPlayerActions> observers)
    {
        // Find all NPCs in the scene that implement ISeesPlayerActions and add them to the list
        ControllerNPC[] npcs = FindObjectsOfType<ControllerNPC>();
        foreach (var npc in npcs)
        {
            if (npc is ISeesPlayerActions observer)
            {
                observers.Add(observer);
            }
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Time.timeScale = 1f;
        _isGameOver = false;

        //_winScreen.SetActive(false);
        //_loseScreen.SetActive(false);

        UpdateChecklistUI();
    }

    private void Update()
    {
        if (_isGameOver && _input.DragInput)
            RestartLevel();

        UpdateSuspicionBar();
    }

    private void UpdateSuspicionBar()
    {
        float maxSuspicion = 0f;
        SuspicionDetector[] detectors = FindObjectsByType<SuspicionDetector>(FindObjectsSortMode.None);
        foreach (var detector in detectors)
        {
            if (detector.SuspicionNormalized > maxSuspicion)
            {
                maxSuspicion = detector.SuspicionNormalized;
            }
        }
        
        // Use the normalized maximum suspicion value (0 to 1)
        if (_suspicionBar != null)
        {
            _suspicionBar.value = maxSuspicion;
        }

        if (_suspicionText != null)
        {
            _suspicionText.text = $"{Mathf.RoundToInt(maxSuspicion * 100f)}%";
        }
    }

    public void RestartLevel()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void CollectTreasure(Teasure.RoomLocale locale)
    {
        switch (locale)
        {
            case Teasure.RoomLocale.StaffRoom: 
                staffRoomTreasure = true; 
                break;
            case Teasure.RoomLocale.VipRoom: 
                vipRoomTreasure = true; 
                break;
            case Teasure.RoomLocale.OfficeRoom: 
                officeRoomTreasure = true; 
                break;
        }

        UpdateChecklistUI();
        Debug.Log($"Collected {locale} treasure! Progress: Staff={staffRoomTreasure}, VIP={vipRoomTreasure}, Office={officeRoomTreasure}");

        // Check if all three have been collected
        if (staffRoomTreasure && vipRoomTreasure && officeRoomTreasure)
        {
            TriggerWin();
        }
    }

    private void UpdateChecklistUI()
    {
        if (_checklistText != null)
        {
            string staffStr = $"{(staffRoomTreasure ? checkBox : uncheckBox)} Staff Room Treasure\n";
            string vipStr = $"{(vipRoomTreasure ? checkBox : uncheckBox)} VIP Room Treasure\n";
            string officeStr = $"{(officeRoomTreasure ? checkBox : uncheckBox)} Office Room Treasure";
            
            _checklistText.text = staffStr + vipStr + officeStr;
        }
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;

        _isGameOver = true;
        Time.timeScale = 0f;
        _loseScreen.SetActive(true);
    }

    public void TriggerWin()
    {
        if (_isGameOver) return;

        _isGameOver = true;
        Time.timeScale = 0f;
        _winScreen.SetActive(true);
    }
}