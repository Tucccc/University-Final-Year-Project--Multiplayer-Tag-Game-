using UnityEngine;
using UnityEngine.UI;

public class CrosshairRegistry : MonoBehaviour
{
    // Stored on the canvas this crosshair belongs to (not static global)
    public RawImage Raw { get; private set; }

    private void Awake()
    {
        Raw = GetComponent<RawImage>();
    }
}