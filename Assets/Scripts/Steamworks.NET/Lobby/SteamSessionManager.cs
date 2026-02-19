using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Steamworks;
using System.Runtime.CompilerServices;
using FishNet;

public class SteamSessionManager : MonoBehaviour
{
    public static SteamSessionManager Instance { get; private set; }

    public event Action<CSteamID> OnLobbyEnteredEvent;
    public event Action<CSteamID> OnMemberJoinedEvent;
    public event Action<CSteamID> OnMemberLeftEvent;

    [SerializeField] private int maxPlayers = 8;
    private CSteamID _originalHost = CSteamID.Nil;

    private CSteamID _pendingJoinLobby = CSteamID.Nil;
    private bool _isSwitchingLobbies;



    public CSteamID CurrentLobbyId { get; private set; } = CSteamID.Nil;
    public bool IsInLobby => CurrentLobbyId != CSteamID.Nil;
    public bool IsHost { get; private set; }

    Callback<LobbyEnter_t> _lobbyEnter;
    Callback<GameLobbyJoinRequested_t> _joinRequested;
    Callback<LobbyChatUpdate_t> _chatUpdate;
    Callback<LobbyCreated_t> _lobbyCreated;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        // Make sure Steam is initialized (SteamManager usually handles this).
        if (!SteamAPI.IsSteamRunning())
        {
            Debug.LogWarning("[SteamSession] Steam not running.");
            return;
        }

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyEnter = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
        _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        _chatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
    }

    public void HostLobby()
    {
        Debug.Log($"[SteamSession] HostLobby clicked. CurrentLobbyId={CurrentLobbyId} IsInLobby={IsInLobby}");

        if (IsInLobby)
        {
            Debug.LogWarning("[SteamSession] Already in a lobby (local state). Refusing to CreateLobby.");
            return;
        }
        CreateLobby(maxPlayers);
    }

    private void CreateLobby(int players)
    {

        Debug.Log("[SteamSession] Creating lobby...");
        IsHost = true;

        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, players);
    }

    public void JoinLobby(CSteamID lobbyId)
    {
        Debug.Log($"[SteamSession] Joining lobby {lobbyId}...");
        IsHost = false;
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    public void LeaveLobby()
    {
        if (!IsInLobby) return;
        Debug.Log("[SteamSession] Leaving lobby...");
        SteamMatchmaking.LeaveLobby(CurrentLobbyId);
        CurrentLobbyId = CSteamID.Nil;
        IsHost = false;
    }

    public void InviteFriendsOverlay()
    {
        if (!IsInLobby) return;
        SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
    }

    string GetPersona(CSteamID id)
    {
        // For other players use GetFriendPersonaName; for yourself, GetPersonaName also works.
        return SteamFriends.GetFriendPersonaName(id);
    }

    void OnLobbyCreated(LobbyCreated_t cb)
    {
        Debug.Log($"[SteamSession] LobbyCreated result={cb.m_eResult} lobby={cb.m_ulSteamIDLobby}");

        if (cb.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"[SteamSession] Lobby create failed: {cb.m_eResult}");
            IsHost = false;
            return;
        }

        CurrentLobbyId = new CSteamID(cb.m_ulSteamIDLobby);
        Debug.Log($"[SteamSession] Lobby created: {CurrentLobbyId}");

        // Optional: store some lobby metadata
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, "version", Application.version);
        SteamMatchmaking.SetLobbyData(CurrentLobbyId, "host", SteamUser.GetSteamID().ToString());

        // We will get LobbyEnter_t after creation too, but loading Lobby here is fine if you want immediacy:
        LoadLobbyScene();
    }

    void OnLobbyEnter(LobbyEnter_t cb)
    {
        var lobbyId = new CSteamID(cb.m_ulSteamIDLobby);

        CurrentLobbyId = lobbyId;

        IsHost = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId) == SteamUser.GetSteamID();

        Debug.Log($"[SteamSession] Entered lobby (CurrentLobbyId={CurrentLobbyId}) IsHost={IsHost}");

        OnLobbyEnteredEvent?.Invoke(CurrentLobbyId);
        LoadLobbyScene();
    }

    void OnJoinRequested(GameLobbyJoinRequested_t cb)
    {
        var targetLobby = cb.m_steamIDLobby;
        Debug.Log($"[SteamSession] Join requested lobby: {targetLobby}");

        // If we are already in a lobby / running FishNet, reset first.
        if (IsInLobby || (FishNet.InstanceFinder.NetworkManager != null &&
                         (FishNet.InstanceFinder.NetworkManager.ClientManager.Started ||
                          FishNet.InstanceFinder.NetworkManager.ServerManager.Started)))
        {
            _pendingJoinLobby = targetLobby;

            if (!_isSwitchingLobbies)
                StartCoroutine(SwitchLobbyThenJoin());
            return;
        }

        JoinLobby(targetLobby);
    }
    private System.Collections.IEnumerator SwitchLobbyThenJoin()
    {
        _isSwitchingLobbies = true;

        // Stop FishNet cleanly
        var nm = FishNet.InstanceFinder.NetworkManager;
        if (nm != null)
        {
            if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
            if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
        }

        // Leave current Steam lobby
        if (CurrentLobbyId != CSteamID.Nil)
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);

        // Reset local state immediately
        CurrentLobbyId = CSteamID.Nil;
        IsHost = false;

        // Give a frame for scene/network objects to tear down
        yield return null;

        // Optional but recommended: force a clean scene before joining
        // (prevents old lobby scene objects / UI from persisting)
        SceneManager.LoadScene("Menu");

        // Wait one more frame after scene swap
        yield return null;

        var lobby = _pendingJoinLobby;
        _pendingJoinLobby = CSteamID.Nil;

        Debug.Log($"[SteamSession] Switching complete. Joining lobby {lobby}...");
        JoinLobby(lobby);

        _isSwitchingLobbies = false;
    }


    void OnLobbyChatUpdate(LobbyChatUpdate_t cb)
    {
        if (new CSteamID(cb.m_ulSteamIDLobby) != CurrentLobbyId) return;

        var changed = new CSteamID(cb.m_ulSteamIDUserChanged);
        var state = (EChatMemberStateChange)cb.m_rgfChatMemberStateChange;

        if (state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
        {
            Debug.Log($"[SteamSession] Member joined: {GetPersona(changed)} ({changed})");
            OnMemberJoinedEvent?.Invoke(changed);
        }
        else if (state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) ||
                 state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) ||
                 state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeKicked) ||
                 state.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeBanned))
        {
            Debug.Log($"[SteamSession] Member left: {GetPersona(changed)} ({changed})");
            OnMemberLeftEvent?.Invoke(changed);

            // If host left (and you're not host), you probably want to return to Bootstrap.
            // Host leaving typically collapses the party experience you want.
            if (!IsHost)
            {
                // You can also detect host via lobby data or owner
                var owner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
                if (owner == CSteamID.Nil || owner == changed)
                    ReturnToBootstrap();
            }
        }
    }

    void LoadLobbyScene()
    {
        if (SceneManager.GetActiveScene().name != "Lobby")
        {
            Debug.Log($"[SteamSession] Loading Lobby scene. Active={SceneManager.GetActiveScene().name}");
            SceneManager.LoadScene("Lobby");
        }
        else
        {
            Debug.Log("[SteamSession] Already in Lobby scene, not loading.");
        }
    }


    public void ReturnToBootstrap()
    {
        Debug.Log("[SteamSession] Returning to BootstrapMenu...");
        LeaveLobby();
        if (SceneManager.GetActiveScene().name != "Menu")
            SceneManager.LoadScene("Menu");
    }

    public void LeaveAndReset()
    {

        // Stop FishNet (safe if not started)
        var nm = FishNet.InstanceFinder.NetworkManager;
        if (nm != null)
        {
            if (nm.ClientManager.Started) nm.ClientManager.StopConnection();
            if (nm.ServerManager.Started) nm.ServerManager.StopConnection(true);
        }

        // Leave lobby (use a local copy)
        var lobbyToLeave = CurrentLobbyId;
        if (lobbyToLeave != CSteamID.Nil)
            SteamMatchmaking.LeaveLobby(lobbyToLeave);

        // IMPORTANT: reset local state immediately (don’t wait for Steam)
        CurrentLobbyId = CSteamID.Nil;
        IsHost = false;

        // Load Bootstrap
        UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
        Debug.Log($"[SteamSession] LeaveAndReset. CurrentLobbyId={CurrentLobbyId} IsHost={IsHost}");
    }

    public void OnApplicationQuit()
    {
        //OnApplicationQuit(); // Causes crashes in editor not sure if it will in build!
    }
}
