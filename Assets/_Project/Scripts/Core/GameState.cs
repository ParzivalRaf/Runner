/// <summary>
/// Состояния игры. Всё, что происходит на экране, зависит только от него:
/// какая панель UI видна, бежит ли игрок, идёт ли время.
/// </summary>
public enum GameState
{
    /// <summary>Главное меню. Трасса видна, но игрок стоит.</summary>
    Menu,

    /// <summary>Забег идёт.</summary>
    Running,

    /// <summary>Пауза. Time.timeScale = 0.</summary>
    Paused,

    /// <summary>Врезался. Ждём выбора игрока: заново или в меню.</summary>
    Dead
}
