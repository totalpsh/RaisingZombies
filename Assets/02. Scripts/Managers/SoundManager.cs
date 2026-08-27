using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class SoundManager : Singleton<SoundManager>
{
    //Todo
    // 1. 에셋 이름 SFX_OOO
    // 2. 라벨 이름 SFX_Common, SFX_StageBoss, SFX_UI 등으로 분류할 것. 
    
    private bool _initialized;

    private AudioSource _bgmSource;
    private AudioSource _uiSource;
    private readonly List<AudioSource> _sfxSource = new();
    
    private readonly Dictionary<string, AudioClip> _clipMap = new();
    private readonly Dictionary<string, AudioClip> _bgmMap = new();
    private readonly Dictionary<string, AudioClip> _sfxMap = new();
    private readonly HashSet<string> _loadedKey = new();

    private string _currentBgmKey;

    private float _bgmVolume = 1f;
    private float _uiVolume = 1f;
    private float _sfxVolume = 1f;
    private bool _bgmEnabled = true; // BGM 출력 활성 상태
    private bool _sfxEnabled = true; // SFX와 UI 효과음 출력 활성 상태

    private Dictionary<string, float> _lastPlayTime = new();
    [SerializeField] private float sameSfxCool = 0.05f;

    [SerializeField] private int sfxSourceCount = 15;

    [Header("SFX Output")]
    [SerializeField, Range(0f, 2f)] private float sfxOutputBoost = 1.6f;
    [SerializeField, Range(0f, 2f)] private float uiOutputBoost = 1.4f;
    [SerializeField, Range(0, 256)] private int bgmPriority = 160;
    [SerializeField, Range(0, 256)] private int sfxPriority = 64;
    [SerializeField, Range(0, 256)] private int uiPriority = 48;

    public bool BgmEnabled => _bgmEnabled; // 현재 BGM 출력 활성 상태
    public bool SfxEnabled => _sfxEnabled; // 현재 SFX와 UI 효과음 출력 활성 상태
    
    public void Init()
    {
        if (_initialized) return;
        _initialized = true;

        CreateAudioSource();
        if (GameSettingsManager.HasInstance) GameSettingsManager.Instance.ApplyToSoundManager(this);
        ApplyVolumes();
    }

    public async Task PreloadSfx(string labelName)
    {
        Debug.Log("PreloadSfx");
        
        var clips = await ResourceManager.Instance.LoadAllAsync<AudioClip>(labelName);

        if (clips == null) return;

        foreach (var clip in clips)
        {
            if (clip == null) continue;

            if (_sfxMap.ContainsKey(clip.name))
                continue;
            _sfxMap.Add(clip.name, clip);
        }
        
        Debug.Log(_sfxMap.Count);
    }
    
    private void CreateAudioSource()
    {
        var bgmGo = new GameObject("BGMSource");
        bgmGo.transform.SetParent(transform);
        _bgmSource = bgmGo.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.priority = bgmPriority;
        _bgmSource.spatialBlend = 0f;
        
        var uiGo = new GameObject("UISource");
        uiGo.transform.SetParent(transform);
        _uiSource = uiGo.AddComponent<AudioSource>();
        _uiSource.playOnAwake = false;
        _uiSource.loop = false;
        _uiSource.priority = uiPriority;
        _uiSource.spatialBlend = 0f;
        
        var sfxRoot = new GameObject("SFXRoot");
        sfxRoot.transform.SetParent(transform, false);

        for (int i = 0; i < sfxSourceCount; i++)
        {
            var sfxGo = new GameObject($"SFX{i}");
            sfxGo.transform.SetParent(sfxRoot.transform, false);
            
            var source = sfxGo.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.priority = sfxPriority;
            source.spatialBlend = 0f;
            
            _sfxSource.Add(source);
        }
    }

    private void ApplyVolumes()
    {
        if (_bgmSource != null) _bgmSource.volume = _bgmEnabled ? _bgmVolume : 0f;
        if (_uiSource != null) _uiSource.volume = _sfxEnabled ? _uiVolume : 0f;

        foreach (var source in _sfxSource)
        {
            if (source != null)
                source.volume = _sfxEnabled ? _sfxVolume : 0f;
        }
    }

    public async void PlayBGM(string key, bool restart = false)
    {
        if (string.IsNullOrEmpty(key)) return;

        if (!restart && _currentBgmKey == key && _bgmSource.isPlaying)
            return;
        
        AudioClip clip = await LoadBGMClip(key);

        if (clip == null)
        {
            Debug.LogError($"SoundManager PlayerBGM {key} 없음");
            return;
        }

        _currentBgmKey = key;
        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    private async Task<AudioClip> LoadBGMClip(string key)
    {
        if (_clipMap.TryGetValue(key, out var cached) && cached != null)
            return cached;

        AudioClip clip = await ResourceManager.Instance.LoadAsync<AudioClip>(key);

        if (clip != null)
            _clipMap[key] = clip;

        return clip;
    }

    public void StopBGM()
    {
        _currentBgmKey = null;
        _bgmSource.Stop();
        _bgmSource.clip = null;
    }

    public void PlaySfx(string key, float volumeScale = 1f)
    {
        
        if (string.IsNullOrEmpty(key)) return;

        if (!_sfxMap.TryGetValue(key, out var clip) || clip == null) return;

        if (_lastPlayTime.TryGetValue(key, out float lastTime))
        {
            if (Time.time - lastTime < sameSfxCool)
                return;
        }
        
        _lastPlayTime[key] = Time.time;
        
        AudioSource source = GetAvailSfxSource();
        
        if (source == null) return;

        source.PlayOneShot(clip, Mathf.Max(0f, volumeScale) * sfxOutputBoost);
    }
    
    public async void PlayUISfx(string key, float volumeScale = 1f)
    {
        if (string.IsNullOrEmpty(key)) return;

        AudioClip clip = await LoadClip(key);
        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] UI clip not found. key={key}");
            return;
        }

        if (_uiSource == null) return;

        _uiSource.PlayOneShot(clip, Mathf.Max(0f, volumeScale) * uiOutputBoost);
    }
    
    public void SetBGMVolume(float volume)
    {
        _bgmVolume = Mathf.Clamp01(volume);
        if (_bgmSource != null)
            _bgmSource.volume = _bgmEnabled ? _bgmVolume : 0f;
    }

    public void SetSFXVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);

        foreach (var source in _sfxSource)
        {
            if (source != null)
                source.volume = _sfxEnabled ? _sfxVolume : 0f;
        }

        // GameFeedbackManager.Instance?.SetSfxVolume(_sfxVolume);//////////////////////////////////////
    }
    
    public void SetUIVolume(float volume)
    {
        _uiVolume = Mathf.Clamp01(volume);
        if (_uiSource != null) _uiSource.volume = _sfxEnabled ? _uiVolume : 0f;
    }

    // BGM 출력만 활성화하거나 음소거하고 기존 볼륨 값은 보존한다.
    public void SetBGMEnabled(bool enabled)
    {
        _bgmEnabled = enabled;
        if (_bgmSource != null) _bgmSource.volume = enabled ? _bgmVolume : 0f;
    }

    // 전투 SFX와 UI 효과음을 함께 활성화하거나 음소거한다.
    public void SetSFXEnabled(bool enabled)
    {
        _sfxEnabled = enabled;
        if (_uiSource != null) _uiSource.volume = enabled ? _uiVolume : 0f;

        foreach (AudioSource source in _sfxSource) // 현재 생성된 모든 SFX 출력 Source
        {
            if (source != null) source.volume = enabled ? _sfxVolume : 0f;
        }
    }

    private AudioSource GetAvailSfxSource()
    {
        for (int i = 0; i < _sfxSource.Count; i++)
        {
            if (!_sfxSource[i].isPlaying)
                return _sfxSource[i];
        }
        
        return _sfxSource.Count > 0 ? _sfxSource[0] : null;
    }

    private async Task<AudioClip> LoadClip(string key)
    {
        if (_clipMap.TryGetValue(key, out var cached) && cached != null)
            return cached;
        
        AudioClip clip = await ResourceManager.Instance.LoadAsync<AudioClip>(key);

        if (clip != null)
            _clipMap[key] = clip;

        return clip;
    }

    // public void ApplyOption(OptionSaveData option)
    // {
    //     if (option == null) return;
    //
    //     SetBGMVolume(option.bgmVolume);
    //     SetSFXVolume(option.sfxVolume);
    //     SetUIVolume(option.sfxVolume);
    // }
}
