using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Короткая проверка перед сборкой: не заменяет игровой тест на телефоне,
/// но ловит сломанную таблицу препятствий и показывает, у кого намеренно
/// включена запасная капсула вместо модели.
/// </summary>
public static class RunnerProjectValidator
{
    [MenuItem("Tools/Runner/Проверки/Проверить проект")]
    private static void ValidateProject()
    {
        var report = new List<string>();

        if (ObstaclePatterns.ValidateTables(out string problem))
            report.Add("✓ Все таблицы препятствий проходимы.");
        else
            report.Add("✗ Таблицы препятствий: " + problem);

        int fallbackCount = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterData", new[] { "Assets/_Project/Characters" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(path);
            if (character != null && character.VisualPrefab == null) fallbackCount++;
        }

        report.Add("✓ Персонажей с временной цветной капсулой: " + fallbackCount + ".");

        // Набор моделей: показываем, что активно и не пустует ли какая-то роль.
        // Роль без модели не ломает игру — она берётся из Original, — но
        // сравнение наборов при этом уже не про то, что кажется.
        var missing = new List<string>();
        foreach (ArtRole role in System.Enum.GetValues(typeof(ArtRole)))
            if (CampusRushModels.Load(role) == null) missing.Add(role.ToString());

        report.Add("✓ Набор моделей: " + CampusRushModels.Active + ".");
        report.Add(missing.Count == 0
            ? "✓ Модель есть у каждой роли."
            : "✗ Нет модели у ролей: " + string.Join(", ", missing));
        string message = string.Join("\n", report);
        Debug.Log("[Runner] " + message.Replace("\n", " | "));
        EditorUtility.DisplayDialog("Проверка Runner", message, "Ок");
    }
}
