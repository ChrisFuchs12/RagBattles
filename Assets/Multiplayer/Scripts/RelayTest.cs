using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Core;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using Unity.Netcode.Transports.UTP;
using Unity.Netcode;
using UnityEngine.UI;
using TMPro;

public class RelayTest : MonoBehaviour
{
    [Header("Required UI Components")]
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TMP_InputField joinInput;
    [SerializeField] private TextMeshProUGUI codeText;

    [Header("Network Components")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;

    private void Awake()
    {
        // Validate critical component references
        ValidateComponentReferences();
    }

    private void ValidateComponentReferences()
    {
        // Check for null references and provide helpful debug information
        if (hostButton == null) Debug.LogError("Host Button is not assigned in the Inspector!");
        if (joinButton == null) Debug.LogError("Join Button is not assigned in the Inspector!");
        if (joinInput == null) Debug.LogError("Join Input Field is not assigned in the Inspector!");
        if (codeText == null) Debug.LogError("Code Text is not assigned in the Inspector!");
        
        // Network manager and transport validation
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
    }

    private void Start()
    {
        // Only proceed if all critical references are present
        if (AreComponentsValid())
        {
            InitializeUnityServicesAsync();
        }
    }

    private bool AreComponentsValid()
    {
        return hostButton != null && 
               joinButton != null && 
               joinInput != null && 
               codeText != null && 
               networkManager != null && 
               unityTransport != null;
    }

    private async void InitializeUnityServicesAsync()
    {
        try 
        {
            // Prevent duplicate initialization
            if (UnityServices.State == ServicesInitializationState.Initialized)
                return;

            // Initialize Unity Services
            await UnityServices.InitializeAsync();

            // Sign in anonymously if not already signed in
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Set up button listeners
            SetupButtonListeners();
        }
        catch (Exception ex)
        {
            HandleInitializationError(ex);
        }
    }

    private void SetupButtonListeners()
    {
        // Clear existing listeners to prevent multiple registrations
        hostButton.onClick.RemoveAllListeners();
        joinButton.onClick.RemoveAllListeners();

        hostButton.onClick.AddListener(CreateRelayAsync);
        joinButton.onClick.AddListener(() => JoinRelayAsync(joinInput.text));
    }

    private async void CreateRelayAsync()
    {
        try 
        {
            // Create relay allocation for 10 max connections
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(10);
            
            // Get join code for the allocation
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            
            // Display join code
            codeText.text = "Code: " + joinCode;
            
            // Set up relay server data
            var relayServerData = new RelayServerData(allocation, "dtls");
            
            // Configure NetworkManager to use relay
            unityTransport.SetRelayServerData(relayServerData);
            
            // Start hosting
            networkManager.StartHost();
        }
        catch (Exception ex)
        {
            HandleRelayError("Host", ex);
        }
    }

    private async void JoinRelayAsync(string joinCode)
    {
        try 
        {
            // Join relay allocation using provided join code
            var joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
            
            // Set up relay server data
            var relayServerData = new RelayServerData(joinAllocation, "dtls");
            
            // Configure NetworkManager to use relay
            unityTransport.SetRelayServerData(relayServerData);
            
            // Start client
            networkManager.StartClient();
        }
        catch (Exception ex)
        {
            HandleRelayError("Join", ex);
        }
    }

    private void HandleInitializationError(Exception ex)
    {
        string errorMessage = $"Initialization Error: {ex.Message}";
        Debug.LogError(errorMessage);
        
        if (codeText != null)
        {
            codeText.text = errorMessage;
        }
    }

    private void HandleRelayError(string operation, Exception ex)
    {
        string errorMessage = $"{operation} Relay Error: {ex.Message}";
        Debug.LogError(errorMessage);
        
        if (codeText != null)
        {
            codeText.text = errorMessage;
        }
    }
}