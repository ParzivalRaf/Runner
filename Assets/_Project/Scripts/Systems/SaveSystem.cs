using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Чтение и запись сейва в JSON-файл.
///
/// Почему файл, а не PlayerPrefs: PlayerPrefs хранит только строки и числа
/// по одному ключу, и как только появятся списки персонажей и апгрейдов,
/// он превратится в кашу. Файл расширяется без боли.
///
/// Битый сейв не роняет игру: любое исключение при чтении означает
/// «начинаем с чистого листа».
///
/// Это статический класс — вешать никуда не надо.
/// </summary>
public static class SaveSystem
{
    private const string FileName = "save.json";

    private static SaveData _cached;

    private static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

    /// <summary>Текущий сейв. При первом обращении читается с диска.</summary>
    public static SaveData Data
    {
        get
        {
            if (_cached == null) _cached = Load();
            return _cached;
        }
    }

    public static SaveData Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new SaveData();

            string json = File.ReadAllText(FilePath);
            SaveData data = JsonUtility.FromJson<SaveData>(json);

            // FromJson возвращает null на пустом или мусорном файле.
            return data ?? new SaveData();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveSystem] Не смог прочитать сейв ({e.Message}). Начинаю новый.");
            return new SaveData();
        }
    }

    public static void Save()
    {
        if (_cached == null) return;

        try
        {
            string json = JsonUtility.ToJson(_cached, prettyPrint: true);
            File.WriteAllText(FilePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveSystem] Не смог записать сейв: {e.Message}");
        }
    }

    /// <summary>Сброс прогресса. Понадобится в настройках на M6.</summary>
    public static void ResetProgress()
    {
        _cached = new SaveData();
        Save();
    }

    /// <summary>Путь к файлу — удобно вывести на экран, когда ищешь сейв руками.</summary>
    public static string DebugPath => FilePath;
}
