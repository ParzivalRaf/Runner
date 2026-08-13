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

        // Campus Rush is composed as a portrait mobile runner. Locking the
        // orientation keeps the camera, lane readability and HUD consistent
        // with the hero art instead of allowing a flattened landscape view.
        if (Application.isMobilePlatform)
            Screen.orientation = ScreenOrientation.Portrait;

        // Чтобы экран не гас во время забега.
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
