using System;
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

        private static string WrongGuessText(GameController g) =>
            g.Target.Language == "Alpha" ? "Bravo" : "Alpha";

        [Test]
        public void StartsInSetupPhase()
        {
            var g = NewGame();
            Assert.AreEqual(GamePhase.Setup, g.Phase);
            Assert.AreEqual(0, g.Score);
            Assert.AreEqual(0, g.RoundNumber);
        }

        [Test]
        public void StartRound_EntersRoundPhase_WithClip()
        {
            var g = NewGame();
            g.StartRound();

            Assert.AreEqual(GamePhase.Round, g.Phase);
            Assert.AreEqual(1, g.RoundNumber);
            Assert.IsNotNull(g.CurrentClip);
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
            var result = g.SubmitGuess(g.Target.Language);

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
            g.SubmitGuess(g.Target.Language);

            Assert.AreEqual(10, g.Score);
            Assert.AreEqual(1, g.Streak);
        }

        [Test]
        public void CorrectGuess_IsCaseInsensitive()
        {
            var g = NewGame();
            g.StartRound();
            var result = g.SubmitGuess(g.Target.Language.ToUpperInvariant());

            Assert.IsTrue(result.Correct);
        }

        [Test]
        public void CorrectGuess_TrimsWhitespace()
        {
            var g = NewGame();
            g.StartRound();
            var result = g.SubmitGuess("  " + g.Target.Language + "  ");

            Assert.IsTrue(result.Correct);
        }

        [Test]
        public void WrongGuess_ScoresZero_AndResetsStreak()
        {
            var g = NewGame();

            // Round 1: build a streak with a correct guess.
            g.StartRound();
            g.SubmitGuess(g.Target.Language);
            Assert.AreEqual(1, g.Streak);

            // Round 2: a wrong guess resets it.
            g.StartRound();
            var result = g.SubmitGuess(WrongGuessText(g));

            Assert.IsFalse(result.Correct);
            Assert.AreEqual(15, g.Score, "score unchanged by the wrong guess");
            Assert.AreEqual(0, g.Streak);
            Assert.AreEqual(GamePhase.Reveal, g.Phase);
        }

        [Test]
        public void SubmitGuess_EmptyGuess_IsWrong()
        {
            var g = NewGame();
            g.StartRound();
            var result = g.SubmitGuess("");

            Assert.IsFalse(result.Correct);
            Assert.AreEqual(0, g.Streak);
        }

        [Test]
        public void SubmitGuess_NullGuess_Throws()
        {
            var g = NewGame();
            g.StartRound();
            Assert.Throws<ArgumentNullException>(() => g.SubmitGuess(null));
        }

        [Test]
        public void SubmitGuess_OutsideRound_Throws()
        {
            var g = NewGame();
            Assert.Throws<InvalidOperationException>(() => g.SubmitGuess("anything"));
        }

        [Test]
        public void FullLoop_AccumulatesScoreAcrossRounds()
        {
            var g = NewGame(7);

            g.StartRound();
            g.SubmitGuess(g.Target.Language);      // +15
            g.StartRound();
            g.MarkAsked();
            g.SubmitGuess(g.Target.Language);      // +10
            g.StartRound();
            g.SubmitGuess(WrongGuessText(g));      // +0

            Assert.AreEqual(25, g.Score);
            Assert.AreEqual(0, g.Streak);
            Assert.AreEqual(3, g.RoundNumber);
        }
    }
}
