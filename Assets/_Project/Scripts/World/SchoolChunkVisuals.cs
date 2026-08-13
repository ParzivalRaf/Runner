using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Собирает лёгкое школьное окружение из простых 3D-форм. Важно, что оно
/// строится ОДИН раз на экземпляр чанка: после этого чанк с декорациями
/// переиспользуется ObjectPool, а не создаёт геометрию во время забега.
///
/// Обычный чанк становится школьным коридором, Pillars — спортзалом,
/// Arches — библиотекой. Все объекты здесь только визуальные и не имеют
/// коллайдеров, поэтому не могут перекрыть дорожку или сломать прыжок.
/// </summary>
public static class SchoolChunkVisuals
{
    private const string DecorRootName = "SchoolDecor";
    private const int IgnoreRaycastLayer = 2;

    private static Material _wall;
    private static Material _lockerBlue;
    private static Material _lockerRed;
    private static Material _wood;
    private static Material _chalk;
    private static Material _notice;
    private static Material _metal;
    private static Material _bookGold;
    private static Material _bookGreen;
    private static Material _bookRed;
    private static Material _cityPurple;
    private static Material _cityCyan;
    private static Material _wallTrim;
    private static Material _neonPink;
    private static Material _neonCyan;

    // Настоящие CC0-модели лежат в Resources, чтобы рантайм мог безопасно
    // подставлять их в уже существующие пуловые чанки. Список читается один
    // раз за запуск, а не на каждом новом куске трассы.
    private static GameObject[] _cityBuildings;
    private static GameObject _largeTree;
    private static GameObject _smallTree;
    private static GameObject _campusBuildingA;
    private static GameObject _campusBuildingB;
    private static GameObject _clockTower;
    private static GameObject _campusTree;
    private static GameObject _campusBanner;

    /// <summary>Добавляет тему к конкретной копии чанка, если её там ещё нет.</summary>
    public static void EnsureBuilt(Chunk chunk)
    {
        if (chunk == null || chunk.transform.Find(DecorRootName) != null) return;

        EnsureMaterials();

        var root = new GameObject(DecorRootName).transform;
        root.SetParent(chunk.transform, false);

        BuildSharedCorridor(root);

        // The old hallway/gym/library primitives formed a canyon of blank
        // boxes. The hero direction is an open rooftop campus, so variation
        // now comes from authored buildings, landscaping and route props.

        // Дальняя линия города делает мир больше школьного коридора. Она
        // стоит далеко за стенами, не имеет коллайдеров и не закрывает обзор
        // с крыши поезда.
        BuildCityBackdrop(root);
    }

    // -------------------------------------------------------------- общий вид

    private static void BuildSharedCorridor(Transform root)
    {
        // Раньше здесь стояли сплошные шестиметровые стены. Они превращали
        // забег в узкий картонный коридор и полностью прятали город. Теперь
        // это открытая эстакада кампуса: низкое ограждение безопасно отделяет
        // трассу, а через него видны здания, небо и световые вывески.
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * 6.78f;
            Box(root, "GuardBase_" + side, new Vector3(x, 0.52f, 15f),
                new Vector3(0.42f, 1.04f, 30f), _bookGreen);
            Box(root, "GuardTop_" + side, new Vector3(x, 1.08f, 15f),
                new Vector3(0.58f, 0.16f, 30f), _wall);

            for (int i = 0; i < 4; i++)
            {
                float z = 3.5f + i * 8f;
                Box(root, "GuardPost_" + side + "_" + i, new Vector3(x, 1.18f, z),
                    new Vector3(0.52f, 1.35f, 0.52f), _bookGreen);
                Box(root, "GuardCap_" + side + "_" + i, new Vector3(x, 1.88f, z),
                    new Vector3(0.62f, 0.15f, 0.62f), _bookGold);
            }
        }

