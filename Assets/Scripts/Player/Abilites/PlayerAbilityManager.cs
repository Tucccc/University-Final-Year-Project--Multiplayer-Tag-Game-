using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;

public class PlayerAbilityManager : NetworkBehaviour
{
    // FishNet v4 sync type (no [SyncVar] attribute).
    private readonly SyncVar<string> _heldAbilityId = new SyncVar<string>("");
    public string HeldAbilityId => _heldAbilityId.Value;

    [Header("Refs")]
    [SerializeField] private AbilityExecutor executor;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // Scene reference: safest to find at runtime for spawned player objects.
        if (executor == null)
            executor = FindFirstObjectByType<AbilityExecutor>();

        if (IsServerInitialized && executor == null)
            Debug.LogError($"[PlayerAbilityManager] AbilityExecutor not found in scene on server for {gameObject.name}.");
    }

    /// Called by Weapon on RIGHT CLICK (owner client).
    /// origin/aimDir should come from camera center (same as tagging).
    public void TryUseAbility(Vector3 origin, Vector3 aimDir)
    {
        if (!IsOwner)
            return;

        if (string.IsNullOrEmpty(_heldAbilityId.Value))
        {
            Debug.Log("[Ability] Tried to use ability but HeldAbilityId is EMPTY.");
            return;
        }

        Debug.Log($"[Ability] Requesting use of '{_heldAbilityId.Value}' origin={origin}");
        UseAbilityServerRpc(_heldAbilityId.Value, origin, aimDir);
    }

    [ServerRpc(RequireOwnership = true)]
    private void UseAbilityServerRpc(string abilityId, Vector3 origin, Vector3 aimDir)
    {
        Debug.Log($"[Ability][SERVER] UseAbilityServerRpc abilityId='{abilityId}' held='{_heldAbilityId.Value}' origin={origin}");

        if (executor == null)
        {
            Debug.LogError("[Ability][SERVER] executor is NULL (AbilityExecutor missing/not found).");
            return;
        }

        // Security/validity check.
        if (abilityId != _heldAbilityId.Value)
        {
            Debug.LogWarning("[Ability][SERVER] abilityId mismatch vs held ability (ignored).");
            return;
        }

        bool success = executor.ServerTryExecute(abilityId, origin, aimDir, gameObject);
        Debug.Log($"[Ability][SERVER] Execute result: {success}");

        if (!success)
            return;

        // Consume.used

        _heldAbilityId.Value = "";
        Debug.Log("[Ability][SERVER] Consumed ability (HeldAbilityId cleared).");

        // Restart per-player roll timer.
        if (AbilityRoller.Instance != null)
            AbilityRoller.Instance.ServerOnAbilityUsed(this);
    }

    /// Server uses this to assign an ability (roller).
    [Server]
    public void Server_SetHeldAbility(string abilityId)
    {
        _heldAbilityId.Value = abilityId;
        Debug.Log($"[Ability][SERVER] Assigned ability '{abilityId}' to {gameObject.name}");
    }
}