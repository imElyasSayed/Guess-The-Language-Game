using System;
using System.Threading.Tasks;
using AccentGuesser.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace AccentGuesser.Services
{
    /// <summary>
    /// STUB HTTP client for the hosted oracle relay (brief §10). The relay is built
    /// separately (the parallel service); this just matches its contract.
    ///
    /// Contract targeted (per build task):
    ///   POST {relayUrl}/oracle
    ///   request  body: { "question": string,
    ///                    "factSheet": { "language", "country", "continent", "forbidden": [..] } }
    ///   response body: { "answer": string }
    ///
    /// NOTE: The brief §10 also describes a leaner client contract ({ roundToken, questionText }
    /// with the relay holding the fact sheet). The build task pins THIS fact-sheet-in-body
    /// contract, so that is what we implement. Swap the request shape here if the relay lands
    /// on the roundToken form.
    ///
    /// SECRETS: never put the Anthropic key here. It lives only in the relay (brief §3, §56).
    /// On any failure the Keep just grunts in character — the round stays playable (§11).
    /// </summary>
    public sealed class HttpOracleClient : IOracleClient
    {
        private readonly string _oracleUrl;
        private readonly Func<string, string[]> _forbiddenLookup;
        private readonly int _timeoutSeconds;

        /// <param name="relayBaseUrl">e.g. "https://say-again-relay.example.workers.dev".</param>
        /// <param name="forbiddenLookup">
        /// Maps a lang_id to its forbidden-word list (from the per-language forbidden JSON fact
        /// sheets in StreamingAssets). May be null → an empty list is sent.
        /// </param>
        public HttpOracleClient(string relayBaseUrl, Func<string, string[]> forbiddenLookup = null, int timeoutSeconds = 15)
        {
            _oracleUrl = relayBaseUrl.TrimEnd('/') + "/oracle";
            _forbiddenLookup = forbiddenLookup;
            _timeoutSeconds = timeoutSeconds;
        }

        [Serializable]
        private class FactSheet
        {
            public string language;
            public string country;
            public string continent;
            public string[] forbidden;
        }

        [Serializable]
        private class OracleRequest
        {
            public string question;
            public FactSheet factSheet;
        }

        [Serializable]
        private class OracleResponse
        {
            public string answer;
        }

        private const string Fallback =
            "The Keep's had too much ale — he just shrugs and waves you off.";

        public async Task<string> AskAsync(string question, ClipInfo target)
        {
            try
            {
                var payload = new OracleRequest
                {
                    question = question,
                    factSheet = new FactSheet
                    {
                        language = target.language,
                        country = target.country,
                        continent = target.continent,
                        forbidden = _forbiddenLookup?.Invoke(target.langId) ?? Array.Empty<string>()
                    }
                };

                string json = JsonUtility.ToJson(payload);

                using var req = new UnityWebRequest(_oracleUrl, UnityWebRequest.kHttpVerbPOST);
                byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = _timeoutSeconds;

                await SendAsync(req);

                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning($"[HttpOracleClient] relay error: {req.error}");
                    return Fallback;
                }

                var parsed = JsonUtility.FromJson<OracleResponse>(req.downloadHandler.text);
                return string.IsNullOrWhiteSpace(parsed?.answer) ? Fallback : parsed.answer;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HttpOracleClient] exception: {e.Message}");
                return Fallback;
            }
        }

        /// <summary>Await a UnityWebRequest without blocking the main thread.</summary>
        private static Task SendAsync(UnityWebRequest req)
        {
            var tcs = new TaskCompletionSource<bool>();
            var op = req.SendWebRequest();
            op.completed += _ => tcs.TrySetResult(true);
            return tcs.Task;
        }
    }
}
