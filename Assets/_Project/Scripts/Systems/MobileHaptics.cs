using UnityEngine;

/// <summary>
/// Короткая виброотдача на телефоне. Раньше настройка «Вибрация» сохранялась,
/// но ни одно игровое событие её не вызывало — поэтому переключатель работал
/// только на словах.
/// </summary>
public static class MobileHaptics
{
    /// <summary>Лёгкий сигнал для полезного действия: бонуса или спасения.</summary>
    public static void Light() => Vibrate();

    /// <summary>Сигнал для столкновения.</summary>
    public static void Crash()
    {
        Vibrate();
        // Handheld.Vibrate не позволяет выбирать силу на iPhone. Второй
        // вызов в тот же кадр всё равно сливается с первым, поэтому один
        // надёжный импульс лучше фальшивой «сильной» вибрации.
    }

    private static void Vibrate()
    {
        if (!Application.isMobilePlatform) return;
        if (!SaveSystem.Data.vibrationEnabled) return;

        Handheld.Vibrate();
    }
}
