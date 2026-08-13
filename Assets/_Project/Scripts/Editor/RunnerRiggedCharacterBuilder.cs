using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Собирает персонажа из скачанной модели со скелетом и анимаций Mixamo.
///
/// Что делает за один клик:
///   1. находит модель тела и файлы анимаций;
///   2. переключает их импорт на Humanoid, чтобы анимации налезли на модель;
///   3. делает контроллер анимаций: бег, полёт, подкат, падение при смерти;
///   4. ставит на голову пластину с фотографией лица;
///   5. подгоняет рост под 2 юнита и подставляет в ассет персонажа.
///
/// Как отличаются файлы: у анимаций Mixamo в имени есть «@», у модели тела —
/// нет. Поэтому раскладывать их по папкам вручную не требуется.
/// </summary>
public static class RunnerRiggedCharacterBuilder
{
    private const string ProjectRoot = "Assets/_Project";
    private const string PrefabFolder = ProjectRoot + "/Prefabs/Characters";
    private const string AnimatorFolder = ProjectRoot + "/Animations";
    private const string TextureFolder = ProjectRoot + "/Textures";
    private const string CharacterFolder = ProjectRoot + "/Characters";

    private const float TargetHeight = 2.0f;

    // Пластина с фотографией нужна только моделям без лица — блочной фигурке
    // и голым телам из паков. У настоящего скана головы лицо уже есть,
    // и фото поверх него только мешает. Поэтому выключатель.
    private const string FaceOption = "Tools/Runner/Клеить фото на лицо";
    private const string FacePrefKey = "Runner.AttachFacePhoto";

    private static bool AttachFaceEnabled
    {
        get => EditorPrefs.GetBool(FacePrefKey, true);
        set => EditorPrefs.SetBool(FacePrefKey, value);
    }

    [MenuItem(FaceOption)]
    private static void ToggleFace() => AttachFaceEnabled = !AttachFaceEnabled;

    [MenuItem(FaceOption, true)]
    private static bool ToggleFaceValidate()
    {
        Menu.SetChecked(FaceOption, AttachFaceEnabled);
        return true;
    }

