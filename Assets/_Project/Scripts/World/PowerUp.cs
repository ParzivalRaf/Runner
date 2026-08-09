using UnityEngine;

/// <summary>
/// Подбираемый бонус. Крутится, слегка покачивается и исчезает при подборе.
/// В пул его возвращает ObstacleSpawner вместе с остальным содержимым чанка.
///
/// Куда вешать: на корень префаба бонуса. Там же нужен коллайдер
/// с галочкой Is Trigger.
/// </summary>
public class PowerUp : MonoBehaviour
{
    [SerializeField] private PowerUpType type = PowerUpType.Magnet;
    [SerializeField] private Transform visual;
    [SerializeField] private float spinSpeed = 90f;
    [SerializeField] private float bobHeight = 0.25f;
    [SerializeField] private float bobSpeed = 2f;

    public PowerUpType Type => type;

    private Vector3 _visualBasePosition;

    private void Awake()
    {
        if (visual != null) _visualBasePosition = visual.localPosition;
    }

    private void OnEnable()
    {
        if (visual == null) return;

        visual.gameObject.SetActive(true);
        visual.localPosition = _visualBasePosition;
    }

    private void Update()
    {
        if (visual == null) return;

        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobHeight;
        visual.localPosition = _visualBasePosition + Vector3.up * bob;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (visual == null || !visual.gameObject.activeSelf) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;
        if (PowerUpManager.Instance == null) return;

        PowerUpManager.Instance.Activate(type);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPowerUp();
        if (GameFeel.Instance != null) GameFeel.Instance.PowerUp();
        if (EffectManager.Instance != null) EffectManager.Instance.PlayCoin(visual.position);

        visual.gameObject.SetActive(false);
    }
}
