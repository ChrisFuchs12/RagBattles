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
    private bool _isLeaving;
    private bool _wasConnected; // tracks whether we were connected last frame

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

        // Record that we are connected when the game scene starts
        _wasConnected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;
    }

    private void Update()
    {
        // Toggle pause panel
        if (Input.GetKeyDown(toggleKey) && pausePanel != null)
        {
            _isPaused = !_isPaused;
            pausePanel.SetActive(_isPaused);
        }

        // Detect host drop — if we were connected but no longer are, return to menu.
        // This is more reliable than OnClientDisconnectCallback which can miss host
        // shutdowns depending on the Netcode version.
        if (_isLeaving) return;

        bool isNowConnected = NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient;

        if (_wasConnected && !isNowConnected)
        {
            Debug.Log("Lost connection to host — returning to main menu.");
            ReturnToMenu();
        }

        _wasConnected = isNowConnected;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Manual leave button
    // ─────────────────────────────────────────────────────────────────────────
    private void OnLeaveClicked()
    {
        if (leaveButton != null)
            leaveButton.interactable = false;

        ReturnToMenu();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Shared exit path for both manual leave and host-drop detection
    // ─────────────────────────────────────────────────────────────────────────
    private void ReturnToMenu()
    {
        if (_isLeaving) return;
        _isLeaving = true;

        // Restore cursor before returning to menu
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        if (leaveButton != null)
            leaveButton.interactable = false;

        if (RelayTest.Instance != null)
            RelayTest.Instance.LeaveAsync();
        else
            Debug.LogError("GameSceneUI: RelayTest.Instance is null — cannot return to menu cleanly.");
    }
}