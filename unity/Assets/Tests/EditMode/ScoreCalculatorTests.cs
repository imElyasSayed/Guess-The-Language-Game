using AccentGuesser.Core;
using NUnit.Framework;

namespace AccentGuesser.EditModeTests
{
    public class ScoreCalculatorTests
    {
        [Test]
        public void Correct_WithoutAsking_ScoresFifteen()
        {
            Assert.AreEqual(15, ScoreCalculator.Points(correct: true, asked: false));
        }

        [Test]
        public void Correct_AfterAsking_ScoresTen()
        {
            Assert.AreEqual(10, ScoreCalculator.Points(correct: true, asked: true));
        }

        [Test]
        public void Wrong_ScoresZero_RegardlessOfAsking()
        {
            Assert.AreEqual(0, ScoreCalculator.Points(correct: false, asked: false));
            Assert.AreEqual(0, ScoreCalculator.Points(correct: false, asked: true));
        }

        [Test]
        public void Correct_IncrementsStreak()
        {
            var r = ScoreCalculator.Evaluate(correct: true, asked: false, currentStreak: 3);
            Assert.AreEqual(4, r.NewStreak);
            Assert.AreEqual(15, r.Points);
            Assert.IsTrue(r.Correct);
        }

        [Test]
        public void Wrong_ResetsStreakToZero()
        {
            var r = ScoreCalculator.Evaluate(correct: false, asked: true, currentStreak: 7);
            Assert.AreEqual(0, r.NewStreak);
            Assert.AreEqual(0, r.Points);
            Assert.IsFalse(r.Correct);
        }
    }
}
