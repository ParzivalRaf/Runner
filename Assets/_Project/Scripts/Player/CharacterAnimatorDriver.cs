using UnityEngine;

/// <summary>
/// Переключает анимации персонажа по тому, что происходит в игре.
///
/// Логики здесь намеренно мало: бежит по земле, летит в воздухе, падает
/// при смерти. Скорость проигрывания бега привязана к реальной скорости
/// игрока, поэтому на разгоне персонаж перебирает ногами чаще — без этого
/// он на 24 юнитах в секунду выглядел бы так, будто едет на коньках.
///
/// Куда вешать: на корень модели персонажа. Сборщик делает это сам.
/// </summary>
public class CharacterAnimatorDriver : MonoBehaviour
{
    [SerializeField] private Animator animator;

    [Tooltip("Скорость игрока, при которой анимация бега играет один к одному.")]
    [SerializeField] private float referenceSpeed = 16f;

    [Tooltip("Границы ускорения анимации, чтобы ноги не превратились в блендер.")]
    [SerializeField] private float minPlaybackSpeed = 0.75f;
    [SerializeField] private float maxPlaybackSpeed = 1.6f;

    private PlayerController _player;
    private bool _dead;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _player = GetComponentInParent<PlayerController>();
    }

    private void OnEnable()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnGameOver += HandleGameOver;
        GameManager.Instance.OnRunStarted += HandleRunStarted;
    }

    private void OnDisable()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnGameOver -= HandleGameOver;
        GameManager.Instance.OnRunStarted -= HandleRunStarted;
    }

    private void HandleGameOver() => _dead = true;

    private void HandleRunStarted()
    {
        _dead = false;
        if (animator != null) animator.speed = 1f;
    }

    private void Update()
    {
        if (animator == null || _player == null) return;

        animator.SetBool("Grounded", _player.IsGrounded);
        animator.SetBool("Dead", _dead);

        // После смерти анимация падения должна доиграть в нормальном темпе,
        // а не в темпе последней скорости забега.
        if (_dead)
        {
            animator.speed = 1f;
            return;
        }

        float ratio = _player.CurrentSpeed / Mathf.Max(0.1f, referenceSpeed);
        animator.speed = Mathf.Clamp(ratio, minPlaybackSpeed, maxPlaybackSpeed);
    }
}
