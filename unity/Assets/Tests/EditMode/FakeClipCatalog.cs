using System;
using System.Collections.Generic;
using AccentGuesser.Core;

namespace AccentGuesser.EditModeTests
{
    /// <summary>
    /// Test double for <see cref="IClipCatalog"/>. In practice a real <see cref="JsonClipCatalog"/>
    /// built from a hand-made clip list works too, and some tests use that; this fake exists so
    /// tests can assert exactly which clip is returned for a language.
    /// </summary>
    internal sealed class FakeClipCatalog : IClipCatalog
    {
        private readonly List<Lang> _langs;
        private readonly Dictionary<string, ClipInfo> _clipByLang;

        public FakeClipCatalog(params (string id, string name)[] langs)
        {
            _langs = new List<Lang>();
            _clipByLang = new Dictionary<string, ClipInfo>();
            foreach (var (id, name) in langs)
            {
                _langs.Add(new Lang(id, name, name + "-land", "Testonia", "common"));
                _clipByLang[id] = new ClipInfo($"clips/{id}.ogg", id, name, name + "-land", "Testonia", "", "common");
            }
        }

        public IReadOnlyList<Lang> GetLanguages(ClipFilter filter) => _langs;

        public ClipInfo GetRandomClip(string langId, ClipFilter filter, Random rng)
            => _clipByLang.TryGetValue(langId, out var c) ? c : null;
    }
}
