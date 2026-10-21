using Game.Runtime.Core;
using Game.Runtime.Data;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Game.Runtime.Gameplay.Campaign;

public class AudioManager : Singleton<AudioManager>
{
    public const string SFXVolumeKey = "SFXVolume", MusicVolumeKey = "MusicVolume";
    [Header("音频数据")] public AudioInfoListSO audioInfoListSO;
    [Header("Audio Sources")] public AudioSource musicSource, sfxSource;
    private float musicVolume = 1, sfxVolume = 1;
    private AudioName currentMusic;

    protected override void Awake()
    {
        base.Awake();
        if (!musicSource) musicSource = gameObject.AddComponent<AudioSource>();
        if (!sfxSource || sfxSource == musicSource) sfxSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = sfxSource.playOnAwake = false;
        musicSource.spatialBlend = sfxSource.spatialBlend = 0;
        musicSource.Stop(); sfxSource.Stop(); sfxSource.loop = false;
        musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1));
        sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SFXVolumeKey, 1));
        UpdateAudioVolumes();
    }
    private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
    private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }
    private void Start() { RouteMusic(SceneManager.GetActiveScene().name); }
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { RouteMusic(scene.name); }
    private void RouteMusic(string scene)
    {
        if (scene == "Gameplay") PlayMusic(AudioName.BGM);
        else if (scene == "TitleScene" || scene == "LevelSelect") PlayMusic(AudioName.MenuBGM);
    }
    public void PlayMusic(AudioName name)
    {
        if (!musicSource) return;
        if (name == currentMusic && musicSource.isPlaying) return;
        var info = audioInfoListSO ? audioInfoListSO.GetAudioInfo(name) : null;
        if (name == AudioName.None || info == null || !info.clip)
        { musicSource.Stop(); currentMusic = AudioName.None; return; }
        currentMusic = name; musicSource.clip = info.clip; musicSource.loop = info.loop;
        UpdateAudioVolumes(); musicSource.Play();
    }
    public void PlaySFX(AudioName name)
    {
        if (!sfxSource) return;
        if (name == AudioName.None) { sfxSource.Stop(); return; }
        var info = audioInfoListSO ? audioInfoListSO.GetAudioInfo(name) : null;
        if (info == null || !info.clip) return;
        // One-shots overlap, so footsteps and clicks cannot cut off the victory cue.
        sfxSource.PlayOneShot(info.clip, info.volume);
    }
    public void SetMusicVolume(float value)
    { musicVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(MusicVolumeKey, musicVolume); UpdateAudioVolumes(); }
    public void SetSFXVolume(float value)
    { sfxVolume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(SFXVolumeKey, sfxVolume); UpdateAudioVolumes(); }
    private void UpdateAudioVolumes()
    {
        var info = audioInfoListSO ? audioInfoListSO.GetAudioInfo(currentMusic) : null;
        if (musicSource) musicSource.volume = musicVolume * (info != null ? info.volume : 1);
        if (sfxSource) sfxSource.volume = sfxVolume;
    }
    public static void Emit(AudioName name) { FindObjectOfType<AudioManager>()?.PlaySFX(name); }
    public static void AttachUI(GameObject root)
    {
        foreach (var button in root.GetComponentsInChildren<Button>(true))
            if (!button.GetComponent<UISoundFeedback>()) button.gameObject.AddComponent<UISoundFeedback>();
    }
}
