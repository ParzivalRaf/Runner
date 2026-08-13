#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Внешний вид игры одной кнопкой: профиль постобработки, небо, туман,
/// солнце и палитра материалов.
///
/// Меню: Tools → Runner → Внешний вид — применить
///
/// Зачем отдельный скрипт, а не часть RunnerSceneBuilder: внешний вид
/// хочется крутить отдельно от геометрии. Пересобирать всю сцену ради
/// смены оттенка неба — долго и рискованно.
///
/// Вызывается и сам, из конца BuildM6Scene, чтобы пересборка сцены
/// не теряла настроенную картинку.
///
/// Это редакторный скрипт: лежит в папке Editor, в сборку игры не попадает.
/// </summary>
public static class RunnerLookBuilder
{
    private const string ProjectRoot = "Assets/_Project";
    private const string MaterialsFolder = ProjectRoot + "/Materials";
    private const string RenderingFolder = ProjectRoot + "/Rendering";
    private const string ProfilePath = RenderingFolder + "/PP_Runner.asset";
    private const string SkyboxPath = MaterialsFolder + "/M_Skybox.mat";
    private const string BrickTexturePath = ProjectRoot + "/Textures/CampusRush/T_CampusBrick_v2.png";

    // Туман должен полностью закрывать даль ДО того места, где появляются
    // новые чанки, иначе видно, как трасса возникает из воздуха.
    // ChunkSpawner держит 4 чанка впереди по 30 юнитов = 120.
    // Значит туман обязан стать непрозрачным раньше 120.
    private const float FogStart = 70f;
    private const float FogEnd = 185f;

    private static readonly Color FogTint = new Color(0.58f, 0.78f, 0.86f);

    /// <summary>
    /// Сила рассеянного света от неба. Было 1.05 — тени заливались
    /// и предметы теряли объём: разница между освещённой и теневой
    /// стороной почти пропадала. 0.85 возвращает объём, ниже опускать
    /// не стоит — мир станет мрачным, а игра про солнечный день.
    /// </summary>
    private const float AmbientIntensity = 0.85f;

    [MenuItem("Tools/Runner/Внешний вид — применить")]
    public static void ApplyFromMenu()
    {
        Apply();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log("[RunnerLookBuilder] Внешний вид применён: постобработка, " +
                  "небо, туман, палитра. Жми Play.");
    }

    /// <summary>
    /// Всё вместе. Безопасно вызывать сколько угодно раз.
    /// </summary>
    public static void Apply()
    {
        VolumeProfile profile = BuildPostProcessProfile();
        AttachProfileToGlobalVolume(profile);

        SetUpPipeline(economy: false);
        SetUpSky();
        SetUpFog();
        SetUpSun();
        SetUpCamera();
        RepaintPalette();

        DynamicGI.UpdateEnvironment();
    }

