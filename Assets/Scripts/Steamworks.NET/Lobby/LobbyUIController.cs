using FishNet.Object.Synchronizing;
using Steamworks;
using System.Collections.Generic;
using TMPro;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.Rendering.DebugUI;

public class LobbyUIController : MonoBehaviour
{
    [Header("Lobby Members UI")]
    [SerializeField] private Transform membersContent;
    [SerializeField] private TMP_Text memberRowPrefab;

    [Header("Buttons")]
    public UnityEngine.UI.Button startButton;

    [Header("Ready Count")]
    public GameObject rowPrefab; // assign in inspector, optional
    [SerializeField] private TMP_Text readyCountText;
    [SerializeField] private Toggle readyToggle;
    private bool _hookedAllReady;


    private readonly HashSet<LobbyPlayerState> _subscribed = new();

    private void Start()
    {
        if (readyToggle != null)
            readyToggle.onValueChanged.AddListener(OnReadyToggleChanged);

        InvokeRepeating(nameof(UpdateReadyCount), 0.25f, 0.5f); // simple + reliable
    }

    private void Update()
    {
        if(!_hookedAllReady && LobbyManager2.Instance != null)
        {
            _hookedAllReady = true;
            LobbyManager2.Instance.AllReady.OnChange += (_, __, ___) => RefreshStartButton();
            RefreshStartButton();
        }
    }

    public void OnReadyToggleChanged(bool isOn)
    {
        // find my LobbyPlayerState (owned object)
        foreach (var p in FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None))
        {
            if (p.IsOwner)
            {
                p.RequestSetReady(isOn);
                break;
            }
        }

        UpdateReadyCount();
    }

    private void UpdateReadyCount()
    {
        var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);

        int total = players.Length;
        int ready = 0;
        foreach (var p in players)
            if (p != null && p.IsReady.Value) ready++;

        if (readyCountText != null)
            readyCountText.text = $"Ready {ready}/{total}";
        RefreshStartButton();
    }


    private void OnEnable()
    {
        // Optional: if your SteamSessionManager exposes events, subscribe here.
        // SteamSessionManager.Instance.OnMemberJoinedEvent += OnMemberChanged;
        // SteamSessionManager.Instance.OnMemberLeftEvent += OnMemberChanged;
        // SteamSessionManager.Instance.OnLobbyEnteredEvent += OnLobbyEntered;

        var s = SteamSessionManager.Instance;
        if (s == null) return;

        s.OnLobbyEnteredEvent += HandleLobbyChanged;
        s.OnMemberJoinedEvent += HandleLobbyChanged;
        s.OnMemberLeftEvent += HandleLobbyChanged;

        RefreshMembers();
        HookReadySubscriptions();
        UpdateReadyCount();
    }

    private void OnDisable()
    {
        // Optional: unsubscribe from events
        // if (SteamSessionManager.Instance == null) return;
        // SteamSessionManager.Instance.OnMemberJoinedEvent -= OnMemberChanged;
        // SteamSessionManager.Instance.OnMemberLeftEvent -= OnMemberChanged;
        // SteamSessionManager.Instance.OnLobbyEnteredEvent -= OnLobbyEntered;

        var s = SteamSessionManager.Instance;
        if (s == null) return;

        s.OnLobbyEnteredEvent -= HandleLobbyChanged;
        s.OnMemberJoinedEvent -= HandleLobbyChanged;
        s.OnMemberLeftEvent -= HandleLobbyChanged;


    }

    private void HandleLobbyChanged(CSteamID _)
    {
        RefreshMembers();
    }

    // Wire this to a refresh button if i want manual
    public void RefreshMembers()
    {
        if (membersContent == null || memberRowPrefab == null)
        {
            Debug.LogWarning("[LobbyUI] Missing membersContent or memberRowPrefab reference.");
            return;
        }

        var session = SteamSessionManager.Instance;
        if (session == null)
        {
            Debug.LogWarning("[LobbyUI] SteamSessionManager.Instance is null.");
            return;
        }

        var lobbyId = session.CurrentLobbyId; // adapt if your property name differs
        if (lobbyId == CSteamID.Nil)
        {
            Debug.LogWarning("[LobbyUI] Not in a Steam lobby; cannot list members.");
            ClearMemberRows();
            return;
        }

        ClearMemberRows();

        int count = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
        Debug.Log($"[LobbyUI] Lobby members: {count}");

        for (int i = 0; i < count; i++)
        {
            CSteamID memberId = SteamMatchmaking.GetLobbyMemberByIndex(lobbyId, i);

            // Name resolution
            string name;
            if (memberId == SteamUser.GetSteamID())
                name = SteamFriends.GetPersonaName();
            else
                name = SteamFriends.GetFriendPersonaName(memberId);

            // Instantiate row
            TMP_Text row = Instantiate(memberRowPrefab, membersContent);
            row.text = name; // or $"{name} ({memberId})" for debugging
        }

        HookReadySubscriptions();
        UpdateReadyCount();
    }

    private void UpdateStartButton()
    {
        var session = SteamSessionManager.Instance;
        if (session == null || session.CurrentLobbyId == CSteamID.Nil)
        {
            startButton.interactable = false;
            return;
        }

        var lobbyId = session.CurrentLobbyId;
        var owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
        var me = SteamUser.GetSteamID();

        startButton.interactable = (owner == me);
        RefreshStartButton();
    }

    private void RefreshStartButton()
    {
        bool isSteamOwner = false;
        if (SteamSessionManager.Instance != null && SteamSessionManager.Instance.IsInLobby)
        {
            var lobbyId = SteamSessionManager.Instance.CurrentLobbyId;
            var owner = SteamMatchmaking.GetLobbyOwner(lobbyId);
            var me = SteamUser.GetSteamID();
            isSteamOwner = (owner == me);
        }

        bool allReady = LobbyManager2.Instance != null && LobbyManager2.Instance.AllReady.Value;

        startButton.interactable = isSteamOwner && allReady;
    }



    private void ClearMemberRows()
    {
        for (int i = membersContent.childCount - 1; i >= 0; i--)
            Destroy(membersContent.GetChild(i).gameObject);
    }

    public void OnStartClicked()
    {
        LobbyManager2.Instance?.RequestStartGame();
    }

    public void OnInviteClicked()
    {
        Debug.Log("[LobbyUI] Invite clicked.");
        SteamSessionManager.Instance?.InviteFriendsOverlay();
    }

    public void OnLeaveClicked()
    {
        Debug.Log("[LobbyUI] Leave clicked.");
        SteamSessionManager.Instance?.LeaveAndReset(); // or LeaveLobby + Load Menu
    }


    LobbyPlayerState FindLocalPlayer()
    {
        foreach (var p in FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None))
            if (p.IsOwner) return p;
        return null;
    }

    public void OnReadyToggleChanaged(bool isOn)
    {
        var me = FindLocalPlayer();
        if (me != null) 
        {
            me.RequestSetReady(isOn);

            Debug.Log($"[LobbyUI] Ready toggle changed");

        }
        else
            Debug.LogWarning("[LobbyUI] Could not find local LobbyPlayerState to set ready.");
    }


    private void HookReadySubscriptions()
    {
        var players = FindObjectsByType<LobbyPlayerState>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            if (p == null || _subscribed.Contains(p))
                continue;

            // FishNet SyncVar OnChange signature is (prev, next, asServer)
            p.IsReady.OnChange += (_, __, ___) =>
            {
                UpdateReadyCount();
                // later: UpdateRowsReadyText(); if you add per-row ready display
            };

            _subscribed.Add(p);
        }
    }


}
