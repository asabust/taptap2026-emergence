// AudioManager.cs

using Game.Runtime.Core;
using Game.Runtime.Data;
using UnityEngine;

public class AudioManager : Singleton<AudioManager>
{
    public const string SFXVolumeKey = "SFXVolume";
    public const string MusicVolumeKey = "MusicVolume";

    [Header("音频数据")]
    public AudioInfoListSO audioInfoListSO;

    [Header("Audio Sources")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    // 音量设置
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;

    private AudioName currentMusic;
    private AudioName currentSFX;

    void Start()
    {
        LoadAudioSettings();
    }

    void OnSceneLoaded(string sceneName)
    {
        PlayMusic(GameManager.Instance.IsGameplay ? AudioName.BGM : AudioName.None);
    }

    public void PlayMusic(AudioName name)
    {
        if (name == AudioName.None)
        {
            musicSource.Stop();
            currentMusic = AudioName.None;
            return;
        }

        if (name == currentMusic)
        {
            return;
        }

        currentMusic = name;
        AudioInf musicInfo = audioInfoListSO.GetAudioInfo(name);
        musicSource.clip = musicInfo.clip;
        musicSource.volume = musicInfo.volume * _musicVolume;
        musicSource.loop = musicInfo.loop;
        musicSource.Play();
    }


    public void PlaySFX(AudioName name)
    {
        if (name == AudioName.None)
        {
            sfxSource.Stop();
            return;
        }

        if (sfxSource.isPlaying && currentSFX == name)
        {
            return;
        }

        currentSFX = name;
        AudioInf audioInf = audioInfoListSO.GetAudioInfo(name);
        sfxSource.clip = audioInf.clip;
        sfxSource.volume = audioInf.volume * _sfxVolume;
        sfxSource.loop = audioInf.loop;
        sfxSource.Play();
        //sfxSource.PlayOneShot(audioInf.clip, audioInf.volume * _sfxVolume);
    }

    public void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        UpdateAudioVolumes();
        PlayerPrefs.SetFloat(MusicVolumeKey, _musicVolume);
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        UpdateAudioVolumes();
        PlayerPrefs.SetFloat(SFXVolumeKey, _sfxVolume);
    }

    private void UpdateAudioVolumes()
    {
        //Debug.Log($"Music Volume: {_musicVolume} | sfxSource:{_sfxVolume}");
        if (musicSource != null)
            musicSource.volume = _musicVolume;

        if (sfxSource != null)
            sfxSource.volume = _sfxVolume;
    }

    private void LoadAudioSettings()
    {
        _musicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 1f);
        _sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);

        UpdateAudioVolumes();
    }

    private void OnEnable()
    {
        EventHandler.AfterSceneLoadEvent += OnSceneLoaded;
    }

    private void OnDisable()
    {
        EventHandler.AfterSceneLoadEvent -= OnSceneLoaded;
    }
}