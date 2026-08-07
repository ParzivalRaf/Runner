using UnityEngine;

/// <summary>Что можно улучшить за монеты.</summary>
public enum UpgradeKind
{
    /// <summary>Магнит держится дольше.</summary>
    MagnetDuration = 0,

    /// <summary>Кофе держится дольше.</summary>
    CoffeeDuration = 1,

    /// <summary>Забег начинается сразу с отметки в сотнях метров.</summary>
    HeadStart = 2
}

/// <summary>
/// Магазин апгрейдов. Работает прямо поверх сейва, поэтому это статический
/// класс без состояния — вешать никуда не надо.
/// </summary>
public static class UpgradeShop
{
    public const int MaxLevel = 5;

    /// <summary>Сколько секунд добавляет один уровень к длительности бонуса.</summary>
    public const float SecondsPerLevel = 1f;

    /// <summary>Сколько метров форы даёт один уровень рывка.</summary>
    public const float MetersPerHeadStartLevel = 100f;

    public static UpgradeKind[] All =>
        new[] { UpgradeKind.MagnetDuration, UpgradeKind.CoffeeDuration, UpgradeKind.HeadStart };

    public static int GetLevel(UpgradeKind kind)
    {
        SaveData data = SaveSystem.Data;

        switch (kind)
        {
            case UpgradeKind.MagnetDuration: return data.magnetLevel;
            case UpgradeKind.CoffeeDuration: return data.coffeeLevel;
            case UpgradeKind.HeadStart: return data.headStartLevel;
            default: return 0;
        }
    }

    private static void SetLevel(UpgradeKind kind, int level)
    {
        SaveData data = SaveSystem.Data;

        switch (kind)
        {
            case UpgradeKind.MagnetDuration: data.magnetLevel = level; break;
            case UpgradeKind.CoffeeDuration: data.coffeeLevel = level; break;
            case UpgradeKind.HeadStart: data.headStartLevel = level; break;
        }
    }

    public static bool IsMaxed(UpgradeKind kind) => GetLevel(kind) >= MaxLevel;

    /// <summary>Цена следующего уровня. Возвращает -1, если улучшать уже некуда.</summary>
    public static int GetPrice(UpgradeKind kind)
    {
        if (IsMaxed(kind)) return -1;

        int level = GetLevel(kind);

        switch (kind)
        {
            case UpgradeKind.MagnetDuration: return 150 * (level + 1);
            case UpgradeKind.CoffeeDuration: return 200 * (level + 1);
            case UpgradeKind.HeadStart: return 300 * (level + 1);
            default: return 0;
        }
    }

    public static bool CanBuy(UpgradeKind kind)
    {
        int price = GetPrice(kind);
        return price >= 0 && SaveSystem.Data.totalCoins >= price;
    }

    /// <summary>Купить следующий уровень. Возвращает false, если не хватило монет.</summary>
    public static bool Buy(UpgradeKind kind)
    {
        if (!CanBuy(kind)) return false;

        int price = GetPrice(kind);
        SaveSystem.Data.totalCoins -= price;
        SetLevel(kind, GetLevel(kind) + 1);
        SaveSystem.Save();

        return true;
    }

    public static string GetName(UpgradeKind kind)
    {
        switch (kind)
        {
            case UpgradeKind.MagnetDuration: return "Магнит дольше";
            case UpgradeKind.CoffeeDuration: return "Кофе дольше";
            case UpgradeKind.HeadStart: return "Рывок на старте";
            default: return kind.ToString();
        }
    }

    public static string GetEffect(UpgradeKind kind)
    {
        int level = GetLevel(kind);

        switch (kind)
        {
            case UpgradeKind.MagnetDuration:
            case UpgradeKind.CoffeeDuration:
                return $"+{level * SecondsPerLevel:0} с";

            case UpgradeKind.HeadStart:
                return $"старт с {level * MetersPerHeadStartLevel:0} м";

            default: return "";
        }
    }

    /// <summary>Фора в метрах, с которой начинается забег.</summary>
    public static float HeadStartDistance =>
        GetLevel(UpgradeKind.HeadStart) * MetersPerHeadStartLevel;

    /// <summary>Прибавка к длительности бонуса от апгрейдов.</summary>
    public static float BonusSecondsFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Magnet:
                return GetLevel(UpgradeKind.MagnetDuration) * SecondsPerLevel;

            case PowerUpType.Coffee:
                return GetLevel(UpgradeKind.CoffeeDuration) * SecondsPerLevel;

            default:
                return 0f;
        }
    }

    /// <summary>Цвет бонуса — один и тот же в мире и в интерфейсе.</summary>
    public static Color ColorFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Magnet: return new Color(0.35f, 0.70f, 0.95f);
            case PowerUpType.Coffee: return new Color(0.55f, 0.33f, 0.18f);
            case PowerUpType.Sneakers: return new Color(0.40f, 0.85f, 0.45f);
            case PowerUpType.DoubleScore: return new Color(0.85f, 0.40f, 0.85f);
            default: return Color.white;
        }
    }

    public static string NameFor(PowerUpType type)
    {
        switch (type)
        {
            case PowerUpType.Magnet: return "Магнит";
            case PowerUpType.Coffee: return "Кофе";
            case PowerUpType.Sneakers: return "Кроссовки";
            case PowerUpType.DoubleScore: return "×2";
            default: return type.ToString();
        }
    }
}
