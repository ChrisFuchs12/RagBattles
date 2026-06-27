using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Core;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
//  UI prefab data container – attach this to your server-row prefab
//  (keep in its own file: ServerListItem.cs)
// ─────────────────────────────────────────────────────────────────────────────
// public class ServerListItem : MonoBehaviour
// {
//     [SerializeField] public TextMeshProUGUI codeLabel;
//     [SerializeField] public TextMeshProUGUI playerCountLabel;
//     [SerializeField] public Button          joinButton;
// }

// ─────────────────────────────────────────────────────────────────────────────
//  Main relay + lobby server-browser controller
// ─────────────────────────────────────────────────────────────────────────────
public class RelayTest : MonoBehaviour
{
    // ── Inspector: original UI ────────────────────────────────────────────────
    [Header("Required UI Components")]
    [SerializeField] private Button           hostButton;
    [SerializeField] private Button           joinButton;
    [SerializeField] private TMP_InputField   joinInput;
    [SerializeField] private TextMeshProUGUI  codeText;

    // ── Inspector: server browser ─────────────────────────────────────────────
    [Header("Server Browser UI")]
    [SerializeField] private Transform        serverListContent;       // Content child of ScrollRect
    [SerializeField] private GameObject       serverListItemPrefab;    // prefab with ServerListItem component
    [SerializeField] private Button           refreshButton;           // optional manual refresh
    [SerializeField] private TextMeshProUGUI  browserStatusText;       // status label

    // ── Inspector: network ────────────────────────────────────────────────────
    [Header("Network Components")]
    [SerializeField] private NetworkManager   networkManager;
    [SerializeField] private UnityTransport   unityTransport;

    // ── Configuration ─────────────────────────────────────────────────────────
    [Header("Server Browser Settings")]
    [SerializeField] private int   maxPlayers             = 10;
    [SerializeField] private float refreshIntervalSeconds = 10f;

    // ── Lobby constants ───────────────────────────────────────────────────────
    private const string KEY_JOIN_CODE = "joinCode";   // lobby data key

