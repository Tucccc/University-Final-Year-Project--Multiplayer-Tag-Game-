// Assets/Scripts/Net/PlayerSpawner.cs
using UnityEngine;
using FishNet.Object;
using FishNet.Connection;
using System.Linq;

public class PlayerSpawner : NetworkBehaviour
{
    [Header("Assign in Arena scene")]
    [SerializeField] private NetworkObject playerPrefab;
    [SerializeField] private Transform[] spawnPoints;

    // Spawn a player object for every connected client that doesn't already have one.
    [Server]
    public void SpawnAllConnected()
    {
        var server = base.NetworkManager.ServerManager;
        foreach (var kvp in server.Clients)
        {
            NetworkConnection conn = kvp.Value;

            // If this client already has a spawned player object, skip.
            if (conn.FirstObject != null)
                continue;

            int idx = (int)(conn.ClientId % Mathf.Max(1, spawnPoints.Length));
            Transform sp = spawnPoints[idx];

            NetworkObject no = Instantiate(playerPrefab, sp.position, sp.rotation);
            server.Spawn(no, conn);

            // Make the very first spawned player "It" if none exists yet.
            var tag = no.GetComponent<TagStatus>();
            if (tag != null && !AnyItPresent())
                tag.SetIt(true);
        }
    }

    // Move every connected player's object back to spawn for a new round.
    [Server]
    public void ResetAllToSpawns()
    {
        var server = base.NetworkManager.ServerManager;
        var conns = server.Clients.Values.ToList();

        for (int i = 0; i < conns.Count; i++)
        {
            var conn = conns[i];
            var no = conn.FirstObject;
            if (no == null) continue;

            Transform sp = spawnPoints[i % Mathf.Max(1, spawnPoints.Length)];

            // If you use CharacterController, disable/enable around teleport to avoid ground snapping issues
            var cc = no.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            no.transform.SetPositionAndRotation(sp.position, sp.rotation);
            if (cc != null) cc.enabled = true;

            // If you use Rigidbody, you may also want to clear velocities:
            // var rb = no.GetComponent<Rigidbody>();
            // if (rb) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
        }
    }

    private bool AnyItPresent()
    {
        var tags = FindObjectsByType<TagStatus>(FindObjectsSortMode.None);
        foreach (var t in tags)
            if (t.IsIt.Value)
                return true;
        return false;
    }
}
