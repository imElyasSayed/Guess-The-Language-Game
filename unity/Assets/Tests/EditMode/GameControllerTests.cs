using System;
using System.Linq;
using AccentGuesser.Core;
using NUnit.Framework;

namespace AccentGuesser.EditModeTests
{
    public class GameControllerTests
    {
        private static GameController NewGame(int seed = 1) =>
            new GameController(
                new FakeClipCatalog(("a", "Alpha"), ("b", "Bravo"), ("c", "Charlie"),
                                    ("d", "Delta"), ("e", "Echo")),
                new Random(seed));

        private static Lang WrongChoice(GameController g) =>
            g.Choices.First(c => !c.Equals(g.Target));

        [Test]
        public void StartsInSetupPhase()
        {
            var g = NewGame();
            Assert.AreEqual(GamePhase.Setup, g.Phase);
            Assert.AreEqual(0, g.Score);
            Assert.AreEqual(0, g.RoundNumber);
        }

        [Test]
        public void StartRound_EntersRoundPhase_WithClipAndChoices()
        {
            var g = NewGame();
            g.StartRound();

            Assert.AreEqual(GamePhase.Round, g.Phase);
            Assert.AreEqual(1, g.RoundNumber);
            Assert.IsNotNull(g.CurrentClip);
            Assert.AreEqual(4, g.Choices.Count);
            Assert.IsFalse(g.Asked);
        }

        [Test]
        public void StartRound_WhileRoundInProgress_Throws()
        {
            var g = NewGame();
            g.StartRound();
            Assert.Throws<InvalidOperationException>(() => g.StartRound());
        }

        [Test]
        public void MarkAsked_LocksAfterOneQuestion()
        {
            var g = NewGame();
            g.StartRound();

            Assert.IsTrue(g.MarkAsked(), "first ask should succeed");
            Assert.IsTrue(g.Asked);
            Assert.IsFalse(g.MarkAsked(), "second ask must be rejected (one-question lock)");
        }

        [Test]
        public void MarkAsked_OutsideRound_ReturnsFalse()
        {
            var g = NewGame();
            Assert.IsFalse(g.MarkAsked());
        }

        [Test]
        public void CorrectGuess_WithoutAsking_ScoresFifteen_AndBumpsStreak()
        {
            var g = NewGame();
            g.StartRound();
            var result = g.SubmitGuess(g.Target);

            Assert.IsTrue(result.Correct);
            Assert.AreEqual(15, g.Score);
            Assert.AreEqual(1, g.Streak);
            Assert.AreEqual(GamePhase.Reveal, g.Phase);
        }

        [Test]
        public void CorrectGuess_AfterAsking_ScoresTen()
        {
            var g = NewGame();
            g.StartRound();
            g.MarkAsked();
            g.SubmitGuess(g.Target);

            Assert.AreEqual(10, g.Score);
            Assert.AreEqual(1, g.Streak);
        }

        [Test]
        public void WrongGuess_ScoresZero_AndResetsStreak()
        {
            var g = NewGame();

            // Round 1: build a streak with a correct guess.
            g.StartRound();
            g.SubmitGuess(g.Target);
            Assert.AreEqual(1, g.Streak);

            // Round 2: a wrong guess resets it.
            g.StartRound();
            var result = g.SubmitGuess(WrongChoice(g));

            Assert.IsFalse(result.Correct);
            Assert.AreEqual(15, g.Score, "score unchanged by the wrong guess");
            Assert.AreEqual(0, g.Streak);
            Assert.AreEqual(GamePhase.Reveal, g.Phase);
        }

        [Test]
        public void SubmitGuess_OutsideRound_Throws()
        {
            var g = NewGame();
            Assert.Throws<InvalidOperationException>(() => g.SubmitGuess(new Lang("x", "X", "Xland", "Nowhere", "common")));
        }

        [Test]
        public void FullLoop_AccumulatesScoreAcrossRounds()
        {
            var g = NewGame(7);

            g.StartRound();
            g.SubmitGuess(g.Target);          // +15
            g.StartRound();
            g.MarkAsked();
            g.SubmitGuess(g.Target);          // +10
            g.StartRound();
            g.SubmitGuess(WrongChoice(g));    // +0

            Assert.AreEqual(25, g.Score);
            Assert.AreEqual(0, g.Streak);
            Assert.AreEqual(3, g.RoundNumber);
        }
    }
}
