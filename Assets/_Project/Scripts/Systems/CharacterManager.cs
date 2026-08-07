using UnityEngine;

/// <summary>
/// Кто сейчас выбран, кто куплен, и что этот выбор даёт в игре.
///
/// Способности сознательно НЕ разбросаны по системам: здесь лежат
/// готовые к употреблению числа (CoinBonusRate, ExtraPowerUpSeconds,
/// StartSpeedBonus), а системы их просто спрашивают. Добавить новую
/// способность = добавить свойство здесь и одну строчку там, где оно нужно.
///
/// Куда вешать: на объект GameManager.
/// В инспекторе: перетащить CharacterDatabase в поле Database
/// и объект Player/Visual в поле Player Visual.
/// </summary>
public class CharacterManager : MonoBehaviour
{
    public static CharacterManager Instance { get; private set; }

    [Header("Ссылки")]
    [Tooltip("Список всех персонажей. Ассет из Assets/_Project/Characters.")]
    [SerializeField] private CharacterDatabase database;

    [Tooltip("Объект Player/Visual. В него подставляется модель и красится капсула.")]
    [SerializeField] private Transform playerVisual;

    /// <summary>Имя дочернего объекта, в котором живёт модель персонажа.</summary>
    private const string ModelRootName = "CharacterModel";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Transform _modelRoot;
    private CharacterData _selected;

    /// <summary>Сколько столкновений ещё простит щит в этом забеге.</summary>
    public int ShieldCharges { get; private set; }

    public CharacterDatabase Database => database;
    public CharacterData Selected => _selected;

    // ------------------------------------------------------- числа способностей

    /// <summary>Доля прибавки к монетам: 0.10 = +10%. 0, если способность другая.</summary>
    public float CoinBonusRate =>
        AbilityValueOf(CharacterAbility.CoinBonus);

    /// <summary>Сколько секунд персонаж добавляет к длительности любого бонуса.</summary>
    public float ExtraPowerUpSeconds =>
        AbilityValueOf(CharacterAbility.LongerPowerUps);

    /// <summary>Прибавка к стартовой скорости, юниты в секунду.</summary>
    public float StartSpeedBonus =>
        AbilityValueOf(CharacterAbility.FastStart);

    /// <summary>Сколько раз щит спасает за забег. 0, если способность другая.</summary>
    public int ShieldCapacity =>
        Mathf.Max(0, Mathf.RoundToInt(AbilityValueOf(CharacterAbility.Shield)));

    private float AbilityValueOf(CharacterAbility ability) =>
        _selected != null && _selected.Ability == ability ? _selected.AbilityValue : 0f;

    // ---------------------------------------------------------------- запуск

    private void Awake()
    {
        Instance = this;

        EnsureDefaultsUnlocked();
        _selected = ResolveSelected();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        ApplyVisual();
    }

    /// <summary>
    /// Персонажи с галочкой «доступен сразу» должны попасть в сейв, иначе
    /// после первого же сохранения список открытых окажется пустым.
    /// </summary>
    private void EnsureDefaultsUnlocked()
    {
        if (database == null) return;

        SaveData data = SaveSystem.Data;
        bool changed = false;

        for (int i = 0; i < database.Count; i++)
        {
            CharacterData character = database.Get(i);
            if (character == null || !character.UnlockedByDefault) continue;
            if (data.unlockedCharacters.Contains(character.Id)) continue;

            data.unlockedCharacters.Add(character.Id);
            changed = true;
        }

        if (changed) SaveSystem.Save();
    }

    /// <summary>
    /// Кто выбран сейчас. Если в сейве лежит id персонажа, которого больше
    /// нет в проекте (переименовали, удалили) — молча откатываемся
    /// на стартового, а не падаем.
    /// </summary>
    private CharacterData ResolveSelected()
    {
        if (database == null) return null;

        CharacterData saved = database.FindById(SaveSystem.Data.selectedCharacterId);
        if (saved != null && IsUnlocked(saved)) return saved;

        return database.FirstFree();
    }

    /// <summary>
    /// Перечитать выбор из сейва. Нужно после сброса прогресса: список
    /// открытых персонажей обнулился, и выбранный мог стать недоступным.
    /// </summary>
    public void ReloadFromSave()
    {
        EnsureDefaultsUnlocked();
        _selected = ResolveSelected();

        if (_selected != null) SaveSystem.Data.selectedCharacterId = _selected.Id;

        ApplyVisual();
    }

    // ---------------------------------------------------------- покупка и выбор

