using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using FishNet.Managing.Scened;

public class RoundManager : NetworkBehaviour
{
    public static RoundManager Instance { get; private set; }

    [Header("Round")]
    [SerializeField] private int roundSeconds = 180;

    private int _timeLeft;
    private bool _roundRunning;

    private readonly Dictionary<NetworkConnection, int> _scores = new();
    private NetworkConnection _currentIt;
    private PlayerSpawner _spawner;
    public static bool IsRoundRunning => Instance != null && Instance._roundRunning;

    private void Awake() => Instance = this;

    public override void OnStartServer()
    {
        base.OnStartServer();

        _spawner = FindFirstObjectByType<PlayerSpawner>();
        _spawner?.SpawnAllConnected();

        _currentIt = FindCurrentIt();
        StartNewRoundServer();
    }

    [Server]
    public void StartNewRoundServer()
    {
        _scores.Clear();
        _timeLeft = roundSeconds;
        _roundRunning = true;

        _spawner ??= FindFirstObjectByType<PlayerSpawner>();
        _spawner?.ResetAllToSpawns();

        _currentIt = FindCurrentIt();

        SetRoundActiveObserversRpc(true);

        StopAllCoroutines();
        StartCoroutine(StartRoundAndTick());
    }

    [Server]
    private System.Collections.IEnumerator StartRoundAndTick()
    {
        yield return null;

        BroadcastScores();
        BroadcastTimer(_timeLeft);

        yield return StartCoroutine(TickTimer());
    }

    [Server]
    private System.Collections.IEnumerator TickTimer()
    {
        while (_roundRunning && _timeLeft > 0)
        {
            yield return new WaitForSeconds(1f);
            _timeLeft--;

            AwardSurvivorTick();
            BroadcastTimer(_timeLeft);
        }

        _roundRunning = false;
        RoundOverObserversRpc();
        SetRoundActiveObserversRpc(false);
    }

    [Server]
    private void AwardSurvivorTick()
    {
        foreach (KeyValuePair<int, NetworkConnection> kvp in NetworkManager.ServerManager.Clients)
        {
            var c = kvp.Value;
            if (c == null || c == _currentIt) continue;

            if (!_scores.ContainsKey(c)) _scores[c] = 0;
            _scores[c] += 1;
        }

        BroadcastScores();
    }

    [Server]
    public void NotifyHandoff(NetworkConnection newIt) => _currentIt = newIt;

    [Server]
    private void BroadcastScores()
    {
        var conns = NetworkManager.ServerManager.Clients.Values.ToList();
        var names = new string[conns.Count];
        var scores = new int[conns.Count];

        for (int i = 0; i < conns.Count; i++)
        {
            var c = conns[i];
            names[i] = FindNameFor(c) ?? $"Player {c.ClientId}";
            scores[i] = _scores.TryGetValue(c, out int s) ? s : 0;
        }

        UpdateScoreboardObserversRpc(names, scores);
    }

    [ObserversRpc]
    private void UpdateScoreboardObserversRpc(string[] names, int[] scores)
        => ScoreboardUI.UpdateAll(names, scores);

    [ObserversRpc]
    private void BroadcastTimer(int secondsLeft)
        => ScoreboardUI.UpdateTimer(secondsLeft);

    [ObserversRpc]
    private void RoundOverObserversRpc()
        => ScoreboardUI.ShowRoundOver();

    [ObserversRpc]
    private void SetRoundActiveObserversRpc(bool running)
        => RoundOverUI.SetRoundActiveClient(running);

    [ServerRpc(RequireOwnership = false)]
    public void RequestUiSnapshotServerRpc(NetworkConnection caller = null)
    {
        if (caller == null) return;

        var conns = NetworkManager.ServerManager.Clients.Values.ToList();
        var names = new string[conns.Count];
        var scores = new int[conns.Count];

        for (int i = 0; i < conns.Count; i++)
        {
            var c = conns[i];
            names[i] = FindNameFor(c) ?? $"Player {c.ClientId}";
            scores[i] = _scores.TryGetValue(c, out int s) ? s : 0;
        }

        SendUiSnapshotTargetRpc(caller, names, scores, _timeLeft, _roundRunning);
    }

    [TargetRpc]
    private void SendUiSnapshotTargetRpc(NetworkConnection target, string[] names, int[] scores, int timeLeft, bool running)
    {
        ScoreboardUI.UpdateAll(names, scores);
        ScoreboardUI.UpdateTimer(timeLeft);
        RoundOverUI.SetRoundActiveClient(running);
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReturnToLobbyServerRpc(NetworkConnection caller = null)
    {
        if (caller == null || caller.ClientId != 0) return;

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkManager.ServerManager.Despawn(NetworkObject);

        var sld = new SceneLoadData("Lobby")
        {
            ReplaceScenes = ReplaceOption.All
        };
        NetworkManager.SceneManager.LoadGlobalScenes(sld);
    }

    [ServerRpc(RequireOwnership = false)]
    public void HostShutdownAllServerRpc(string reason, NetworkConnection caller = null)
    {
        if (caller == null || caller.ClientId != 0) return;
        PreShutdownObserversRpc(string.IsNullOrWhiteSpace(reason) ? "Host ended the game." : reason);
    }

    [ObserversRpc]
    private void PreShutdownObserversRpc(string reason)
    {
        DisconnectNotice.Message = reason;
    }

    [Server]
    private NetworkConnection FindCurrentIt()
    {
        var allTags = FindObjectsByType<TagStatus>(FindObjectsSortMode.None);
        foreach (var tag in allTags)
            if (tag.IsIt.Value)
                return tag.NetworkObject?.Owner;

        return null;
    }

    [Server]
    private string FindNameFor(NetworkConnection c)
    {
        if (c?.Objects != null)
        {
            foreach (var no in c.Objects)
            {
                var id = no.gameObject.GetComponent<PlayerIdentity>();
                if (id != null)
                {
                    var n = id.DisplayName.Value;
                    if (!string.IsNullOrWhiteSpace(n)) return n;
                }
            }
        }
        return null;
    }
}
