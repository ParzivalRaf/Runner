using UnityEngine;

/// <summary>
/// Превращает базовые игровые препятствия в читаемые школьные предметы.
/// Декорации не имеют коллайдеров и не меняют размеры опасной зоны — вся
/// механика прыжка, подката и поезда остаётся прежней.
/// </summary>
public static class SchoolObstacleVisuals
{
    private const string DetailsRootName = "SchoolDetails";
    private const int IgnoreRaycastLayer = 2;

    private static Material _blue;
    private static Material _yellow;
    private static Material _wood;
    private static Material _window;
    private static Material _metal;
    private static Material _cream;
    private static Material _terracotta;
    private static Material _cobalt;
    private static Material _teal;
    private static Material _gold;
    private static Material _ink;
    private static Material _leaf;

    public static void EnsureBuilt(Obstacle obstacle)
    {
        if (obstacle == null || obstacle.transform.Find(DetailsRootName) != null) return;

        EnsureMaterials();

        var root = new GameObject(DetailsRootName).transform;
        root.SetParent(obstacle.transform, false);

        HidePrototypeRenderers(obstacle, root);

        switch (obstacle.ObstacleKind)
        {
            case Obstacle.Kind.Block:
                BuildAuthoredModel(root, "CR_Locker");
                break;

            case Obstacle.Kind.JumpOver:
                BuildAuthoredModel(root, "HeroKit/CR_Barricade");
                break;

            case Obstacle.Kind.SlideUnder:
                BuildAuthoredModel(root, "CR_SlideGate");
                break;

            case Obstacle.Kind.Train:
                BuildAuthoredModel(root, "HeroKit/CR_CampusTrain");
                break;
        }
    }