    /// <summary>
    /// Экономный режим: то же самое, но с выключенными дорогими вещами.
    /// Нужен, чтобы замерить, во сколько кадров обходится красота.
    /// Порядок замера: этим пунктом снять число, обычным — снять снова,
    /// разница и есть цена.
    /// </summary>
    [MenuItem("Tools/Runner/Внешний вид — экономный (для замеров FPS)")]
    public static void ApplyEconomy()
    {
        VolumeProfile profile = BuildPostProcessProfile();
        AttachProfileToGlobalVolume(profile);

        SetUpPipeline(economy: true);
        SetUpSky();
        SetUpFog();
        SetUpSun();
        SetUpCamera();
        RepaintPalette();

        DynamicGI.UpdateEnvironment();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[RunnerLookBuilder] Экономный режим: сглаживание выключено, " +
                  "рендер в 80% разрешения, тени жёсткие и в один каскад. " +
                  "Вернуть красоту — «Внешний вид — применить».");
    }

    // ========================================================== настройки URP

    /// <summary>
    /// Настройки самого пайплайна. Их не видно в сцене, но они решают
    /// больше, чем половина постобработки.
    ///
    /// ЧТО БЫЛО НЕ ТАК. В обоих ассетах стояло m_SoftShadowsSupported = 0,
    /// а солнцу в коде назначались мягкие тени. Пайплайн такую настройку
    /// молча выбрасывает: тени рисовались жёсткими, с рваным краем.
    /// Настройка была, эффекта не было — поэтому этого и не искали.
    ///
    /// ЦЕНА КАЖДОГО ПУНКТА НА ТЕЛЕФОНЕ (порядок величин, проверять замером):
    /// - сглаживание MSAA 2x: на плиточных мобильных GPU дёшево, единицы
    ///   процентов, потому что происходит внутри тайла;
    /// - масштаб рендера 0.8 → 1.0: самый дорогой пункт, пикселей
    ///   становится в полтора раза больше. Зато пропадает мыло;
    /// - мягкие тени: один лишний проход фильтрации по карте теней;
    /// - два каскада вместо одного: карта теней рисуется дважды,
    ///   но ближние тени перестают быть лесенкой.
    /// </summary>
    private static void SetUpPipeline(bool economy)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset == null) continue;

            var so = new SerializedObject(asset);

            // Поля закрытые, публичных сеттеров у части из них нет —
            // поэтому через SerializedObject, а не через свойства.
            SetInt(so, "m_MSAA", economy ? 1 : 2);
            SetFloat(so, "m_RenderScale", economy ? 0.8f : 1.0f);
            SetBool(so, "m_SoftShadowsSupported", !economy);
            SetInt(so, "m_ShadowCascadeCount", economy ? 1 : 2);
            SetInt(so, "m_MainLightShadowmapResolution", economy ? 1024 : 2048);
            SetFloat(so, "m_ShadowDistance", economy ? 50f : 65f);

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
        }
    }

    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
        else Debug.LogWarning("[RunnerLookBuilder] Нет поля " + name + " в ассете URP.");
    }

    private static void SetFloat(SerializedObject so, string name, float value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.floatValue = value;
        else Debug.LogWarning("[RunnerLookBuilder] Нет поля " + name + " в ассете URP.");
    }

    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) { Debug.LogWarning("[RunnerLookBuilder] Нет поля " + name + " в ассете URP."); return; }

        // В ассетах URP такие флаги лежат как bool, но у части версий — как int.
        if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = value;
        else p.intValue = value ? 1 : 0;
    }

    // ============================================================ постобработка

    /// <summary>
    /// Заполняет профиль с нуля: старые эффекты сносятся, новые ставятся
    /// заново. Так значения предсказуемы — иначе после ручных правок
    /// в инспекторе непонятно, что реально применилось.
    ///
    /// Обратная сторона: **ручные правки профиля стираются при каждом
    /// запуске.** Если подобрал значения в инспекторе и они нравятся —
    /// перенеси их сюда в код, иначе потеряешь.
    /// </summary>
    private static VolumeProfile BuildPostProcessProfile()
    {
        EnsureFolder(RenderingFolder);

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);

        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        else
        {
            // Файл переиспользуем, содержимое чистим. Если удалять и создавать
            // заново, у ассета каждый раз меняется внутренний идентификатор,
            // и git видит правку сцены даже когда ничего не изменилось.
            for (int i = profile.components.Count - 1; i >= 0; i--)
            {
                VolumeComponent stale = profile.components[i];
                if (stale != null) Object.DestroyImmediate(stale, true);
            }

            profile.components.Clear();
        }

        // --- Тонемаппинг ---
        // Neutral, а не ACES: ACES заметно дороже на мобилке и слишком сильно
        // тянет цвета в кино-контраст. Для яркой аркады Neutral честнее.
        var tonemapping = AddOverride<Tonemapping>(profile);
        Override(tonemapping.mode, TonemappingMode.Neutral);

        // --- Bloom ---
        // Главный источник «дорогой» картинки и главный риск по FPS.
        // Поэтому: рендерим в четверть разрешения, 4 итерации,
        // качественная фильтрация выключена. Порог 1.0 — светятся только
        // объекты с эмиссией ярче единицы, то есть монеты и разметка,
        // а не вся трасса.
        // Порог 0.9, а не 1.0: на референсе светятся не только эмиссивные
        // монеты, но и сами яркие поверхности — кремовые карнизы, разметка.
        // Ниже 0.85 опускать нельзя: начнёт светиться вся трасса.
        var bloom = AddOverride<Bloom>(profile);
        Override(bloom.threshold, 0.9f);
        Override(bloom.intensity, 0.48f);
        Override(bloom.scatter, 0.42f);
        Override(bloom.clamp, 16f);
        Override(bloom.tint, new Color(1f, 0.93f, 0.82f));
        Override(bloom.highQualityFiltering, false);
        Override(bloom.downscale, BloomDownscaleMode.Quarter);
        Override(bloom.maxIterations, 4);

        // --- Виньетка ---
        // Затемняет углы, взгляд сам собирается к центру, где бежит игрок.
        // Стоит копейки: это математика по экрану, без лишних проходов.
        var vignette = AddOverride<Vignette>(profile);
        Override(vignette.color, new Color(0.07f, 0.09f, 0.13f));
        Override(vignette.intensity, 0.20f);
        Override(vignette.smoothness, 0.35f);
        Override(vignette.rounded, false);

        // --- Цветокоррекция ---
        // Всё, что ниже, запекается в одну таблицу цветов (LUT) один раз
        // за кадр. Поэтому добавить сюда ещё эффектов почти бесплатно.
        var colorAdjustments = AddOverride<ColorAdjustments>(profile);
        // Контраст и насыщенность подняты под референс: там цвета звонкие,
        // а тени тёмные. Экспозиция при этом опущена — иначе поднятый
        // контраст выбивает светлые места в белое.
        Override(colorAdjustments.postExposure, 0.04f);
        Override(colorAdjustments.contrast, 11f);
        Override(colorAdjustments.saturation, 16f);
        Override(colorAdjustments.colorFilter, new Color(1f, 0.99f, 0.95f));

        // Тёплый баланс белого: закат должен читаться как закат.
        var whiteBalance = AddOverride<WhiteBalance>(profile);
        Override(whiteBalance.temperature, 3f);
        Override(whiteBalance.tint, 0f);

        // Тени уводим в холодный фиолет, света — в тёплый.
        // Классическая пара, за которую картинка перестаёт выглядеть плоской.
        var splitToning = AddOverride<SplitToning>(profile);
        Override(splitToning.shadows, new Color(0.12f, 0.28f, 0.35f));
        Override(splitToning.highlights, new Color(1.0f, 0.82f, 0.52f));
        Override(splitToning.balance, 1f);

        // --- Два эффекта «на будущее», выключенные в ноль ---
        // Сами по себе они ничего не делают и ничего не стоят: URP включает
        // их шейдерные ветки, только когда сила больше нуля. Нужны они, чтобы
        // ScreenEffects мог поднять их в рантайме под кофе.
        // Без записи здесь их просто не было бы в профиле и крутить было бы нечего.
        var chromaticAberration = AddOverride<ChromaticAberration>(profile);
        Override(chromaticAberration.intensity, 0f);

        var lensDistortion = AddOverride<LensDistortion>(profile);
        Override(lensDistortion.intensity, 0f);
        Override(lensDistortion.scale, 1f);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        return profile;
    }

    private static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
    {
        // false, а не true: включаем галочки только у тех полей, которые
        // выставляем руками ниже. С true Unity пометит как переопределённые
        // вообще все параметры, включая те, о которых мы не думали.
        T component = profile.Add<T>(false);

        component.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;

        // Компонент профиля — отдельный объект. Без этого он не сохранится
        // в .asset и после перезапуска Unity профиль окажется пустым.
        AssetDatabase.AddObjectToAsset(component, profile);

        return component;
    }

    private static void Override<T>(VolumeParameter<T> parameter, T value)
    {
        parameter.overrideState = true;
        parameter.value = value;
    }

    private static void AttachProfileToGlobalVolume(VolumeProfile profile)
    {
        Volume volume = null;

        foreach (Volume candidate in Object.FindObjectsByType<Volume>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!candidate.isGlobal) continue;
            volume = candidate;
            break;
        }

        if (volume == null)
        {
            var go = new GameObject("Global Volume");
            volume = go.AddComponent<Volume>();
        }

        volume.gameObject.name = "Global Volume";
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.weight = 1f;
        volume.sharedProfile = profile;

        EditorUtility.SetDirty(volume);
    }

    // ================================================================== небо

    /// <summary>
    /// Процедурный скайбокс, а не картинка: ничего не надо скачивать,
    /// весит ноль, цвет крутится ползунками.
    /// </summary>
    private static void SetUpSky()
    {
        Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
        Shader neonSky = Shader.Find("Runner/Neon Sunset Sky");

        if (sky == null)
        {
            Shader shader = neonSky != null ? neonSky : Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[RunnerLookBuilder] Шейдер Skybox/Procedural не найден, небо пропущено.");
                return;
            }

            EnsureFolder(MaterialsFolder);
            sky = new Material(shader);
            AssetDatabase.CreateAsset(sky, SkyboxPath);
        }

        // Предыдущая версия использовала стандартное Procedural Skybox,
        // который под мобильным tonemapping уходил в сплошной жёлтый цвет.
        // Наш шейдер даёт чёткий фиолетовый верх, магентовый горизонт и
        // большой аркадный диск солнца без скачивания тяжёлой панорамы.
        if (neonSky != null && sky.shader != neonSky)
            sky.shader = neonSky;

        if (sky.shader == neonSky)
        {
            SetColorIfExists(sky, "_TopColor", new Color(0.055f, 0.54f, 0.88f));
            SetColorIfExists(sky, "_HorizonColor", new Color(0.46f, 0.80f, 0.94f));
            SetColorIfExists(sky, "_GroundColor", new Color(0.72f, 0.86f, 0.90f));
            SetColorIfExists(sky, "_SunColor", new Color(1.65f, 1.32f, 0.80f));
            SetColorIfExists(sky, "_CloudColor", new Color(1f, 0.96f, 0.86f));
            SetFloatIfExists(sky, "_CloudAmount", 0.54f);
            // Keep the sun as a small compositional accent in the upper-right;
            // the previous disc dominated the route and competed with hazards.
            sky.SetVector("_SunDirection", new Vector4(0.58f, 0.22f, 0.78f, 0f));
            SetFloatIfExists(sky, "_SunSize", 0.0012f);

            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.72f, 0.78f, 0.80f);
            RenderSettings.ambientIntensity = AmbientIntensity;
            EditorUtility.SetDirty(sky);
            return;
        }

        // Диск солнца режимом Simple: HighQuality на мобилке считает лишнее,
        // а разницу на маленьком экране не видно.
        // Это поле с раскрывающимся списком, поэтому мало записать число —
        // надо ещё переключить ключевое слово шейдера, иначе останется
        // старый вариант.
        SetFloatIfExists(sky, "_SunDisk", 1f);
        sky.DisableKeyword("_SUNDISK_NONE");
        sky.EnableKeyword("_SUNDISK_SIMPLE");
        sky.DisableKeyword("_SUNDISK_HIGH_QUALITY");

        SetFloatIfExists(sky, "_SunSize", 0.045f);
        SetFloatIfExists(sky, "_SunSizeConvergence", 4f);
        SetFloatIfExists(sky, "_AtmosphereThickness", 0.85f);
        SetFloatIfExists(sky, "_Exposure", 0.72f);
        SetColorIfExists(sky, "_SkyTint", new Color(0.25f, 0.08f, 0.45f));
        SetColorIfExists(sky, "_GroundColor", new Color(0.03f, 0.01f, 0.10f));

        EditorUtility.SetDirty(sky);

        RenderSettings.skybox = sky;

        // Окружающий свет берём из неба: бесплатно и автоматически
        // согласовано с его цветом.
        RenderSettings.ambientMode = AmbientMode.Skybox;
        RenderSettings.ambientIntensity = AmbientIntensity;
    }

    // ================================================================= туман

    private static void SetUpFog()
    {
        RenderSettings.fog = true;

        // Linear, а не Exponential: у линейного есть явные «начало» и «конец»
        // в юнитах. Можно точно посчитать, что даль закрыта раньше,
        // чем появляются новые чанки. С экспоненциальным пришлось бы подбирать.
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = FogTint;
        RenderSettings.fogStartDistance = FogStart;
        RenderSettings.fogEndDistance = FogEnd;
    }

    // ================================================================ солнце

    private static void SetUpSun()
    {
        Light sun = null;

        foreach (Light candidate in Object.FindObjectsByType<Light>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.type != LightType.Directional) continue;
            sun = candidate;
            break;
        }

        if (sun == null) return;

        // Низкое солнце сбоку-сзади: тени ложатся вдоль трассы и поперёк неё,
        // по ним читается скорость. При солнце в зените тени под ногами
        // и картинка плоская.
        sun.transform.rotation = Quaternion.Euler(48f, 218f, 0f);
        sun.color = new Color(1f, 0.91f, 0.76f);
        sun.intensity = 1.42f;
        sun.shadows = LightShadows.Soft;

        // 0.55 давало полупрозрачные тени — предметы висели над землёй,
        // а не стояли на ней. 0.78 — тени тёмные, но не чёрные: остаток
        // засвечивает рассеянный свет неба, как на референсе.
        sun.shadowStrength = 0.78f;

        EditorUtility.SetDirty(sun);

        // Скайбокс должен знать, где рисовать диск солнца.
        RenderSettings.sun = sun;
    }

    // ================================================================ камера

    private static void SetUpCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        cam.allowHDR = true;

        var data = cam.GetUniversalAdditionalCameraData();
        if (data == null) return;

        data.renderPostProcessing = true;

        // FXAA, а не SMAA: сглаживает по готовому кадру, стоит доли
        // миллисекунды. На мобилке с Render Scale 0.8 края лестницей
        // заметны, и это самое дешёвое лекарство.
        data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

        CameraFollow follow = cam.GetComponent<CameraFollow>();
        if (follow != null)
        {
            var followSo = new SerializedObject(follow);
            followSo.FindProperty("offset").vector3Value = new Vector3(0f, 5.3f, -6.4f);
            followSo.FindProperty("pitch").floatValue = 21f;
            followSo.FindProperty("baseFov").floatValue = 53f;
            followSo.FindProperty("maxFov").floatValue = 66f;
            followSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Imported FBX files previously introduced mixed handedness and made
        // the world feel mirrored. Keep the camera hierarchy strictly
        // right-handed and let every authored prop use explicit rotations.
        cam.transform.localScale = Vector3.one;
        if (cam.GetComponent<CampusBackgroundLayers>() == null)
            cam.gameObject.AddComponent<CampusBackgroundLayers>();

        EditorUtility.SetDirty(cam);
    }

    // =============================================================== палитра

    /// <summary>
    /// Перекрашивает уже существующие материалы. RunnerSceneBuilder их
    /// только создаёт и больше не трогает, поэтому смена палитры живёт здесь.
    ///
    /// Эмиссия — не украшение: bloom светит только по тому, что ярче
    /// его порога. Без эмиссии постобработка будет почти не видна.
    /// </summary>
    private static void RepaintPalette()
    {
        // Трасса и окружение — холодные и тёмные, чтобы тёплое небо
        // и яркие подбираемые предметы отделялись от них сами собой.
        Paint("M_Ground", new Color(0.92f, 0.40f, 0.22f), smoothness: 0.30f, metallic: 0.01f);
        ApplyGroundTexture();
        Paint("M_Rail", new Color(0.05f, 0.38f, 0.36f), smoothness: 0.38f, metallic: 0.12f);
        Paint("M_Prop", new Color(0.90f, 0.78f, 0.59f), smoothness: 0.28f, metallic: 0.01f);

        // Разметка светится слабо: даёт ощущение скорости, но не перетягивает
        // внимание с монет. Если полосы засветятся в белую кашу — это первая
        // ручка, которую надо крутить вниз.
        Paint("M_Marker", new Color(0.98f, 0.91f, 0.75f), smoothness: 0.24f,
              emission: new Color(1f, 0.84f, 0.52f), emissionIntensity: 1.04f);

        // Игрок остаётся оранжевым — цвет уже привычный, и на фиолетовой
        // трассе он читается лучше всего.
        Paint("M_Player", new Color(0.05f, 0.31f, 0.64f), smoothness: 0.22f);

        // Цвет препятствия = что с ним делать. Не меняем язык, только
        // делаем его чище и ярче.
        Paint("M_ObstacleBody", new Color(0.72f, 0.22f, 0.12f), smoothness: 0.30f, metallic: 0.02f);
        Paint("M_ObstacleBlock", new Color(0.72f, 0.22f, 0.12f), smoothness: 0.28f);
        Paint("M_ObstacleJump", new Color(0.05f, 0.27f, 0.64f), smoothness: 0.28f);
        Paint("M_ObstacleSlide", new Color(0.02f, 0.43f, 0.39f), smoothness: 0.28f);

        // Монета — самый яркий объект в кадре. Так и надо: игрок должен
        // видеть цепочку монет раньше, чем препятствие.
        Paint("M_Coin", new Color(1f, 0.66f, 0.10f), smoothness: 0.62f, metallic: 0.55f,
              emission: new Color(1f, 0.58f, 0.10f), emissionIntensity: 1.55f);

        Paint("M_PowerUpMagnet", new Color(0.40f, 0.62f, 1f), smoothness: 0.5f,
              emission: new Color(0.40f, 0.62f, 1f), emissionIntensity: 2.2f);
        Paint("M_PowerUpCoffee", new Color(0.86f, 0.42f, 0.20f), smoothness: 0.5f,
              emission: new Color(0.86f, 0.42f, 0.20f), emissionIntensity: 2.2f);
        Paint("M_PowerUpSneakers", new Color(0.36f, 0.92f, 0.52f), smoothness: 0.5f,
              emission: new Color(0.36f, 0.92f, 0.52f), emissionIntensity: 2.2f);
        Paint("M_PowerUpDoubleScore", new Color(0.90f, 0.44f, 0.92f), smoothness: 0.5f,
              emission: new Color(0.90f, 0.44f, 0.92f), emissionIntensity: 2.2f);

        Paint("M_Crash", new Color(1f, 0.30f, 0.24f), smoothness: 0.3f,
              emission: new Color(1f, 0.30f, 0.24f), emissionIntensity: 2.4f);

        // Поезд не светится: он не «нельзя» и не приз. Светится только
        // окантовка крыши, и она сделана из M_Marker — того же материала,
        // что разметка. Так игрок читает «сюда можно встать» тем же языком,
        // которым уже читает полосы под ногами.
        Paint("M_Train", new Color(0.05f, 0.25f, 0.62f), smoothness: 0.40f, metallic: 0.18f);
        Paint("M_TrainRoof", new Color(0.92f, 0.82f, 0.66f), smoothness: 0.34f, metallic: 0.10f);
        Paint("M_TrainWindow", new Color(0.04f, 0.20f, 0.30f), smoothness: 0.62f, metallic: 0.08f);

        AssetDatabase.SaveAssets();
    }

    private static void ApplyGroundTexture()
    {
        TextureImporter importer = AssetImporter.GetAtPath(BrickTexturePath) as TextureImporter;
        if (importer != null)
        {
            bool changed = importer.wrapMode != TextureWrapMode.Repeat || importer.sRGBTexture == false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 4;
            importer.sRGBTexture = true;
            if (changed) importer.SaveAndReimport();
        }

        Texture2D brick = AssetDatabase.LoadAssetAtPath<Texture2D>(BrickTexturePath);
        Material ground = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_Ground.mat");
        if (brick == null || ground == null) return;
        if (ground.HasProperty("_BaseMap"))
        {
            ground.SetTexture("_BaseMap", brick);
            ground.SetTextureScale("_BaseMap", new Vector2(5f, 20f));
        }
        if (ground.HasProperty("_MainTex"))
        {
            ground.SetTexture("_MainTex", brick);
            ground.SetTextureScale("_MainTex", new Vector2(5f, 20f));
        }
        EditorUtility.SetDirty(ground);
    }

    private static void Paint(string assetName, Color color, float smoothness,
                              float metallic = 0f,
                              Color? emission = null, float emissionIntensity = 0f)
    {
        string path = $"{MaterialsFolder}/{assetName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) return;

        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);

        if (emission.HasValue && emissionIntensity > 0f)
        {
            // Цвет за пределами 0..1 — это и есть HDR. Именно поэтому
            // bloom с порогом 1.0 подхватывает только такие материалы.
            Color hdr = emission.Value * emissionIntensity;

            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", hdr);

            // Без глобального освещения: запечённого света в проекте нет,
            // а лишний расчёт на мобилке ни к чему.
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        else
        {
            material.DisableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", Color.black);
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }

    // ======================================================== вспомогательное

    private static void SetFloatIfExists(Material material, string property, float value)
    {
        if (material.HasProperty(property)) material.SetFloat(property, value);
    }

    private static void SetColorIfExists(Material material, string property, Color value)
    {
        if (material.HasProperty(property)) material.SetColor(property, value);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        int lastSlash = path.LastIndexOf('/');
        string parent = path.Substring(0, lastSlash);
        string leaf = path.Substring(lastSlash + 1);

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }
}
#endif
