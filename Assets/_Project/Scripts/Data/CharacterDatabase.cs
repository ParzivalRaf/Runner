using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Список всех персонажей в порядке показа в карусели.
///
/// Отдельный ассет, а не поиск по папке: порядок карусели должен быть
/// предсказуемым, а поиск по папке зависит от алфавита и легко ломается
/// при переименовании файла.
///
/// Создать: Assets → Create → Runner → Character Database.
/// Лежать должен в Assets/_Project/Characters/.
/// </summary>
[CreateAssetMenu(fileName = "CharacterDatabase", menuName = "Runner/Character Database", order = 1)]
public class CharacterDatabase : ScriptableObject
{
    [Tooltip("Порядок здесь = порядок в карусели. Первым ставь стартового.")]
    [SerializeField] private List<CharacterData> characters = new List<CharacterData>();

    public IReadOnlyList<CharacterData> Characters => characters;

    public int Count => characters.Count;

    public CharacterData Get(int index)
    {
        if (index < 0 || index >= characters.Count) return null;
        return characters[index];
    }

    public CharacterData FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].Id == id) return characters[i];
        }

        return null;
    }

    public int IndexOf(string id)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].Id == id) return i;
        }

        return -1;
    }

    /// <summary>
    /// Первый персонаж, доступный без покупки. Нужен как запасной вариант,
    /// когда в сейве лежит id персонажа, которого больше нет в проекте.
    /// </summary>
    public CharacterData FirstFree()
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].UnlockedByDefault) return characters[i];
        }

        return Count > 0 ? characters[0] : null;
    }
}
