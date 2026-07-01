using Unity.Netcode;
using UnityEngine;

public class ScopeSystem : NetworkBehaviour
{
    [Header("Cameras")]
    [SerializeField] private Camera    thirdPersonCamera;
    [SerializeField] private Transform firstPersonTarget;

    [Header("Scope Gun Model")]
    [SerializeField] private GameObject scopeGunModel;
    [SerializeField] private Transform  scopeGunTip;        // muzzle on the scope-view gun (local player)

    [Header("Scope Settings")]
    [SerializeField] private float     cameraMoveDuration = 0.2f;
    [SerializeField] private float     scopeZoomFOV       = 20f;
    [SerializeField] private float     hitRange           = 500f;
    [SerializeField] private LayerMask hitLayers          = ~0;

    [Header("Input")]
    [SerializeField] private KeyCode scopeKey = KeyCode.Mouse1;

    [Header("Projectile")]
    [SerializeField] private Transform  projectileSpawnPoint;   // normal gun tip (used by server / other clients)
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      projectileSpeed = 100f;

    [Header("Mesh Layer")]
    [SerializeField] private GameObject playerMeshRoot;
    [SerializeField] private GameObject playerEyesRoot;
    [SerializeField] private GameObject playerGunRoot;
    [SerializeField] private string     localPlayerLayer  = "LocalPlayer";
    [SerializeField] private string     remotePlayerLayer = "RemotePlayer";
    [SerializeField] private string     gunLayer          = "GunLayer";

    [Header("Gun Sway")]
    [Tooltip("How far the gun drifts (in local units) per unit of mouse movement.")]
    [SerializeField] private float swayAmount        = 0.02f;
    [Tooltip("Clamp on how far the positional sway can drift.")]
    [SerializeField] private float maxSwayAmount     = 0.06f;
    [Tooltip("How many degrees the gun tilts per unit of mouse movement.")]
    [SerializeField] private float rotationSwayAmount = 4f;
    [Tooltip("Clamp on how far the rotational sway can tilt.")]
    [SerializeField] private float maxRotationSway   = 10f;
    [Tooltip("Higher = snappier / less floaty sway smoothing.")]
    [SerializeField] private float swaySmooth        = 6f;
    [Header("Gun Sway - Idle Breathing")]
    [Tooltip("How far the gun idly bobs when the mouse isn't moving.")]
    [SerializeField] private float idleSwayAmount    = 0.004f;
    [Tooltip("Speed of the idle breathing motion.")]
    [SerializeField] private float idleSwaySpeed     = 1.2f;

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

