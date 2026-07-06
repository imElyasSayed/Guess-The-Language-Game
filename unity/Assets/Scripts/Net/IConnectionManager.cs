using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace AccentGuesser.Net
{
    /// <summary>
    /// Transport-agnostic host/join seam (design spec §"Connection layer"). Everything above this
    /// interface — the match, the UI — is identical whether we connect by IP, by Unity Relay, or
    /// (later) by Steam. A <c>SteamConnectionManager</c> will implement the same two methods.
    /// </summary>
    public interface IConnectionManager
    {
        /// <summary>Start hosting. Returns a join code (empty string for direct-IP, which uses the shown IP).</summary>
        Task<string> HostAsync();

        /// <summary>Join a host. <paramref name="joinInfo"/> is a Relay join code or an "ip:port" for direct.</summary>
        Task<bool> JoinAsync(string joinInfo);
    }

    /// <summary>
    /// Zero-dependency LAN / same-machine connection over UnityTransport. No Unity Gaming Services
    /// account needed — ideal for a first two-instance playtest. For a remote friend, use
    /// <see cref="RelayConnectionManager"/> instead.
    /// </summary>
    public sealed class DirectConnectionManager : IConnectionManager
    {
        private readonly string _bindIp;
        private readonly ushort _port;

        public DirectConnectionManager(string bindIp = "0.0.0.0", ushort port = 7777)
        {
            _bindIp = bindIp;
            _port = port;
        }

        public Task<string> HostAsync()
        {
            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetConnectionData(_bindIp, _port, "0.0.0.0");
            bool ok = NetworkManager.Singleton.StartHost();
            return Task.FromResult(ok ? $"{GetLocalIPv4()}:{_port}" : "");
        }

        public Task<bool> JoinAsync(string joinInfo)
        {
            string ip = joinInfo;
            ushort port = _port;
            int colon = joinInfo.LastIndexOf(':');
            if (colon > 0)
            {
                ip = joinInfo.Substring(0, colon);
                ushort.TryParse(joinInfo.Substring(colon + 1), out port);
            }

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetConnectionData(ip, port);
            return Task.FromResult(NetworkManager.Singleton.StartClient());
        }

        private static string GetLocalIPv4()
        {
            foreach (var ip in System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    return ip.ToString();
            return "127.0.0.1";
        }
    }
}
