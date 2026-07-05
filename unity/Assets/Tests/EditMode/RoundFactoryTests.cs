using System;
using System.Collections.Generic;
using System.Linq;
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
        public void CreateRound_ProducesFourDistinctChoices_IncludingTarget()
        {
            var round = RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, new Random(1));

            Assert.AreEqual(4, round.Choices.Count);
            Assert.AreEqual(4, round.Choices.Select(c => c.Id).Distinct().Count(), "choices must be distinct");
            CollectionAssert.Contains(round.Choices.Select(c => c.Id).ToList(), round.Target.Id,
                "the target must be among the choices");
        }

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

            Assert.AreEqual(a.Target.Id, b.Target.Id);
            CollectionAssert.AreEqual(
                a.Choices.Select(c => c.Id).ToList(),
                b.Choices.Select(c => c.Id).ToList(),
                "same seed must yield the same target and choice order");
        }

        [Test]
        public void CreateRound_Throws_WhenFewerThanFourLanguages()
        {
            var tiny = new FakeClipCatalog(("a", "Alpha"), ("b", "Bravo"), ("c", "Charlie"));
            Assert.Throws<InvalidOperationException>(
                () => RoundFactory.CreateRound(tiny, ClipFilter.None, new Random(1)));
        }

        [Test]
        public void CreateRound_NullArgs_Throw()
        {
            Assert.Throws<ArgumentNullException>(
                () => RoundFactory.CreateRound(null, ClipFilter.None, new Random(1)));
            Assert.Throws<ArgumentNullException>(
                () => RoundFactory.CreateRound(SixLangCatalog(), ClipFilter.None, null));
        }

        [Test]
        public void Shuffle_IsDeterministic_AndPreservesElements()
        {
            var list1 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            var list2 = new List<int> { 1, 2, 3, 4, 5, 6, 7, 8 };
            RoundFactory.Shuffle(list1, new Random(99));
            RoundFactory.Shuffle(list2, new Random(99));

            CollectionAssert.AreEqual(list1, list2);
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7, 8 }, list1);
        }
    }
}
