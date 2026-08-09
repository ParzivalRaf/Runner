using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Крутит постобработку прямо во время забега: под кофе кадр ведёт и рвёт
/// по цветам, двойные очки уводят картинку в золото, а на максимальной
/// скорости слегка сжимаются углы.
///
/// Это то, ради чего вообще стоило настраивать Volume: статичный профиль
/// делает картинку красивой один раз, а реагирующий — превращает бонус
/// из строчки в HUD в ощущение.
///
/// Куда вешать: на объект GameManager.
/// Ссылку на Global Volume можно не проставлять — найдёт сам.
/// </summary>
public class ScreenEffects : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private Volume globalVolume;
    [SerializeField] private PlayerController player;

    [Header("Кофе")]
    [Tooltip("Сила расслоения цветов по краям кадра.")]
    [SerializeField] private float coffeeChromatic = 0.7f;

    [Tooltip("Искажение линзы. Отрицательное — кадр как бы засасывает вперёд.")]
    [SerializeField] private float coffeeDistortion = -0.28f;

    [SerializeField] private float coffeeExtraVignette = 0.14f;
    [SerializeField] private float coffeeExtraSaturation = 18f;

    [Header("Двойные очки")]
    [SerializeField] private Color doubleScoreFilter = new Color(1f, 0.88f, 0.55f);

    [Header("Скорость")]
    [Tooltip("Добавка к виньетке на максимальной скорости.")]
    [SerializeField] private float speedVignette = 0.07f;

    [SerializeField] private float speedReferenceMin = 14f;
    [SerializeField] private float speedReferenceMax = 24f;

    [Header("Плавность")]
    [Tooltip("За сколько секунд эффект доходит до цели.")]
    [SerializeField] private float smoothTime = 0.25f;

    // Ниже этого значения силу дожимаем ровно в ноль. Это не косметика:
    // URP включает ветки шейдера для аберраций и искажения по признаку
    // «сила больше нуля». Оставить 0.001 — значит платить за них весь забег.
    private const float OffThreshold = 0.004f;

    private ChromaticAberration _chromatic;
    private LensDistortion _distortion;
    private Vignette _vignette;
    private ColorAdjustments _color;

    private float _baseVignette;
    private float _baseSaturation;
    private Color _baseFilter;

    private float _chromaticValue, _chromaticVelocity;
    private float _distortionValue, _distortionVelocity;
    private float _vignetteValue, _vignetteVelocity;
    private float _saturationValue, _saturationVelocity;
    private float _goldValue, _goldVelocity;

    private bool _ready;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<PlayerController>();
        if (globalVolume == null) FindGlobalVolume();
    }

    private void Start() => Bind();

    private void FindGlobalVolume()
    {
        foreach (Volume candidate in FindObjectsByType<Volume>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate.isGlobal) continue;
            globalVolume = candidate;
            return;
        }
    }

    /// <summary>
    /// Берём именно volume.profile, а НЕ sharedProfile.
    ///
    /// sharedProfile — это сам файл на диске. Правка его из игры в редакторе
    /// молча перезаписала бы ассет, и настройки «уползали» бы после каждого
    /// запуска Play. Обращение к profile создаёт рабочую копию на время сцены.
    /// </summary>
    private void Bind()
    {
        if (globalVolume == null || globalVolume.sharedProfile == null) return;

        VolumeProfile profile = globalVolume.profile;

        profile.TryGet(out _chromatic);
        profile.TryGet(out _distortion);
        profile.TryGet(out _vignette);
        profile.TryGet(out _color);

        _baseVignette = _vignette != null ? _vignette.intensity.value : 0f;
        _baseSaturation = _color != null ? _color.saturation.value : 0f;
        _baseFilter = _color != null ? _color.colorFilter.value : Color.white;

        _vignetteValue = _baseVignette;
        _saturationValue = _baseSaturation;

        _ready = true;
    }

    private void Update()
    {
        // Профиль мог быть ещё не готов на первом кадре — пробуем снова.
        // Дешевле одной проверки в кадр, чем тихо не работать всю сессию.
        if (!_ready)
        {
            Bind();
            if (!_ready) return;
        }

        bool running = GameManager.Instance != null && GameManager.Instance.IsRunning;

        bool coffee = running && PowerUpManager.Instance != null
                              && PowerUpManager.Instance.IsActive(PowerUpType.Coffee);

        bool doubleScore = running && PowerUpManager.Instance != null
                                   && PowerUpManager.Instance.IsActive(PowerUpType.DoubleScore);

        float speedT = 0f;
        if (running && player != null)
        {
            speedT = Mathf.InverseLerp(speedReferenceMin, speedReferenceMax, player.CurrentSpeed);
        }

        float dt = Time.unscaledDeltaTime;

        // --- цели ---
        float chromaticTarget = coffee ? coffeeChromatic : 0f;
        float distortionTarget = coffee ? coffeeDistortion : 0f;
        float vignetteTarget = _baseVignette
                             + (coffee ? coffeeExtraVignette : 0f)
                             + speedVignette * speedT;
        float saturationTarget = _baseSaturation + (coffee ? coffeeExtraSaturation : 0f);
        float goldTarget = doubleScore ? 1f : 0f;

        // --- сглаживание ---
        _chromaticValue = Smooth(_chromaticValue, chromaticTarget, ref _chromaticVelocity, dt);
        _distortionValue = Smooth(_distortionValue, distortionTarget, ref _distortionVelocity, dt);
        _vignetteValue = Smooth(_vignetteValue, vignetteTarget, ref _vignetteVelocity, dt);
        _saturationValue = Smooth(_saturationValue, saturationTarget, ref _saturationVelocity, dt);
        _goldValue = Smooth(_goldValue, goldTarget, ref _goldVelocity, dt);

        // --- применение ---
        if (_chromatic != null)
            _chromatic.intensity.value = Snap(_chromaticValue);

        if (_distortion != null)
            _distortion.intensity.value = Snap(_distortionValue);

        if (_vignette != null)
            _vignette.intensity.value = Mathf.Clamp01(_vignetteValue);

        if (_color != null)
        {
            _color.saturation.value = Mathf.Clamp(_saturationValue, -100f, 100f);
            _color.colorFilter.value = Color.Lerp(_baseFilter, doubleScoreFilter, _goldValue);
        }
    }

    private float Smooth(float current, float target, ref float velocity, float dt) =>
        Mathf.SmoothDamp(current, target, ref velocity, smoothTime, Mathf.Infinity, dt);

    /// <summary>Дожать почти-ноль до ровного нуля, чтобы эффект выключился совсем.</summary>
    private static float Snap(float value) =>
        Mathf.Abs(value) < OffThreshold ? 0f : value;
}
