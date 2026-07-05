using System;

namespace AccentGuesser.Core
{
    /// <summary>
    /// One playable clip + its origin. Mirrors a row of the prep pipeline's SQLite
    /// <c>clips</c> table (see brief §7), but with camelCase field names so Unity's
    /// <c>JsonUtility</c> can deserialize the <c>clips.json</c> manifest directly.
    ///
    /// The <c>db_to_clips_json.sh</c> tool aliases the snake_case DB columns
    /// (lang_id) to these names when exporting the manifest, keeping the two in sync.
    /// </summary>
    [Serializable]
    public sealed class ClipInfo
    {
        /// <summary>Primary key from the DB (optional for the JSON manifest).</summary>
        public int id;

        /// <summary>Relative path under StreamingAssets, e.g. "clips/es_419_00412.ogg".</summary>
        public string file;

        /// <summary>FLEURS lang_id, e.g. "es_419".</summary>
        public string langId;

        /// <summary>Display name, e.g. "Spanish".</summary>
        public string language;

        /// <summary>Origin country (the answer), e.g. "Mexico".</summary>
        public string country;

        /// <summary>Continent, e.g. "North America".</summary>
        public string continent;

        /// <summary>Transcription of the utterance (may be empty).</summary>
        public string transcription;

        /// <summary>"common" | "all".</summary>
        public string difficulty;

        public ClipInfo() { }

        public ClipInfo(string file, string langId, string language, string country,
                        string continent, string transcription, string difficulty, int id = 0)
        {
            this.id = id;
            this.file = file;
            this.langId = langId;
            this.language = language;
            this.country = country;
            this.continent = continent;
            this.transcription = transcription;
            this.difficulty = difficulty;
        }

        /// <summary>Project this clip's origin metadata into a <see cref="Lang"/>.</summary>
        public Lang ToLang() => new Lang(langId, language, country, continent, difficulty);
    }
}
