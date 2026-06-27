using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
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

public class RelayTest : MonoBehaviour
{
    // ── Inspector: required UI ────────────────────────────────────────────────
    [Header("Required UI Components")]
    [SerializeField] private Button          hostButton;
    [SerializeField] private Button          joinButton;
    [SerializeField] private TMP_InputField  joinInput;
    [SerializeField] private TextMeshProUGUI codeText;

    // ── Inspector: server browser ─────────────────────────────────────────────
    [Header("Server Browser UI")]
    [SerializeField] private Transform       serverListContent;
    [SerializeField] private GameObject      serverListItemPrefab;
    [SerializeField] private Button          refreshButton;
    [SerializeField] private TextMeshProUGUI browserStatusText;

    // ── Inspector: panels ─────────────────────────────────────────────────────
    [Header("UI Panels")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject connectedPanel;

    // ── Inspector: network ────────────────────────────────────────────────────
    [Header("Network Components")]
    [SerializeField] private NetworkManager  networkManager;
    [SerializeField] private UnityTransport  unityTransport;

    // ── Inspector: scene + config ─────────────────────────────────────────────
    [Header("Scene Settings")]
    [SerializeField] private string gameSceneName  = "GameScene";
    [SerializeField] private string menuSceneName  = "MenuScene";

    [Header("Server Browser Settings")]
    [SerializeField] private int   maxPlayers             = 10;
    [SerializeField] private float refreshIntervalSeconds = 10f;

    // ── Lobby constants ───────────────────────────────────────────────────────
    private const string KEY_JOIN_CODE = "joinCode";

    // ── Singleton so GameSceneUI can call LeaveAsync ──────────────────────────
    public static RelayTest Instance { get; private set; }

    // ── Private state ─────────────────────────────────────────────────────────
    private Lobby _hostedLobby;
    private float _heartbeatTimer;
    private float _refreshTimer;
    private bool  _joiningOrHosting;

    // ─────────────────────────────────────────────────────────────────────────
    //  Unity lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        // Singleton setup — NetworkManagerBootstrapper keeps this alive across scenes
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        ValidateComponentReferences();
    }

    private void Start()
    {
        ShowLobbyPanel();

        if (AreComponentsValid())
            InitializeUnityServicesAsync();
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleServerListRefresh();
    }

    // NOTE: lobby cleanup is now handled explicitly in LeaveAsync so the host
    // can await it properly before shutting Netcode down.

    // ─────────────────────────────────────────────────────────────────────────
    //  Panel switching
    // ─────────────────────────────────────────────────────────────────────────
    private void ShowLobbyPanel()
    {
        if (lobbyPanel     != null) lobbyPanel.SetActive(true);
        if (connectedPanel != null) connectedPanel.SetActive(false);
    }

