using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Собирает модель персонажа из фотографии лица и нескольких коробок.
///
/// Зачем так, а не полноценный скан: путь «фото → готовый персонаж в игре»
/// должен работать целиком и за минуту, иначе пятерых учителей не собрать
/// никогда. Скан головы, чистка в Blender и риг в Mixamo дают красивее,
/// но это часы на человека. Здесь — секунды, и результат уже узнаваемый.
///
/// Что получается: фигурка ростом ровно 2 юнита, пивот на полу — как
/// и требует поле Visual Prefab в ассете персонажа. Голова коробкой,
/// спереди на ней стоит пластина с фотографией и вырезанным фоном.
///
/// Модель статичная, без анимации. В игре сейчас анимации нет ни у кого,
/// так что это не шаг назад.
/// </summary>
public static class RunnerCharacterBuilder
{
    private const string TextureFolder = "Assets/_Project/Textures";
    private const string MaterialFolder = "Assets/_Project/Materials";
    private const string PrefabFolder = "Assets/_Project/Prefabs/Characters";
    private const string CharacterFolder = "Assets/_Project/Characters";

    // Пропорции. Общая высота ровно 2 — на неё завязаны прыжок и подкат.
    //
    // Голова намеренно великовата относительно тела. Причины две:
    // на экране телефона фигурка размером с ноготь, и реалистичная голова
    // превращается в пиксель, по которому никого не узнать;
    // и крупная голова просто смешнее, а игра про это.
    private const float TotalHeight = 2.0f;

    private const float LegHeight = 0.60f;
    private const float LegWidth = 0.18f;
    private const float LegSpread = 0.15f;

    private const float TorsoHeight = 0.68f;
    private const float TorsoWidth = 0.56f;
    private const float TorsoDepth = 0.32f;

    private const float ArmHeight = 0.58f;
    private const float ArmWidth = 0.14f;
    private const float ArmSpread = 0.35f;

    private const float HeadHeight = 0.72f;
    private const float HeadWidth = 0.62f;
    private const float HeadDepth = 0.55f;

    [MenuItem("Tools/Runner/Персонаж — собрать из фото")]
    public static void BuildRafael()
    {
        Build(
            characterAsset: "Character_rookie",
            faceTexture: "Face_Rafael",
            prefabName: "Char_Rafael",
            hair: new Color(0.24f, 0.18f, 0.14f),
            skin: new Color(0.68f, 0.51f, 0.47f),
            clothes: new Color(0.13f, 0.13f, 0.15f),
            legs: new Color(0.17f, 0.18f, 0.22f));
    }

