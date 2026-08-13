#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Открывает сцену игры сразу при запуске Unity.
///
/// Зачем: Unity помнит последнюю открытую сцену в папке Library. Library —
/// это кэш, он не хранится в git и пересоздаётся при каждом переносе
/// проекта, обновлении редактора или чистке. После этого Unity открывает
/// пустую безымянную сцену Untitled, и дальше всё выглядит сломанным:
/// сборщики ругаются, Play запускает пустоту.
///
/// Безопасность: сцена подставляется ТОЛЬКО если открыта безымянная и
/// нетронутая сцена. Если ты что-то в ней уже делал (в заголовке звёздочка)
/// или открыл любую другую сцену — скрипт не вмешивается.
/// </summary>
[InitializeOnLoad]
public static class RunnerSceneAutoOpen
{
    private const string GameScenePath = "Assets/_Project/Scenes/Game.unity";

    static RunnerSceneAutoOpen()
    {
        // Ждём конца загрузки редактора: во время неё открывать сцену нельзя.
        EditorApplication.delayCall += TryOpen;
    }

    private static void TryOpen()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;

        Scene active = EditorSceneManager.GetActiveScene();

        // Уже открыта нормальная сцена — не трогаем.
        if (!string.IsNullOrEmpty(active.path)) return;

        // В безымянной сцене есть несохранённая работа — не трогаем.
        if (active.isDirty) return;

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScenePath) == null) return;

        EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
        Debug.Log("[Runner] Открыта сцена игры: " + GameScenePath);
    }
}
#endif
