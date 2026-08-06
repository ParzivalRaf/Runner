using System.Collections.Generic;

/// <summary>
/// Таблица допустимых раскладок препятствий в одном ряду.
///
/// Ряд — это три полосы, записанные тремя символами:
///   '.' — пусто
///   'B' — высокое препятствие (только объехать)
///   'J' — низкое (перепрыгнуть)
///   'S' — балка сверху (подкат)
///
/// Правило безопасности из ТЗ выполняется по построению: в таблице нет
/// ни одной раскладки без прохода. Если в ряду нет ни одной точки '.',
/// то все три полосы обязаны быть одного проходимого типа (JJJ или SSS).
/// Комбинаций вида "BJS" здесь нет и быть не может.
/// </summary>
public static class ObstaclePatterns
{
    private static readonly string[] Tier0 =
    {
        "...", "...",
        "B..", ".B.", "..B",
        "J..", ".J.", "..J"
    };

    private static readonly string[] Tier1 =
    {
        "...",
        "B..", ".B.", "..B",
        "J..", ".J.", "..J",
        "S..", ".S.", "..S",
        "BB.", ".BB", "B.B",
        "JJJ"
    };

    private static readonly string[] Tier2 =
    {
        "...",
        "B..", ".B.", "..B",
        "J..", ".J.", "..J",
        "S..", ".S.", "..S",
        "BB.", ".BB", "B.B",
        "JJ.", ".JJ", "J.J",
        "SS.", ".SS", "S.S",
        "BJ.", ".JB", "JB.", ".BJ",
        "BS.", ".SB", "SB.", ".BS",
        "JJJ", "SSS"
    };

    private static readonly string[] Tier3 =
    {
        "B..", ".B.", "..B",
        "J..", ".J.", "..J",
        "S..", ".S.", "..S",
        "BB.", ".BB", "B.B",
        "JJ.", ".JJ", "J.J",
        "SS.", ".SS", "S.S",
        "BJ.", ".JB", "JB.", ".BJ",
        "BS.", ".SB", "SB.", ".BS",
        "JS.", ".SJ", "SJ.", ".JS",
        "JJJ", "SSS"
    };

    /// <summary>Кривая сложности из ТЗ, раздел 5.2.</summary>
    public static int TierForDistance(float distance)
    {
        if (distance < 200f) return 0;
        if (distance < 600f) return 1;
        if (distance < 1200f) return 2;
        return 3;
    }

    public static IReadOnlyList<string> ForTier(int tier)
    {
        switch (tier)
        {
            case 0: return Tier0;
            case 1: return Tier1;
            case 2: return Tier2;
            default: return Tier3;
        }
    }

    /// <summary>Сколько рядов чанка заполняем на этой сложности.</summary>
    public static int RowsForTier(int tier) => tier == 0 ? 1 : 2;

    /// <summary>
    /// Ряд, в котором нет ни одной пустой полосы, требует действия
    /// (прыжка или подката) — такие ряды нельзя ставить слишком часто.
    /// </summary>
    public static bool RequiresAction(string pattern) => !pattern.Contains(".");

    /// <summary>
    /// Полосы, по которым можно проехать этот ряд.
    /// Для JJJ и SSS проходимы все три — просто надо вовремя нажать.
    /// </summary>
    public static bool[] PassableLanes(string pattern)
    {
        var lanes = new bool[3];

        if (RequiresAction(pattern))
        {
            lanes[0] = lanes[1] = lanes[2] = true;
            return lanes;
        }

        for (int i = 0; i < 3; i++) lanes[i] = pattern[i] == '.';
        return lanes;
    }

    /// <summary>
    /// Есть ли полоса, проходимая и в предыдущем ряду, и в этом.
    /// Если есть — игрок гарантированно проедет, даже не перестраиваясь.
    /// </summary>
    public static bool SharesLane(bool[] previous, bool[] next)
    {
        for (int i = 0; i < 3; i++)
        {
            if (previous[i] && next[i]) return true;
        }
        return false;
    }
}
