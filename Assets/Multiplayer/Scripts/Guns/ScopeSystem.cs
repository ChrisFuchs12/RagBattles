using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ScopeSystem : NetworkBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera    thirdPersonCamera;
    [SerializeField] private Transform firstPersonTarget;

    [Header("Scope UI")]
    [SerializeField] private Canvas scopeCanvas;
    [SerializeField] private Image  scopeOverlay;

    [Header("Scope Settings")]
    [SerializeField] private float     cameraMoveDuration = 0.2f;
    [SerializeField] private float     scopeZoomFOV       = 20f;
    [SerializeField] private float     hitRange           = 500f;
    [SerializeField] private LayerMask hitLayers          = ~0;

    [Header("Input")]
    [SerializeField] private KeyCode scopeKey = KeyCode.Mouse1;

    [Header("Projectile")]
    [SerializeField] private Transform  projectileSpawnPoint;
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 100f;

    [Header("Mesh Layer")]
    [SerializeField] private GameObject playerMeshRoot;
    [SerializeField] private GameObject playerEyesRoot;
    [SerializeField] private GameObject playerGunRoot;
    [SerializeField] private string     localPlayerLayer  = "LocalPlayer";
    [SerializeField] private string     remotePlayerLayer = "RemotePlayer";

    // ── Private state ─────────────────────────────────────────────────────────
    private CamController  _camController;
    private Transform      _cameraParent;

    private Vector3    _localOriginPos;
    private Quaternion _localOriginRot;
    private float      _originFOV;
    private int        _originalCullingMask;

    private bool       _isScoped;
    private bool       _transitioning;
    private float      _transitionProgress;
    private Vector3    _transStartPos;
    private Quaternion _transStartRot;

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Non-owners still need their mesh layer assigned so the local
            // player's camera culls them correctly when scoped in
            AssignMeshLayer();
            enabled = false;
            return;
        }

        _camController = GetComponent<CamController>();
        if (_camController == null)
            Debug.LogError("ScopeSystem: CamController not found on this GameObject!");

        ValidateReferences();

        // Exclude the local player's own mesh layer from raycasts to prevent self-hits
        int localLayer = LayerMask.NameToLayer(localPlayerLayer);
        if (localLayer != -1)
            hitLayers &= ~(1 << localLayer);
        else
            Debug.LogWarning("ScopeSystem: LocalPlayer layer not found – self-hit exclusion skipped.");

        if (thirdPersonCamera != null)
        {
            _cameraParent        = thirdPersonCamera.transform.parent;
            _localOriginPos      = thirdPersonCamera.transform.localPosition;
            _localOriginRot      = thirdPersonCamera.transform.localRotation;
            _originFOV           = thirdPersonCamera.fieldOfView;
            _originalCullingMask = thirdPersonCamera.cullingMask;
        }

        // Assign mesh, eyes and gun to LocalPlayer layer so the culling mask
        // can hide all of them when fully scoped in
        AssignMeshLayer();

        SetScopeUIVisible(false);
        SetLocalPlayerLayerVisible(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mesh layer assignment — owner → LocalPlayer, everyone else → RemotePlayer
    //  Runs on ALL instances so every client assigns remote players correctly.
    // ─────────────────────────────────────────────────────────────────────────
    private void AssignMeshLayer()
    {
        string layerName = IsOwner ? localPlayerLayer : remotePlayerLayer;
        int    layer     = LayerMask.NameToLayer(layerName);

        if (layer == -1)
        {
            Debug.LogError("ScopeSystem: Layer '" + layerName + "' does not exist! " +
                           "Add it in Project Settings > Tags & Layers.");
            return;
        }

        if (playerMeshRoot != null)
            SetLayerRecursively(playerMeshRoot, layer);
        else
            Debug.LogWarning("ScopeSystem: Player Mesh Root not assigned – layer assignment skipped.");

        if (playerEyesRoot != null)
            SetLayerRecursively(playerEyesRoot, layer);
        else
            Debug.LogWarning("ScopeSystem: Player Eyes Root not assigned – eyes will not be culled.");

        if (playerGunRoot != null)
            SetLayerRecursively(playerGunRoot, layer);
        else
            Debug.LogWarning("ScopeSystem: Player Gun Root not assigned – gun will not be culled.");
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!IsOwner) return;

        HandleScopeInput();
        HandleCameraTransition();

        if (_isScoped && !_transitioning && Input.GetKeyDown(KeyCode.Mouse0))
            FireRaycast();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void HandleScopeInput()
    {
        if (Input.GetKeyDown(scopeKey) && !_isScoped) StartScope();
        if (Input.GetKeyUp(scopeKey)   &&  _isScoped) EndScope();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void StartScope()
    {
        _isScoped           = true;
        _transitioning      = true;
        _transitionProgress = 0f;

        _transStartPos = thirdPersonCamera.transform.position;
        _transStartRot = thirdPersonCamera.transform.rotation;

        SetLocalPlayerLayerVisible(true);
        SetScopeUIVisible(false);
    }

    private void EndScope()
    {
        _isScoped           = false;
        _transitioning      = true;
        _transitionProgress = 0f;

        _transStartPos = thirdPersonCamera.transform.position;
        _transStartRot = thirdPersonCamera.transform.rotation;

        SetLocalPlayerLayerVisible(true);
        SetScopeUIVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void HandleCameraTransition()
    {
        if (!_transitioning || thirdPersonCamera == null || firstPersonTarget == null) return;

        _transitionProgress += Time.deltaTime / cameraMoveDuration;
        _transitionProgress  = Mathf.Clamp01(_transitionProgress);

        float t = Mathf.SmoothStep(0f, 1f, _transitionProgress);

        if (_isScoped)
        {
            thirdPersonCamera.transform.position = Vector3.Lerp(
                _transStartPos, firstPersonTarget.position, t);

            thirdPersonCamera.transform.rotation = Quaternion.Slerp(
                _transStartRot, firstPersonTarget.rotation, t);

            thirdPersonCamera.fieldOfView = Mathf.Lerp(_originFOV, scopeZoomFOV, t);

            if (_transitionProgress >= 1f)
            {
                _transitioning = false;
                SetLocalPlayerLayerVisible(false);
                SetScopeUIVisible(true);
            }
        }
        else
        {
            Vector3    destPos = GetThirdPersonWorldPos();
            Quaternion destRot = GetThirdPersonWorldRot();

            thirdPersonCamera.transform.position = Vector3.Lerp(_transStartPos, destPos, t);
            thirdPersonCamera.transform.rotation = Quaternion.Slerp(_transStartRot, destRot, t);
            thirdPersonCamera.fieldOfView        = Mathf.Lerp(scopeZoomFOV, _originFOV, t);

            if (_transitionProgress >= 1f)
            {
                _transitioning = false;

                thirdPersonCamera.transform.localPosition = _localOriginPos;
                thirdPersonCamera.transform.localRotation = _localOriginRot;
                thirdPersonCamera.fieldOfView             = _originFOV;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private Vector3 GetThirdPersonWorldPos()
    {
        return _cameraParent != null
            ? _cameraParent.TransformPoint(_localOriginPos)
            : _localOriginPos;
    }

    private Quaternion GetThirdPersonWorldRot()
    {
        return _cameraParent != null
            ? _cameraParent.rotation * _localOriginRot
            : _localOriginRot;
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void SetLocalPlayerLayerVisible(bool visible)
    {
        if (thirdPersonCamera == null) return;

        int layer = LayerMask.NameToLayer(localPlayerLayer);
        if (layer == -1)
        {
            Debug.LogError("ScopeSystem: Layer '" + localPlayerLayer + "' does not exist!");
            return;
        }

        thirdPersonCamera.cullingMask = visible
            ? _originalCullingMask
            : _originalCullingMask & ~(1 << layer);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void FireRaycast()
    {
        if (thirdPersonCamera == null) return;

        Ray ray = new Ray(
            thirdPersonCamera.transform.position,
            thirdPersonCamera.transform.forward);

        Vector3 targetPoint = Physics.Raycast(ray, out RaycastHit hit, hitRange, hitLayers)
            ? hit.point
            : ray.GetPoint(hitRange);

        if (hit.collider != null)
            LogHit(hit);
        else
            Debug.Log("ScopeSystem: No hit detected - firing toward max range.");

        SpawnProjectileServerRpc(projectileSpawnPoint.position, targetPoint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    [ServerRpc]
    private void SpawnProjectileServerRpc(Vector3 spawnPos, Vector3 targetPoint)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("ScopeSystem: Projectile Prefab not assigned!");
            return;
        }

        Vector3 direction = (targetPoint - spawnPos).normalized;

        Quaternion spawnRot = direction != Vector3.zero
            ? Quaternion.LookRotation(direction)
            : Quaternion.identity;

        GameObject projectileGO = Instantiate(projectilePrefab, spawnPos, spawnRot);

        NetworkObject netObj = projectileGO.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
        else
            Debug.LogError("ScopeSystem: Projectile prefab is missing a NetworkObject component!");

        ScopeProjectile projectile = projectileGO.GetComponent<ScopeProjectile>();
        if (projectile != null)
            projectile.Initialise(targetPoint, projectileSpeed);
        else
            Debug.LogError("ScopeSystem: Projectile prefab is missing a ScopeProjectile component!");
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void LogHit(RaycastHit hit)
    {
        Debug.Log("ScopeSystem: [" + NetworkManager.Singleton.LocalClientId + "] " +
                  "Hit '" + hit.collider.gameObject.name + "' " +
                  "on layer '" + LayerMask.LayerToName(hit.collider.gameObject.layer) + "' " +
                  "at " + hit.point + " | distance: " + hit.distance.ToString("F1") + "m");
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void SetScopeUIVisible(bool visible)
    {
        if (scopeCanvas  != null) scopeCanvas.gameObject.SetActive(visible);
        if (scopeOverlay != null) scopeOverlay.gameObject.SetActive(visible);
    }

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkDespawn()
    {
        if (!IsOwner) return;

        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.transform.localPosition = _localOriginPos;
            thirdPersonCamera.transform.localRotation = _localOriginRot;
            thirdPersonCamera.fieldOfView             = _originFOV;
            thirdPersonCamera.cullingMask             = _originalCullingMask;
        }

        SetScopeUIVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void ValidateReferences()
    {
        if (thirdPersonCamera    == null) Debug.LogError("ScopeSystem: Third Person Camera not assigned!");
        if (firstPersonTarget    == null) Debug.LogError("ScopeSystem: First Person Target not assigned!");
        if (projectileSpawnPoint == null) Debug.LogError("ScopeSystem: Projectile Spawn Point not assigned!");
        if (projectilePrefab     == null) Debug.LogError("ScopeSystem: Projectile Prefab not assigned!");
        if (playerMeshRoot       == null) Debug.LogWarning("ScopeSystem: Player Mesh Root not assigned – layer assignment disabled.");
        if (playerEyesRoot       == null) Debug.LogWarning("ScopeSystem: Player Eyes Root not assigned – eyes will not be culled.");
        if (playerGunRoot        == null) Debug.LogWarning("ScopeSystem: Player Gun Root not assigned – gun will not be culled.");
        if (scopeCanvas          == null) Debug.LogWarning("ScopeSystem: Scope Canvas not assigned – overlay disabled.");
        if (scopeOverlay         == null) Debug.LogWarning("ScopeSystem: Scope Overlay not assigned – overlay disabled.");
    }
}