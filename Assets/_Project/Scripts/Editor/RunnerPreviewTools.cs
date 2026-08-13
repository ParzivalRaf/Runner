#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RunnerPreviewTools
{
    private const string PendingKey = "RunnerPreviewTools.PendingRun";
    private const string FreezeKey = "RunnerPreviewTools.FreezeFrame";
    private static double _enteredAt;
    private static double _freezeAt;

    static RunnerPreviewTools()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    [MenuItem("Tools/Runner/Превью — запустить забег")]
    private static void StartPreviewRun()
    {
        SessionState.SetBool(FreezeKey, false);
        SessionState.SetBool(PendingKey, true);
        if (EditorApplication.isPlaying)
        {
            _enteredAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= TryStartRun;
            EditorApplication.update += TryStartRun;
        }
        else
        {
            EditorApplication.EnterPlaymode();
        }
    }

    [MenuItem("Tools/Runner/Превью — стоп-кадр для проверки")]
    private static void StartFrozenPreview()
    {
        SessionState.SetBool(FreezeKey, true);
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPaused = false;
        if (EditorApplication.isPlaying)
        {
            _enteredAt = EditorApplication.timeSinceStartup;
            EditorApplication.update -= TryStartRun;
            EditorApplication.update += TryStartRun;
        }
        else EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode ||
            !SessionState.GetBool(PendingKey, false)) return;

        _enteredAt = EditorApplication.timeSinceStartup;
        EditorApplication.update -= TryStartRun;
        EditorApplication.update += TryStartRun;
    }

    private static void TryStartRun()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= TryStartRun;
            return;
        }

        if (EditorApplication.timeSinceStartup - _enteredAt < 0.8d) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.StartRun();
        if (SessionState.GetBool(FreezeKey, false))
        {
            _freezeAt = EditorApplication.timeSinceStartup + 1.35d;
            EditorApplication.update -= FreezePreview;
            EditorApplication.update += FreezePreview;
        }
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= TryStartRun;
        Debug.Log("[RunnerPreviewTools] Preview run started.");
    }

    private static void FreezePreview()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= FreezePreview;
            return;
        }
        if (EditorApplication.timeSinceStartup < _freezeAt) return;
        EditorApplication.isPaused = true;
        EditorApplication.update -= FreezePreview;
        SessionState.SetBool(FreezeKey, false);
        Debug.Log("[RunnerPreviewTools] Frozen comparison frame ready.");
    }
}
#endif
