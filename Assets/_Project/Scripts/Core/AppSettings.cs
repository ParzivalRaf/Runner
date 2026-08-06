using UnityEngine;

/// <summary>
/// Глобальные настройки приложения. Запускается автоматически перед загрузкой
/// первой сцены — вешать никуда не надо, статический класс.
/// </summary>
public static class AppSettings
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // На мобилках vSync игнорируется, частоту задаём вручную.
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;

        // Чтобы экран не гас во время забега.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
