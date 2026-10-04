using UnityEngine;

public class AudioManager : MonoBehaviour
{
    internal static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _musicEnabled = PlayerPrefs.GetInt(PrefKeyMusic, 1) == 1;
        _sfxEnabled   = PlayerPrefs.GetInt(PrefKeysfx,   1) == 1;
        _musicVolume  = PlayerPrefs.GetFloat(PrefKeyMusicVol, 0.5f);
        _sfxVolume    = PlayerPrefs.GetFloat(PrefKeySfxVol,   1.0f);

        EnsureGoldMineSources();
        ApplyMusicVolume();
        ApplySfxVolume();
    }

    private const string PrefKeyMusic    = "audio_music_enabled";
    private const string PrefKeysfx      = "audio_sfx_enabled";
    private const string PrefKeyMusicVol = "audio_music_volume";
    private const string PrefKeySfxVol   = "audio_sfx_volume";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgMusicSource;
    [SerializeField] private AudioSource uiSource;
    [SerializeField] private AudioSource wheelSegmentSource;
    [SerializeField] private AudioSource reserveSource;

    [Header("Audio Clips")]
    [SerializeField] private AudioClip clipGameMainBg;
    [SerializeField] private AudioClip clipBetPlusMinus;
    [SerializeField] private AudioClip clipMaxBetReached;
    [SerializeField] private AudioClip clipPrimaryActionButton;
    [SerializeField] private AudioClip clipGeneralButtonClick;
    [SerializeField] private AudioClip clipPopupOpenClose;
    [SerializeField] private AudioClip clipAutoplayPanelOpen;
    [SerializeField] private AudioClip clipWheelSegmentTick;
    [SerializeField] private AudioClip clipReelStop;

    [Header("Gold Mine Mania Clips")]
    [SerializeField] private AudioClip clipGoldMineBigWin;
    [SerializeField] private AudioClip clipGoldMineBoots;
    [SerializeField] private AudioClip clipGoldMineDonkeyIconInSlot;
    [SerializeField] private AudioClip clipGoldMineReelSpinning;
    [SerializeField] private AudioClip clipGoldMineSmallWin;
    [SerializeField] private AudioClip clipGoldMineTension;
    [SerializeField] private AudioClip clipGoldMineTrainFreeSpins;
    [SerializeField] private AudioClip clipGoldMineTrainIconInSlot;
    [SerializeField] private AudioClip clipGoldMineTrainTransition;
    [SerializeField] private AudioClip clipGoldMineWildIcon;
    [SerializeField] private AudioClip clipGoldMineBonusBg;
    [SerializeField] private AudioClip clipGoldMineRespinBg;
    [SerializeField] private AudioClip clipGoldMineFreeSpinStartPanel;
    [SerializeField] private AudioClip clipGoldMineExplosionBlast;
    [SerializeField] private AudioClip clipGoldMineManWithTrain;
    [SerializeField] private AudioClip clipGoldMineSparkle;
    [SerializeField] private AudioClip clipGoldMineGoldGoingUpward;
    [SerializeField] private AudioClip clipGoldMineRespinTrigger;
    [SerializeField] private AudioClip clipGoldMineBarrelsMerge;
    [SerializeField] private AudioClip clipGoldMineSlotTrainConversion;
    [SerializeField] private AudioClip clipGoldMineTrolleyMan;

    private AudioSource goldMineReelSpinSource;
    private AudioSource goldMineTensionSource;
    private AudioSource goldMinePresentationSource;
    private AudioSource goldMineWinSource;

    private bool _musicEnabled = true;
    private bool _sfxEnabled   = true;
    private float _musicVolume = 0.5f;
    private float _sfxVolume   = 1.0f;

    internal bool MusicEnabled => _musicEnabled;
    internal bool SfxEnabled   => _sfxEnabled;
    internal float MusicVolume => _musicVolume;
    internal float SfxVolume   => _sfxVolume;

    internal void SetMusicEnabled(bool on)
    {
        _musicEnabled = on;
        PlayerPrefs.SetInt(PrefKeyMusic, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxEnabled(bool on)
    {
        _sfxEnabled = on;
        PlayerPrefs.SetInt(PrefKeysfx, on ? 1 : 0);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    internal void SetMusicVolume(float volume)
    {
        _musicVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeyMusicVol, _musicVolume);
        PlayerPrefs.Save();
        ApplyMusicVolume();
    }

    internal void SetSfxVolume(float volume)
    {
        _sfxVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(PrefKeySfxVol, _sfxVolume);
        PlayerPrefs.Save();
        ApplySfxVolume();
    }

    private void ApplyMusicVolume()
    {
        if (bgMusicSource == null) return;
        bgMusicSource.volume = _musicEnabled ? _musicVolume : 0f;
    }

    private void ApplySfxVolume()
    {
        float v = _sfxEnabled ? _sfxVolume : 0f;
        if (uiSource           != null) uiSource.volume           = v;
        if (wheelSegmentSource != null) wheelSegmentSource.volume = v;
        if (reserveSource      != null) reserveSource.volume      = v;
        if (goldMineReelSpinSource != null) goldMineReelSpinSource.volume = v;
        if (goldMineTensionSource != null) goldMineTensionSource.volume = v;
        if (goldMinePresentationSource != null) goldMinePresentationSource.volume = v;
        if (goldMineWinSource != null) goldMineWinSource.volume = v;
    }

    /// <summary>
    /// Uses UI source (AudioSource 2). If busy/playing, falls back to reserve source (AudioSource 4).
    /// </summary>
    private void PlayUISound(AudioClip clip, float volumeScale = 1f)
    {
        if (!_sfxEnabled || clip == null) return;

        volumeScale = Mathf.Clamp01(volumeScale);

        if (uiSource != null && !uiSource.isPlaying)
        {
            uiSource.PlayOneShot(clip, volumeScale);
        }
        else if (reserveSource != null)
        {
            reserveSource.PlayOneShot(clip, volumeScale);
        }
        else if (uiSource != null)
        {
            uiSource.PlayOneShot(clip, volumeScale);
        }
    }

    private void PlayLoop(AudioSource source, AudioClip clip)
    {
        if (source == null || clip == null) return;
        source.clip   = clip;
        source.loop   = true;
        source.volume = _musicEnabled ? _musicVolume : 0f;
        source.Play();
    }

    private void StopSource(AudioSource source)
    {
        if (source == null) return;
        source.Stop();
        source.loop = false;
    }

    private void EnsureGoldMineSources()
    {
        goldMineReelSpinSource = EnsureRuntimeSfxSource(
            goldMineReelSpinSource,
            "GoldMine Reel Spin Audio");
        goldMineTensionSource = EnsureRuntimeSfxSource(
            goldMineTensionSource,
            "GoldMine Tension Audio");
        goldMinePresentationSource = EnsureRuntimeSfxSource(
            goldMinePresentationSource,
            "GoldMine Presentation Audio");
        goldMineWinSource = EnsureRuntimeSfxSource(
            goldMineWinSource,
            "GoldMine Win Audio");
    }

    private AudioSource EnsureRuntimeSfxSource(
        AudioSource source,
        string childName)
    {
        if (source != null) return source;

        Transform existing = transform.Find(childName);
        GameObject sourceObject = existing != null
            ? existing.gameObject
            : new GameObject(childName);
        if (existing == null)
        {
            sourceObject.transform.SetParent(transform, false);
        }

        source = sourceObject.GetComponent<AudioSource>();
        if (source == null)
        {
            source = sourceObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = _sfxEnabled ? _sfxVolume : 0f;
        return source;
    }

    private void PlayControlledSfx(
        AudioSource source,
        AudioClip clip,
        bool loop)
    {
        if (!_sfxEnabled || source == null || clip == null) return;

        source.Stop();
        source.clip = clip;
        source.loop = loop;
        source.volume = _sfxVolume;
        source.Play();
    }

    private static void StopControlledSfx(
        AudioSource source,
        AudioClip expectedClip)
    {
        if (source == null || source.clip != expectedClip) return;

        source.Stop();
        source.loop = false;
        source.clip = null;
    }

    // 1. Game Main BG
    internal void PlayBgMusic()
    {
        if (bgMusicSource == null || clipGameMainBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipGameMainBg) return;

        bgMusicSource.clip   = clipGameMainBg;
        bgMusicSource.loop   = true;
        bgMusicSource.volume = _musicEnabled ? _musicVolume : 0f;
        bgMusicSource.Play();
    }

    internal void PlayMainBg() => PlayBgMusic();

    internal void StopBgMusic()
    {
        StopSource(bgMusicSource);
    }

    // 2. Bet Plus / Bet Minus (one for both)
    internal void PlayBetPlusMinus()
    {
        PlayUISound(clipBetPlusMinus);
    }

    internal void PlayBetPlus()  => PlayBetPlusMinus();
    internal void PlayBetMinus() => PlayBetPlusMinus();

    // 3. Max Bet Reached
    internal void PlayMaxBetReached()
    {
        PlayUISound(clipMaxBetReached);
    }

    // Spin / Stop / Take / AutoplayStop / WheelStart Btn Sound
    internal void PlayPrimaryActionButton()
    {
        PlayUISound(clipPrimaryActionButton != null ? clipPrimaryActionButton : clipGeneralButtonClick);
    }

    internal void PlaySpinStart()    => PlayPrimaryActionButton();
    internal void PlaySpinStop()     => PlayPrimaryActionButton();
    internal void PlayTakeButton()   => PlayPrimaryActionButton();
    internal void PlayAutoplayStop() => PlayPrimaryActionButton();
    internal void PlayWheelStart()   => PlayPrimaryActionButton();

    // General Button Click
    internal void PlayButton()
    {
        PlayUISound(clipGeneralButtonClick);
    }

    internal void PlayGeneralButtonClick() => PlayButton();

    // Popup Open Close Sound
    internal void PlayPopupOpenClose()
    {
        PlayUISound(clipPopupOpenClose != null ? clipPopupOpenClose : clipGeneralButtonClick);
    }

    internal void PlayPopupClose() => PlayPopupOpenClose();
    internal void PlayPopupOpen()  => PlayPopupOpenClose();

    // Autoplay Panel Open Sound
    internal void PlayAutoplayPanelOpen()
    {
        PlayUISound(clipAutoplayPanelOpen != null ? clipAutoplayPanelOpen : clipPopupOpenClose);
    }

    // Bonus Wheel Spin Segment Tick
    internal void PlayWheelSegmentTick()
    {
        if (!_sfxEnabled || clipWheelSegmentTick == null) return;
        if (wheelSegmentSource != null)
        {
            wheelSegmentSource.PlayOneShot(clipWheelSegmentTick);
        }
        else
        {
            PlayUISound(clipWheelSegmentTick);
        }
    }

    // Slot Reel Column Stop Sound
    internal void PlayReelStop()
    {
        if (!_sfxEnabled || clipReelStop == null) return;

        if (wheelSegmentSource != null)
            wheelSegmentSource.PlayOneShot(clipReelStop);
        else
            PlayUISound(clipReelStop);
    }

    internal void PlayGoldMineReelSpinning()
    {
        PlayControlledSfx(
            goldMineReelSpinSource,
            clipGoldMineReelSpinning,
            true);
    }

    internal void StopGoldMineReelSpinning()
    {
        StopControlledSfx(
            goldMineReelSpinSource,
            clipGoldMineReelSpinning);
    }

    internal void PlayGoldMineTension()
    {
        PlayControlledSfx(
            goldMineTensionSource,
            clipGoldMineTension,
            true);
    }

    internal void StopGoldMineTension()
    {
        StopControlledSfx(goldMineTensionSource, clipGoldMineTension);
    }

    internal void PlayGoldMineTrainFreeSpins()
    {
        PlayControlledSfx(
            goldMinePresentationSource,
            clipGoldMineTrainFreeSpins,
            false);
    }

    internal void StopGoldMineTrainFreeSpins()
    {
        StopControlledSfx(
            goldMinePresentationSource,
            clipGoldMineTrainFreeSpins);
    }

    internal void PlayGoldMineTrainTransition()
    {
        PlayControlledSfx(
            goldMinePresentationSource,
            clipGoldMineTrainTransition,
            false);
    }

    internal void StopGoldMineTrainTransition()
    {
        StopControlledSfx(
            goldMinePresentationSource,
            clipGoldMineTrainTransition);
    }

    internal void PlayGoldMineFreeSpinStartPanel()
    {
        PlayControlledSfx(
            goldMinePresentationSource,
            clipGoldMineFreeSpinStartPanel,
            false);
    }

    internal void StopGoldMineFreeSpinStartPanel()
    {
        StopControlledSfx(
            goldMinePresentationSource,
            clipGoldMineFreeSpinStartPanel);
    }

    internal void PlayGoldMineTrainIcon()
    {
        PlayUISound(clipGoldMineTrainIconInSlot);
    }

    internal void PlayGoldMineResultWin(
        bool useWildSound,
        bool useDonkeySound,
        bool useBootsSound)
    {
        AudioClip clip = useWildSound
            ? clipGoldMineWildIcon
            : useDonkeySound
                ? clipGoldMineDonkeyIconInSlot
                : useBootsSound
                    ? clipGoldMineBoots
                    : clipGoldMineSmallWin;
        PlayControlledSfx(goldMineWinSource, clip, false);
    }

    internal void PlayGoldMineBigWin()
    {
        PlayControlledSfx(
            goldMineWinSource,
            clipGoldMineBigWin,
            false);
    }

    internal void StopGoldMineWin()
    {
        if (goldMineWinSource == null) return;

        goldMineWinSource.Stop();
        goldMineWinSource.loop = false;
        goldMineWinSource.clip = null;
    }

    internal void PlayGoldMineBonusBg()
    {
        if (bgMusicSource == null || clipGoldMineBonusBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipGoldMineBonusBg) return;

        PlayLoop(bgMusicSource, clipGoldMineBonusBg);
    }

    internal void PlayGoldMineRespinBg()
    {
        if (bgMusicSource == null || clipGoldMineRespinBg == null) return;
        if (bgMusicSource.isPlaying && bgMusicSource.clip == clipGoldMineRespinBg) return;

        PlayLoop(bgMusicSource, clipGoldMineRespinBg);
    }

    internal void StopGoldMineRespinBg()
    {
        if (bgMusicSource == null || bgMusicSource.clip != clipGoldMineRespinBg) return;

        StopSource(bgMusicSource);
        bgMusicSource.clip = null;
    }

    internal void PlayGoldMineExplosionBlast()
    {
        PlayUISound(clipGoldMineExplosionBlast, 0.7f);
    }

    internal void PlayGoldMineManWithTrain()
    {
        PlayUISound(clipGoldMineManWithTrain);
    }

    internal void PlayGoldMineSparkle()
    {
        PlayUISound(clipGoldMineSparkle);
    }

    internal void PlayGoldMineGoldGoingUpward()
    {
        PlayUISound(clipGoldMineGoldGoingUpward);
    }

    internal void PlayGoldMineRespinTrigger()
    {
        PlayUISound(clipGoldMineRespinTrigger);
    }

    internal void PlayGoldMineBarrelsMerge()
    {
        PlayUISound(clipGoldMineBarrelsMerge);
    }

    internal void PlayGoldMineSlotTrainConversion()
    {
        PlayUISound(clipGoldMineSlotTrainConversion);
    }

    internal void PlayGoldMineTrolleyMan()
    {
        PlayUISound(clipGoldMineTrolleyMan);
    }

    internal void PlayGoldMineManDonkeyExit()
    {
        PlayUISound(clipAutoplayPanelOpen);
    }

    private bool isForceMuted = false;

    internal void SetMuteAll(bool forceMute)
    {
        if (forceMute == isForceMuted) return;
        isForceMuted = forceMute;

        AudioListener.volume = forceMute ? 0f : 1f;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        SetMuteAll(!hasFocus);
    }

    private void OnApplicationPause(bool isPaused)
    {
        SetMuteAll(isPaused);
    }
}
