using System.Collections.Generic;
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
    /// <summary>Все монеты, сейчас лежащие на трассе. Нужен магниту.</summary>
    public static readonly List<Coin> Active = new List<Coin>();

    [Tooltip("Скорость вращения, градусов в секунду.")]
    [SerializeField] private float spinSpeed = 180f;

    [Tooltip("Сколько монет засчитывается за подбор.")]
    [SerializeField] private int value = 1;

    [Tooltip("Визуальная часть. Крутим её, а не корень с коллайдером.")]
    [SerializeField] private Transform visual;

    /// <summary>Монета ещё лежит и её можно подобрать.</summary>
    public bool IsAvailable => visual != null && visual.gameObject.activeSelf;

    private void OnEnable()
    {
        // Из пула монета может прийти уже подобранной — возвращаем вид.
        if (visual != null) visual.gameObject.SetActive(true);

        Active.Add(this);
    }

    private void OnDisable()
    {
        Active.Remove(this);
    }

    private void Update()
    {
        // Space.World, а не Self: монета лежит «на ребре», и её собственная
        // ось Y смотрит вбок — крутить надо вокруг мировой вертикали.
        if (visual != null) visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsAvailable) return;
        if (other.GetComponentInParent<PlayerController>() == null) return;

        int amount = value;
        if (PowerUpManager.Instance != null) amount *= PowerUpManager.Instance.CoinMultiplier;

        if (ScoreManager.Instance != null) ScoreManager.Instance.AddCoins(amount);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayCoin();
        if (EffectManager.Instance != null) EffectManager.Instance.PlayCoin(visual.position);

        visual.gameObject.SetActive(false);
    }
}
