using System;
using AccentGuesser.Core;
using NUnit.Framework;

namespace AccentGuesser.EditModeTests
{
    public class RoundFactoryTests
    {
        private static IClipCatalog SixLangCatalog() =>
            new FakeClipCatalog(
                ("a", "Alpha"), ("b", "Bravo"), ("c", "Charlie"),
                ("d", "Delta"), ("e", "Echo"), ("f", "Foxtrot"));

        [Test]
        public void CreateRound_ClipMatchesTargetLanguage()
        {
            var round = RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, new Random(42));
            Assert.AreEqual(round.Target.Id, round.Clip.langId);
        }

        [Test]
        public void CreateRound_IsDeterministic_ForAGivenSeed()
        {
            var a = RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, new Random(1234));
            var b = RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, new Random(1234));

            Assert.AreEqual(a.Target.Id, b.Target.Id, "same seed must yield the same target");
        }

        [Test]
        public void CreateRound_Succeeds_WithSingleLanguage()
        {
            var single = new FakeClipCatalog(("a", "Alpha"));
            var round = RoundFactory.CreateRound(single, ClipFilter.None, new Random(1));
            Assert.AreEqual("a", round.Target.Id);
        }

        [Test]
        public void CreateRound_Throws_WhenNoLanguagesAvailable()
        {
            var empty = new FakeClipCatalog();
            Assert.Throws<InvalidOperationException>(
                () => RoundFactory.CreateRound(empty, ClipFilter.None, new Random(1)));
        }

        [Test]
        public void CreateRound_NullArgs_Throw()
        {
            Assert.Throws<ArgumentNullException>(
                () => RoundFactory.CreateRound(null, ClipFilter.None, new Random(1)));
            Assert.Throws<ArgumentNullException>(
                () => RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, null));
        }
    }
}
