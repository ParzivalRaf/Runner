using UnityEngine;

/// <summary>
/// Пока активен бонус «магнит», подтягивает к игроку все монеты в радиусе.
/// Монеты не собираются сами: они просто подлетают вплотную, и срабатывает
/// обычный триггер подбора.
///
/// Список активных монет ведёт сам Coin — так не нужен ни поиск по сцене,
/// ни физический OverlapSphere каждый кадр.
///
/// Куда вешать: на объект Player.
/// </summary>
public class CoinMagnet : MonoBehaviour
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float pullSpeed = 16f;

    [Tooltip("На какой высоте от пивота игрока находится точка притяжения.")]
    [SerializeField] private float targetHeight = 1f;

    private void Update()
    {
        if (PowerUpManager.Instance == null) return;
        if (!PowerUpManager.Instance.IsActive(PowerUpType.Magnet)) return;

        Vector3 target = transform.position + Vector3.up * targetHeight;
        float radiusSqr = radius * radius;
        float step = pullSpeed * Time.deltaTime;

        var coins = Coin.Active;
        for (int i = coins.Count - 1; i >= 0; i--)
        {
            Coin coin = coins[i];
            if (coin == null || !coin.IsAvailable) continue;

            Vector3 position = coin.transform.position;

            // Монеты позади игрока не тянем — иначе они летают за спиной.
            if (position.z < target.z - 1f) continue;
            if ((position - target).sqrMagnitude > radiusSqr) continue;

            coin.transform.position = Vector3.MoveTowards(position, target, step);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.70f, 0.95f, 0.5f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * targetHeight, radius);
    }
}
