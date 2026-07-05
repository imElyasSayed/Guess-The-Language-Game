using System;
using System.Collections.Generic;

namespace AccentGuesser.Core
{
    /// <summary>The three phases of the round loop (brief §4).</summary>
    public enum GamePhase
    {
        Setup,
        Round,
        Reveal
    }

    /// <summary>
    /// Pure, MonoBehaviour-free phase state machine that owns the single-player game state
    /// (brief §2, §4). Fully unit-testable: inject an <see cref="IClipCatalog"/> and a
    /// <see cref="System.Random"/>.
    ///
    /// Lifecycle: Setup --StartRound--> Round --SubmitGuess--> Reveal --StartRound--> Round ...
    /// </summary>
    public sealed class GameController
    {
        private readonly IClipCatalog _catalog;
        private readonly Random _rng;

        public GameController(IClipCatalog catalog, Random rng)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
        }

        // ---- State ------------------------------------------------------------
        public GamePhase Phase { get; private set; } = GamePhase.Setup;
        public int Score { get; private set; }
        public int Streak { get; private set; }
        public int RoundNumber { get; private set; }

        /// <summary>True once the single per-round question has been consumed (one-question lock).</summary>
        public bool Asked { get; private set; }

        public ClipInfo CurrentClip { get; private set; }
        public Lang Target { get; private set; }
        public IReadOnlyList<Lang> Choices { get; private set; } = Array.Empty<Lang>();

        /// <summary>Populated on <see cref="SubmitGuess"/>; drives the Reveal overlay.</summary>
        public ScoreResult LastResult { get; private set; }
        public Lang LastGuess { get; private set; }

        // ---- Transitions ------------------------------------------------------

        /// <summary>
        /// Begin a new round with the given filter. Valid from Setup or Reveal.
        /// Resets the one-question lock, draws a clip + 4 choices, and enters Round.
        /// </summary>
        public void StartRound(string difficulty = null, string region = null)
        {
            if (Phase == GamePhase.Round)
                throw new InvalidOperationException("A round is already in progress.");

            var filter = new ClipFilter(difficulty, region);
            var round = RoundFactory.CreateRound(_catalog, filter, _rng);

            CurrentClip = round.Clip;
            Target = round.Target;
            Choices = round.Choices;
            Asked = false;
            LastGuess = null;
            RoundNumber++;
            Phase = GamePhase.Round;
        }

        /// <summary>
        /// Consume the round's single question. Returns false if not in Round phase or the
        /// question was already asked (the lock is already engaged). The App disables the
        /// "Ask the Keep" button on a false/after-first result.
        /// </summary>
        public bool MarkAsked()
        {
            if (Phase != GamePhase.Round || Asked)
                return false;
            Asked = true;
            return true;
        }

        /// <summary>
        /// Resolve a guess: score it (bonus if the player did NOT ask), update score/streak,
        /// and enter Reveal. Valid only in Round phase.
        /// </summary>
        public ScoreResult SubmitGuess(Lang choice)
        {
            if (Phase != GamePhase.Round)
                throw new InvalidOperationException("Can only guess during the Round phase.");
            if (choice == null)
                throw new ArgumentNullException(nameof(choice));

            bool correct = choice.Equals(Target);
            var result = ScoreCalculator.Evaluate(correct, Asked, Streak);

            Score += result.Points;
            Streak = result.NewStreak;
            LastResult = result;
            LastGuess = choice;
            Phase = GamePhase.Reveal;
            return result;
        }
    }
}
