using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(AudioSource))]
public class MenuMusicManager : MonoBehaviour
{
    private static MenuMusicManager instance;
    private AudioSource musicSource;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = GetComponent<AudioSource>();
        musicSource.loop = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "GameScene")
        {
            musicSource.Stop();
        }
        else if (scene.name == "MainMenu" ||
                 scene.name.StartsWith("HostClient"))
        {
            if (!musicSource.isPlaying)
            {
                musicSource.Play();
            }
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            instance = null;
        }
    }
}