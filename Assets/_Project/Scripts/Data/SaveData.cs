using System;
using System.Collections.Generic;

/// <summary>
/// Всё, что игра помнит между запусками. Обычный сериализуемый класс —
/// его целиком превращает в JSON JsonUtility.
///
/// Поле version пригодится, когда структура изменится: по нему можно будет
/// понять старый сейв и мигрировать его, а не выбрасывать прогресс игрока.
/// </summary>
[Serializable]
public class SaveData
{
    public int version = 1;

    public float bestDistance;
    public int bestCoinsInRun;
    public int totalCoins;
    public int runsPlayed;

    public string selectedCharacterId = "";
    public List<string> unlockedCharacters = new List<string>();

    // Уровни апгрейдов из магазина, 0..UpgradeShop.MaxLevel
    public int magnetLevel;
    public int coffeeLevel;
    public int headStartLevel;

    public bool musicEnabled = true;
    public bool soundEnabled = true;
    public bool vibrationEnabled = true;
}