        // Authored, rounded props break up the repeated box geometry and
        // establish the friendly campus identity without obstructing lanes.
        BuildCampusProp(root, ArtRole.Planter, new Vector3(-7.72f, 0f, 4.4f),
            Quaternion.Euler(0f, 18f, 0f), 0.80f);
        BuildCampusProp(root, ArtRole.Bench, new Vector3(7.78f, 0f, 8.5f),
            Quaternion.Euler(0f, -90f, 0f), 0.82f);
        BuildCampusProp(root, ArtRole.Lamp, new Vector3(-7.78f, 0f, 17.2f),
            Quaternion.Euler(0f, 0f, 0f), 0.82f);
        BuildCampusProp(root, ArtRole.Planter, new Vector3(7.72f, 0f, 25.0f),
            Quaternion.Euler(0f, -22f, 0f), 0.74f);
    }

    // --------------------------------------------------------------- коридор

    private static void BuildHallway(Transform root)
    {
        BuildSign(root, "HALLWAY", new Vector3(0f, 5.15f, 2.0f), 0.42f, _notice);

        for (int i = 0; i < 3; i++)
        {
            float z = 5f + i * 9.5f;
            BuildLockerBank(root, -1, z, i % 2 == 0 ? _lockerBlue : _lockerRed);
            BuildLockerBank(root, 1, z, i % 2 == 0 ? _lockerRed : _lockerBlue);
        }

        BuildClassroomDoor(root, -1, 10f, "101");
        BuildClassroomDoor(root, 1, 20f, "102");
        BuildNoticeBoard(root, -1, 23.5f);
        BuildNoticeBoard(root, 1, 12.5f);
    }

    private static void BuildLockerBank(Transform root, int side, float z, Material colour)
    {
        float x = side * 8.05f;
        float frontX = side * 7.18f;
        string prefix = side < 0 ? "LockerL_" : "LockerR_";

        Box(root, prefix + z, new Vector3(x, 1.65f, z),
            new Vector3(1.65f, 3.3f, 2.4f), colour);

        // Три дверцы, швы и ручки превращают один куб в понятный шкафчик.
        for (int door = -1; door <= 1; door++)
        {
            float doorZ = z + door * 0.72f;
            Box(root, prefix + "Door_" + door + "_" + z,
                new Vector3(frontX, 1.72f, doorZ), new Vector3(0.06f, 2.85f, 0.62f), colour);
            Box(root, prefix + "Handle_" + door + "_" + z,
                new Vector3(frontX - side * 0.06f, 1.72f, doorZ + 0.18f),
                new Vector3(0.08f, 0.16f, 0.08f), _metal);
        }
    }

    private static void BuildClassroomDoor(Transform root, int side, float z, string room)
    {
        float x = side * 8.10f;
        Box(root, "Door_" + room, new Vector3(x, 2.05f, z),
            new Vector3(0.10f, 4.1f, 2.25f), _wood);
        Box(root, "DoorWindow_" + room, new Vector3(side * 8.02f, 2.75f, z),
            new Vector3(0.04f, 0.9f, 0.68f), _notice);
        BuildSideSign(root, room, side, z + 1.28f, 3.95f);
    }

    private static void BuildNoticeBoard(Transform root, int side, float z)
    {
        float x = side * 8.07f;
        Box(root, "NoticeFrame_" + side + "_" + z, new Vector3(x, 3.4f, z),
            new Vector3(0.10f, 2.15f, 2.55f), _wood);
        Box(root, "NoticeBoard_" + side + "_" + z, new Vector3(side * 7.99f, 3.4f, z),
            new Vector3(0.04f, 1.78f, 2.18f), _chalk);

        for (int i = 0; i < 3; i++)
        {
            Material paper = i == 1 ? _bookGold : _notice;
            Box(root, "Flyer_" + side + "_" + i + "_" + z,
                new Vector3(side * 7.95f, 3.35f + i * 0.32f, z - 0.62f + i * 0.52f),
                new Vector3(0.02f, 0.42f, 0.50f), paper);
        }
    }

    // ---------------------------------------------------------------- спортзал

    private static void BuildGym(Transform root)
    {
        BuildSign(root, "GYM", new Vector3(0f, 5.15f, 2.0f), 0.55f, _bookGold);

        BuildScoreboard(root, -1, 15f);
        BuildScoreboard(root, 1, 15f);
        BuildHoop(root, -1, 6.5f);
        BuildHoop(root, 1, 23.5f);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int row = 0; row < 2; row++)
            {
                float z = 7f + row * 16f;
                BuildBleachers(root, side, z);
            }
        }
    }

    private static void BuildScoreboard(Transform root, int side, float z)
    {
        float x = side * 8.06f;
        Box(root, "Scoreboard_" + side, new Vector3(x, 4.45f, z),
            new Vector3(0.10f, 1.35f, 2.75f), _chalk);
        BuildSideSign(root, "24 : 24", side, z, 4.40f);
    }

    private static void BuildHoop(Transform root, int side, float z)
    {
        float x = side * 8.06f;
        Box(root, "Backboard_" + side + "_" + z, new Vector3(x, 3.75f, z),
            new Vector3(0.10f, 1.55f, 1.8f), _notice);

        // Четыре планки — дешёвое, но хорошо читаемое кольцо без сложной сетки.
        float innerX = side * 7.86f;
        Box(root, "HoopTop_" + side + "_" + z, new Vector3(innerX, 3.25f, z),
            new Vector3(0.10f, 0.08f, 0.95f), _lockerRed);
        Box(root, "HoopBottom_" + side + "_" + z, new Vector3(innerX, 2.75f, z),
            new Vector3(0.10f, 0.08f, 0.95f), _lockerRed);
        Box(root, "HoopFront_" + side + "_" + z, new Vector3(innerX, 3.0f, z - 0.44f),
            new Vector3(0.10f, 0.55f, 0.08f), _lockerRed);
        Box(root, "HoopBack_" + side + "_" + z, new Vector3(innerX, 3.0f, z + 0.44f),
            new Vector3(0.10f, 0.55f, 0.08f), _lockerRed);
    }

    private static void BuildBleachers(Transform root, int side, float z)
    {
        float x = side * 7.95f;
        for (int step = 0; step < 3; step++)
        {
            float height = 0.42f + step * 0.42f;
            float stepX = x + side * step * 0.32f;
            Box(root, "Bleacher_" + side + "_" + z + "_" + step,
                new Vector3(stepX, height * 0.5f, z),
                new Vector3(1.25f, height, 4.1f), _wood);
        }
    }

    // --------------------------------------------------------------- библиотека

    private static void BuildLibrary(Transform root)
    {
        BuildSign(root, "LIBRARY", new Vector3(0f, 5.15f, 2.0f), 0.42f, _bookGreen);

        for (int i = 0; i < 3; i++)
        {
            float z = 5f + i * 9.5f;
            BuildBookshelf(root, -1, z);
            BuildBookshelf(root, 1, z);
        }

        BuildChalkboard(root, -1, 15f, "QUIET");
        BuildChalkboard(root, 1, 15f, "READ");
    }

    private static void BuildBookshelf(Transform root, int side, float z)
    {
        float x = side * 8.05f;
        float frontX = side * 7.17f;
        string prefix = side < 0 ? "ShelfL_" : "ShelfR_";

        Box(root, prefix + z, new Vector3(x, 2.15f, z),
            new Vector3(1.65f, 4.3f, 2.45f), _wood);

        for (int row = 0; row < 3; row++)
        {
            float y = 0.8f + row * 1.12f;
            Box(root, prefix + "Board_" + row + "_" + z,
                new Vector3(frontX, y, z), new Vector3(0.07f, 0.09f, 2.18f), _bookGold);

            for (int book = 0; book < 3; book++)
            {
                Material colour = (row + book) % 3 == 0 ? _bookRed :
                                  (row + book) % 3 == 1 ? _bookGreen : _bookGold;
                Box(root, prefix + "Book_" + row + "_" + book + "_" + z,
                    new Vector3(frontX - side * 0.05f, y + 0.28f, z - 0.62f + book * 0.62f),
                    new Vector3(0.09f, 0.50f + 0.07f * book, 0.38f), colour);
            }
        }
    }

    private static void BuildChalkboard(Transform root, int side, float z, string text)
    {
        float x = side * 8.08f;
        Box(root, "ChalkFrame_" + side + "_" + z, new Vector3(x, 3.5f, z),
            new Vector3(0.10f, 2.2f, 2.8f), _wood);
        Box(root, "Chalk_" + side + "_" + z, new Vector3(side * 8.00f, 3.5f, z),
            new Vector3(0.04f, 1.85f, 2.45f), _chalk);
        BuildSideSign(root, text, side, z, 3.5f);
    }

    // --------------------------------------------------------- городской фон

    private static void BuildCityBackdrop(Transform root)
    {
        EnsureCityModels();
        if (_campusBuildingA == null || _campusBuildingB == null) return;

        int variant = Mathf.Abs(root.parent.GetInstanceID()) % 3;
        for (int side = -1; side <= 1; side += 2)
        {
            for (int slot = 0; slot < 1; slot++)
            {
                bool first = ((slot + side + variant) & 1) == 0;
                GameObject source = first ? _campusBuildingA : _campusBuildingB;
                ArtRole role = first ? ArtRole.BuildingA : ArtRole.BuildingB;
                float z = 17.0f;
                BuildHeroProp(root, source, role,
                    new Vector3(side * 14.5f, 0f, z),
                    Quaternion.Euler(0f, side < 0 ? -90f : 90f, 0f),
                    0.78f);
            }

            BuildHeroProp(root, _campusTree, ArtRole.Tree, new Vector3(side * 9.5f, 0f, 5.8f),
                Quaternion.Euler(0f, side * 18f, 0f), 1.22f);
            BuildHeroProp(root, _campusTree, ArtRole.Tree, new Vector3(side * 10.0f, 0f, 24.0f),
                Quaternion.Euler(0f, side * -22f, 0f), 1.08f);
            BuildCampusFlag(root, side, 7.0f);
            BuildCampusFlag(root, side, 22.5f);
        }

        if (variant == 0)
            BuildHeroProp(root, _clockTower, ArtRole.ClockTower, new Vector3(14.5f, 0f, 25f),
                Quaternion.Euler(0f, 90f, 0f), 0.82f);
    }

    private static void BuildNeonBillboard(Transform root, int side, float z, string text, Material neon)
    {
        float x = side * 7.35f;
        Box(root, "BillboardBack_" + text + "_" + side, new Vector3(x, 4.15f, z),
            new Vector3(0.10f, 1.45f, 3.6f), _wallTrim);
        Box(root, "BillboardTop_" + text + "_" + side, new Vector3(side * 7.27f, 4.88f, z),
            new Vector3(0.035f, 0.06f, 3.4f), neon);
        Box(root, "BillboardBottom_" + text + "_" + side, new Vector3(side * 7.27f, 3.42f, z),
            new Vector3(0.035f, 0.06f, 3.4f), neon);

        GameObject label = new GameObject("BillboardText_" + text);
        label.layer = IgnoreRaycastLayer;
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(side * 7.26f, 4.16f, z);
        label.transform.localRotation = Quaternion.Euler(0f, side < 0 ? -90f : 90f, 0f);

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = text.Length > 8 ? 0.26f : 0.42f;
        textMesh.fontSize = 56;
        textMesh.color = neon.color;
    }

    private static void EnsureCityModels()
    {
        if (_cityBuildings != null) return;

        GameObject[] all = Resources.LoadAll<GameObject>("RunnerVisuals/City");
        var buildings = new List<GameObject>();

        for (int i = 0; i < all.Length; i++)
        {
            GameObject source = all[i];
            if (source == null) continue;

            if (source.name.StartsWith("building-")) buildings.Add(source);
        }

        _cityBuildings = buildings.ToArray();
        _largeTree = Resources.Load<GameObject>("RunnerVisuals/City/tree-large");
        _smallTree = Resources.Load<GameObject>("RunnerVisuals/City/tree-small");
        _campusBuildingA = CampusRushModels.Load(ArtRole.BuildingA);
        _campusBuildingB = CampusRushModels.Load(ArtRole.BuildingB);
        _clockTower = CampusRushModels.Load(ArtRole.ClockTower);
        _campusTree = CampusRushModels.Load(ArtRole.Tree);
        _campusBanner = CampusRushModels.Load(ArtRole.Banner);
    }

    private static void BuildHeroProp(Transform root, GameObject source, ArtRole role,
                                      Vector3 position, Quaternion rotation, float scale)
    {
        if (source == null) return;
        GameObject instance = Object.Instantiate(source, root);
        instance.name = source.name;
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation * CampusRushModels.AxisFix(role);
        instance.transform.localScale = Vector3.one * scale;
        foreach (Transform node in instance.GetComponentsInChildren<Transform>(true))
        {
            node.gameObject.layer = IgnoreRaycastLayer;
            Vector3 nodeScale = node.localScale;
            node.localScale = new Vector3(Mathf.Abs(nodeScale.x), Mathf.Abs(nodeScale.y), Mathf.Abs(nodeScale.z));
        }
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            // Покраска по именам частей — только для Original: у чужих
            // наборов другие имена, и она сделала бы их одноцветными.
            if (!CampusRushModels.UseImportedMaterials)
                renderer.sharedMaterial = CampusMaterialForPart(renderer.gameObject.name);
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static void BuildCampusFlag(Transform root, int side, float z)
    {
        float x = side * 6.52f;
        Box(root, "FlagPole_" + side + "_" + z, new Vector3(x, 2.35f, z),
            new Vector3(0.11f, 4.7f, 0.11f), _metal);
        Box(root, "FlagCap_" + side + "_" + z, new Vector3(x, 4.78f, z),
            new Vector3(0.24f, 0.24f, 0.24f), _bookGold);
        Box(root, "FlagPanel_" + side + "_" + z,
            new Vector3(side * 6.39f, 3.55f, z + 0.58f),
            new Vector3(0.06f, 1.65f, 1.02f), _lockerBlue);
        Box(root, "FlagGoldTop_" + side + "_" + z,
            new Vector3(side * 6.35f, 4.32f, z + 0.58f),
            new Vector3(0.05f, 0.10f, 1.08f), _bookGold);
    }

    /// <summary>
    /// Backdrop props keep their authored multi-material colour blocking. The
    /// earlier single tint flattened every facade into a dark silhouette.
    /// </summary>
    private static void TintBackdrop(GameObject root, Material material)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = IgnoreRaycastLayer;

        foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);

        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.sharedMaterial == null)
                renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static void BuildCampusProp(Transform root, ArtRole role,
                                        Vector3 position, Quaternion rotation, float scale)
    {
        GameObject source = CampusRushModels.Load(role);
        if (source == null) return;

        GameObject instance = Object.Instantiate(source, root);
        instance.name = role.ToString();
        instance.layer = IgnoreRaycastLayer;
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation * CampusRushModels.AxisFix(role);
        instance.transform.localScale = Vector3.one * scale;

        foreach (Transform node in instance.GetComponentsInChildren<Transform>(true))
            node.gameObject.layer = IgnoreRaycastLayer;

        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);

        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (!CampusRushModels.UseImportedMaterials)
                renderer.sharedMaterial = CampusMaterialForPart(renderer.gameObject.name);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static Material CampusMaterialForPart(string partName)
    {
        if (partName.Contains("Cream")) return _wall;
        if (partName.Contains("White") || partName.Contains("Stone")) return _wall;
        if (partName.Contains("BrickLight")) return _lockerRed;
        if (partName.Contains("Brick") || partName.Contains("Red")) return _lockerRed;
        if (partName.Contains("Terracotta")) return _lockerRed;
        if (partName.Contains("Cobalt")) return _lockerBlue;
        if (partName.Contains("Teal")) return _bookGreen;
        if (partName.Contains("Gold")) return _bookGold;
        if (partName.Contains("Glass")) return _cityCyan;
        if (partName.Contains("Metal") || partName.Contains("Navy")) return _chalk;
        if (partName.Contains("Wood")) return _wood;
        if (partName.Contains("Leaf") || partName.Contains("Green")) return _bookGreen;
        return _chalk;
    }

    // -------------------------------------------------------------- примитивы

    private static void BuildSign(Transform root, string text, Vector3 position, float size, Material colour)
    {
        // Таблички живут на стене, а не висят над дорожкой. Иначе на крыше
        // поезда камера смотрела бы прямо сквозь них.
        const int side = 1;
        float wallX = side * 7.14f;
        Box(root, "Sign_" + text, new Vector3(wallX, position.y, position.z),
            new Vector3(0.04f, 0.78f, 3.5f), _chalk);

        GameObject label = new GameObject("SignText_" + text);
        label.layer = IgnoreRaycastLayer;
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(side * 7.08f, position.y, position.z);
        label.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = size * 0.25f;
        textMesh.fontSize = 36;
        textMesh.color = colour.color;
    }

    private static void BuildSideSign(Transform root, string text, int side, float z, float y)
    {
        GameObject label = new GameObject("Label_" + text + "_" + side + "_" + z);
        label.layer = IgnoreRaycastLayer;
        label.transform.SetParent(root, false);
        label.transform.localPosition = new Vector3(side * 7.08f, y, z);
        label.transform.localRotation = Quaternion.Euler(0f, side < 0 ? -90f : 90f, 0f);

        TextMesh textMesh = label.AddComponent<TextMesh>();
        textMesh.text = text;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = 0.08f;
        textMesh.fontSize = 32;
        textMesh.color = _notice.color;
    }

    private static void Box(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.layer = IgnoreRaycastLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Object.Destroy(collider);

        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // --------------------------------------------------------------- палитра

    private static void EnsureMaterials()
    {
        if (_wall != null) return;

        _wall = CreateMaterial(new Color(0.92f, 0.82f, 0.66f), 0.02f);
        _wallTrim = CreateMaterial(new Color(0.78f, 0.63f, 0.45f), 0.04f);
        _lockerBlue = CreateMaterial(new Color(0.05f, 0.25f, 0.62f), 0.06f);
        _lockerRed = CreateMaterial(new Color(0.72f, 0.22f, 0.12f), 0.03f);
        _wood = CreateMaterial(new Color(0.39f, 0.18f, 0.08f), 0.02f);
        _chalk = CreateMaterial(new Color(0.045f, 0.075f, 0.12f), 0.06f);
        _notice = CreateMaterial(new Color(0.96f, 0.59f, 0.10f), 0.04f);
        _metal = CreateMaterial(new Color(0.18f, 0.25f, 0.31f), 0.35f);
        _bookGold = CreateMaterial(new Color(0.96f, 0.59f, 0.10f), 0.04f);
        _bookGreen = CreateMaterial(new Color(0.02f, 0.43f, 0.39f), 0.04f);
        _bookRed = CreateMaterial(new Color(0.72f, 0.22f, 0.12f), 0.03f);
        _cityPurple = CreateMaterial(new Color(0.46f, 0.29f, 0.31f), 0.04f);
        _cityCyan = CreateMaterial(new Color(0.18f, 0.38f, 0.45f), 0.05f);
        _neonPink = CreateMaterial(new Color(0.96f, 0.59f, 0.10f), 0.04f,
            new Color(0.96f, 0.50f, 0.12f), 1.15f);
        _neonCyan = CreateMaterial(new Color(0.02f, 0.43f, 0.39f), 0.04f,
            new Color(0.02f, 0.38f, 0.34f), 1.10f);
    }

    private static Material CreateMaterial(Color colour, float metallic = 0f,
                                           Color? emission = null, float emissionIntensity = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color")) material.color = colour;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        if (emission.HasValue && emissionIntensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
                material.SetColor("_EmissionColor", emission.Value * emissionIntensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        material.enableInstancing = true;
        return material;
    }
}
