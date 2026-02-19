using FishNet;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

    public readonly SyncVar<string> WinnerName = new SyncVar<string>("");
    private readonly Dictionary<NetworkConnection, PlayerIdentity> _identityByConn = new();

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
    public void RegisterIdentity(NetworkConnection conn, PlayerIdentity id)
    {
        if (conn == null || id == null) return;
        _identityByConn[conn] = id;
    }

    [Server]
    public void UnregisterIdentity(NetworkConnection conn)
    {
        if (conn == null) return;
        _identityByConn.Remove(conn);
    }

    [Server]
    public void StartNewRoundServer()
    {
        _scores.Clear();

        // IMPORTANT: clear winner before round starts so it doesn't stick.
        WinnerName.Value = "";
        ClearRoundUiObserversRpc();

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

        ComputeWinnerServer();
        BroadcastScores(); // optional but nice so final scoreboard is correct

        // IMPORTANT: if SetRoundActiveClient(false) disables gameplay/UI roots,
        // do it BEFORE showing round over UI.
        SetRoundActiveObserversRpc(false);
        RoundOverObserversRpc();
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
    private void ComputeWinnerServer()
    {
        var conns = NetworkManager.ServerManager.Clients.Values.ToList();

        NetworkConnection bestConn = null;
        int bestScore = int.MinValue;

        foreach (var c in conns)
        {
            int s = _scores.TryGetValue(c, out int sc) ? sc : 0;
            if (s > bestScore)
            {
                bestScore = s;
                bestConn = c;
            }
        }

        WinnerName.Value = FindNameFor(bestConn) ?? (bestConn != null ? $"Player {bestConn.ClientId}" : "Nobody");
    }

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

    [ObserversRpc(BufferLast = true)]
    private void UpdateScoreboardObserversRpc(string[] names, int[] scores)
        => ScoreboardUI.UpdateAll(names, scores);

    [ObserversRpc(BufferLast = true)]
    private void BroadcastTimer(int secondsLeft)
        => ScoreboardUI.UpdateTimer(secondsLeft);

    [ObserversRpc(BufferLast = true)]
    private void RoundOverObserversRpc()
        => ScoreboardUI.ShowRoundOver();

    [ObserversRpc(BufferLast = true)]
    private void SetRoundActiveObserversRpc(bool running)
    {
        RoundOverUI.SetRoundActiveClient(running);
        if (running)
            ScoreboardUI.HideRoundOver();
    }

    [ObserversRpc(BufferLast = true)]
    private void ClearRoundUiObserversRpc()
        => ScoreboardUI.HideRoundOver();

    [ServerRpc(RequireOwnership = false)]
    public void StartNewRoundServerRpc()
    {
        if (!IsServerInitialized) return;
        StartNewRoundServer();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ReturnToLobbyServerRpc()
    {
        if (!IsServerInitialized) return;
        ReturnToLobbyServer();
    }

    [Server]
    private void ReturnToLobbyServer()
    {
        var sld = new SceneLoadData("Lobby")
        {
            ReplaceScenes = ReplaceOption.All
        };
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }

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

        // If round isn't running, make sure round-over UI is visible for late joiners / late UI init.
        if (!running)
            ScoreboardUI.ShowRoundOver();
        else
            ScoreboardUI.HideRoundOver();
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
