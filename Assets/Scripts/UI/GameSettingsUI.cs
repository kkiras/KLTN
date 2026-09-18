using UnityEngine;
using UnityEngine.UI;

public class GameSettingsUI : MonoBehaviour
{
    [Header("Settings UI")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioSource sfxAudioSource;

    private const string MusicVolumeKey = "MusicVolume";
    private const string SFXVolumeKey = "SFXVolume";

    private void Start()
    {
        // Đọc âm lượng đã lưu
        float savedMusicVolume =
            PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f);

        float savedSFXVolume =
            PlayerPrefs.GetFloat(SFXVolumeKey, 0.7f);

        // Cập nhật vị trí slider
        musicSlider.value = savedMusicVolume;
        sfxSlider.value = savedSFXVolume;

        // Áp dụng âm lượng ngay khi vào trận
        musicAudioSource.volume = savedMusicVolume;
        sfxAudioSource.volume = savedSFXVolume;
    }

    public void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void SetMusicVolume(float volume)
    {
        musicAudioSource.volume = volume;

        PlayerPrefs.SetFloat(MusicVolumeKey, volume);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float volume)
    {
        sfxAudioSource.volume = volume;

        PlayerPrefs.SetFloat(SFXVolumeKey, volume);
        PlayerPrefs.Save();
    }
}