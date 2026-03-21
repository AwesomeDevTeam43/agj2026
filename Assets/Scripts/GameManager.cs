using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [SerializeField] private InputHandler _input;
    public static GameManager Instance { get; private set; }
    [Header("UI Panels")]
    [SerializeField] private GameObject _winScreen;
    [SerializeField] private GameObject _loseScreen;

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