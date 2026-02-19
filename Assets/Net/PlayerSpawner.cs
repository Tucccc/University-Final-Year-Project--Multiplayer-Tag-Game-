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



    [Server]
    public void ResetAllToSpawns()
    {
        var server = base.NetworkManager.ServerManager;
        var conns = server.Clients.Values.ToList();

        foreach (var conn in conns)
        {
            var no = conn.FirstObject;
            if (no == null) continue;

            int idx = conn.ClientId % Mathf.Max(1, spawnPoints.Length);
            Transform sp = spawnPoints[idx];

            // Server authoritative teleport
            var cc = no.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            no.transform.SetPositionAndRotation(sp.position, sp.rotation);

            var rb = no.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (cc != null) cc.enabled = true;

            Physics.SyncTransforms();

            // IMPORTANT: Send TargetRpc from the PLAYER object (always observed by owner)
            var respawn = no.GetComponent<PlayerRespawn>();
            if (respawn != null)
                respawn.TargetSnapToSpawn(conn, sp.position, sp.rotation);
            else
                Debug.LogWarning($"Player {no.name} missing PlayerRespawn component; client snap may fail.");
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