    public bool IsUnlocked(CharacterData character)
    {
        if (character == null) return false;
        if (character.UnlockedByDefault) return true;

        return SaveSystem.Data.unlockedCharacters.Contains(character.Id);
    }

    public bool CanBuy(CharacterData character)
    {
        if (character == null || IsUnlocked(character)) return false;

        return SaveSystem.Data.totalCoins >= character.Price;
    }

    /// <summary>Купить и сразу выбрать. false, если не хватило монет.</summary>
    public bool Buy(CharacterData character)
    {
        if (!CanBuy(character)) return false;

        SaveData data = SaveSystem.Data;
        data.totalCoins -= character.Price;
        data.unlockedCharacters.Add(character.Id);
        SaveSystem.Save();

        Select(character);
        return true;
    }

    /// <summary>Выбрать уже открытого персонажа. false, если он ещё закрыт.</summary>
    public bool Select(CharacterData character)
    {
        if (!IsUnlocked(character)) return false;

        _selected = character;
        SaveSystem.Data.selectedCharacterId = character.Id;
        SaveSystem.Save();

        ApplyVisual();
        return true;
    }

    // ------------------------------------------------------------------- забег

    /// <summary>Вызывает GameManager перед каждым забегом.</summary>
    public void ResetRun()
    {
        ShieldCharges = ShieldCapacity;
    }

    /// <summary>
    /// Потратить один заряд щита. true — столкновение прощено.
    /// Вызывает PlayerCollision.
    /// </summary>
    public bool TryConsumeShield()
    {
        if (ShieldCharges <= 0) return false;

        ShieldCharges--;
        return true;
    }

    // ---------------------------------------------------------------- внешность

    /// <summary>
    /// Ставит модель выбранного персонажа и красит заглушку.
    ///
    /// Instantiate здесь не нарушает правило «никаких Instantiate после
    /// старта»: это происходит в меню, при смене персонажа, а не каждый кадр.
    /// </summary>
    public void ApplyVisual()
    {
        if (playerVisual == null) return;

        // Персонажа нет (база не назначена или пуста) — оставляем игрока
        // как есть. Иначе мы бы перекрасили его в белый и это выглядело бы
        // как поломка, хотя дело просто в незаполненном поле.
        if (_selected == null) return;

        EnsureModelRoot();
        ClearModelRoot();

        Color tint = _selected.Tint;
        GameObject prefab = _selected.VisualPrefab;

        if (prefab != null)
        {
            GameObject model = Instantiate(prefab, _modelRoot);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            // Модель есть — прячем капсулу-заглушку, но сам объект Visual
            // оставляем: PlayerController сжимает именно его при подкате.
            SetPlaceholderVisible(false);
        }
        else
        {
            SetPlaceholderVisible(true);
            TintPlaceholder(tint);
        }
    }

    private void EnsureModelRoot()
    {
        if (_modelRoot != null) return;

        Transform existing = playerVisual.Find(ModelRootName);
        if (existing != null)
        {
            _modelRoot = existing;
            return;
        }

        var go = new GameObject(ModelRootName);
        go.transform.SetParent(playerVisual, false);
        _modelRoot = go.transform;
    }

    private void ClearModelRoot()
    {
        if (_modelRoot == null) return;

        for (int i = _modelRoot.childCount - 1; i >= 0; i--)
            Destroy(_modelRoot.GetChild(i).gameObject);
    }

    /// <summary>Заглушка — это все рендереры под Visual, кроме модели персонажа.</summary>
    private void SetPlaceholderVisible(bool visible)
    {
        Renderer[] renderers = playerVisual.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsInsideModel(renderers[i].transform)) continue;
            renderers[i].enabled = visible;
        }
    }

    /// <summary>
    /// Красим через MaterialPropertyBlock, а не material.color: обращение
    /// к material создаёт копию материала на каждый рендерер, и они утекают.
    /// </summary>
    private void TintPlaceholder(Color color)
    {
        Renderer[] renderers = playerVisual.GetComponentsInChildren<Renderer>(true);
        var block = new MaterialPropertyBlock();

        for (int i = 0; i < renderers.Length; i++)
        {
            if (IsInsideModel(renderers[i].transform)) continue;

            renderers[i].GetPropertyBlock(block);
            block.SetColor(BaseColorId, color);   // URP Lit
            block.SetColor(ColorId, color);       // на случай встроенного шейдера
            renderers[i].SetPropertyBlock(block);
        }
    }

    private bool IsInsideModel(Transform candidate) =>
        _modelRoot != null && candidate.IsChildOf(_modelRoot);
}
