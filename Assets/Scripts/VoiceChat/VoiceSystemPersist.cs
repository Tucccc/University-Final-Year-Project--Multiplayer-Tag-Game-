using UnityEngine;

public class VoiceSystemPersist : MonoBehaviour
{
    private static bool exists;

    private void Awake()
    {
        if (exists)
        {
            Destroy(gameObject);
            return;
        }

        exists = true;
        DontDestroyOnLoad(gameObject);
    }
}