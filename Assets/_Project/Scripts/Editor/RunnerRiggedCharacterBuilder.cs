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
///   3. делает контроллер анимаций: бег, полёт, падение при смерти;
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

    [MenuItem("Tools/Runner/Персонаж — собрать с анимацией")]
    public static void BuildRigged()
    {
        string bodyPath = FindBody();

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
        AnimatorController controller = BuildController(clipPaths);

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

        AttachFace(animator);

        var driver = root.AddComponent<CharacterAnimatorDriver>();
        var so = new SerializedObject(driver);
        so.FindProperty("animator").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();

        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        string prefabPath = PrefabFolder + "/Char_Rigged.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssignToCharacter("Character_rookie", prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Runner",
            "Готово.\n\nТело: " + Path.GetFileName(bodyPath) +
            "\nАнимаций подключено: " + clipPaths.Count +
            "\n\nМодель: " + prefabPath +
            "\nПодставлена в Новичка.\n\nЖми Play.", "Ок");
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
    /// Три состояния и ничего лишнего: бежит, летит, упал.
    ///
    /// Больше состояний сейчас не нужно — подкат в игре делается сжатием
    /// фигурки, а не отдельной анимацией, и отдельный клип на него только
    /// рассинхронизировался бы с этим сжатием.
    /// </summary>
    private static AnimatorController BuildController(List<string> clipPaths)
    {
        Directory.CreateDirectory(AnimatorFolder);
        AssetDatabase.Refresh();

        string path = AnimatorFolder + "/Character.controller";
        AssetDatabase.DeleteAsset(path);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimationClip runClip = Pick(clipPaths, "running", "walking");
        AnimationClip airClip = Pick(clipPaths, "jumping", "falling");
        AnimationClip deadClip = Pick(clipPaths, "stumble", "dive", "falling to landing");

        AnimatorState run = sm.AddState("Run");
        run.motion = runClip;
        sm.defaultState = run;

        AnimatorState air = sm.AddState("Air");
        air.motion = airClip != null ? airClip : runClip;

        AnimatorState dead = sm.AddState("Dead");
        dead.motion = deadClip != null ? deadClip : airClip;

        Transition(run, air, "Grounded", false, 0.10f);
        Transition(air, run, "Grounded", true, 0.12f);

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