    /// <summary>
    /// Собирает одну фигурку и сразу подставляет её в ассет персонажа.
    /// Для следующего учителя достаточно повторить вызов с другими именами.
    /// </summary>
    public static void Build(string characterAsset, string faceTexture, string prefabName,
                             Color hair, Color skin, Color clothes, Color legs)
    {
        string texPath = $"{TextureFolder}/{faceTexture}.png";
        var face = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        if (face == null)
        {
            EditorUtility.DisplayDialog("Runner",
                $"Не нашёл текстуру лица:\n{texPath}", "Ок");
            return;
        }

        PrepareTextureImport(texPath);

        Material mFace = MakeCutoutMaterial($"M_Char_{prefabName}_Face", face);
        Material mHair = MakeMaterial($"M_Char_{prefabName}_Hair", hair);
        Material mSkin = MakeMaterial($"M_Char_{prefabName}_Skin", skin);
        Material mCloth = MakeMaterial($"M_Char_{prefabName}_Cloth", clothes);
        Material mLegs = MakeMaterial($"M_Char_{prefabName}_Legs", legs);

        var root = new GameObject(prefabName);

        // Всё тело живёт внутри Rig, а не прямо в корне. Корень трогать нельзя:
        // его положение выставляет система персонажей. А Rig можно свободно
        // покачивать вверх-вниз на бегу.
        var rig = new GameObject("Rig");
        rig.transform.SetParent(root.transform, false);

        float legTop = LegHeight;
        float torsoTop = legTop + TorsoHeight;

        // Ноги. Узел стоит на уровне таза, а коробка свисает из него вниз —
        // иначе нога вращалась бы вокруг своей середины и болталась,
        // как стрелка компаса, вместо того чтобы шагать от бедра.
        Transform hipL = Pivot(rig, "Hip_L", new Vector3(-LegSpread, legTop, 0f));
        Transform hipR = Pivot(rig, "Hip_R", new Vector3(LegSpread, legTop, 0f));

        foreach (Transform hip in new[] { hipL, hipR })
        {
            Box(hip.gameObject, "Leg", mLegs,
                new Vector3(0f, -LegHeight * 0.5f, 0f),
                new Vector3(LegWidth, LegHeight, LegWidth));
        }

        // Туловище
        Box(rig, "Torso", mCloth,
            new Vector3(0f, legTop + TorsoHeight * 0.5f, 0f),
            new Vector3(TorsoWidth, TorsoHeight, TorsoDepth));

        // Руки. Тот же приём: узел на уровне плеча, рука свисает.
        // Кисти телесные — по ним на бегу и видно, что фигурка машет руками.
        float shoulderY = torsoTop - 0.06f;
        var shoulders = new Transform[2];

        for (int i = 0; i < 2; i++)
        {
            int side = i == 0 ? -1 : 1;
            string tag = side < 0 ? "L" : "R";

            Transform shoulder = Pivot(rig, $"Shoulder_{tag}",
                new Vector3(side * ArmSpread, shoulderY, 0f));
            shoulders[i] = shoulder;

            float armLen = ArmHeight * 0.78f;
            float handLen = ArmHeight * 0.22f;

            Box(shoulder.gameObject, $"Arm_{tag}", mCloth,
                new Vector3(0f, -armLen * 0.5f, 0f),
                new Vector3(ArmWidth, armLen, ArmWidth));

            Box(shoulder.gameObject, $"Hand_{tag}", mSkin,
                new Vector3(0f, -armLen - handLen * 0.5f, 0f),
                new Vector3(ArmWidth * 1.05f, handLen, ArmWidth * 1.05f));
        }

        // Голова.
        //
        // Отдельный пустой объект-держатель, а не сразу коробка: череп и лицо
        // должны крутиться вместе и вокруг шеи, а не каждый по себе.
        // Плюс у держателя единичный масштаб, поэтому лицо на нём
        // не растягивается вслед за пропорциями черепа.
        float headCenter = torsoTop + HeadHeight * 0.5f;

        Transform headPivot = Pivot(rig, "Head", new Vector3(0f, headCenter, 0f));

        Box(headPivot.gameObject, "Skull", mHair, Vector3.zero,
            new Vector3(HeadWidth, HeadHeight, HeadDepth));

        // Лицо — очень тонкая коробка, прижатая к переду головы.
        //
        // Не плоский Quad намеренно: у Quad одна видимая сторона, и если
        // ошибиться с разворотом, лицо окажется невидимым, а понять это
        // можно только запустив игру. Коробка видна с любой стороны,
        // ошибиться нечем.
        //
        // Игрок бежит в сторону +Z, поэтому «перёд» это +Z.
        Box(headPivot.gameObject, "Face", mFace,
            new Vector3(0f, 0.02f, HeadDepth * 0.5f + 0.006f),
            new Vector3(HeadWidth * 0.94f, HeadHeight * 0.92f, 0.012f));

        // Разворот головы к камере на ярких моментах — иначе лица
        // в забеге не видно вообще никогда.
        var turn = root.AddComponent<CharacterHeadTurn>();
        var turnSo = new SerializedObject(turn);
        turnSo.FindProperty("head").objectReferenceValue = headPivot;
        turnSo.ApplyModifiedPropertiesWithoutUndo();

        // Бег: махи руками и ногами, покачивание, поза в прыжке.
        var cycle = root.AddComponent<CharacterRunCycle>();
        var cycleSo = new SerializedObject(cycle);
        cycleSo.FindProperty("rig").objectReferenceValue = rig.transform;
        cycleSo.FindProperty("hipLeft").objectReferenceValue = hipL;
        cycleSo.FindProperty("hipRight").objectReferenceValue = hipR;
        cycleSo.FindProperty("shoulderLeft").objectReferenceValue = shoulders[0];
        cycleSo.FindProperty("shoulderRight").objectReferenceValue = shoulders[1];
        cycleSo.ApplyModifiedPropertiesWithoutUndo();

        // Папку могли ещё ни разу не создавать. Refresh нужен сразу:
        // без него Unity не знает о новой папке и не даст в неё сохранить.
        Directory.CreateDirectory(PrefabFolder);
        AssetDatabase.Refresh();

        string prefabPath = $"{PrefabFolder}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);

        AssignToCharacter(characterAsset, prefabPath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Runner",
            $"Готово.\n\nМодель: {prefabPath}\n" +
            $"Подставлена в: {characterAsset}\n\n" +
            "Жми Play и выбирай этого персонажа.", "Ок");
    }

    // ------------------------------------------------------------ мелочи

    private static void PrepareTextureImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.maxTextureSize = 512;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Пустой узел с единичным масштабом. Нужен там, где что-то должно
    /// вращаться: у коробки масштаб неединичный, и всё, что к ней прицеплено,
    /// растянулось бы вслед за ней.
    /// </summary>
    private static Transform Pivot(GameObject parent, string name, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = localPosition;
        return go.transform;
    }

    private static GameObject Box(GameObject parent, string name, Material material,
                                  Vector3 center, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;

        // Коллайдеров на модели быть не должно: столкновения считает
        // отдельный коллайдер на самом игроке, а лишние только мешают
        // лучу, которым игрок ищет пол под ногами.
        Object.DestroyImmediate(go.GetComponent<Collider>());

        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    private static Material MakeMaterial(string name, Color color)
    {
        Material m = LoadOrCreate(name);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", 0.12f);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material MakeCutoutMaterial(string name, Texture2D texture)
    {
        Material m = LoadOrCreate(name);
        m.SetColor("_BaseColor", Color.white);
        m.SetTexture("_BaseMap", texture);
        m.SetFloat("_Smoothness", 0.05f);

        // Отсечение по прозрачности, а не полупрозрачность: вырезанный фон
        // либо есть, либо нет, полутона тут не нужны. Такой режим дешевле
        // на телефоне и не создаёт проблем с порядком отрисовки.
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;

        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material LoadOrCreate(string name)
    {
        Directory.CreateDirectory(MaterialFolder);
        string path = $"{MaterialFolder}/{name}.mat";

        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var created = new Material(shader);
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    /// <summary>
    /// Кладёт готовый префаб в поле Visual Prefab ассета персонажа.
    /// Поле приватное, поэтому идём через SerializedObject — так же,
    /// как это делает сам инспектор.
    /// </summary>
    private static void AssignToCharacter(string characterAsset, string prefabPath)
    {
        string path = $"{CharacterFolder}/{characterAsset}.asset";
        var data = AssetDatabase.LoadAssetAtPath<CharacterData>(path);

        if (data == null)
        {
            Debug.LogWarning($"[RunnerCharacterBuilder] Не нашёл персонажа: {path}");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        var so = new SerializedObject(data);
        so.FindProperty("visualPrefab").objectReferenceValue = prefab;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(data);
    }
}
