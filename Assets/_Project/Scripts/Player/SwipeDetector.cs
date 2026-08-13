using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Читает свайпы пальцем (или мышью в редакторе) и стрелки/WASD на клавиатуре,
/// превращая их в четыре события направления.
///
/// Куда вешать: на объект Player (тот же, где PlayerController).
/// </summary>
public class SwipeDetector : MonoBehaviour
{
    public enum Direction
    {
        Left,
        Right,
        Up,
        Down
    }

    [Header("Порог свайпа")]
    [Tooltip("Минимальная длина свайпа в пикселях.")]
    [SerializeField] private float minPixelDistance = 50f;

    [Tooltip("Минимальная длина свайпа как доля ширины экрана. Берётся большее из двух значений.")]
    [SerializeField] private float screenWidthFraction = 0.08f;

    [Tooltip("Дольше этого времени жест свайпом уже не считается (секунды).")]
    [SerializeField] private float maxSwipeTime = 0.5f;

    [Header("Отладка")]
    [Tooltip("Печатать распознанные свайпы в консоль.")]
    [SerializeField] private bool logSwipes = false;

    /// <summary>Срабатывает один раз на каждый распознанный свайп или нажатие клавиши.</summary>
    public event Action<Direction> OnSwipe;

    private Vector2 _startPosition;
    private float _startTime;
    private bool _isTracking;

    private float Threshold => Mathf.Max(minPixelDistance, Screen.width * screenWidthFraction);

    private void Update()
    {
        ReadKeyboard();
        ReadPointer();
    }

    // ---------------------------------------------------------------- клавиатура

    private void ReadKeyboard()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            Emit(Direction.Left);

        if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            Emit(Direction.Right);

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
            Emit(Direction.Up);

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
            Emit(Direction.Down);
    }

    // ------------------------------------------------------------ палец / мышь

    private void ReadPointer()
    {
        // Pointer.current — это и тачскрин на телефоне, и мышь в редакторе.
        Pointer pointer = Pointer.current;
        if (pointer == null) return;

        bool pressed = pointer.press.wasPressedThisFrame;
        bool released = pointer.press.wasReleasedThisFrame;

        if (pressed)
        {
            _startPosition = pointer.position.ReadValue();
            _startTime = Time.unscaledTime;
            _isTracking = true;
        }

        if (!_isTracking) return;

        // Раньше отпускание пальца обрабатывалось ДО проверки дистанции.
        // На быстром свайпе вниз движение и отпускание часто приходят в один
        // кадр, поэтому жест просто выбрасывался и подкат не запускался.
        // Теперь финальную позицию тоже проверяем как полноценный свайп.
        if (released)
        {
            TryEmitPointerSwipe(pointer);
            _isTracking = false;
            return;
        }

        if (pressed) return;

        // Слишком долгий жест — это уже не свайп, а удержание.
        if (Time.unscaledTime - _startTime > maxSwipeTime)
        {
            _isTracking = false;
            return;
        }

        // Свайп засчитывается сразу при прохождении порога, не ждём
        // отпускания — так управление остаётся отзывчивым.
        if (TryEmitPointerSwipe(pointer)) _isTracking = false;
    }

    /// <summary>
    /// Возвращает true, если текущее смещение уже можно считать свайпом.
    /// Вызывается и в движении пальца, и в кадре его отпускания.
    /// </summary>
    private bool TryEmitPointerSwipe(Pointer pointer)
    {
        if (Time.unscaledTime - _startTime > maxSwipeTime) return false;

        Vector2 delta = pointer.position.ReadValue() - _startPosition;
        if (delta.magnitude < Threshold) return false;

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
            Emit(delta.x > 0f ? Direction.Right : Direction.Left);
        else
            Emit(delta.y > 0f ? Direction.Up : Direction.Down);

        return true;
    }

    // ---------------------------------------------------------------------

    private void Emit(Direction direction)
    {
        if (logSwipes) Debug.Log($"[Swipe] {direction}");
        OnSwipe?.Invoke(direction);
    }
}
