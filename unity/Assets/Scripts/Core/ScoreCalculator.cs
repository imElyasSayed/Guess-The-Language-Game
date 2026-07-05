namespace AccentGuesser.Core
{
    /// <summary>Outcome of scoring a single guess.</summary>
    public readonly struct ScoreResult
    {
        public readonly bool Correct;
        /// <summary>Points earned this guess.</summary>
        public readonly int Points;
        /// <summary>Streak value after applying this guess.</summary>
        public readonly int NewStreak;

        public ScoreResult(bool correct, int points, int newStreak)
        {
            Correct = correct;
            Points = points;
            NewStreak = newStreak;
        }
    }

    /// <summary>
    /// Pure scoring rules (brief §2). The one-question-per-round bonus is a HARD rule:
    ///   correct + did NOT ask  => +15   (feeds the "Trust Your Ear" achievement, §13)
    ///   correct + asked        => +10
    ///   wrong                  =>  +0 and the streak resets
    ///   any correct guess increments the streak; any wrong guess resets it to 0.
    /// </summary>
    public static class ScoreCalculator
    {
        public const int PointsCorrectNotAsked = 15;
        public const int PointsCorrectAsked = 10;
        public const int PointsWrong = 0;

        public static int Points(bool correct, bool asked)
        {
            if (!correct) return PointsWrong;
            return asked ? PointsCorrectAsked : PointsCorrectNotAsked;
        }

        public static ScoreResult Evaluate(bool correct, bool asked, int currentStreak)
        {
            int points = Points(correct, asked);
            int newStreak = correct ? currentStreak + 1 : 0;
            return new ScoreResult(correct, points, newStreak);
        }
    }
}
