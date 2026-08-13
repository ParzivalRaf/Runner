using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Живая витрина персонажа на экране выбора.
///
/// Это не второй игровой мир и не отдельная сцена. Компонент создаёт маленькую
/// студию далеко от трассы, показывает её только специальной камерой в
/// RenderTexture и выводит эту картинку в RawImage интерфейса. Поэтому модель
/// можно рассматривать крупно, а она никогда не попадёт в камеру забега.
///
/// Куда вешать: на объект LobbyPreview, который создаёт RunnerSceneBuilder.
/// В инспекторе: назначить RawImage из панели персонажей. Остальное создаётся
/// само при запуске и не требует ручной настройки.
/// </summary>
public class CharacterLobbyPreview : MonoBehaviour
{
    // Слой зарезервирован только для витрины. Основная камера специально
    // исключает его из своей маски, а камера витрины видит только его.
    private const int PreviewLayer = 30;

    // Студию уносим далеко вниз. Даже если кто-то позже случайно включит слой
    // витрины на основной камере, под ногами игрока она не окажется.
    private static readonly Vector3 StudioPosition = new Vector3(0f, -100f, 0f);

    [Header("Экран выбора")]
    [Tooltip("Прямоугольник UI, в который рисует отдельная камера витрины.")]
    [SerializeField] private RawImage targetImage;

    [Tooltip("Сколько градусов в секунду персонаж поворачивается на подиуме.")]
    [SerializeField] private float turnSpeed = 18f;

    private Camera _camera;
    private Transform _modelAnchor;
    private GameObject _shownModel;
    private RenderTexture _texture;
    private CharacterData _shownCharacter;
    private bool _shownUnlocked;
    private bool _isVisible;

    // Материалы этих деталей принадлежат только витрине. Поэтому их можно
    // перекрашивать под выбранного героя, не меняя материалы в самом проекте.
    private Renderer[] _accentRenderers;