    // Gun sway state
    private Vector3    _scopeGunOriginLocalPos;
    private Quaternion _scopeGunOriginLocalRot;
    private Vector3    _currentSwayPos;
    private Quaternion _currentSwayRot = Quaternion.identity;
    private float      _idleSwayTimer;

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            AssignMeshLayer();
            enabled = false;
            return;
        }

        _camController = GetComponent<CamController>();
        if (_camController == null)
            Debug.LogError("ScopeSystem: CamController not found on this GameObject!");

        ValidateReferences();

        // Exclude LocalPlayer and GunLayer from raycasts to prevent self-hits
        int localLayer = LayerMask.NameToLayer(localPlayerLayer);
        if (localLayer != -1)
            hitLayers &= ~(1 << localLayer);
        else
            Debug.LogWarning("ScopeSystem: LocalPlayer layer not found – self-hit exclusion skipped.");

        int gunLayerIndex = LayerMask.NameToLayer(gunLayer);
        if (gunLayerIndex != -1)
            hitLayers &= ~(1 << gunLayerIndex);
        else
            Debug.LogWarning("ScopeSystem: GunLayer not found – scope gun self-hit exclusion skipped.");

        if (thirdPersonCamera != null)
        {
            _cameraParent        = thirdPersonCamera.transform.parent;
            _localOriginPos      = thirdPersonCamera.transform.localPosition;
            _localOriginRot      = thirdPersonCamera.transform.localRotation;
            _originFOV           = thirdPersonCamera.fieldOfView;
            _originalCullingMask = thirdPersonCamera.cullingMask;
        }

        AssignMeshLayer();

        // Put the scope gun on GunLayer so it is visible when scoped in
        // but excluded from raycasts and independent of the LocalPlayer culling mask
        if (scopeGunModel != null)
        {
            int layer = LayerMask.NameToLayer(gunLayer);
            if (layer != -1)
                SetLayerRecursively(scopeGunModel, layer);
            else
                Debug.LogWarning("ScopeSystem: GunLayer not found – scope gun placed on Default layer.");

            // Cache the gun's rest pose so sway can offset from it and always
            // return to the correct place when idle.
            _scopeGunOriginLocalPos = scopeGunModel.transform.localPosition;
            _scopeGunOriginLocalRot = scopeGunModel.transform.localRotation;

            scopeGunModel.SetActive(false);
        }
        else
        {
            Debug.LogWarning("ScopeSystem: Scope Gun Model not assigned – no gun model will appear.");
        }

        SetLocalPlayerLayerVisible(true);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mesh layer assignment — owner → LocalPlayer, everyone else → RemotePlayer
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

        if (playerMeshRoot != null) SetLayerRecursively(playerMeshRoot, layer);
        else Debug.LogWarning("ScopeSystem: Player Mesh Root not assigned – layer assignment skipped.");

        if (playerEyesRoot != null) SetLayerRecursively(playerEyesRoot, layer);
        else Debug.LogWarning("ScopeSystem: Player Eyes Root not assigned – eyes will not be culled.");

        if (playerGunRoot != null) SetLayerRecursively(playerGunRoot, layer);
        else Debug.LogWarning("ScopeSystem: Player Gun Root not assigned – gun will not be culled.");
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
        HandleGunSway();

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
        SetScopeGunVisible(false);
    }

    private void EndScope()
    {
        _isScoped           = false;
        _transitioning      = true;
        _transitionProgress = 0f;

        _transStartPos = thirdPersonCamera.transform.position;
        _transStartRot = thirdPersonCamera.transform.rotation;

        SetLocalPlayerLayerVisible(true);
        SetScopeGunVisible(false);

        // Reset sway so the gun doesn't "snap" from a stale offset next time it's raised.
        _currentSwayPos = Vector3.zero;
        _currentSwayRot = Quaternion.identity;
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
                SetScopeGunVisible(true);
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
    //  HandleGunSway — offsets the scope gun model from its rest pose based on
    //  mouse movement (look-based sway) plus a subtle idle "breathing" drift
    //  when the mouse is still. Purely a local, visual-only effect: it never
    //  touches gameplay state (aim direction, raycasts, projectile spawn).
    // ─────────────────────────────────────────────────────────────────────────
    private void HandleGunSway()
    {
        if (scopeGunModel == null || !scopeGunModel.activeSelf) return;

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // Positional sway — gun lags opposite the look direction.
        float swayX = Mathf.Clamp(-mouseX * swayAmount, -maxSwayAmount, maxSwayAmount);
        float swayY = Mathf.Clamp(-mouseY * swayAmount, -maxSwayAmount, maxSwayAmount);

        // Rotational sway — gun tilts into the movement.
        float rotX = Mathf.Clamp(mouseY * rotationSwayAmount, -maxRotationSway, maxRotationSway);
        float rotY = Mathf.Clamp(-mouseX * rotationSwayAmount, -maxRotationSway, maxRotationSway);

        // Idle breathing — small sine drift so the gun isn't perfectly static
        // when the player holds still while scoped.
        _idleSwayTimer += Time.deltaTime * idleSwaySpeed;
        float idleX = Mathf.Sin(_idleSwayTimer)       * idleSwayAmount;
        float idleY = Mathf.Sin(_idleSwayTimer * 0.5f) * idleSwayAmount;

        Vector3    targetSwayPos = new Vector3(swayX + idleX, swayY + idleY, 0f);
        Quaternion targetSwayRot = Quaternion.Euler(rotX, rotY, 0f);

        _currentSwayPos = Vector3.Lerp(_currentSwayPos, targetSwayPos, Time.deltaTime * swaySmooth);
        _currentSwayRot = Quaternion.Slerp(_currentSwayRot, targetSwayRot, Time.deltaTime * swaySmooth);

        scopeGunModel.transform.localPosition = _scopeGunOriginLocalPos + _currentSwayPos;
        scopeGunModel.transform.localRotation = _scopeGunOriginLocalRot * _currentSwayRot;
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
    private void SetScopeGunVisible(bool visible)
    {
        if (scopeGunModel != null)
        {
            Debug.Log("ScopeSystem: SetScopeGunVisible → " + visible);
            scopeGunModel.SetActive(visible);
        }
        else
        {
            Debug.LogWarning("ScopeSystem: scopeGunModel is null!");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  FireRaycast — uses scopeGunTip as the local spawn origin so the bullet
    //  visually comes from the scope gun's muzzle. The server uses this position
    //  too, which is sent via the ServerRpc. Other clients see the projectile
    //  spawn from projectileSpawnPoint via the server fallback in the ServerRpc.
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

        // Use the scope gun tip as the local visual spawn point if available,
        // otherwise fall back to the normal projectile spawn point
        Vector3 localSpawnPos = scopeGunTip != null
            ? scopeGunTip.position
            : projectileSpawnPoint.position;

        SpawnProjectileServerRpc(localSpawnPos, projectileSpawnPoint.position, targetPoint);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  localSpawnPos  — scope gun tip world position (sent by the owner client)
    //  remoteSpawnPos — normal gun tip world position (used for all other clients)
    //  targetPoint    — world position the projectile flies toward
    // ─────────────────────────────────────────────────────────────────────────
    [ServerRpc]
    private void SpawnProjectileServerRpc(Vector3 localSpawnPos, Vector3 remoteSpawnPos, Vector3 targetPoint)
    {
        if (projectilePrefab == null)
        {
            Debug.LogError("ScopeSystem: Projectile Prefab not assigned!");
            return;
        }

        Vector3 direction = (targetPoint - localSpawnPos).normalized;

        Quaternion spawnRot = direction != Vector3.zero
            ? Quaternion.LookRotation(direction)
            : Quaternion.identity;

        // Spawn at the scope gun tip position — the owner sees it come from their
        // scope gun muzzle. The projectile travels to the same target so hit
        // detection is unaffected.
        GameObject projectileGO = Instantiate(projectilePrefab, localSpawnPos, spawnRot);

        NetworkObject netObj = projectileGO.GetComponent<NetworkObject>();
        if (netObj != null)
            netObj.Spawn();
        else
            Debug.LogError("ScopeSystem: Projectile prefab is missing a NetworkObject component!");

        ScopeProjectile projectile = projectileGO.GetComponent<ScopeProjectile>();
        if (projectile != null)
            // Pass remoteSpawnPos so the projectile can reposition itself on
            // non-owner clients to appear to come from the normal gun tip
            projectile.Initialise(localSpawnPos, remoteSpawnPos, targetPoint, projectileSpeed);
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

        SetScopeGunVisible(false);
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void ValidateReferences()
    {
        if (thirdPersonCamera    == null) Debug.LogError("ScopeSystem: Third Person Camera not assigned!");
        if (firstPersonTarget    == null) Debug.LogError("ScopeSystem: First Person Target not assigned!");
        if (projectileSpawnPoint == null) Debug.LogError("ScopeSystem: Projectile Spawn Point not assigned!");
        if (projectilePrefab     == null) Debug.LogError("ScopeSystem: Projectile Prefab not assigned!");
        if (scopeGunModel        == null) Debug.LogWarning("ScopeSystem: Scope Gun Model not assigned – no gun model will appear.");
        if (scopeGunTip          == null) Debug.LogWarning("ScopeSystem: Scope Gun Tip not assigned – falling back to normal spawn point.");
        if (playerMeshRoot       == null) Debug.LogWarning("ScopeSystem: Player Mesh Root not assigned – layer assignment disabled.");
        if (playerEyesRoot       == null) Debug.LogWarning("ScopeSystem: Player Eyes Root not assigned – eyes will not be culled.");
        if (playerGunRoot        == null) Debug.LogWarning("ScopeSystem: Player Gun Root not assigned – gun will not be culled.");
    }
}