    // ── Private state ─────────────────────────────────────────────────────────
    private Lobby  _hostedLobby;          // our lobby (host only)
    private float  _heartbeatTimer;       // keeps lobby alive
    private float  _refreshTimer;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        ValidateComponentReferences();
    }

    private void Start()
    {
        if (AreComponentsValid())
            InitializeUnityServicesAsync();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleServerListRefresh();
    }

    private async void OnDestroy()
    {
        // Delete our lobby when the host leaves so it disappears from the list
        if (_hostedLobby != null)
        {
            try   { await LobbyService.Instance.DeleteLobbyAsync(_hostedLobby.Id); }
            catch { /* best-effort */ }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lobby heartbeat – must ping every 30 s or Unity auto-deletes the lobby
    // ─────────────────────────────────────────────────────────────────────────
    private void HandleLobbyHeartbeat()
    {
        if (_hostedLobby == null) return;

        _heartbeatTimer -= Time.deltaTime;
        if (_heartbeatTimer <= 0f)
        {
            _heartbeatTimer = 25f; // ping every 25 s (well within the 30 s limit)
            _ = LobbyService.Instance.SendHeartbeatPingAsync(_hostedLobby.Id);
        }
    }

    private void HandleServerListRefresh()
    {
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            _refreshTimer = refreshIntervalSeconds;
            _ = RefreshServerListAsync();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Validation helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void ValidateComponentReferences()
    {
        if (hostButton == null) Debug.LogError("Host Button is not assigned in the Inspector!");
        if (joinButton == null) Debug.LogError("Join Button is not assigned in the Inspector!");
        if (joinInput  == null) Debug.LogError("Join Input Field is not assigned in the Inspector!");
        if (codeText   == null) Debug.LogError("Code Text is not assigned in the Inspector!");

        if (networkManager == null)
        {
            networkManager = FindObjectOfType<NetworkManager>();
            if (networkManager == null) Debug.LogError("NetworkManager not found in the scene!");
        }

        if (unityTransport == null)
        {
            unityTransport = FindObjectOfType<UnityTransport>();
            if (unityTransport == null) Debug.LogError("UnityTransport not found in the scene!");
        }

        if (serverListContent    == null) Debug.LogWarning("ServerListContent not assigned – browser disabled.");
        if (serverListItemPrefab == null) Debug.LogWarning("ServerListItemPrefab not assigned – browser disabled.");
    }

    private bool AreComponentsValid() =>
        hostButton     != null &&
        joinButton     != null &&
        joinInput      != null &&
        codeText       != null &&
        networkManager != null &&
        unityTransport != null;

    private bool IsBrowserReady() =>
        serverListContent    != null &&
        serverListItemPrefab != null;

    // ─────────────────────────────────────────────────────────────────────────
    //  Initialization
    // ─────────────────────────────────────────────────────────────────────────
    private async void InitializeUnityServicesAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();

            SetupButtonListeners();

            _refreshTimer = 0f; // trigger an immediate first refresh
        }
        catch (Exception ex)
        {
            HandleInitializationError(ex);
        }
    }

    private void SetupButtonListeners()
    {
        hostButton.onClick.RemoveAllListeners();
        joinButton.onClick.RemoveAllListeners();

        hostButton.onClick.AddListener(CreateRelayAndLobbyAsync);
        joinButton.onClick.AddListener(() => JoinByCodeAsync(joinInput.text));

        if (refreshButton != null)
        {
            refreshButton.onClick.RemoveAllListeners();
            refreshButton.onClick.AddListener(() => _ = RefreshServerListAsync());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HOST – create Relay allocation + Lobby entry
    // ─────────────────────────────────────────────────────────────────────────
    private async void CreateRelayAndLobbyAsync()
    {
        try
        {
            // 1. Create Relay allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode       = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            codeText.text = "Code: " + joinCode;

            // 2. Start Netcode host via Relay
            var relayServerData = new RelayServerData(allocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);
            networkManager.StartHost();

            // 3. Create a public Lobby and store the relay join code in its data
            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = false,
                Data = new Dictionary<string, DataObject>
                {
                    {
                        KEY_JOIN_CODE,
                        new DataObject(
                            visibility: DataObject.VisibilityOptions.Public,
                            value: joinCode)
                    }
                }
            };

            _hostedLobby    = await LobbyService.Instance.CreateLobbyAsync("Game Lobby", maxPlayers, lobbyOptions);
            _heartbeatTimer = 0f; // start heartbeat immediately

            // 4. Keep player count in sync
            networkManager.OnClientConnectedCallback  += clientId => { _ = UpdateLobbyPlayerCountAsync(); };
            networkManager.OnClientDisconnectCallback += clientId => { _ = UpdateLobbyPlayerCountAsync(); };
        }
        catch (Exception ex)
        {
            HandleRelayError("Host", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  JOIN – by relay code directly (original button behaviour, unchanged)
    // ─────────────────────────────────────────────────────────────────────────
    private async void JoinByCodeAsync(string joinCode)
    {
        try
        {
            var joinAllocation  = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var relayServerData = new RelayServerData(joinAllocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);
            networkManager.StartClient();
        }
        catch (Exception ex)
        {
            HandleRelayError("Join", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  JOIN – from server browser row (joins via Lobby → reads relay code)
    // ─────────────────────────────────────────────────────────────────────────
    private async void JoinLobbyAsync(Lobby lobby)
    {
        try
        {
            // Join the Lobby so the player count updates
            await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

            // Retrieve the relay join code stored in the lobby's data
            if (!lobby.Data.TryGetValue(KEY_JOIN_CODE, out var joinCodeData))
            {
                Debug.LogError("Lobby is missing relay join code data!");
                return;
            }

            // Use the relay code to connect via Netcode
            await JoinRelayAsync(joinCodeData.Value);
        }
        catch (Exception ex)
        {
            HandleRelayError("Lobby Join", ex);
        }
    }

    private async Task JoinRelayAsync(string joinCode)
    {
        var joinAllocation  = await RelayService.Instance.JoinAllocationAsync(joinCode);
        var relayServerData = new RelayServerData(joinAllocation, "dtls");
        unityTransport.SetRelayServerData(relayServerData);
        networkManager.StartClient();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Keep player count accurate in the lobby (host only)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task UpdateLobbyPlayerCountAsync()
    {
        if (_hostedLobby == null) return;
        try
        {
            // Refresh our local copy so CurrentPlayers is accurate
            _hostedLobby = await LobbyService.Instance.GetLobbyAsync(_hostedLobby.Id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Lobby player count update error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Server browser – fetch lobbies from Unity Lobby service
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RefreshServerListAsync()
    {
        if (!IsBrowserReady()) return;
        if (!AuthenticationService.Instance.IsSignedIn) return;

        SetBrowserStatus("Refreshing…");

        try
        {
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    // Only show lobbies that still have open slots
                    new QueryFilter(
                        field:    QueryFilter.FieldOptions.AvailableSlots,
                        op:       QueryFilter.OpOptions.GT,
                        value:    "0")
                },
                Order = new List<QueryOrder>
                {
                    // Newest lobbies first
                    new QueryOrder(
                        asc:   false,
                        field: QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);
            RebuildServerList(response.Results);
        }
        catch (Exception ex)
        {
            SetBrowserStatus($"Error loading servers: {ex.Message}");
            Debug.LogWarning($"Server browser refresh error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Rebuild the scroll-view rows from a list of Lobby objects
    // ─────────────────────────────────────────────────────────────────────────
    private void RebuildServerList(List<Lobby> lobbies)
    {
        // Clear old rows
        foreach (Transform child in serverListContent)
            Destroy(child.gameObject);

        if (lobbies == null || lobbies.Count == 0)
        {
            SetBrowserStatus("No active servers found.");
            return;
        }

        SetBrowserStatus($"{lobbies.Count} server(s) found.");

        foreach (var lobby in lobbies)
        {
            // Skip lobbies that don't have our relay code key (shouldn't happen, but safe)
            if (!lobby.Data.ContainsKey(KEY_JOIN_CODE)) continue;

            GameObject   row  = Instantiate(serverListItemPrefab, serverListContent);
            ServerListItem item = row.GetComponent<ServerListItem>();

            if (item == null)
            {
                Debug.LogWarning("ServerListItemPrefab is missing a ServerListItem component!");
                Destroy(row);
                continue;
            }

            string relayCode = lobby.Data[KEY_JOIN_CODE].Value;

            if (item.codeLabel        != null) item.codeLabel.text        = $"Code: {relayCode}";
            if (item.playerCountLabel != null) item.playerCountLabel.text  = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

            if (item.joinButton != null)
            {
                Lobby capturedLobby = lobby; // capture for lambda
                item.joinButton.onClick.RemoveAllListeners();
                item.joinButton.onClick.AddListener(() => JoinLobbyAsync(capturedLobby));
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Error / status helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void SetBrowserStatus(string message)
    {
        if (browserStatusText != null)
            browserStatusText.text = message;
    }

    private void HandleInitializationError(Exception ex)
    {
        string msg = $"Initialization Error: {ex.Message}";
        Debug.LogError(msg);
        if (codeText != null) codeText.text = msg;
    }

    private void HandleRelayError(string operation, Exception ex)
    {
        string msg = $"{operation} Relay Error: {ex.Message}";
        Debug.LogError(msg);
        if (codeText != null) codeText.text = msg;
    }
}