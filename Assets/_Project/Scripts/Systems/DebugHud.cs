using UnityEngine;

/// <summary>
/// Временный отладочный текст поверх экрана: дистанция, скорость, состояние.
/// Нужен только на этапе M1, чтобы видеть, что механика работает.
/// На M6 заменим нормальным UI и этот скрипт удалим.
///
/// Куда вешать: на объект Player.
/// </summary>
public class DebugHud : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private bool show = true;

    private GUIStyle _style;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
    }

    private void OnGUI()
    {
        if (!show || player == null) return;

        if (_style == null)
        {
            _style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(Screen.height * 0.028f),
                fontStyle = FontStyle.Bold
            };
            _style.normal.textColor = Color.white;
        }

        string state = player.IsSliding ? "ПОДКАТ"
                     : player.IsGrounded ? "БЕГ"
                     : "ПРЫЖОК";

        string text = $"{player.Distance:F0} м\n" +
                      $"скорость {player.CurrentSpeed:F1}\n" +
                      $"полоса {player.CurrentLane}\n" +
                      $"{state}";

        float margin = Screen.height * 0.04f;
        var rect = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height * 0.3f);

        // тень для читаемости на светлом фоне
        GUIStyle shadow = new GUIStyle(_style);
        shadow.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, shadow);
        GUI.Label(rect, text, _style);
    }
}
