using UnityEngine;

/// <summary>
/// Препятствие на трассе. Само по себе ничего не делает — это метка,
/// по которой игрок понимает, что во что-то врезался, а подкат понимает,
/// что над головой балка.
///
/// Куда вешать: на корень префаба препятствия. Там же должен быть
/// BoxCollider с галочкой Is Trigger.
/// </summary>
public class Obstacle : MonoBehaviour
{
    public enum Kind
    {
        /// <summary>Высокое: не перепрыгнуть и не подкатиться, только сменить полосу.</summary>
        Block,

        /// <summary>Низкое: перепрыгнуть.</summary>
        JumpOver,

        /// <summary>Балка сверху: проехать подкатом.</summary>
        SlideUnder,

        /// <summary>
        /// Поезд: длинный, борта убивают, а по крыше можно бежать.
        /// Единственное препятствие, которое даёт выбор маршрута,
        /// а не одно правильное действие.
        /// </summary>
        Train
    }

    [SerializeField] private Kind kind = Kind.Block;

    public Kind ObstacleKind => kind;

    private void Awake()
    {
        if (!Application.isPlaying) return;

        // Детали создаются при предварительном наполнении пулов и остаются
        // у объекта до конца сессии. Игровой коллайдер на корне не трогаем.
        SchoolObstacleVisuals.EnsureBuilt(this);
    }

    /// <summary>
    /// Вариант балки сверху. У низкой балки есть два честных решения —
    /// прыгнуть или проехать под ней. Высокая стоит выше вершины обычного
    /// прыжка, поэтому остаётся только подкат.
    /// </summary>
    public enum SlideVariant
    {
        JumpOrSlide,
        SlideOnly
    }

    public SlideVariant CurrentSlideVariant { get; private set; } = SlideVariant.JumpOrSlide;

    /// <summary>
    /// Настраивает один и тот же пуловый префаб на низкую или высокую балку.
    /// Так не нужно плодить почти одинаковые префабы и отдельные пулы.
    /// Вызывается каждый раз при выдаче объекта из пула, поэтому вариант не
    /// «протекает» в следующий чанк при переиспользовании.
    /// </summary>
    public void ConfigureSlideVariant(SlideVariant variant)
    {
        if (kind != Kind.SlideUnder) return;

        CurrentSlideVariant = variant;

        // Низкая балка: под ней проходит коллайдер подката (до y=0.9),
        // а обычный прыжок перепрыгивает её верх (y=1.8).
        // Высокая: низ всё ещё выше подката, но верх y=2.55 — выше ног в
        // вершине обычного прыжка y=2.2. Значит прыгнуть уже нельзя.
        float beamY = variant == SlideVariant.SlideOnly ? 2.175f : 1.45f;
        float beamHeight = variant == SlideVariant.SlideOnly ? 0.75f : 0.7f;
        float postHeight = variant == SlideVariant.SlideOnly ? 2.55f : 1.8f;

        BoxCollider trigger = GetComponent<BoxCollider>();
        if (trigger != null)
        {
            trigger.center = new Vector3(0f, beamY, 0f);
            trigger.size = new Vector3(1.7f, beamHeight, 0.7f);
        }

        SetBoxTransform("Visual", beamY, beamHeight);
        SetBoxTransform("Post_-1", postHeight * 0.5f, postHeight);
        SetBoxTransform("Post_1", postHeight * 0.5f, postHeight);

        // The authored Campus Rush gate is built at the low-beam height.
        // Scale the complete silhouette so the visual always matches the
        // gameplay collider for both generated variants.
        // Имя — роль, а не файл: модель ворот зависит от активного набора
        // (см. CampusRushArt.cs), а называется всегда одинаково.
        Transform authoredGate = transform.Find("SchoolDetails/" + ArtRole.ObstacleSlide);
        if (authoredGate != null)
        {
            float visualScale = beamY / 1.76f;
            authoredGate.localScale = new Vector3(1f, visualScale, 1f);
        }
    }

    private void SetBoxTransform(string childName, float y, float height)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;

        Vector3 position = child.localPosition;
        position.y = y;
        child.localPosition = position;

        Vector3 scale = child.localScale;
        scale.y = height;
        child.localScale = scale;
    }

    /// <summary>
    /// Размеры поезда. Лежат здесь, потому что их должны знать двое: сборщик
    /// сцены, который строит префаб, и генератор, который решает, где поезд
    /// начинается и на какой высоте класть монеты на крышу.
    ///
    /// Если развести эти числа по двум файлам, они рано или поздно разъедутся,
    /// и монеты повиснут в воздухе над крышей или утонут в ней.
    /// </summary>
    public static class TrainMetrics
    {
        /// <summary>
        /// Длина одного вагона. Ровно треть чанка (30) — и это не совпадение:
        /// вагоны выкладываются встык от начала чанка до конца, поэтому
        /// состав в соседних чанках стыкуется без щелей. Игрок бежит
        /// по крышам сколько угодно долго, ни разу не спрыгнув.
        ///
        /// Если менять — менять вместе с ChunkLength в сборщике сцены,
        /// иначе между вагонами появятся дыры.
        /// </summary>
        public const float Length = 10f;

        public const float Width = 1.7f;

        /// <summary>
        /// Высота крыши. НАМЕРЕННО выше прыжка (2.2): попасть на крышу
        /// обычным прыжком нельзя, только по пандусу. Так устроено
        /// в Subway Surfers, и это правильно — иначе поезд перестаёт быть
        /// препятствием и становится трамплином на реакцию.
        ///
        /// Побочное следствие, которое я оставляю сознательно: бонус
        /// «кроссовки» умножает высоту прыжка на 1.8, то есть до 3.96,
        /// и с ним на крышу можно запрыгнуть где угодно. В оригинале
        /// Super Sneakers работают ровно так же.
        /// </summary>
        public const float RoofHeight = 2.6f;

        /// <summary>
        /// Докуда достаёт убивающий триггер. Обязан быть НИЖЕ крыши, иначе
        /// вставший на крышу игрок окажется внутри триггера и умрёт
        /// в момент приземления.
        ///
        /// Заодно это делает поезд непрыгаемым: на вершине прыжка ноги
        /// игрока на 2.2, а он сам занимает 2.2..4.2 — и всё ещё задевает
        /// триггер, который кончается на 2.5.
        /// </summary>
        public const float KillHeight = 2.5f;

        /// <summary>
        /// Пандус занимает место одного вагона: подъём, потом ровная площадка.
        /// Площадка нужна, чтобы игрок доехал до полной высоты крыши ДО того,
        /// как войдёт в зону убивающего триггера первого вагона.
        /// Без неё он въезжал бы в вагон на высоте 1.75 при пороге 1.70 —
        /// пять сантиметров запаса, и любой рывок кадра означает смерть.
        /// </summary>
        public const float RampLength = Length;

        /// <summary>
        /// Горизонтальная длина самого подъёма. Крыша стала выше, поэтому
        /// подъём удлинён — иначе пандус стал бы круче и по нему пришлось бы
        /// «карабкаться», а он должен пробегаться без единого нажатия.
        /// </summary>
        public const float RampRun = 7f;
    }
}
