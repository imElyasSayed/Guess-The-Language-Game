using System;
using System.Linq;
using AccentGuesser.Core;
using NUnit.Framework;

namespace AccentGuesser.EditModeTests
{
    /// <summary>
    /// Unit tests for the pure host-authoritative <see cref="MatchController"/>
    /// (design spec: docs/superpowers/specs/2026-07-06-multiplayer-design.md).
    /// No netcode — same style as <see cref="GameControllerTests"/>.
    /// </summary>
    public class MatchControllerTests
    {
        private static MatchController NewMatch(int seed = 1) =>
            new MatchController(
                new FakeClipCatalog(("a", "Alpha"), ("b", "Bravo"), ("c", "Charlie"),
                                    ("d", "Delta"), ("e", "Echo")),
                new Random(seed));

        private static string Correct(MatchController m) => m.Target.Language;
        private static string Wrong(MatchController m) => m.Target.Language == "Alpha" ? "Bravo" : "Alpha";
        private static Player P(MatchController m, string id) => m.Roster.First(p => p.Id == id);

        // ---- Setup / roster ---------------------------------------------------

        [Test]
        public void StartsInSetup_WithEmptyRoster()
        {
            var m = NewMatch();
            Assert.AreEqual(MatchPhase.Setup, m.Phase);
            Assert.AreEqual(0, m.Roster.Count);
            Assert.AreEqual(0, m.RoundNumber);
        }

        [Test]
        public void StartRound_WithNoPlayers_Throws()
        {
            var m = NewMatch();
            Assert.Throws<InvalidOperationException>(() => m.StartRound());
        }

        [Test]
        public void AddPlayer_DuplicateId_Throws()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            Assert.Throws<ArgumentException>(() => m.AddPlayer("p1", "Dup"));
        }

