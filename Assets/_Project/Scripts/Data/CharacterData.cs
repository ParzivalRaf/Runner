using UnityEngine;

/// <summary>
/// Описание одного играбельного персонажа.
///
/// ScriptableObject — это «файл-настройка» в проекте: обычный ассет, который
/// можно создать через Assets → Create и настраивать в инспекторе, не трогая
/// код. Добавить нового учителя = создать ещё один такой файл.
///
/// Модель (visualPrefab) можно оставить пустой: тогда игрок будет обычной
/// капсулой, покрашенной в цвет персонажа. Так система работает уже сейчас,
/// а когда появятся сканы учителей — просто подставим префабы, код не изменится.
/// </summary>
[CreateAssetMenu(fileName = "Character", menuName = "Runner/Character", order = 0)]
public class CharacterData : ScriptableObject
{
    [Header("Опознание")]
    [Tooltip("Постоянный ключ для сейва. Менять НЕЛЬЗЯ — иначе игрок потеряет " +
             "купленного персонажа. Латиницей, без пробелов.")]
    [SerializeField] private string id = "character";

    [Tooltip("Как персонажа зовут на экране выбора.")]
    [SerializeField] private string displayName = "Учитель";

    [Tooltip("Фраза, которую он говорит. Показывается на экране выбора.")]
    [TextArea(2, 3)]
    [SerializeField] private string catchPhrase = "";

    [Header("Внешность")]
    [Tooltip("Цвет капсулы-заглушки и рамки на экране выбора.")]
    [SerializeField] private Color tint = Color.white;

    [Tooltip("Модель персонажа. Пусто = цветная капсула. " +
             "Пивот модели должен стоять на полу, рост примерно 2 юнита.")]
    [SerializeField] private GameObject visualPrefab;

    [Header("Открытие")]
    [Tooltip("Цена в монетах. У стартового персонажа 0.")]
    [SerializeField] private int price = 0;

    [Tooltip("Доступен сразу, без покупки.")]
    [SerializeField] private bool unlockedByDefault = false;

    [Header("Способность")]
    [SerializeField] private CharacterAbility ability = CharacterAbility.None;

    [Tooltip("Смысл числа зависит от способности — см. комментарии в CharacterAbility.")]
    [SerializeField] private float abilityValue = 0f;

    public string Id => id;
    public string DisplayName => displayName;
    public string CatchPhrase => catchPhrase;
    public Color Tint => tint;
    public GameObject VisualPrefab => visualPrefab;
    public int Price => price;
    public bool UnlockedByDefault => unlockedByDefault;
    public CharacterAbility Ability => ability;
    public float AbilityValue => abilityValue;

    /// <summary>Человеческое описание способности для экрана выбора.</summary>
    public string AbilityDescription
    {
        get
        {
            switch (ability)
            {
                case CharacterAbility.CoinBonus:
                    return $"Монет больше на {abilityValue * 100f:0}%";

                case CharacterAbility.LongerPowerUps:
                    return $"Бонусы держатся на {abilityValue:0.#} с дольше";

                case CharacterAbility.Shield:
                    return abilityValue >= 2f
                        ? $"Прощает {abilityValue:0} столкновения за забег"
                        : "Прощает одно столкновение за забег";

                case CharacterAbility.FastStart:
                    return $"Стартовая скорость выше на {abilityValue:0.#}";

                default:
                    return "Без способности";
            }
        }
    }

    /// <summary>Короткая строчка под именем в карусели.</summary>
    public string AbilityShort
    {
        get
        {
            switch (ability)
            {
                case CharacterAbility.CoinBonus: return $"+{abilityValue * 100f:0}% монет";
                case CharacterAbility.LongerPowerUps: return $"бонусы +{abilityValue:0.#} с";
                case CharacterAbility.Shield: return "щит";
                case CharacterAbility.FastStart: return "быстрый старт";
                default: return "—";
            }
        }
    }

    private void OnValidate()
    {
        // Пустой id ломает сейв молча: персонаж просто никогда не «запомнится».
        // Лучше поймать это в редакторе.
        if (string.IsNullOrWhiteSpace(id))
            Debug.LogWarning($"[CharacterData] У ассета «{name}» пустой Id — сейв его не запомнит.", this);

        price = Mathf.Max(0, price);
    }
}
