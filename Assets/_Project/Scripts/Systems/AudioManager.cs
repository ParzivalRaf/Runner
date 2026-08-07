using UnityEngine;

/// <summary>
/// Весь звук игры: музыка на отдельном источнике и короткие эффекты
/// на маленьком пуле AudioSource.
///
/// Почему пул, а не PlayOneShot на одном источнике: на подборе серии монет
/// звуки накладываются, и одного источника не хватает — новый обрывал бы
/// предыдущий. Пять голосов перекрывают любую реальную ситуацию.
///
/// AudioSource не зависит от Time.timeScale, поэтому на паузе музыку
/// приходится останавливать вручную — сама она не замолчит.
///
/// Куда вешать: на объект GameManager.
/// В инспекторе: перетащить клипы из Assets/_Project/Audio.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Музыка")]
    [SerializeField] private AudioClip musicClip;
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.30f;

    [Header("Звуки")]
    [SerializeField] private AudioClip coinClip;
    [SerializeField] private AudioClip powerUpClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip slideClip;
    [SerializeField] private AudioClip crashClip;
    [SerializeField] private AudioClip buttonClip;
    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 0.65f;

    [Header("Пул голосов")]
    [Tooltip("Сколько коротких звуков может звучать одновременно.")]
    [SerializeField] private int voiceCount = 5;

    [Header("Серия монет")]
    [Tooltip("На сколько полутонов растёт звук за каждую монету подряд.")]
    [SerializeField] private float coinPitchStep = 0.06f;

    [Tooltip("Максимальный множитель высоты звука монеты.")]
    [SerializeField] private float coinPitchMax = 1.7f;

    [Tooltip("Пауза, после которой серия монет считается прерванной, секунды.")]
    [SerializeField] private float coinStreakTimeout = 0.7f;

    private AudioSource _music;
    private AudioSource[] _voices;
    private int _nextVoice;

    private int _coinStreak;
    private float _lastCoinTime = -99f;

    private void Awake()
    {
        Instance = this;

        _music = gameObject.AddComponent<AudioSource>();
        _music.clip = musicClip;
        _music.loop = true;
        _music.playOnAwake = false;
        _music.volume = musicVolume;
        _music.spatialBlend = 0f;   // 2D: громкость не зависит от положения

        _voices = new AudioSource[Mathf.Max(1, voiceCount)];

        for (int i = 0; i < _voices.Length; i++)
        {
            AudioSource voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
            _voices[i] = voice;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (GameManager.Instance == null) return;

        GameManager.Instance.OnStateChanged -= HandleStateChanged;
        GameManager.Instance.OnRunStarted -= HandleRunStarted;
        GameManager.Instance.OnGameOver -= HandleGameOver;
    }

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStateChanged += HandleStateChanged;
            GameManager.Instance.OnRunStarted += HandleRunStarted;
            GameManager.Instance.OnGameOver += HandleGameOver;
        }

        ApplySettings();
    }

    // ------------------------------------------------------------- настройки

    /// <summary>
    /// Перечитать галочки из сейва. Вызывает UIManager, когда игрок
    /// переключил музыку или звук в настройках.
    /// </summary>
    public void ApplySettings()
    {
        SaveData data = SaveSystem.Data;

        _music.volume = musicVolume;

        if (!data.musicEnabled)
        {
            _music.Stop();
        }
        else if (!_music.isPlaying && musicClip != null)
        {
            _music.clip = musicClip;
            _music.Play();
        }
    }

    private bool SoundOn => SaveSystem.Data.soundEnabled;

    // ---------------------------------------------------------------- события

    private void HandleStateChanged(GameState state)
    {
        if (!SaveSystem.Data.musicEnabled || musicClip == null) return;

        // На паузе музыка молчит: AudioSource живёт вне Time.timeScale
        // и иначе продолжал бы играть под остановленной игрой.
        if (state == GameState.Paused) _music.Pause();
        else if (!_music.isPlaying) _music.UnPause();
    }

    private void HandleRunStarted()
    {
        _coinStreak = 0;
        _lastCoinTime = -99f;
    }

    private void HandleGameOver() => PlayCrash();

    // ----------------------------------------------------------------- звуки

    /// <summary>
    /// Монета. Каждая следующая в серии звучит выше — стандартный приём
    /// раннеров: игрок слышит, что собирает цепочку, а не отдельные монеты.
    /// </summary>
    public void PlayCoin()
    {
        if (Time.unscaledTime - _lastCoinTime > coinStreakTimeout) _coinStreak = 0;
        else _coinStreak++;

        _lastCoinTime = Time.unscaledTime;

        float pitch = Mathf.Min(coinPitchMax, 1f + _coinStreak * coinPitchStep);
        Play(coinClip, pitch);
    }

    public void PlayPowerUp() => Play(powerUpClip, 1f);
    public void PlayJump() => Play(jumpClip, Random.Range(0.96f, 1.04f));
    public void PlaySlide() => Play(slideClip, Random.Range(0.96f, 1.04f));
    public void PlayCrash() => Play(crashClip, 1f);
    public void PlayButton() => Play(buttonClip, 1f);

    private void Play(AudioClip clip, float pitch)
    {
        if (clip == null || !SoundOn) return;

        AudioSource voice = _voices[_nextVoice];
        _nextVoice = (_nextVoice + 1) % _voices.Length;

        voice.pitch = pitch;
        voice.PlayOneShot(clip, sfxVolume);
    }
}
