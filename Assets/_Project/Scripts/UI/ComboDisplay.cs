using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Показывает серию: «x7» с толчком по размеру на каждое новое попадание
/// и плавным исчезновением, когда серия оборвалась.
///
/// Живёт на HudPanel, поэтому включается и выключается вместе с ней —
/// подписка в OnEnable сама снимается, когда игрок ушёл в меню.
///
/// Куда вешать: на объект с Text внутри HudPanel.
/// </summary>
[RequireComponent(typeof(Text))]
public class ComboDisplay : MonoBehaviour
{
    [Tooltip("Насколько текст подпрыгивает в момент попадания.")]
    [SerializeField] private float punchScale = 0.45f;

    [Tooltip("За сколько секунд толчок гаснет.")]
    [SerializeField] private float punchDecay = 0.18f;

    [Tooltip("За сколько секунд текст исчезает после обрыва серии.")]
    [SerializeField] private float fadeTime = 0.35f;

    [Header("Цвета по длине серии")]
    [SerializeField] private Color lowColor = new Color(1f, 0.95f, 0.72f);
    [SerializeField] private Color highColor = new Color(1f, 0.45f, 0.18f);

    [Tooltip("Серия, на которой цвет достигает максимума.")]
    [SerializeField] private int colorPeakAt = 20;

    private Text _text;
    private RectTransform _rect;

    private float _punch;
    private float _alpha;
    private int _combo;
    private bool _subscribed;

    private void Awake()
    {
        _text = GetComponent<Text>();
        _rect = GetComponent<RectTransform>();

        _alpha = 0f;
        ApplyVisual();
    }

    private void OnEnable()
    {
        _combo = 0;
        _punch = 0f;
        _alpha = 0f;
        ApplyVisual();

        Subscribe();
    }

    private void OnDisable()
    {
        if (!_subscribed) return;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.OnComboChanged -= HandleCombo;

        _subscribed = false;
    }

    /// <summary>
    /// Порядок Awake между объектами Unity не гарантирует, поэтому на самом
    /// первом кадре ScoreManager.Instance может быть ещё пустым. Пробуем
    /// подписаться повторно в Update, пока не получится.
    /// </summary>
    private void Subscribe()
    {
        if (_subscribed || ScoreManager.Instance == null) return;

        ScoreManager.Instance.OnComboChanged += HandleCombo;
        _subscribed = true;
    }

    private void HandleCombo(int combo)
    {
        _combo = combo;

        if (combo <= 0) return;

        if (ScoreManager.Instance != null && combo < ScoreManager.Instance.ComboShowFrom) return;

        _text.text = $"x{combo}";
        _alpha = 1f;
        _punch = punchScale;
    }

    private void Update()
    {
        Subscribe();

        // unscaledDeltaTime: во время хитстопа интерфейс обязан продолжать
        // жить, иначе цифра застывает ровно в самый заметный момент.
        float dt = Time.unscaledDeltaTime;

        _punch = Mathf.MoveTowards(_punch, 0f, punchScale / Mathf.Max(0.01f, punchDecay) * dt);

        bool visible = _combo > 0
                    && (ScoreManager.Instance == null || _combo >= ScoreManager.Instance.ComboShowFrom);

        if (!visible) _alpha = Mathf.MoveTowards(_alpha, 0f, dt / Mathf.Max(0.01f, fadeTime));

        ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (_text == null) return;

        float heat = colorPeakAt > 1 ? Mathf.InverseLerp(1f, colorPeakAt, _combo) : 1f;

        Color color = Color.Lerp(lowColor, highColor, heat);
        color.a = _alpha;
        _text.color = color;

        if (_rect != null) _rect.localScale = Vector3.one * (1f + _punch);
    }
}