    /// <summary>
    /// Создаёт папку так, чтобы о ней сразу узнал Unity.
    ///
    /// Обычного создания папки на диске мало: пока AssetDatabase о ней
    /// не знает, сохранение в неё молча падает, и кнопка «срабатывает»
    /// без всякого результата. Ровно так и терялся префаб персонажа.
    /// </summary>
    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }

    // У каждого героя свой пункт. Сначала выделяем в Project нужный FBX,
    // потом выбираем имя героя. Так новая сборка не перезаписывает прошлую.
    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией/Новичок")]
    private static void BuildRookie() => BuildRigged("rookie");

    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией/Физрук")]
    private static void BuildPe() => BuildRigged("pe");

    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией/Математичка")]
    private static void BuildMath() => BuildRigged("math");

    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией/Химичка")]
    private static void BuildChem() => BuildRigged("chem");

    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией/Директор")]
    private static void BuildPrincipal() => BuildRigged("principal");

    /// <summary>
    /// Builds the art-directed Campus Rush cast in one deterministic pass.
    /// Character ids, prices and abilities remain untouched; only the visual
    /// prefab and its animation controller are replaced.
    /// </summary>
    [MenuItem("Tools/Runner/Campus Rush — собрать весь состав")]
    private static void BuildCampusRoster()
    {
        var roster = new Dictionary<string, string>
        {
            { "rookie", "Assets/Resources/CampusRush/Characters/CR_Rookie.fbx" },
            { "pe", "Assets/Resources/CampusRush/Characters/CR_PE.fbx" },
            { "math", "Assets/Resources/CampusRush/Characters/CR_Math.fbx" },
            { "chem", "Assets/Resources/CampusRush/Characters/CR_Chem.fbx" },
            { "principal", "Assets/Resources/CampusRush/Characters/CR_Principal.fbx" },
        };

        try
        {
            foreach (KeyValuePair<string, string> entry in roster)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(entry.Value) == null)
                    throw new FileNotFoundException("Campus character model is missing", entry.Value);

                BuildRiggedInner(entry.Key, entry.Value, showDialog: false, attachFace: false);
            }

            EditorUtility.DisplayDialog("Campus Rush",
                "Готово. Пять персонажей собраны в одном стиле и подключены к анимациям.", "Ок");
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorUtility.DisplayDialog("Campus Rush", "Сборка состава сорвалась.\n\n" + e.Message, "Ок");
        }
    }

    [MenuItem("Tools/Runner/Анимации — подготовить подкат")]
    private static void PrepareSlideForExistingControllers()
    {
        string[] guids = AssetDatabase.FindAssets("t:AnimatorController", new[] { AnimatorFolder });
        int updated = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null || controller.layers.Length == 0) continue;

            if (!EnsureSlideSupport(controller)) continue;

            EditorUtility.SetDirty(controller);
            updated++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Runner",
            "Готово. Обновлено контроллеров: " + updated + ".\n\n" +
            "Открой нужный Character_*.controller, выбери состояние Slide " +
            "и перетащи свой клип в поле Motion. Пока клипа нет, подкат " +
            "безопасно использует анимацию бега.", "Ок");
    }

    [MenuItem("Tools/Runner/Анимации — починить Soccer Tackle")]
    private static void RepairSoccerTackle()
    {
        const string tacklePath = ProjectRoot + "/Models/The Boss@Soccer Tackle.fbx";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(tacklePath) == null)
        {
            EditorUtility.DisplayDialog("Runner", "Не найден файл Soccer Tackle.\n\n" + tacklePath, "Ок");
            return;
        }

        // Humanoid обязателен: Generic-клип из The Boss не может надёжно
        // проигрываться на скелете X Bot. После импорта снова возьмём клип
        // из FBX и назначим его всем существующим Slide-состояниям.
        MakeHumanoid(tacklePath, isBody: false);
        AnimationClip tackle = ClipFrom(tacklePath);
        if (tackle == null)
        {
            EditorUtility.DisplayDialog("Runner", "Unity не смогла извлечь клип Soccer Tackle.", "Ок");
            return;
        }

        int updated = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { AnimatorFolder }))
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (controller == null || controller.layers.Length == 0) continue;

            EnsureSlideSupport(controller);
            AnimatorState slide = FindState(controller.layers[0].stateMachine, "Slide");
            if (slide == null || slide.motion == tackle) continue;

            slide.motion = tackle;
            EditorUtility.SetDirty(controller);
            updated++;
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Runner", "Soccer Tackle готов. Обновлено контроллеров: " + updated + ".", "Ок");
    }

    private static void BuildRigged(string characterId)
    {
        try
        {
            BuildRiggedInner(characterId, null, showDialog: true, attachFace: AttachFaceEnabled);
        }
        catch (System.Exception e)
        {
            // Без этого исключение уходит только в консоль, а на экране
            // не происходит ничего — выглядит как «кнопка не работает».
            Debug.LogException(e);

            EditorUtility.DisplayDialog("Runner",
                "Сборка сорвалась.\n\n" + e.Message +
                "\n\nПодробности в окне Console (Window → General → Console).", "Ок");
        }
    }

    private static void BuildRiggedInner(string characterId, string explicitBodyPath,
                                         bool showDialog, bool attachFace)
    {
        CharacterData targetCharacter = AssetDatabase.LoadAssetAtPath<CharacterData>(
            CharacterFolder + "/Character_" + characterId + ".asset");

        if (targetCharacter == null)
        {
            EditorUtility.DisplayDialog("Runner",
                "Не нашёл ассет выбранного персонажа.\n\n" +
                "Ожидался файл: Character_" + characterId + ".asset", "Ок");
            return;
        }

        string bodyPath = explicitBodyPath ?? FindBody();

        if (bodyPath == null)
        {
            EditorUtility.DisplayDialog("Runner",
                "Не нашёл модель тела.\n\n" +
                "В проекте лежат только анимации (файлы со значком @ в имени).\n" +
                "Положи FBX персонажа из пака Quaternius в " + ProjectRoot + "/Models " +
                "и нажми ещё раз.", "Ок");
            return;
        }

        // 1. Импорт как Humanoid — иначе анимации не налезут на скелет.
        MakeHumanoid(bodyPath, isBody: true);

        List<string> clipPaths = FindAnimations();
        foreach (string path in clipPaths) MakeHumanoid(path, isBody: false);

        AssetDatabase.Refresh();

        // 2. Контроллер анимаций.
        AnimatorController controller = BuildController(clipPaths, characterId);

        // 3. Фигурка.
        var body = AssetDatabase.LoadAssetAtPath<GameObject>(bodyPath);
        var root = new GameObject("Char_Rigged");

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(body, root.transform);
        instance.transform.localPosition = Vector3.zero;

        ScaleToHeight(root, instance);

        var animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;

        // Корневое движение выключено: игрока двигает код, а не анимация.
        // Иначе персонаж поедет сам по себе и уйдёт из-под камеры.
        animator.applyRootMotion = false;

        if (attachFace) AttachFace(animator);

        var driver = root.AddComponent<CharacterAnimatorDriver>();
        var so = new SerializedObject(driver);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        EnsureFolder(PrefabFolder);

        // Id персонажа нельзя менять: по нему хранится сейв. Поэтому имя
        // префаба тоже строим из Id — у каждого героя свой стабильный файл.
        string prefabPath = PrefabFolder + "/Char_" + characterId + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssignToCharacter("Character_" + characterId, prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (showDialog)
        {
            EditorUtility.DisplayDialog("Runner",
                "Готово.\n\nПерсонаж: " + targetCharacter.DisplayName +
                "\nТело: " + Path.GetFileName(bodyPath) +
                "\nАнимаций подключено: " + clipPaths.Count +
                "\n\nМодель: " + prefabPath +
                "\nПодставлена выбранному персонажу.\n\nЖми Play.", "Ок");
        }
    }

    // ------------------------------------------------------------ поиск

    private static IEnumerable<string> AllModelPaths()
    {
        return AssetDatabase.FindAssets("t:Model", new[] { ProjectRoot })
                            .Select(AssetDatabase.GUIDToAssetPath)
                            .Distinct();
    }

    /// <summary>
    /// Ищет модель тела.
    ///
    /// Одного правила «без @ в имени» мало: в паке Quaternius лежат ещё
    /// два десятка причёсок, бород и бровей, и это тоже модели без «@».
    /// Поэтому сначала ищем по слову FullBody, и только если не нашли —
    /// берём самый большой файл, отбросив явные аксессуары.
    /// </summary>
    private static string FindBody()
    {
        // Если модель выделена в окне Project — берём именно её.
        // Так не нужно гадать между своей моделью, телом из пака
        // и двумя десятками причёсок, и так же будет работать
        // для каждого следующего учителя.
        if (Selection.activeObject != null)
        {
            string picked = AssetDatabase.GetAssetPath(Selection.activeObject);

            if (!string.IsNullOrEmpty(picked)
                && picked.ToLowerInvariant().EndsWith(".fbx")
                && !Path.GetFileName(picked).Contains("@"))
            {
                return picked;
            }
        }

        string[] notBody = { "hair", "eyebrow", "beard", "buns", "buzzed", "parted" };

        List<string> candidates = AllModelPaths()
            .Where(p => !Path.GetFileName(p).Contains("@"))
            .Where(p =>
            {
                string name = Path.GetFileName(p).ToLowerInvariant();
                return !notBody.Any(name.Contains);
            })
            .ToList();

        string full = candidates.FirstOrDefault(
            p => Path.GetFileName(p).ToLowerInvariant().Contains("fullbody"));

        if (full != null) return full;

        return candidates
            .OrderByDescending(p => new FileInfo(p).Length)
            .FirstOrDefault();
    }

    private static List<string> FindAnimations()
    {
        return AllModelPaths().Where(p => Path.GetFileName(p).Contains("@")).ToList();
    }

    // ------------------------------------------------------------ импорт

    private static void MakeHumanoid(string path, bool isBody)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;

        bool changed = false;

        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }

        if (isBody)
        {
            importer.importAnimation = false;
        }
        else
        {
            importer.importAnimation = true;

            // Бег обязан зациклиться, иначе персонаж делает один шаг и замирает.
            bool shouldLoop = path.ToLowerInvariant().Contains("running")
                           || path.ToLowerInvariant().Contains("walking");

            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = shouldLoop;
                clips[i].lockRootHeightY = true;
            }

            if (clips.Length > 0)
            {
                importer.clipAnimations = clips;
                changed = true;
            }
        }

        if (changed || isBody) importer.SaveAndReimport();
    }

    // ------------------------------------------------------------ анимации

    private static AnimationClip ClipFrom(string path)
    {
        return AssetDatabase.LoadAllAssetsAtPath(path)
                            .OfType<AnimationClip>()
                            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    private static AnimationClip Pick(List<string> paths, params string[] keywords)
    {
        foreach (string keyword in keywords)
        {
            string hit = paths.FirstOrDefault(
                p => Path.GetFileName(p).ToLowerInvariant().Contains(keyword));

            if (hit != null) return ClipFrom(hit);
        }

        return null;
    }

    /// <summary>
    /// Четыре состояния: бежит, летит, подкат, упал. Если отдельного клипа
    /// подката ещё нет, состоянию временно ставится бег — механика не зависает,
    /// а художник может подставить свой Motion позже через Animator.
    /// </summary>
    private static AnimatorController BuildController(List<string> clipPaths, string characterId)
    {
        EnsureFolder(AnimatorFolder);

        // Контроллер отдельный для каждого героя. Пересборка Химички не
        // меняет контроллеры, префабы и анимации остальных персонажей.
        string path = AnimatorFolder + "/Character_" + characterId + ".controller";
        AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Sliding", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimationClip runClip = Pick(clipPaths, "running", "walking");
        AnimationClip airClip = Pick(clipPaths, "jumping", "falling");
        AnimationClip deadClip = Pick(clipPaths, "stumble", "dive", "falling to landing");

        AnimatorState run = sm.AddState("Run");
        run.motion = runClip;
        sm.defaultState = run;

        AnimatorState air = sm.AddState("Air");
        air.motion = airClip != null ? airClip : runClip;

        AnimatorState slide = sm.AddState("Slide");
        AnimationClip slideClip = Pick(clipPaths, "sliding", "slide", "soccer tackle", "tackle");
        slide.motion = slideClip != null ? slideClip : runClip;

        AnimatorState dead = sm.AddState("Dead");
        dead.motion = deadClip != null ? deadClip : airClip;

        Transition(run, air, "Grounded", false, 0.10f);
        Transition(air, run, "Grounded", true, 0.12f);
        Transition(run, slide, "Sliding", true, 0.04f);
        Transition(slide, run, "Sliding", false, 0.06f);

        // Смерть — из любого состояния: врезаться можно и на земле, и в воздухе.
        AnimatorStateTransition toDead = sm.AddAnyStateTransition(dead);
        toDead.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
        toDead.duration = 0.05f;
        toDead.hasExitTime = false;
        toDead.canTransitionToSelf = false;

        AnimatorStateTransition fromDead = dead.AddTransition(run);
        fromDead.AddCondition(AnimatorConditionMode.IfNot, 0f, "Dead");
        fromDead.duration = 0.05f;
        fromDead.hasExitTime = false;

        EditorUtility.SetDirty(controller);
        return controller;
    }

    /// <summary>
    /// Добавляет подкат в уже созданный контроллер и не трогает остальные
    /// состояния, клипы и переходы. Нужен для Новичка и Физрука, которые уже
    /// лежат в проекте и не должны пересобираться из FBX.
    /// </summary>
    private static bool EnsureSlideSupport(AnimatorController controller)
    {
        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState run = FindState(sm, "Run") ?? sm.defaultState;
        if (run == null) return false;

        bool changed = EnsureBoolParameter(controller, "Sliding");

        AnimatorState slide = FindState(sm, "Slide");
        if (slide == null)
        {
            slide = sm.AddState("Slide");
            slide.motion = run.motion;
            changed = true;
        }

        changed |= EnsureTransition(run, slide, "Sliding", true, 0.04f);
        changed |= EnsureTransition(slide, run, "Sliding", false, 0.06f);
        return changed;
    }

    private static AnimatorState FindState(AnimatorStateMachine stateMachine, string name)
    {
        foreach (ChildAnimatorState child in stateMachine.states)
        {
            if (child.state != null && child.state.name == name) return child.state;
        }

        return null;
    }

    private static bool EnsureBoolParameter(AnimatorController controller, string name)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
        {
            if (parameter.name == name) return false;
        }

        controller.AddParameter(name, AnimatorControllerParameterType.Bool);
        return true;
    }

    private static bool EnsureTransition(AnimatorState from, AnimatorState to,
                                         string parameter, bool value, float duration)
    {
        AnimatorConditionMode expected = value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot;
        foreach (AnimatorStateTransition existing in from.transitions)
        {
            if (existing.destinationState != to) continue;

            foreach (AnimatorCondition condition in existing.conditions)
            {
                if (condition.parameter == parameter && condition.mode == expected) return false;
            }
        }

        Transition(from, to, parameter, value, duration);
        return true;
    }

    private static void Transition(AnimatorState from, AnimatorState to,
                                   string parameter, bool value, float duration)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.AddCondition(value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
                       0f, parameter);
        t.duration = duration;
        t.hasExitTime = false;
    }

    // ------------------------------------------------------------ голова и рост

    /// <summary>
    /// Подгоняет модель под рост 2 юнита: на этот рост завязаны прыжок,
    /// подкат и высота крыш поездов.
    /// </summary>
    private static void ScaleToHeight(GameObject root, GameObject instance)
    {
        Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        if (bounds.size.y <= 0.01f) return;

        float scale = TargetHeight / bounds.size.y;
        instance.transform.localScale = Vector3.one * scale;

        // После масштабирования ноги могли уехать от нуля — прижимаем к полу.
        renderers = instance.GetComponentsInChildren<Renderer>();
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

        float footOffset = bounds.min.y - root.transform.position.y;
        instance.transform.localPosition -= new Vector3(0f, footOffset, 0f);
    }

    /// <summary>
    /// Вешает фотографию лица на кость головы. Если текстуры нет — просто
    /// пропускаем, тело всё равно рабочее.
    /// </summary>
    private static void AttachFace(Animator animator)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TextureFolder + "/Face_Rafael.png");
        if (texture == null) return;

        // Спрашивать кость головы можно только у humanoid-скелета,
        // иначе Unity ругается в консоль.
        if (!animator.isHuman) return;

        Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
        if (head == null) return;

        Material material = FaceMaterial(texture);

        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "FacePhoto";
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        plate.GetComponent<MeshRenderer>().sharedMaterial = material;

        plate.transform.SetParent(head, false);

        // Размер в системе координат кости, поэтому берём его от неё же.
        float unit = 1f / Mathf.Max(0.0001f, head.lossyScale.x);
        plate.transform.localScale = new Vector3(0.30f, 0.34f, 0.01f) * unit;
        plate.transform.localPosition = new Vector3(0f, 0.09f * unit, 0.10f * unit);
    }

    private static Material FaceMaterial(Texture2D texture)
    {
        string path = ProjectRoot + "/Materials/M_Char_Rigged_Face.mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);

        Material m = existing != null
            ? existing
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));

        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        if (existing == null) AssetDatabase.CreateAsset(m, path);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void AssignToCharacter(string characterAsset, string prefabPath)
    {
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(
            CharacterFolder + "/" + characterAsset + ".asset");

        if (data == null) return;

        var so = new SerializedObject(data);
        so.FindProperty("visualPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(data);
    }
}
