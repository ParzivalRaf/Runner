using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Роль модели в игре. Код просит роль, а не файл: какой именно файл
/// подставится — решает активный набор моделей (см. ArtSetSelection).
/// </summary>
public enum ArtRole
{
    Train,
    Ramp,
    ObstacleBlock,
    ObstacleJump,
    ObstacleSlide,
    BuildingA,
    BuildingB,
    ClockTower,
    Tree,
    Banner,
    Bench,
    Lamp,
    Planter,
}

/// <summary>
/// Набор моделей. Переключается через Tools → Runner → Набор моделей.
/// </summary>
public enum ArtSet
{
    /// <summary>Текущие модели проекта: базовый набор + HeroKit.</summary>
    Original,

    /// <summary>ChatGPT, набор Premium_Campus_City: кампус, кирпич, часовая башня.</summary>
    GptPremium,

    /// <summary>ChatGPT, базовый набор: метро-раннер, рельсы, два поезда.</summary>
    GptBase,

    /// <summary>Claude: процедурный набор, собранный кодом и сконвертированный в OBJ.</summary>
    Claude,
}

/// <summary>
/// Одно место, где решается, какая модель подставляется под какую роль.
///
/// ЗАЧЕМ. Раньше пути к моделям были прописаны в пяти файлах, и сменить
/// набор означало править их все. Теперь код спрашивает роль, а таблица
/// ниже отвечает, какой файл грузить в активном наборе. Смена набора —
/// одна константа в ArtSetSelection.cs, которую пишет пункт меню.
///
/// ЧЕГО НЕТ В НАБОРЕ — берётся из Original. Так сравнение не разваливается
/// из-за того, что в новом ките нет, скажем, подкатных ворот.
///
/// ОСИ. У Original компенсация осей нужна (см. историю ниже). У остальных
/// наборов модели заведены через RunnerArtSetTools: там поворот, масштаб
/// и посадка на землю запечены в префаб при импорте, поэтому в рантайме
/// ничего доворачивать не надо.
///
/// ИСТОРИЯ ОСЕЙ (Original). Blender при экспорте в FBX не запекает поворот
/// осей в вершины, а вешает на узел: у всех наших FBX Lcl Rotation =
/// (-90, 0, 0), и Unity его применяет. Базовый набор собран по-блендеровски
/// (вверх — Z) и приходит правильно. HeroKit собран по-юнитевски (вверх — Y),
/// поэтому тот же -90 кладёт его набок: поезд длиной 10.07 вставал башней.
/// Компенсация — HeroKitNeedsAxisFix. После переэкспорта поставить false.
/// Подробности: docs/ОСИ_МОДЕЛЕЙ.md.
/// </summary>
public static class CampusRushModels
{
    private const string Root = "CampusRush/";
    private const string HeroPrefix = "HeroKit/";

    /// <summary>Активный набор. Значение пишет пункт меню в ArtSetSelection.cs.</summary>
    public static ArtSet Active => ArtSetSelection.Active;

    /// <summary>
    /// true — HeroKit ещё собран по-юнитевски и его надо доворачивать.
    /// false — набор переэкспортирован по-блендеровски, компенсация не нужна.
    /// </summary>
    public const bool HeroKitNeedsAxisFix = true;

    /// <summary>Компенсация для HeroKit: отменяет импортный -90 по X.</summary>
    public static Quaternion HeroAxisFix =>
        HeroKitNeedsAxisFix ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;

    /// <summary>
    /// В Original цвета назначает код (SchoolObstacleVisuals.MaterialForPart)
    /// по именам частей — «Cobalt», «Gold», «Brick». В чужих наборах имена
    /// другие, и такая покраска сделала бы их одноцветными. Поэтому у чужих
    /// наборов оставляем материалы, пришедшие из файла: иначе сравнение
    /// нечестное.
    /// </summary>
    public static bool UseImportedMaterials => Active != ArtSet.Original;

    // ---------- таблица ролей ----------

    private static readonly Dictionary<ArtRole, string> OriginalPaths = new()
    {
        { ArtRole.Train,         HeroPrefix + "CR_CampusTrain" },
        { ArtRole.Ramp,          HeroPrefix + "CR_TrainRamp" },
        { ArtRole.ObstacleBlock, "CR_Locker" },
        { ArtRole.ObstacleJump,  HeroPrefix + "CR_Barricade" },
        { ArtRole.ObstacleSlide, "CR_SlideGate" },
        { ArtRole.BuildingA,     HeroPrefix + "CR_CampusBuilding_A" },
        { ArtRole.BuildingB,     HeroPrefix + "CR_CampusBuilding_B" },
        { ArtRole.ClockTower,    HeroPrefix + "CR_ClockTower" },
        { ArtRole.Tree,          HeroPrefix + "CR_CampusTree" },
        { ArtRole.Banner,        HeroPrefix + "CR_CampusBanner" },
        { ArtRole.Bench,         "CR_Bench" },
        { ArtRole.Lamp,          "CR_Lamp" },
        { ArtRole.Planter,       "CR_Planter" },
    };

