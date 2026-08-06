using UnityEngine;

/// <summary>
/// Монета: крутится на месте и исчезает, когда её подобрали.
///
/// В пул её возвращает не она сама, а тот, кто её поставил — так объект
/// не может потеряться. Здесь мы только выключаем её из виду.
///
/// Куда вешать: на корень префаба монеты. Там же нужен коллайдер
/// с галочкой Is Trigger.
/// </summary>
public class Coin : MonoBehaviour
{
    [Tooltip("Скорость вращения, градусов в секунду.")]
    [SerializeField] private float spinSpeed = 180f;

    [Tooltip("Сколько монет засчитывается за подбор.")]
    [SerializeField] private int value = 1;

    [Tooltip("Визуальная часть. Крутим её, а не корень с коллайдером.")]
    [SerializeField] private Transform visual;

    private void OnEnable()
    {
        // Из пула монета может прийти уже подобранной — возвращаем вид.
        if (visual != null) visual.gameObject.SetActive(true);
    }

    private void Update()
    {
        // Space.World, а не Self: монета лежит «на ребре», и её собственная
        // ось Y смотрит вбок — крутить надо вокруг мировой вертикали.
        if (visual != null) visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (visual == null || !visual.gameObject.activeSelf) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        if (ScoreManager.Instance != null) ScoreManager.Instance.AddCoins(value);

        visual.gameObject.SetActive(false);
    }
}
