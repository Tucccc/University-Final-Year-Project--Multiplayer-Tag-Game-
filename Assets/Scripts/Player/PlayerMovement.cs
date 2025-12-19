using UnityEngine;
using FishNet.Object;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField] private float speed = 6f;
    [SerializeField] private CharacterController cc;
    [SerializeField] private Camera ownerCam; // assign in prefab, or we find it

    private void Awake()
    {
        if (!cc) cc = GetComponent<CharacterController>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // Grab the child camera
        if (ownerCam == null)
            ownerCam = GetComponentInChildren<Camera>(true);

        // Only the owner keeps their camera + listener enabled.
        var cam = ownerCam ?? GetComponentInChildren<Camera>(true);
        var al = cam ? cam.GetComponent<AudioListener>() : null;

        if (IsOwner)
        {
            if (cam) cam.enabled = true;
            if (al) al.enabled = true;
        }
        else
        {
            if (cam) cam.enabled = false;
            if (al) al.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsOwner) return;

        // Freeze movement while round over screen is up
        if (!RoundOverUI.ControlsEnabled) return;

        float ix = 0f, iy = 0f;
#if ENABLE_INPUT_SYSTEM
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb != null)
        {
            ix = (kb.aKey.isPressed ? -1 : 0) + (kb.dKey.isPressed ? 1 : 0);
            iy = (kb.sKey.isPressed ? -1 : 0) + (kb.wKey.isPressed ? 1 : 0); // W=+1, S=-1
        }
#else
        ix = Input.GetAxisRaw("Horizontal");
        iy = Input.GetAxisRaw("Vertical");
#endif

        // Camera-relative movement on the XZ plane
        Camera camRef = ownerCam != null ? ownerCam : Camera.main;
        Vector3 fwd = Vector3.forward;
        Vector3 right = Vector3.right;

        if (camRef != null)
        {
            fwd = Vector3.ProjectOnPlane(camRef.transform.forward, Vector3.up).normalized;
            right = Vector3.ProjectOnPlane(camRef.transform.right, Vector3.up).normalized;
        }

        Vector3 move = fwd * iy + right * ix;
        if (move.sqrMagnitude > 1f) move.Normalize();

        if (cc != null)
            cc.Move(move * speed * Time.deltaTime);
        else
            transform.position += move * speed * Time.deltaTime;
    }
}
