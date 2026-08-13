using UnityEngine;

/// <summary>
/// Одно место, где чинится расхождение осей между двумя наборами моделей.
///
/// ЧТО СЛУЧИЛОСЬ. Blender при экспорте в FBX не запекает поворот осей
/// в сами вершины, а вешает его на узел модели: в каждом FBX у всех узлов
/// стоит Lcl Rotation = (-90, 0, 0). Unity этот поворот честно применяет.
///
/// Для базового набора (build_campus_rush_kit.py) это правильно: он собран
/// по-блендеровски, вверх — ось Z. Шкафчик, лампа, арка, скамейка, клумба,
/// барьер и подкатные ворота приходят в Unity ровно так, как задуманы.
///
/// Геройский набор (build_campus_rush_hero_kit.py) собран сразу
/// по-юнитевски: вверх — ось Y, вдоль — Z. Тот же -90 разворачивает его
/// набок. Поезд длиной 9.75 встаёт на нос и превращается в башню высотой
/// 9.75, пандус длиной 7 — в стену высотой 7, часовая башня ложится
/// на бок, барьер расплющивается по полу. Именно это видно в игре.
///
/// НАСТОЯЩЕЕ ЛЕЧЕНИЕ — переэкспорт геройского набора из Blender так же,
/// как собран базовый (вверх — Z). Инструкция в docs/ОСИ_МОДЕЛЕЙ.md.
/// Пока этого не сделано, поворот компенсируется здесь.
///
/// ПОСЛЕ ПЕРЕЭКСПОРТА: поставить HeroKitNeedsAxisFix = false. Больше
/// ничего трогать не нужно — все пять мест, где грузятся модели, спрашивают
/// поворот отсюда.
/// </summary>
public static class CampusRushModels
{
    private const string Root = "CampusRush/";
    private const string HeroPrefix = "HeroKit/";

    /// <summary>
    /// true — геройский набор ещё собран по-юнитевски и его надо доворачивать.
    /// false — набор переэкспортирован по-блендеровски, компенсация не нужна.
    /// </summary>
    public const bool HeroKitNeedsAxisFix = true;

    /// <summary>Компенсация для геройского набора: отменяет импортный -90 по X.</summary>
    public static Quaternion HeroAxisFix =>
        HeroKitNeedsAxisFix ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

    /// <summary>
    /// Поворот-компенсация по имени ресурса. Базовому набору не нужна ничего,
    /// геройскому — HeroAxisFix. Имя — то же, что уходит в Resources.Load,
    /// без префикса "CampusRush/".
    /// </summary>
    public static Quaternion AxisFix(string resourceName) =>
        resourceName != null && resourceName.StartsWith(HeroPrefix)
            ? HeroAxisFix
            : Quaternion.identity;

    /// <summary>Загрузка модели набора. Существует, чтобы префикс был в одном месте.</summary>
    public static GameObject Load(string resourceName) =>
        Resources.Load<GameObject>(Root + resourceName);
}
