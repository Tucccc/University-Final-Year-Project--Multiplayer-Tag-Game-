using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class Health : NetworkBehaviour
{
    // Make the SyncVar itself readonly. Modify only .Value.
    public readonly SyncVar<int> Current = new SyncVar<int>(100);

    [SerializeField] private int max = 100;

    public override void OnStartNetwork()
    {
        base.OnStartNetwork();
        Current.OnChange += OnHealthChanged;
    }

    public override void OnStopNetwork()
    {
        base.OnStopNetwork();
        Current.OnChange -= OnHealthChanged;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        Current.Value = max;   // ? change the value, not the field
    }

    [Server]
    public void DealDamage(int amount)
    {
        Current.Value = Mathf.Max(0, Current.Value - amount);
        if (Current.Value == 0)
            OnDeathServer();
    }

    [Server]
    private void OnDeathServer()
    {
        Current.Value = max;
        transform.position = Vector3.zero;
    }

    private void OnHealthChanged(int prev, int next, bool asServer)
    {
        // Hook UI/VFX here if needed
    }
}
