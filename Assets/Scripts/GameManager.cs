using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

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

    private bool _isGameOver = false;

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