    private static void HidePrototypeRenderers(Obstacle obstacle, Transform detailsRoot)
    {
        foreach (Renderer renderer in obstacle.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer.transform.IsChildOf(detailsRoot)) continue;
            renderer.enabled = false;
        }
    }

    private static void BuildAuthoredModel(Transform root, string resourceName)
    {
        GameObject source = Resources.Load<GameObject>("CampusRush/" + resourceName);
        if (source == null)
        {
            Debug.LogWarning("[SchoolObstacleVisuals] Missing Campus Rush model: " + resourceName);
            return;
        }

        GameObject model = Object.Instantiate(source, root);
        model.name = resourceName.Substring(resourceName.LastIndexOf('/') + 1);
        model.transform.localPosition = Vector3.zero;
        // Поворот берётся из CampusRushModels: базовый набор приходит ровно,
        // геройский надо доворачивать, пока он не переэкспортирован.
        model.transform.localRotation = CampusRushModels.AxisFix(resourceName);
        model.transform.localScale = Vector3.one;

        foreach (Transform node in model.GetComponentsInChildren<Transform>(true))
        {
            node.gameObject.layer = IgnoreRaycastLayer;
            Vector3 nodeScale = node.localScale;
            node.localScale = new Vector3(Mathf.Abs(nodeScale.x), Mathf.Abs(nodeScale.y), Mathf.Abs(nodeScale.z));
        }

        foreach (Collider collider in model.GetComponentsInChildren<Collider>(true))
            Object.Destroy(collider);

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = MaterialForPart(renderer.gameObject.name);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
        }
    }

    private static Material MaterialForPart(string partName)
    {
        if (partName.Contains("Cream")) return _cream;
        if (partName.Contains("White") || partName.Contains("Stone")) return _cream;
        if (partName.Contains("BrickLight")) return _terracotta;
        if (partName.Contains("Brick") || partName.Contains("Red")) return _terracotta;
        if (partName.Contains("Terracotta")) return _terracotta;
        if (partName.Contains("Cobalt")) return _cobalt;
        if (partName.Contains("Teal")) return _teal;
        if (partName.Contains("Gold")) return _gold;
        if (partName.Contains("Glass")) return _window;
        if (partName.Contains("Metal") || partName.Contains("Navy")) return _metal;
        if (partName.Contains("Wood")) return _wood;
        if (partName.Contains("Leaf") || partName.Contains("Green")) return _leaf;
        return _ink;
    }

    // -------------------------------------------------------------- шкафчик

    private static void BuildLocker(Transform root)
    {
        // Видимая игроку передняя сторона препятствия находится по -Z.
        Box(root, "LockerDoor", new Vector3(0f, 1.42f, -0.37f),
            new Vector3(1.48f, 2.56f, 0.05f), _blue);

        for (int i = -1; i <= 1; i++)
        {
            float x = i * 0.44f;
            Box(root, "LockerVent_" + i, new Vector3(x, 2.05f, -0.405f),
                new Vector3(0.24f, 0.05f, 0.02f), _metal);
        }

        Box(root, "LockerHandle", new Vector3(0.48f, 1.30f, -0.41f),
            new Vector3(0.10f, 0.28f, 0.03f), _yellow);
        Box(root, "LockerLabel", new Vector3(-0.36f, 2.48f, -0.41f),
            new Vector3(0.38f, 0.20f, 0.03f), _yellow);
    }

    // ---------------------------------------------------------------- парта

    private static void BuildDesk(Transform root)
    {
        // Низкий исходный барьер становится крышкой парты с четырьмя ножками.
        Box(root, "DeskTop", new Vector3(0f, 0.94f, 0f),
            new Vector3(1.95f, 0.16f, 0.95f), _wood);

        for (int x = -1; x <= 1; x += 2)
        {
            for (int z = -1; z <= 1; z += 2)
            {
                Box(root, "DeskLeg_" + x + "_" + z,
                    new Vector3(x * 0.75f, 0.43f, z * 0.30f),
                    new Vector3(0.12f, 0.86f, 0.12f), _metal);
            }
        }

        Box(root, "Notebook", new Vector3(-0.35f, 1.05f, -0.08f),
            new Vector3(0.55f, 0.05f, 0.36f), _yellow);
    }

    // ------------------------------------------------------------- балка-парта

    private static void BuildDeskBeam(Transform root)
    {
        // Основная балка меняет высоту в ConfigureSlideVariant. Маленькие
        // школьные детали стоят сбоку и не мешают ни обычному, ни высокому
        // варианту подката.
        Box(root, "DeskBadgeLeft", new Vector3(-0.64f, 1.48f, -0.38f),
            new Vector3(0.26f, 0.26f, 0.04f), _yellow);
        Box(root, "DeskBadgeRight", new Vector3(0.64f, 1.48f, -0.38f),
            new Vector3(0.26f, 0.26f, 0.04f), _yellow);
    }

    // --------------------------------------------------------------- вагон метро

    private static void BuildTrainWindows(Transform root)
    {
        // Поезд остаётся обычным поездом: только добавляем окна, чтобы вагон
        // не выглядел одним цветным прямоугольником. Крышу и триггер не трогаем.
        for (int i = 0; i < 3; i++)
        {
            float z = 2.0f + i * 3.0f;
            Box(root, "WindowLeft_" + i, new Vector3(-0.87f, 1.65f, z),
                new Vector3(0.04f, 0.82f, 1.75f), _window);
            Box(root, "WindowRight_" + i, new Vector3(0.87f, 1.65f, z),
                new Vector3(0.04f, 0.82f, 1.75f), _window);
        }

        Box(root, "TrainRearWindow", new Vector3(0f, 1.70f, -0.03f),
            new Vector3(1.18f, 0.82f, 0.04f), _window);
    }

    // -------------------------------------------------------------- служебное

    private static void Box(Transform parent, string name, Vector3 position,
                            Vector3 scale, Material material)
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

    private static void EnsureMaterials()
    {
        if (_blue != null) return;

        _blue = CreateMaterial(new Color(0.10f, 0.32f, 0.64f), 0.25f);
        _yellow = CreateMaterial(new Color(0.96f, 0.72f, 0.12f), 0.12f);
        _wood = CreateMaterial(new Color(0.42f, 0.21f, 0.08f));
        _window = CreateMaterial(new Color(0.05f, 0.20f, 0.28f), 0.35f);
        _metal = CreateMaterial(new Color(0.30f, 0.34f, 0.40f), 0.70f);
        _cream = CreateMaterial(new Color(0.92f, 0.82f, 0.66f));
        _terracotta = CreateMaterial(new Color(0.72f, 0.22f, 0.12f));
        _cobalt = CreateMaterial(new Color(0.05f, 0.25f, 0.62f));
        _teal = CreateMaterial(new Color(0.02f, 0.43f, 0.39f));
        _gold = CreateMaterial(new Color(0.96f, 0.59f, 0.10f), 0.08f);
        _ink = CreateMaterial(new Color(0.045f, 0.075f, 0.12f), 0.18f);
        _leaf = CreateMaterial(new Color(0.08f, 0.43f, 0.24f));
    }

    private static Material CreateMaterial(Color colour, float metallic = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
        if (material.HasProperty("_Color")) material.color = colour;
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.35f);
        material.enableInstancing = true;
        return material;
    }
}
