using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;

// ─────────────────────────────────────────────────────────────────────────────
//  Place this script on a GameObject in your Game Scene.
//  Assign your leave button and optionally a pause panel in the Inspector.
// ─────────────────────────────────────────────────────────────────────────────
public class GameSceneUI : MonoBehaviour
{
    [Header("Game Scene UI")]
    [SerializeField] private Button     leaveButton;
    [SerializeField] private GameObject pausePanel;

    [Header("Settings")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    private bool _isPaused;
    private bool _isLeaving; // prevents double-handling

    private void Start()
    {
        if (leaveButton != null)
        {
            leaveButton.onClick.RemoveAllListeners();
            leaveButton.onClick.AddListener(OnLeaveClicked);
        }
        else
        {
            Debug.LogWarning("GameSceneUI: Leave Button is not assigned in the Inspector!");
        }

        if (pausePanel != null)
            pausePanel.SetActive(false);

        // Listen for host disconnection — fired on clients when the host drops
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to avoid stale callbacks
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && pausePanel != null)
        {
            _isPaused = !_isPaused;
            pausePanel.SetActive(_isPaused);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Called on ALL clients when any client disconnects.
    //  When the host drops, Netcode fires this with the server client ID (0),
    //  which is how we detect a host disconnection on the client side.
    // ─────────────────────────────────────────────────────────────────────────
    private void OnClientDisconnected(ulong clientId)
    {
        // Only react if we are a client (not the host) and the server disconnected
        bool isClient        = NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost;
        bool serverDropped   = clientId == NetworkManager.ServerClientId;

        if (isClient && serverDropped)
        {
            Debug.Log("Host disconnected — returning to main menu.");
            ReturnToMenu();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Manual leave button
    // ─────────────────────────────────────────────────────────────────────────
    private void OnLeaveClicked()
    {
        if (RelayTest.Instance == null)
        {
            Debug.LogError("GameSceneUI: RelayTest.Instance is null — is NetworkManagerBootstrapper keeping it alive?");
            return;
        }

        if (leaveButton != null)
            leaveButton.interactable = false;

        ReturnToMenu();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared exit path for both manual leave and host-drop detection
    // ─────────────────────────────────────────────────────────────────────────
    private void ReturnToMenu()
    {
        if (_isLeaving) return; // prevent being called twice
        _isLeaving = true;

        if (leaveButton != null)
            leaveButton.interactable = false;

        if (RelayTest.Instance != null)
            RelayTest.Instance.LeaveAsync();
        else
            Debug.LogError("GameSceneUI: RelayTest.Instance is null — cannot return to menu cleanly.");
    }
}