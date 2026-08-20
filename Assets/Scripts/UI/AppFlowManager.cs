using UnityEngine;
using UnityEngine.SceneManagement;

public class AppFlowManager : MonoBehaviour
{
    private const string SCENE_MATCH_NAME = "SceneMatch";
    private const string SCENE_LOGIN_NAME = "LoginScene";
    public static AppFlowManager Instance { get; private set; }

    private void Start()
    {
        if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            if (AuthManager.Instance != null)
            {
                AuthManager.Instance.TryAutoLogin();
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

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            // Dedicated Server start ở SceneMatch
            SceneManager.LoadScene(SCENE_MATCH_NAME);
            return;
        }
    }
    private void OnEnable()
    {
        AuthManager.OnLoginSuccess += LoadFindMatchScene;
        AuthManager.OnLogoutSuccess += LoadLoginScene;
    }

    private void OnDisable()
    {
        AuthManager.OnLoginSuccess -= LoadFindMatchScene;
        AuthManager.OnLogoutSuccess -= LoadLoginScene;
    }

    private void LoadFindMatchScene()
    {
        SceneManager.LoadScene(SCENE_MATCH_NAME); 
    }

    private void LoadLoginScene()
    {
        SceneManager.LoadScene(SCENE_LOGIN_NAME);
    }
}