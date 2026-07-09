using System;
using Unity.Netcode;

namespace AccentGuesser.Net
{
    /// <summary>
    /// The client-visible phase of a round. Mirrors <see cref="AccentGuesser.Core.MatchPhase"/>
    /// but is a wire type so the Core assembly stays netcode-free.
    /// </summary>
    public enum NetPhase : byte
    {
        Setup = 0,
        Listen = 1,
        Reveal = 2
    }

    /// <summary>
    /// One roster entry as seen by clients. Deliberately excludes the guess TEXT until reveal —
    /// only <see cref="HasLocked"/> replicates during LISTEN so nobody can copy another's answer
    /// (design spec §"Networking contract & secrecy").
    /// </summary>
    public struct PlayerView : INetworkSerializable, IEquatable<PlayerView>
    {
        public string Id;
        public string Name;
        public int Score;
        public int Streak;
        public bool HasLocked;
        public bool HasAsked;   // spent their one question this round

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Name);
            s.SerializeValue(ref Score);
            s.SerializeValue(ref Streak);
            s.SerializeValue(ref HasLocked);
            s.SerializeValue(ref HasAsked);
        }

        public bool Equals(PlayerView o) =>
            Id == o.Id && Name == o.Name && Score == o.Score && Streak == o.Streak
            && HasLocked == o.HasLocked && HasAsked == o.HasAsked;
    }

    /// <summary>
    /// The full redacted snapshot broadcast to clients each time the host mutates round state.
    /// Contains NO target and NO fact sheet — the answer only ever arrives in <see cref="RoundResultView"/>
    /// at REVEAL.
    /// </summary>
    public struct RoundView : INetworkSerializable
    {
        public NetPhase Phase;
        public int RoundNumber;
        public string ClipId;        // clip file path; the client loads audio locally, never the answer
        public double TimerDeadline;  // server time (NetworkManager.ServerTime.Time) the timer fires
        public PlayerView[] Roster;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            byte phase = (byte)Phase;
            s.SerializeValue(ref phase);
            Phase = (NetPhase)phase;
            s.SerializeValue(ref RoundNumber);
            s.SerializeValue(ref ClipId);
            s.SerializeValue(ref TimerDeadline);

            int count = Roster?.Length ?? 0;
            s.SerializeValue(ref count);
            if (s.IsReader) Roster = new PlayerView[count];
            for (int i = 0; i < count; i++)
                Roster[i].NetworkSerialize(s);
        }
    }

    /// <summary>Per-player outcome revealed at REVEAL — the first time guess text and points cross the wire.</summary>
    public struct PlayerResultView : INetworkSerializable
    {
        public string Id;
        public string Guess;
        public bool Correct;
        public int Points;
        public int NewScore;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Id);
            s.SerializeValue(ref Guess);
            s.SerializeValue(ref Correct);
            s.SerializeValue(ref Points);
            s.SerializeValue(ref NewScore);
        }
    }

    /// <summary>The REVEAL payload: the answer plus every player's outcome.</summary>
    public struct RoundResultView : INetworkSerializable
    {
        public string TargetLanguage;
        public string TargetCountry;
        public PlayerResultView[] Results;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref TargetLanguage);
            s.SerializeValue(ref TargetCountry);

            int count = Results?.Length ?? 0;
            s.SerializeValue(ref count);
            if (s.IsReader) Results = new PlayerResultView[count];
            for (int i = 0; i < count; i++)
                Results[i].NetworkSerialize(s);
        }
    }
}
