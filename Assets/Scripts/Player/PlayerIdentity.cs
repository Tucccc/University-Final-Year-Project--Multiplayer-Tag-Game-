// Scripts/Game/PlayerIdentity.cs
using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerIdentity : NetworkBehaviour
{
    public readonly SyncVar<string> DisplayName = new("Player");

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        // FishNet rule: inside OnStartNetwork use base.Owner.IsLocalClient
        if (base.Owner != null && base.Owner.IsLocalClient)
        {
            string n = PlayerPrefs.GetString("player_name", "Player");
            if (string.IsNullOrWhiteSpace(n)) n = "Player";
            if (n.Length > 16) n = n.Substring(0, 16);
            RequestSetNameServerRpc(n);
        }
    }

    [ServerRpc(RequireOwnership = true)]
    private void RequestSetNameServerRpc(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        DisplayName.Value = name.Trim();
    }
}
