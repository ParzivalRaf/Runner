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
        SlideUnder
    }

    [SerializeField] private Kind kind = Kind.Block;

    public Kind ObstacleKind => kind;
}
