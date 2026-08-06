using UnityEngine;

/// <summary>
/// Временный отладочный интерфейс поверх экрана: дистанция, скорость,
/// состояние, FPS, а после столкновения — экран проигрыша.
///
/// Это заглушка на время этапов M1–M5. На M6 всё это заменит нормальный
/// Canvas-интерфейс, и скрипт можно будет удалить.
///
/// Куда вешать: на объект Player.
/// </summary>
public class DebugHud : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private ChunkSpawner spawner;
    [SerializeField] private bool show = true;

    private GUIStyle _style;
    private GUIStyle _shadow;
    private GUIStyle _big;

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (spawner == null) spawner = FindFirstObjectByType<ChunkSpawner>();
    }

    private void BuildStyles()
    {
        if (_style != null) return;

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.026f),
            fontStyle = FontStyle.Bold
        };
        _style.normal.textColor = Color.white;

        _shadow = new GUIStyle(_style);
        _shadow.normal.textColor = new Color(0f, 0f, 0f, 0.6f);

        _big = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(Screen.height * 0.045f),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true
        };
        _big.normal.textColor = Color.white;
    }

    private void OnGUI()
    {
        if (!show || player == null) return;

        BuildStyles();
        DrawStats();
        DrawGameOver();
    }

    private void DrawStats()
    {
        string state = player.IsSliding ? "ПОДКАТ"
                     : player.IsGrounded ? "БЕГ"
                     : "ПРЫЖОК";

        string text = $"{player.Distance:F0} м\n" +
                      $"скорость {player.CurrentSpeed:F1}\n" +
                      $"полоса {player.CurrentLane}\n" +
                      $"{state}\n" +
                      $"FPS {1f / Mathf.Max(0.0001f, Time.smoothDeltaTime):F0}";

        if (spawner != null) text += $"\nчанков {spawner.ActiveChunkCount}";

        float margin = Screen.height * 0.04f;
        var rect = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height * 0.35f);

        GUI.Label(new Rect(rect.x + 2f, rect.y + 2f, rect.width, rect.height), text, _shadow);
        GUI.Label(rect, text, _style);
    }

    private void DrawGameOver()
    {
        GameManager game = GameManager.Instance;
        if (game == null || game.IsRunning) return;

        Color previous = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previous;

        var rect = new Rect(0f, Screen.height * 0.32f, Screen.width, Screen.height * 0.36f);
        GUI.Label(rect,
                  $"ВРЕЗАЛСЯ\n\n{game.LastRunDistance:F0} м\n\nтап или любая клавиша — заново",
                  _big);
    }
}