    /// <summary>
    /// Роли, которые заведены в каждом из чужих наборов. Файлы лежат в
    /// Resources/CampusRush/Sets/&lt;набор&gt;/&lt;роль&gt;.prefab и создаются
    /// пунктом меню «Наборы моделей — импортировать». Роли, которых в списке
    /// нет, берутся из Original.
    /// </summary>
    private static readonly Dictionary<ArtSet, HashSet<ArtRole>> SetCoverage = new()
    {
        {
            ArtSet.GptPremium, new HashSet<ArtRole>
            {
                ArtRole.Train, ArtRole.Ramp, ArtRole.ObstacleBlock, ArtRole.ObstacleJump,
                ArtRole.BuildingA, ArtRole.BuildingB, ArtRole.ClockTower,
                ArtRole.Tree, ArtRole.Banner,
            }
        },
        {
            ArtSet.GptBase, new HashSet<ArtRole>
            {
                ArtRole.Train, ArtRole.Ramp, ArtRole.ObstacleBlock, ArtRole.ObstacleJump,
                ArtRole.ObstacleSlide, ArtRole.Tree, ArtRole.Bench, ArtRole.Lamp,
            }
        },
        {
            ArtSet.Claude, new HashSet<ArtRole>
            {
                ArtRole.Train, ArtRole.Ramp, ArtRole.ObstacleBlock, ArtRole.ObstacleJump,
                ArtRole.BuildingA, ArtRole.BuildingB, ArtRole.ClockTower,
                ArtRole.Tree, ArtRole.Lamp,
            }
        },
    };

    /// <summary>Папка набора внутри Resources/CampusRush/Sets.</summary>
    public static string SetFolder(ArtSet set) => set.ToString();

    /// <summary>Путь ресурса для роли в наборе, или null если роль в наборе не заведена.</summary>
    public static string PathFor(ArtSet set, ArtRole role)
    {
        if (set == ArtSet.Original)
            return OriginalPaths.TryGetValue(role, out string original) ? original : null;

        return SetCoverage.TryGetValue(set, out HashSet<ArtRole> covered) && covered.Contains(role)
            ? "Sets/" + SetFolder(set) + "/" + role
            : null;
    }

    /// <summary>Модель для роли из активного набора. Если её там нет — из Original.</summary>
    public static GameObject Load(ArtRole role)
    {
        string path = PathFor(Active, role);
        GameObject model = path != null ? Resources.Load<GameObject>(Root + path) : null;
        if (model != null) return model;

        string fallback = PathFor(ArtSet.Original, role);
        if (fallback == null) return null;

        if (path != null)
            Debug.LogWarning($"[CampusRushModels] В наборе {Active} нет модели для {role} " +
                             $"({Root + path}). Беру из Original. Прогони Tools → Runner → " +
                             "Наборы моделей — импортировать все.");

        return Resources.Load<GameObject>(Root + fallback);
    }

    /// <summary>
    /// Поворот-компенсация. Нужна только моделям Original из HeroKit —
    /// у остальных наборов оси запечены в префаб при импорте.
    /// </summary>
    public static Quaternion AxisFix(ArtRole role)
    {
        string path = PathFor(Active, role);
        bool cameFromOriginal = path == null;
        if (!cameFromOriginal && Active != ArtSet.Original) return Quaternion.identity;

        string originalPath = PathFor(ArtSet.Original, role);
        return originalPath != null && originalPath.StartsWith(HeroPrefix)
            ? HeroAxisFix
            : Quaternion.identity;
    }

    /// <summary>Загрузка по прямому имени ресурса. Осталась для мест, где роли нет.</summary>
    public static GameObject Load(string resourceName) =>
        Resources.Load<GameObject>(Root + resourceName);

    /// <summary>Компенсация по прямому имени ресурса.</summary>
    public static Quaternion AxisFix(string resourceName) =>
        resourceName != null && resourceName.StartsWith(HeroPrefix)
            ? HeroAxisFix
            : Quaternion.identity;
}
