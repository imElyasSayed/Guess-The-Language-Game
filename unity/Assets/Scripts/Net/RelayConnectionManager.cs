using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

namespace AccentGuesser.Net
{
    /// <summary>
    /// Unity Relay connection over UnityTransport (design spec §"Connection layer"). The host
    /// allocates a relay and gets a short join code to share; the friend joins by that code — no
    /// port forwarding, works over the internet. Requires the project to be linked to a Unity
    /// Gaming Services project once (see docs runbook). Swaps 1:1 with Steam later.
    /// </summary>
    public sealed class RelayConnectionManager : IConnectionManager
    {
        private readonly int _maxConnections;

        public RelayConnectionManager(int maxConnections = 7) // 8 total incl. host
        {
            _maxConnections = maxConnections;
        }

        private static async Task EnsureSignedIn()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<string> HostAsync()
        {
            await EnsureSignedIn();

            Allocation alloc = await RelayService.Instance.CreateAllocationAsync(_maxConnections);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(alloc.AllocationId);

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(new RelayServerData(alloc, "dtls"));

            return NetworkManager.Singleton.StartHost() ? joinCode : "";
        }

        public async Task<bool> JoinAsync(string joinCode)
        {
            await EnsureSignedIn();

            JoinAllocation alloc;
            try { alloc = await RelayService.Instance.JoinAllocationAsync(joinCode.Trim()); }
            catch (Exception e) { Debug.LogWarning($"[Relay] join failed: {e.Message}"); return false; }

            var utp = NetworkManager.Singleton.GetComponent<UnityTransport>();
            utp.SetRelayServerData(new RelayServerData(alloc, "dtls"));

            return NetworkManager.Singleton.StartClient();
        }
    }
}
