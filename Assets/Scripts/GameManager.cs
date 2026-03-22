using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System;

public class GameManager : MonoBehaviour
{
    [SerializeField] private InputHandler _input;
    public static GameManager Instance { get; private set; }
    [Header("UI Panels")]
    [SerializeField] private GameObject _endScreen;
    [SerializeField] private string _winMessage = "Congratulations, you have achieved success in your mission!";
    [SerializeField] private string _loseMessage = "You have been caught! Better luck next time.";
    [SerializeField] private TextMeshProUGUI _endScreenText;

    [Header("End Screen Buttons (Sprite-based)")]
    [SerializeField] private GameObject _restartSpriteBtn;
    [SerializeField] private GameObject _quitSpriteBtn;


    [Header("Game Object References")]
    private bool staffRoomTreasure = false;
    private bool vipRoomTreasure = false;
    private bool officeRoomTreasure = false;

    [Header("Checklist UI")]
    [SerializeField] private TextMeshProUGUI _checklistText;
    [SerializeField] private string checkBox = "[X]";
    [SerializeField] private string uncheckBox = "[ ]";

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

        if (_endScreen != null)
            _endScreen.SetActive(false);

        if (_restartSpriteBtn != null)
            _restartSpriteBtn.SetActive(false);

        if (_quitSpriteBtn != null)
            _quitSpriteBtn.SetActive(false);

        UpdateChecklistUI();
    }

    private void Update()
    {
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
    }

    public void RestartLevel()
    {
        Time.timeScale = 1f; // Restore time scale before reloading
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void QuitGame()
    {
        Debug.Log("Game Quit requested!");
        Application.Quit();
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

    private void DisableGameplayUI()
    {
        if (_checklistText != null) _checklistText.gameObject.SetActive(false);
    }

    private void ShowEndScreen(string message)
    {
        _isGameOver = true;
        Time.timeScale = 0f;
        DisableGameplayUI();

        if (_endScreenText != null) _endScreenText.text = message;
        if (_endScreen != null) _endScreen.SetActive(true);

        if (_restartSpriteBtn != null) _restartSpriteBtn.SetActive(true);
        if (_quitSpriteBtn != null) _quitSpriteBtn.SetActive(true);
    }

    public void TriggerGameOver()
    {
        if (_isGameOver) return;
        ShowEndScreen(_loseMessage);
    }

    public void TriggerWin()
    {
        if (_isGameOver) return;
        ShowEndScreen(_winMessage);
    }
}