    private void Awake()
    {
        BuildStudio();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        // RenderTexture создан в памяти, а не как файл проекта. Его обязательно
        // освобождаем, иначе при каждом перезапуске Play mode копилась бы память.
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
        }
    }

    private void Update()
    {
        if (!_isVisible || _modelAnchor == null || _shownModel == null) return;

        // Витрина должна жить даже когда игра стоит в меню. Берём обычное
        // deltaTime: Time.timeScale в меню остаётся равным единице.
        _modelAnchor.Rotate(0f, turnSpeed * Time.deltaTime, 0f, Space.Self);
    }

    /// <summary>
    /// Показать выбранного в карусели персонажа. Вызывается каждый раз при
    /// листании, но пересоздаёт модель только когда персонаж или его статус
    /// действительно изменился.
    /// </summary>
    public void ShowCharacter(CharacterData character, bool unlocked)
    {
        if (character == _shownCharacter && unlocked == _shownUnlocked && _shownModel != null)
            return;

        _shownCharacter = character;
        _shownUnlocked = unlocked;
        SetAccentColor(character != null && unlocked
            ? character.Tint
            : new Color(0.36f, 0.28f, 0.52f));

        if (_shownModel != null)
        {
            Destroy(_shownModel);
            _shownModel = null;
        }

        if (_modelAnchor == null) return;

        _modelAnchor.localRotation = Quaternion.identity;

        // Закрытого учителя не показываем целиком: так покупка сохраняет
        // маленькую интригу. Вместо настоящей модели стоит тёмная фигурка.
        if (character == null || !unlocked)
        {
            _shownModel = CreateSilhouette();
        }
        else if (character.VisualPrefab != null)
        {
            _shownModel = Instantiate(character.VisualPrefab, _modelAnchor);
            _shownModel.transform.localPosition = Vector3.zero;
            _shownModel.transform.localRotation = Quaternion.identity;

            PrepareModelForLobby(_shownModel);
            CenterModelOnPodium(_shownModel);
        }
        else
        {
            _shownModel = CreatePlaceholder(character.Tint);
        }
    }

    /// <summary>Включить или выключить рендер витрины вместе с панелью.</summary>
    public void SetVisible(bool visible)
    {
        _isVisible = visible;
        if (_camera != null) _camera.enabled = visible;
    }

    private void BuildStudio()
    {
        // Если компонент случайно продублировали в сцене, второй не должен
        // создавать ещё одну камеру и второй RenderTexture поверх первого.
        if (_camera != null) return;

        var studio = new GameObject("LobbyStudio");
        studio.transform.position = StudioPosition;
        SetLayerRecursively(studio, PreviewLayer);

        var anchor = new GameObject("CharacterAnchor");
        anchor.transform.SetParent(studio.transform, false);
        anchor.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        _modelAnchor = anchor.transform;
        SetLayerRecursively(anchor, PreviewLayer);

        _accentRenderers = CreateStudioSet(studio.transform);
        CreateLights(studio.transform);
        CreateCamera(studio.transform);

        // Основная камера не должна видеть студию. Делаем это здесь, а не
        // вручную в инспекторе, чтобы полная пересборка сцены не могла забыть
        // важную настройку.
        Camera main = Camera.main;
        if (main != null) main.cullingMask &= ~(1 << PreviewLayer);
    }

    private void CreateCamera(Transform studio)
    {
        var go = new GameObject("LobbyCamera");
        go.transform.SetParent(studio, false);
        go.transform.localPosition = new Vector3(0f, 1.35f, -5.8f);
        go.transform.LookAt(studio.position + new Vector3(0f, 1.2f, 0f));

        _camera = go.AddComponent<Camera>();
        _camera.clearFlags = CameraClearFlags.SolidColor;
        // Непрозрачный фон делает витрину самостоятельной маленькой сценой,
        // а не просто вырезанной моделью на прямоугольнике интерфейса.
        _camera.backgroundColor = new Color(0.018f, 0.012f, 0.055f, 1f);
        _camera.cullingMask = 1 << PreviewLayer;
        _camera.fieldOfView = 29f;
        _camera.nearClipPlane = 0.05f;
        _camera.farClipPlane = 30f;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;

        // Соотношение сторон совпадает с широкой карточкой интерфейса.
        // Квадратная текстура здесь растягивала бы персонажа по горизонтали.
        _texture = new RenderTexture(960, 700, 16, RenderTextureFormat.ARGB32);
        _texture.name = "LobbyPreviewTexture";
        _texture.Create();
        _camera.targetTexture = _texture;

        if (targetImage != null) targetImage.texture = _texture;
    }

    /// <summary>
    /// Собирает небольшую сцену для витрины: подиум, неоновые кольца и
    /// архитектурный фон. Всё — примитивы, поэтому в проект не добавляются
    /// случайные ассеты, а разные персонажи всё равно ощущаются особенными.
    /// </summary>
    private static Renderer[] CreateStudioSet(Transform studio)
    {
        var accents = new System.Collections.Generic.List<Renderer>();

        GameObject backdrop = GameObject.CreatePrimitive(PrimitiveType.Cube);
        backdrop.name = "Backdrop";
        backdrop.transform.SetParent(studio, false);
        backdrop.transform.localPosition = new Vector3(0f, 2.4f, 2.15f);
        backdrop.transform.localScale = new Vector3(5.7f, 4.8f, 0.12f);
        RemoveCollider(backdrop);
        SetLayerRecursively(backdrop, PreviewLayer);
        backdrop.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.045f, 0.025f, 0.12f));

        // Два тонких световых столба за спиной дают глубину даже у капсулы-
        // заглушки, когда настоящая модель ещё не подключена.
        for (int i = -1; i <= 1; i += 2)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "NeonPillar";
            pillar.transform.SetParent(studio, false);
            pillar.transform.localPosition = new Vector3(i * 2.0f, 2.15f, 2.0f);
            pillar.transform.localScale = new Vector3(0.07f, 3.7f, 0.11f);
            RemoveCollider(pillar);
            SetLayerRecursively(pillar, PreviewLayer);
            Renderer renderer = pillar.GetComponent<Renderer>();
            renderer.sharedMaterial = CreateAccentMaterial(new Color(0.5f, 0.3f, 0.96f));
            accents.Add(renderer);
        }

        GameObject podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        podium.name = "Podium";
        podium.transform.SetParent(studio, false);
        podium.transform.localPosition = new Vector3(0f, 0f, 0f);
        podium.transform.localScale = new Vector3(1.22f, 0.12f, 1.22f);
        RemoveCollider(podium);
        SetLayerRecursively(podium, PreviewLayer);

        podium.GetComponent<Renderer>().sharedMaterial = CreateMaterial(new Color(0.10f, 0.055f, 0.22f));

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "PodiumGlow";
        ring.transform.SetParent(studio, false);
        ring.transform.localPosition = new Vector3(0f, 0.126f, 0f);
        ring.transform.localScale = new Vector3(1.42f, 0.018f, 1.42f);
        RemoveCollider(ring);
        SetLayerRecursively(ring, PreviewLayer);

        Renderer ringRenderer = ring.GetComponent<Renderer>();
        ringRenderer.sharedMaterial = CreateAccentMaterial(new Color(0.5f, 0.3f, 0.96f));
        accents.Add(ringRenderer);

        GameObject outerRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outerRing.name = "PodiumOuterGlow";
        outerRing.transform.SetParent(studio, false);
        outerRing.transform.localPosition = new Vector3(0f, 0.032f, 0f);
        outerRing.transform.localScale = new Vector3(1.82f, 0.012f, 1.82f);
        RemoveCollider(outerRing);
        SetLayerRecursively(outerRing, PreviewLayer);
        Renderer outerRenderer = outerRing.GetComponent<Renderer>();
        outerRenderer.sharedMaterial = CreateAccentMaterial(new Color(0.23f, 0.7f, 0.92f));
        accents.Add(outerRenderer);

        return accents.ToArray();
    }

    private static void CreateLights(Transform studio)
    {
        CreateLight(studio, "KeyLight", new Vector3(-2.5f, 3.3f, -2f),
                    new Color(0.9f, 0.78f, 1f), 2.2f, 8f);
        CreateLight(studio, "RimLight", new Vector3(2.4f, 2.1f, 0.9f),
                    new Color(0.38f, 0.74f, 1f), 3.0f, 6f);
        CreateLight(studio, "WarmLight", new Vector3(0f, 1.2f, -3.2f),
                    new Color(1f, 0.43f, 0.68f), 1.35f, 5f);
    }

    private static void CreateLight(Transform studio, string name, Vector3 position,
                                    Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(studio, false);
        go.transform.localPosition = position;
        SetLayerRecursively(go, PreviewLayer);

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << PreviewLayer;
    }

    private GameObject CreateSilhouette()
    {
        return CreatePlaceholder(new Color(0.09f, 0.08f, 0.14f));
    }

    private GameObject CreatePlaceholder(Color color)
    {
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "LobbyPlaceholder";
        body.transform.SetParent(_modelAnchor, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        body.transform.localScale = new Vector3(0.72f, 0.9f, 0.72f);
        RemoveCollider(body);
        SetLayerRecursively(body, PreviewLayer);

        Renderer renderer = body.GetComponent<Renderer>();
        renderer.sharedMaterial = CreateMaterial(color);
        return body;
    }

    private static void PrepareModelForLobby(GameObject model)
    {
        SetLayerRecursively(model, PreviewLayer);

        // В лобби персонаж стоит красиво и спокойно. Беговой драйвер и
        // разворот головы рассчитаны на PlayerController внутри забега, а в
        // витрине дали бы пустые ссылки или заставили бы модель бежать на месте.
        foreach (CharacterAnimatorDriver driver in model.GetComponentsInChildren<CharacterAnimatorDriver>(true))
            driver.enabled = false;

        foreach (CharacterHeadTurn headTurn in model.GetComponentsInChildren<CharacterHeadTurn>(true))
            headTurn.enabled = false;

        foreach (CharacterRunCycle runCycle in model.GetComponentsInChildren<CharacterRunCycle>(true))
            runCycle.enabled = false;

        foreach (Animator animator in model.GetComponentsInChildren<Animator>(true))
            animator.enabled = false;
    }

    /// <summary>
    /// FBX-файлы редко имеют одинаковый pivot: один автор кладёт его под
    /// ногами, другой — в центре мира, третий вообще сбоку от модели. В игре
    /// это можно терпеть, но на подиуме сразу видно. Ставим видимую модель
    /// по её реальным границам: центр — над серединой подиума, ноги — на нём.
    /// </summary>
    private void CenterModelOnPodium(GameObject model)
    {
        if (_modelAnchor == null) return;

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        Vector3 target = _modelAnchor.position;
        model.transform.position += new Vector3(target.x - bounds.center.x,
                                                target.y - bounds.min.y,
                                                target.z - bounds.center.z);
    }

    private static Material CreateMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        var material = new Material(shader);
        material.color = color;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", 0.18f);
        return material;
    }

    private static Material CreateAccentMaterial(Color color)
    {
        Material material = CreateMaterial(color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 1.35f);
        return material;
    }

    private void SetAccentColor(Color color)
    {
        if (_accentRenderers == null) return;

        Color litColor = Color.Lerp(color, Color.white, 0.12f);
        foreach (Renderer renderer in _accentRenderers)
        {
            if (renderer == null || renderer.sharedMaterial == null) continue;

            Material material = renderer.sharedMaterial;
            material.color = litColor;
            material.SetColor("_BaseColor", litColor);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", litColor * 1.3f);
        }
    }

    private static void RemoveCollider(GameObject go)
    {
        Collider collider = go.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;

        for (int i = 0; i < root.transform.childCount; i++)
            SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
    }
}