        [Test]
        public void StartRound_EntersListen_AssignsClip_FreshQuestions()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            Assert.AreEqual(MatchPhase.Listen, m.Phase);
            Assert.AreEqual(1, m.RoundNumber);
            Assert.IsNotNull(m.CurrentClip);
            Assert.IsFalse(P(m, "p1").HasAsked);
            Assert.IsFalse(P(m, "p2").HasAsked);
        }

        [Test]
        public void StartRound_WhileListen_Throws()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.StartRound();
            Assert.Throws<InvalidOperationException>(() => m.StartRound());
        }

        // ---- One question each ------------------------------------------------

        [Test]
        public void EveryPlayer_MayAskOnce_RepeatsRejected()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            Assert.IsTrue(m.MarkAsked("p1"), "every player owns a question");
            Assert.IsTrue(m.MarkAsked("p2"), "…including the second player, same round");
            Assert.IsFalse(m.MarkAsked("p1"), "second ask rejected (one question each)");
            Assert.IsFalse(m.MarkAsked("ghost"), "unknown player rejected");
        }

        [Test]
        public void LockedPlayer_CannotAsk_AskFirstThenGuess()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two"); // keeps the round open past p1's lock
            m.StartRound();

            m.LockGuess("p1", Correct(m));
            Assert.IsFalse(m.MarkAsked("p1"), "a locked guess seals the round — no asking after");
        }

        [Test]
        public void Questions_RefreshEachRound()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.StartRound();
            Assert.IsTrue(m.MarkAsked("p1"));
            EndRoundByTimer(m);

            m.StartRound();
            Assert.IsTrue(m.MarkAsked("p1"), "a new round grants a fresh question");
        }

        // ---- Scoring tiers (Trust Your Ear) -----------------------------------

        [Test]
        public void NoQuestion_Correct_Scores15()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.StartRound();

            m.LockGuess("p1", Correct(m));   // trusted the ear, never asked
            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
            Assert.AreEqual(15, P(m, "p1").Score);
            Assert.AreEqual(1, P(m, "p1").Streak);
        }

        [Test]
        public void AskedThenLocked_Correct_Scores10()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.StartRound();

            m.MarkAsked("p1");
            m.LockGuess("p1", Correct(m));   // leaned on the Keep
            Assert.AreEqual(10, P(m, "p1").Score);
        }

        [Test]
        public void TierIsPerPlayer_ByOwnQuestion()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            // p1 spends their question; p2 never asks. Hearing SOMEONE ELSE'S answer is free —
            // only your own question drops you to the +10 tier.
            m.MarkAsked("p1");
            m.LockGuess("p2", Correct(m));
            m.LockGuess("p1", Correct(m));

            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
            Assert.AreEqual(15, P(m, "p2").Score, "no question of their own = +15 tier");
            Assert.AreEqual(10, P(m, "p1").Score, "asked the Keep = +10 tier");
        }

        [Test]
        public void WrongGuess_ScoresZero_ResetsStreak()
        {
            var m = NewMatch(7);
            m.AddPlayer("p1", "One");

            m.StartRound();
            m.LockGuess("p1", Correct(m));       // +15, streak 1
            Assert.AreEqual(1, P(m, "p1").Streak);

            m.StartRound();
            m.LockGuess("p1", Wrong(m));          // +0, streak reset
            Assert.AreEqual(15, P(m, "p1").Score);
            Assert.AreEqual(0, P(m, "p1").Streak);
        }

        // ---- Reveal triggers --------------------------------------------------

        [Test]
        public void Reveal_FiresWhenAllPlayersLocked()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            m.LockGuess("p1", Correct(m));
            Assert.AreEqual(MatchPhase.Listen, m.Phase, "still waiting on p2");
            m.LockGuess("p2", Wrong(m));
            Assert.AreEqual(MatchPhase.Reveal, m.Phase, "all locked -> reveal");
        }

        [Test]
        public void Reveal_FiresOnTimer_UnlockedPlayersScoreZero()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            m.LockGuess("p1", Correct(m));   // p2 goes idle
            m.ExpireTimer();

            Assert.AreEqual(MatchPhase.Reveal, m.Phase);
            Assert.AreEqual(15, P(m, "p1").Score);
            Assert.AreEqual(0, P(m, "p2").Score, "idle player scores nothing");
            Assert.IsFalse(P(m, "p2").HasLocked);
        }

        [Test]
        public void FirstLockIsFinal_SecondLockIgnored()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two"); // keep round open so we can attempt a re-lock
            m.StartRound();

            Assert.IsTrue(m.LockGuess("p1", Wrong(m)));
            Assert.IsFalse(m.LockGuess("p1", Correct(m)), "second lock must be rejected");

            m.ExpireTimer();
            Assert.AreEqual(0, P(m, "p1").Score, "the first (wrong) guess stands");
        }

        // ---- Drop-in / disconnect --------------------------------------------

        [Test]
        public void JoinDuringListen_Pending_EntersNextRound()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.StartRound();

            m.AddPlayer("p2", "Two");                    // mid-round join
            Assert.AreEqual(1, m.Roster.Count, "joiner is pending, not active this round");

            m.ExpireTimer();
            m.StartRound();
            Assert.AreEqual(2, m.Roster.Count, "joiner is active next round");
            Assert.AreEqual(0, P(m, "p2").Score);
        }

        [Test]
        public void DisconnectMidRound_UngatesReveal()
        {
            var m = NewMatch();
            m.AddPlayer("p1", "One");
            m.AddPlayer("p2", "Two");
            m.StartRound();

            m.LockGuess("p1", Correct(m));
            Assert.AreEqual(MatchPhase.Listen, m.Phase);

            m.RemovePlayer("p2");                         // last un-locked player leaves
            Assert.AreEqual(MatchPhase.Reveal, m.Phase, "removing the straggler triggers reveal");
            Assert.AreEqual(15, P(m, "p1").Score);
        }

        [Test]
        public void SoloMatch_ReproducesSinglePlayerScoring()
        {
            var m = NewMatch();
            m.AddPlayer("solo", "Solo");

            m.StartRound();
            m.LockGuess("solo", Correct(m));            // no question -> +15
            Assert.AreEqual(15, P(m, "solo").Score);

            m.StartRound();
            m.MarkAsked("solo");
            m.LockGuess("solo", Correct(m));            // asked -> +10
            Assert.AreEqual(25, P(m, "solo").Score);
        }

        // ---- helpers ----------------------------------------------------------

        private static void EndRoundByTimer(MatchController m) => m.ExpireTimer();
    }
}
