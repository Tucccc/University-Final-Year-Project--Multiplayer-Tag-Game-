using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

public class PlayerRespawn : NetworkBehaviour
{
    [TargetRpc]
    public void TargetSnapToSpawn(NetworkConnection target, Vector3 pos, Quaternion rot)
    {
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(pos, rot);

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Unity Rigidbody uses velocity / angularVelocity.
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Optional hard snap
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            Physics.SyncTransforms();
            rb.isKinematic = wasKinematic;
        }
        else
        {
            Physics.SyncTransforms();
        }

        if (cc != null) cc.enabled = true;
    }
}
