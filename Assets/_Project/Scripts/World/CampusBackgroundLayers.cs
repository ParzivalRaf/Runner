using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A camera-relative, three-depth campus skyline. It stays beyond gameplay
/// chunks and follows only a fraction of lateral camera motion, producing
/// stable parallax instead of repeating the same buildings every 30 metres.
/// </summary>
public sealed class CampusBackgroundLayers : MonoBehaviour
{
    private const int BackgroundLayer = 2;

    [SerializeField] private Transform target;
    [SerializeField] private float nearDistance = 72f;
    [SerializeField] private float middleDistance = 98f;
    [SerializeField] private float farDistance = 128f;

    private Transform _root;
    private Transform _near;
    private Transform _middle;
    private Transform _far;

    private static Material _brick;
    private static Material _brickLight;
    private static Material _cream;
    private static Material _teal;
    private static Material _glass;
    private static Material _tree;
    private static Material _treeLight;
    private static Material _skylineA;
    private static Material _skylineB;
    private static Material _cloud;

    private void Awake()
    {
        if (target == null)
        {
            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null) target = player.transform;
        }
        Build();
    }

    private void LateUpdate()
    {
        if (_root == null || target == null) return;
        float z = target.position.z;
        _near.localPosition = new Vector3(-target.position.x * 0.08f, 0f, z + nearDistance);
        _middle.localPosition = new Vector3(-target.position.x * 0.045f, 0f, z + middleDistance);
        _far.localPosition = new Vector3(-target.position.x * 0.018f, 0f, z + farDistance);
    }

    private void Build()
    {
        EnsureMaterials();
        _root = new GameObject("CampusBackgroundLayers").transform;
        // Keep the skyline in world space. Parenting it to a pitched/shaking
        // camera would rotate buildings and clouds together with the lens.
        _root.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _near = Layer("NearCampus");
        _middle = Layer("MiddleDistrict");
        _far = Layer("FarSkyline");

        BuildNearCampus();
        BuildMiddleDistrict();
        BuildFarSkyline();
    }

    private void OnDestroy()
    {
        if (_root != null) Destroy(_root.gameObject);
    }

    private Transform Layer(string name)
    {
        Transform layer = new GameObject(name).transform;
        layer.SetParent(_root, false);
        return layer;
    }

    private void BuildNearCampus()
    {
        GameObject buildingA = Resources.Load<GameObject>("CampusRush/HeroKit/CR_CampusBuilding_A");
        GameObject buildingB = Resources.Load<GameObject>("CampusRush/HeroKit/CR_CampusBuilding_B");
        GameObject tower = Resources.Load<GameObject>("CampusRush/HeroKit/CR_ClockTower");
        GameObject tree = Resources.Load<GameObject>("CampusRush/HeroKit/CR_CampusTree");

        Hero(_near, buildingA, new Vector3(-20f, -2f, 5f), Quaternion.Euler(0, 20, 0), 1.45f);
        Hero(_near, buildingB, new Vector3(19f, -2f, 9f), Quaternion.Euler(0, -24, 0), 1.38f);
        Hero(_near, tower, new Vector3(11f, -2f, 21f), Quaternion.Euler(0, 0, 0), 1.32f);

        for (int side = -1; side <= 1; side += 2)
        {
            for (int i = 0; i < 4; i++)
            {
                float x = side * (10.5f + i * 4.6f);
                Hero(_near, tree, new Vector3(x, -1.8f, 2f + i * 5f),
                    Quaternion.Euler(0, i * 37f, 0), 1.15f - i * 0.08f);
            }
        }
    }

    private void BuildMiddleDistrict()
    {
        // Warm mid-rise buildings form the second readable band behind trees.
        for (int i = -5; i <= 5; i++)
        {
            if (i == 0) continue;
            float x = i * 6.1f;
            float height = 7.5f + Mathf.Abs((i * 37) % 5) * 1.35f;
            float width = 4.5f + Mathf.Abs((i * 17) % 3) * 0.75f;
            Material body = (i & 1) == 0 ? _brick : _brickLight;
            Box(_middle, "District_" + i, new Vector3(x, height * 0.5f - 2.5f, 4f + Mathf.Abs(i % 3) * 3f),
                new Vector3(width, height, 4.2f), body, 0.20f);
            Box(_middle, "Cornice_" + i, new Vector3(x, height - 2.35f, 4f + Mathf.Abs(i % 3) * 3f),
                new Vector3(width + .35f, .35f, 4.55f), _cream, .08f);

            for (int row = 0; row < 3; row++)
                for (int col = -1; col <= 1; col++)
                    Box(_middle, "Window_" + i + "_" + row + "_" + col,
                        new Vector3(x + col * width * .25f, 1.0f + row * 1.65f,
                            1.82f + Mathf.Abs(i % 3) * 3f),
                        new Vector3(width * .13f, .78f, .08f), _glass, .025f);
        }

        // Tree-canopy ribbon separates the architecture bands like the hero art.
        for (int i = -11; i <= 11; i++)
        {
            float x = i * 3.35f;
            Sphere(_middle, "Canopy_" + i,
                new Vector3(x, 5.7f + Mathf.Abs(i % 3) * .38f, -1f + Mathf.Abs(i % 4)),
                new Vector3(4.0f, 3.0f, 2.55f), (i & 1) == 0 ? _tree : _treeLight);
        }
    }

    private void BuildFarSkyline()
    {
        // Cool desaturated towers sit inside atmospheric perspective.
        for (int i = -8; i <= 8; i++)
        {
            float x = i * 6.6f;
            float height = 10f + Mathf.Abs((i * 41) % 8) * 1.8f;
            float width = 3.8f + Mathf.Abs((i * 13) % 4) * .65f;
            Material mat = (i & 1) == 0 ? _skylineA : _skylineB;
            Box(_far, "Tower_" + i, new Vector3(x, height * .5f - 2.8f, Mathf.Abs(i % 4) * 2f),
                new Vector3(width, height, 3.3f), mat, .16f);
            if (i % 3 == 0)
                Box(_far, "Spire_" + i, new Vector3(x, height + 1.6f, Mathf.Abs(i % 4) * 2f),
                    new Vector3(.20f, 3.4f, .20f), mat, .04f);
        }

        // Puffy cloud clusters reinforce depth even when the procedural sky
        // happens to leave a clear patch at the horizon.
        for (int cluster = -3; cluster <= 3; cluster++)
        {
            float baseX = cluster * 17f;
            float y = 14f + Mathf.Abs(cluster % 2) * 5f;
            for (int puff = 0; puff < 4; puff++)
                Sphere(_far, "Cloud_" + cluster + "_" + puff,
                    new Vector3(baseX + (puff - 1.5f) * 3.3f, y + (puff & 1) * 1.7f, 8f),
                    new Vector3(5.4f, 3.1f, 2.2f), _cloud);
        }
    }

    private static void Hero(Transform parent, GameObject source, Vector3 position,
                             Quaternion rotation, float scale)
    {
        if (source == null) return;
        GameObject instance = Instantiate(source, parent);
        instance.name = source.name + "_Backdrop";
        instance.transform.localPosition = position;
        instance.transform.localRotation = rotation * CampusRushModels.HeroAxisFix;
        instance.transform.localScale = Vector3.one * Mathf.Abs(scale);
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.layer = BackgroundLayer;
            Vector3 childScale = child.localScale;
            child.localScale = new Vector3(Mathf.Abs(childScale.x), Mathf.Abs(childScale.y), Mathf.Abs(childScale.z));
        }
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) Destroy(collider);
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial = HeroMaterial.For(renderer.gameObject.name);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }

    private static GameObject Box(Transform parent, string name, Vector3 position,
                                  Vector3 scale, Material material, float bevel)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name; go.layer = BackgroundLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        return go;
    }

    private static void Sphere(Transform parent, string name, Vector3 position,
                               Vector3 scale, Material material)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name; go.layer = BackgroundLayer;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        Destroy(go.GetComponent<Collider>());
        Renderer renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void EnsureMaterials()
    {
        if (_brick != null) return;
        _brick = Mat(new Color(.70f,.18f,.10f));
        _brickLight = Mat(new Color(.82f,.31f,.18f));
        _cream = Mat(new Color(.91f,.80f,.62f));
        _teal = Mat(new Color(.05f,.33f,.32f));
        _glass = Mat(new Color(.08f,.31f,.42f), .48f);
        _tree = Mat(new Color(.20f,.45f,.13f));
        _treeLight = Mat(new Color(.38f,.60f,.18f));
        _skylineA = Mat(new Color(.35f,.56f,.66f));
        _skylineB = Mat(new Color(.46f,.63f,.70f));
        _cloud = Mat(new Color(1f,.96f,.86f), .12f);
    }

    private static Material Mat(Color color, float smoothness = .28f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var material = new Material(shader) { color = color, enableInstancing = true };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        return material;
    }
}
