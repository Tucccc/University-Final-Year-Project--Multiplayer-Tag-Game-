using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using Steamworks;

public class PlayerIdentity : NetworkBehaviour
{
    public readonly SyncVar<string> DisplayName = new("Player");

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();

        Debug.Log($"[PlayerIdentity] OnStartNetwork. IsOwner={Owner.IsLocalClient} OwnerIsLocal={(Owner != null && Owner.IsLocalClient)}");

        if (Owner != null && Owner.IsLocalClient)
        {
            string n = Steamworks.SteamFriends.GetPersonaName();
            Debug.Log($"[PlayerIdentity] Local persona name='{n}'");

            if (string.IsNullOrWhiteSpace(n)) n = "Player";
            if (n.Length > 16) n = n.Substring(0, 16);

            Debug.Log($"[PlayerIdentity] Sending name='{n}' to server");
            RequestSetNameServerRpc(n);
        }
    }
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!IsOwner) return;

        string n = Steamworks.SteamFriends.GetPersonaName();
        if (string.IsNullOrWhiteSpace(n)) n = "Player";
        if (n.Length > 16) n = n.Substring(0, 16);

        RequestSetNameServerRpc(n);
    }



    [ServerRpc(RequireOwnership = true)]
    private void RequestSetNameServerRpc(string name)
    {
        Debug.Log($"[PlayerIdentity] ServerRpc received name='{name}'");
        DisplayName.Value = name;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RoundManager.Instance?.RegisterIdentity(Owner, this);
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        RoundManager.Instance?.UnregisterIdentity(Owner);
    }
}