    private void ShowConnectedPanel()
    {
        if (lobbyPanel     != null) lobbyPanel.SetActive(false);
        if (connectedPanel != null) connectedPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Public leave method — called by GameSceneUI.LeaveButton
    // ─────────────────────────────────────────────────────────────────────────
    public async void LeaveAsync()
    {
        try
        {
            bool isHost = networkManager.IsHost;

            if (isHost)
            {
                // Delete lobby so it disappears from the browser for everyone
                if (_hostedLobby != null)
                {
                    try   { await LobbyService.Instance.DeleteLobbyAsync(_hostedLobby.Id); }
                    catch { /* best-effort */ }
                    _hostedLobby = null;
                }

                networkManager.Shutdown();
            }
            else
            {
                networkManager.Shutdown();
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Leave error: {ex.Message}");
        }
        finally
        {
            // Reset state
            _joiningOrHosting = false;

            // Shutdown needs a frame to complete before loading a new scene
            await System.Threading.Tasks.Task.Delay(100);

            // Return to menu scene and reset UI
            SceneManager.LoadScene(menuSceneName);
            ShowLobbyPanel();
            UnlockUI();

            if (codeText != null) codeText.text = string.Empty;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Heartbeat
    // ─────────────────────────────────────────────────────────────────────────
    private void HandleLobbyHeartbeat()
    {
        if (_hostedLobby == null) return;
        _heartbeatTimer -= Time.deltaTime;
        if (_heartbeatTimer <= 0f)
        {
            _heartbeatTimer = 25f;
            _ = LobbyService.Instance.SendHeartbeatPingAsync(_hostedLobby.Id);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Auto-refresh
    // ─────────────────────────────────────────────────────────────────────────
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
    //  Validation
    // ─────────────────────────────────────────────────────────────────────────
    private void ValidateComponentReferences()
    {
        if (hostButton == null) Debug.LogError("Host Button is not assigned in the Inspector!");
        if (joinButton == null) Debug.LogError("Join Button is not assigned in the Inspector!");
        if (joinInput  == null) Debug.LogError("Join Input Field is not assigned in the Inspector!");
        if (codeText   == null) Debug.LogError("Code Text is not assigned in the Inspector!");

        if (lobbyPanel     == null) Debug.LogWarning("Lobby Panel not assigned – panel switching disabled.");
        if (connectedPanel == null) Debug.LogWarning("Connected Panel not assigned – panel switching disabled.");

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

            _refreshTimer = 0f;
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
    //  UI helpers
    // ─────────────────────────────────────────────────────────────────────────
    private void LockUI(string statusMessage)
    {
        _joiningOrHosting          = true;
        hostButton.interactable    = false;
        joinButton.interactable    = false;
        if (refreshButton != null)
            refreshButton.interactable = false;
        SetBrowserStatus(statusMessage);
    }

    private void UnlockUI()
    {
        _joiningOrHosting          = false;
        hostButton.interactable    = true;
        joinButton.interactable    = true;
        if (refreshButton != null)
            refreshButton.interactable = true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  HOST
    // ─────────────────────────────────────────────────────────────────────────
    private async void CreateRelayAndLobbyAsync()
    {
        if (_joiningOrHosting) return;
        LockUI("Creating server…");

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers);
            string joinCode       = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var relayServerData = new RelayServerData(allocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);
            networkManager.StartHost();

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
            _heartbeatTimer = 0f;

            networkManager.OnClientConnectedCallback  += clientId => { _ = UpdateLobbyPlayerCountAsync(); };
            networkManager.OnClientDisconnectCallback += clientId => { _ = UpdateLobbyPlayerCountAsync(); };

            codeText.text = "Code: " + joinCode;
            ShowConnectedPanel();

            networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }
        catch (Exception ex)
        {
            UnlockUI();
            ShowLobbyPanel();
            HandleRelayError("Host", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  JOIN by code
    // ─────────────────────────────────────────────────────────────────────────
    private async void JoinByCodeAsync(string joinCode)
    {
        if (_joiningOrHosting) return;
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            SetBrowserStatus("Please enter a join code.");
            return;
        }

        LockUI("Joining server…");

        try
        {
            var joinAllocation  = await RelayService.Instance.JoinAllocationAsync(joinCode);
            var relayServerData = new RelayServerData(joinAllocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);

            codeText.text = "Code: " + joinCode;
            ShowConnectedPanel();

            networkManager.StartClient();
        }
        catch (Exception ex)
        {
            UnlockUI();
            ShowLobbyPanel();
            HandleRelayError("Join", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  JOIN from server browser
    // ─────────────────────────────────────────────────────────────────────────
    private async void JoinLobbyAsync(Lobby lobby)
    {
        if (_joiningOrHosting) return;
        LockUI("Joining server…");

        try
        {
            await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id);

            if (!lobby.Data.TryGetValue(KEY_JOIN_CODE, out var joinCodeData))
            {
                UnlockUI();
                SetBrowserStatus("Error: lobby is missing relay code.");
                return;
            }

            var joinAllocation  = await RelayService.Instance.JoinAllocationAsync(joinCodeData.Value);
            var relayServerData = new RelayServerData(joinAllocation, "dtls");
            unityTransport.SetRelayServerData(relayServerData);

            codeText.text = "Code: " + joinCodeData.Value;
            ShowConnectedPanel();

            networkManager.StartClient();
        }
        catch (Exception ex)
        {
            UnlockUI();
            ShowLobbyPanel();
            HandleRelayError("Lobby Join", ex);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Lobby player count sync
    // ─────────────────────────────────────────────────────────────────────────
    private async Task UpdateLobbyPlayerCountAsync()
    {
        if (_hostedLobby == null) return;
        try
        {
            _hostedLobby = await LobbyService.Instance.GetLobbyAsync(_hostedLobby.Id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Lobby player count update error: {ex.Message}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Server browser
    // ─────────────────────────────────────────────────────────────────────────
    private async Task RefreshServerListAsync()
    {
        if (!IsBrowserReady()) return;
        if (!AuthenticationService.Instance.IsSignedIn) return;
        if (_joiningOrHosting) return;

        SetBrowserStatus("Refreshing…");

        try
        {
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 25,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(
                        field: QueryFilter.FieldOptions.AvailableSlots,
                        op:    QueryFilter.OpOptions.GT,
                        value: "0")
                },
                Order = new List<QueryOrder>
                {
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

    private void RebuildServerList(List<Lobby> lobbies)
    {
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
            if (!lobby.Data.ContainsKey(KEY_JOIN_CODE)) continue;

            GameObject     row  = Instantiate(serverListItemPrefab, serverListContent);
            ServerListItem item = row.GetComponent<ServerListItem>();

            if (item == null)
            {
                Debug.LogWarning("ServerListItemPrefab is missing a ServerListItem component!");
                Destroy(row);
                continue;
            }

            string relayCode = lobby.Data[KEY_JOIN_CODE].Value;

            if (item.codeLabel        != null) item.codeLabel.text       = $"Code: {relayCode}";
            if (item.playerCountLabel != null) item.playerCountLabel.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

            if (item.joinButton != null)
            {
                Lobby capturedLobby = lobby;
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