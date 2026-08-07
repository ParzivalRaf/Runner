using UnityEngine;

/// <summary>
/// Поджимает свой RectTransform под безопасную зону экрана, чтобы кнопки
/// и текст не уезжали под «чёлку», вырез камеры или полоску жестов.
///
/// Куда вешать: на объект-контейнер внутри Canvas. Все панели интерфейса
/// должны лежать внутри него.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    private RectTransform _rect;
    private Rect _lastSafeArea;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        Apply();
    }

    private void Update()
    {
        // Безопасная зона меняется при повороте экрана и на некоторых
        // устройствах — при появлении системных панелей.
        if (Screen.safeArea == _lastSafeArea &&
            Screen.width == _lastScreenSize.x &&
            Screen.height == _lastScreenSize.y)
            return;

        Apply();
    }

    private void Apply()
    {
        if (_rect == null) return;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safeArea = Screen.safeArea;
        _lastSafeArea = safeArea;
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        Vector2 min = safeArea.position;
        Vector2 max = safeArea.position + safeArea.size;

        min.x /= Screen.width;
        min.y /= Screen.height;
        max.x /= Screen.width;
        max.y /= Screen.height;

        _rect.anchorMin = min;
        _rect.anchorMax = max;
        _rect.offsetMin = Vector2.zero;
        _rect.offsetMax = Vector2.zero;
    }
}
