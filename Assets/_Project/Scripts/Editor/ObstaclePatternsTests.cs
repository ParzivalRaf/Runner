using NUnit.Framework;

/// <summary>
/// Быстрая защита таблиц от случайно добавленной непроходимой раскладки.
/// Запуск: Window → General → Test Runner → EditMode.
/// </summary>
public class ObstaclePatternsTests
{
    [Test]
    public void AllSourcePatternsArePlayable()
    {
        bool valid = ObstaclePatterns.ValidateTables(out string problem);
        Assert.IsTrue(valid, problem);
    }

    [TestCase("T..")]
    [TestCase(".T.")]
    [TestCase("..T")]
    public void TrainRowsLeaveTwoClearGroundLanes(string pattern)
    {
        Assert.IsTrue(ObstaclePatterns.HasTwoClearGroundLanesAroundTrains(pattern));
    }

    [TestCase("TB.")]
    [TestCase(".TS")]
    [TestCase("J.T")]
    public void TrainRowsRejectBlockedEscapeRoutes(string pattern)
    {
        Assert.IsFalse(ObstaclePatterns.HasTwoClearGroundLanesAroundTrains(pattern));
    }
}
