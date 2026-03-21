using UnityEngine;
using UnityEngine.SceneManagement;

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
    
    private string checkBox = "\udb83\udc52";
    private string uncheckBox = "\udb83\udc52";

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

        _winScreen.SetActive(false);
        _loseScreen.SetActive(false);

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

        Debug.Log($"Collected {locale} treasure! Progress: Staff={staffRoomTreasure}, VIP={vipRoomTreasure}, Office={officeRoomTreasure}");

        // Check if all three have been collected
        if (staffRoomTreasure && vipRoomTreasure && officeRoomTreasure)
        {
            TriggerWin();
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