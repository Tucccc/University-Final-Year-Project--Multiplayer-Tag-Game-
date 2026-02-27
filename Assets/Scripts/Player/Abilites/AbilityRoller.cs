using System.Collections;
using System.Collections.Generic;
using FishNet.Object;
using UnityEngine;

public class AbilityRoller : NetworkBehaviour
{
    public static AbilityRoller Instance { get; private set; }

    [Header("Config")]
    [SerializeField] private AbilityDatabase database;
    [SerializeField] private float rollDelaySeconds = 10f;

    [Header("Randomness")]
    [Tooltip("Set this once per match for stable randomness. If 0, a random seed is chosen on server start.")]
    [SerializeField] private int matchSeed = 0;

    // Track per-player roll routine + roll counts so they don't all match.
    private readonly Dictionary<PlayerAbilityManager, Coroutine> _pendingRolls = new();
    private readonly Dictionary<PlayerAbilityManager, int> _rollCount = new();
    private readonly Dictionary<PlayerAbilityManager, Queue<string>> _recentIds = new();

    [SerializeField] private int recentHistory = 2; // prevents immediate repeats

    private void Awake()
    {
        Instance = this;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        if (matchSeed == 0)
            matchSeed = Random.Range(int.MinValue, int.MaxValue);

        Debug.Log($"[AbilityRoller] Server started. matchSeed={matchSeed}");
    }

    /// Call this when a round starts (server only).
    [Server]
    public void ServerStartRound(IEnumerable<PlayerAbilityManager> players)
    {
        Debug.Log("[AbilityRoller] Round start -> scheduling initial rolls.");

        foreach (var p in players)
            ServerScheduleRoll(p, rollDelaySeconds);
    }

    /// Call this when a round ends (server only).
    [Server]
    public void ServerEndRound()
    {
        Debug.Log("[AbilityRoller] Round end -> cancelling rolls.");

        foreach (var kvp in _pendingRolls)
            if (kvp.Value != null)
                StopCoroutine(kvp.Value);

        _pendingRolls.Clear();
        _rollCount.Clear();
        _recentIds.Clear();
    }

    /// Called by PlayerAbilityManager on the SERVER after it successfully uses an ability.
    [Server]
    public void ServerOnAbilityUsed(PlayerAbilityManager player)
    {
        // Restart that player's timer.
        ServerScheduleRoll(player, rollDelaySeconds);
    }

    [Server]
    public void ServerScheduleRoll(PlayerAbilityManager player, float delay)
    {
        if (player == null || database == null) return;

        // Cancel any existing pending roll for this player.
        if (_pendingRolls.TryGetValue(player, out var existing) && existing != null)
            StopCoroutine(existing);

        _pendingRolls[player] = StartCoroutine(RollAfterDelay(player, delay));

        Debug.Log($"[AbilityRoller] Scheduled roll for {NameOf(player)} in {delay:0.0}s");
    }

    [Server]
    private IEnumerator RollAfterDelay(PlayerAbilityManager player, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (player == null) yield break;
        if (database == null || database.abilities == null || database.abilities.Count == 0)
        {
            Debug.LogWarning("[AbilityRoller] No abilities in database!");
            yield break;
        }

        // If they already have an ability (eg. you changed design), you can skip or replace.
        if (!string.IsNullOrEmpty(player.HeldAbilityId))
        {
            Debug.Log($"[AbilityRoller] {NameOf(player)} already holds '{player.HeldAbilityId}', skipping roll.");
            yield break;
        }

        string pickedId = PickForPlayer(player);

        player.Server_SetHeldAbility(pickedId);

        RememberRecent(player, pickedId);

        Debug.Log($"[AbilityRoller] Rolled '{pickedId}' for {NameOf(player)}");
    }

    [Server]
    private string PickForPlayer(PlayerAbilityManager player)
    {
        // Roll index per player so seeds diverge.
        int rc = 0;
        _rollCount.TryGetValue(player, out rc);
        rc++;
        _rollCount[player] = rc;

        // Create deterministic-ish RNG per player per roll.
        // (Different players -> different NetId -> different results)
        int seed = matchSeed
                   ^ (rc * 73856093)
                   ^ (player.OwnerId * 19349663);

        var rng = new System.Random(seed);

        // Build candidates with anti-repeat filter.
        Queue<string> recent = GetRecentQueue(player);
        List<AbilityDefinition> candidates = new();

        foreach (var a in database.abilities)
        {
            if (a == null || string.IsNullOrEmpty(a.id)) continue;
            if (recent.Contains(a.id)) continue; // avoid recent repeats
            candidates.Add(a);
        }

        // If everything got filtered out, fall back to full list.
        if (candidates.Count == 0)
            candidates.AddRange(database.abilities);

        // Weighted pick.
        int totalWeight = 0;
        for (int i = 0; i < candidates.Count; i++)
            totalWeight += Mathf.Max(1, candidates[i].weight);

        int roll = rng.Next(0, totalWeight);
        int running = 0;

        for (int i = 0; i < candidates.Count; i++)
        {
            running += Mathf.Max(1, candidates[i].weight);
            if (roll < running)
                return candidates[i].id;
        }

        return candidates[0].id;
    }

    [Server]
    private void RememberRecent(PlayerAbilityManager player, string id)
    {
        var q = GetRecentQueue(player);
        q.Enqueue(id);
        while (q.Count > Mathf.Max(0, recentHistory))
            q.Dequeue();
    }

    private Queue<string> GetRecentQueue(PlayerAbilityManager player)
    {
        if (!_recentIds.TryGetValue(player, out var q) || q == null)
        {
            q = new Queue<string>();
            _recentIds[player] = q;
        }
        return q;
    }

    private string NameOf(PlayerAbilityManager p)
    {
        // Best-effort: show owner id + object name.
        return $"{p.gameObject.name} (OwnerId={p.OwnerId})";
    }
}