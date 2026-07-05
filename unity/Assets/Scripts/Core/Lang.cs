using System;

namespace AccentGuesser.Core
{
    /// <summary>
    /// A guessable language + its origin metadata. Pure POCO, no Unity dependency.
    /// One <see cref="Lang"/> corresponds to a FLEURS <c>lang_id</c> (e.g. "es_419").
    /// Derived from the distinct languages present in the clip catalog.
    /// </summary>
    [Serializable]
    public sealed class Lang : IEquatable<Lang>
    {
        /// <summary>FLEURS lang_id, e.g. "es_419". This is the identity used everywhere.</summary>
        public string Id;

        /// <summary>Display name, e.g. "Spanish".</summary>
        public string Language;

        /// <summary>Origin country (the answer), e.g. "Mexico".</summary>
        public string Country;

        /// <summary>Continent, e.g. "North America".</summary>
        public string Continent;

        /// <summary>"common" | "all" — used for difficulty filtering.</summary>
        public string Difficulty;

        public Lang() { }

        public Lang(string id, string language, string country, string continent, string difficulty)
        {
            Id = id;
            Language = language;
            Country = country;
            Continent = continent;
            Difficulty = difficulty;
        }

        public bool Equals(Lang other) => other != null && string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => Equals(obj as Lang);
        public override int GetHashCode() => Id != null ? Id.GetHashCode() : 0;
        public override string ToString() => $"{Language} ({Country})";
    }
}
