using UnityEngine;
using UnityEngine.UI;

public class PlayerCardName : MonoBehaviour
{
    private Text nameText;

    void Start()
    {
        // Auto-find ALL Text components and pick the first one (your name text)
        Text[] texts = GetComponentsInChildren<Text>(true);
        if (texts.Length > 0)
        {
            nameText = texts[0]; // First Text = name (score is handled elsewhere)
            Debug.Log($"Found name text: {nameText.name}");
        }
    }

    void Update()
    {
        // Update name from corresponding player (by array index)
        PlayerIdentity[] players = Object.FindObjectsOfType<PlayerIdentity>();
        int playerIndex = transform.GetSiblingIndex();

        if (nameText != null && playerIndex < players.Length && players[playerIndex] != null)
        {
            nameText.text = players[playerIndex].DisplayName.Value;
        }
    }
}
