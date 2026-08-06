using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Простой пул объектов. Вместо Instantiate/Destroy во время игры мы один раз
/// создаём нужное количество копий и потом только включаем и выключаем их.
/// Это главный способ убрать фризы на слабых телефонах: сборщик мусора
/// перестаёт срабатывать посреди забега.
///
/// Это обычный C#-класс, не MonoBehaviour — вешать никуда не надо,
/// его создаёт ChunkSpawner.
/// </summary>
public class ObjectPool
{
    private readonly GameObject _prefab;
    private readonly Transform _parent;
    private readonly Stack<GameObject> _available = new Stack<GameObject>();

    /// <summary>Сколько экземпляров всего создано — удобно для отладки.</summary>
    public int TotalCreated { get; private set; }

    public ObjectPool(GameObject prefab, Transform parent, int prewarmCount = 0)
    {
        _prefab = prefab;
        _parent = parent;

        for (int i = 0; i < prewarmCount; i++)
            _available.Push(CreateNew());
    }

    private GameObject CreateNew()
    {
        GameObject instance = Object.Instantiate(_prefab, _parent);
        instance.name = _prefab.name;
        instance.SetActive(false);
        TotalCreated++;
        return instance;
    }

    /// <summary>Достать объект из пула (или создать новый, если пул пуст).</summary>
    public GameObject Get()
    {
        GameObject instance = _available.Count > 0 ? _available.Pop() : CreateNew();
        instance.SetActive(true);
        return instance;
    }

    /// <summary>Вернуть объект в пул. Объект выключается, но не уничтожается.</summary>
    public void Release(GameObject instance)
    {
        if (instance == null) return;

        instance.SetActive(false);
        instance.transform.SetParent(_parent, false);
        _available.Push(instance);
    }
}
