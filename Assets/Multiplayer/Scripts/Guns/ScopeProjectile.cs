using Unity.Netcode;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ScopeProjectile.cs
//
//  The server calls Initialise() immediately after spawning, passing both the
//  local (scope gun tip) and remote (normal gun tip) spawn positions.
//
//  - The owner client sees the projectile fly from the scope gun muzzle.
//  - All other clients see it fly from the normal gun tip position.
//  - All clients fly toward the same target point so hit detection is consistent.
// ─────────────────────────────────────────────────────────────────────────────
public class ScopeProjectile : NetworkBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 10f;

    // Synced so all clients get the correct values
    private NetworkVariable<Vector3> _localSpawnPos  = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> _remoteSpawnPos = new NetworkVariable<Vector3>();
    private NetworkVariable<Vector3> _targetPoint    = new NetworkVariable<Vector3>();
    private NetworkVariable<float>   _speed          = new NetworkVariable<float>();
    private NetworkVariable<ulong>   _ownerClientId  = new NetworkVariable<ulong>();

    private bool  _initialised;
    private float _lifetimeTimer;

    // ─────────────────────────────────────────────────────────────────────────
    //  Called by ScopeSystem on the server right after Spawn()
    // ─────────────────────────────────────────────────────────────────────────
    public void Initialise(Vector3 localSpawnPos, Vector3 remoteSpawnPos, Vector3 targetPoint, float speed)
    {
        _localSpawnPos.Value  = localSpawnPos;
        _remoteSpawnPos.Value = remoteSpawnPos;
        _targetPoint.Value    = targetPoint;
        _speed.Value          = speed;
        _ownerClientId.Value  = OwnerClientId;

        _initialised = true;

        // Server sees the projectile from the scope gun tip
        transform.position = localSpawnPos;
        FaceTarget();
    }

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        _targetPoint.OnValueChanged    += (_, __) => OnVariableReady();
        _speed.OnValueChanged          += (_, __) => OnVariableReady();
        _localSpawnPos.OnValueChanged  += (_, __) => OnVariableReady();
        _remoteSpawnPos.OnValueChanged += (_, __) => OnVariableReady();
        _ownerClientId.OnValueChanged  += (_, __) => OnVariableReady();
    }

    private void OnVariableReady()
    {
        if (_speed.Value <= 0f) return;

        _initialised = true;

        // Position the projectile based on whether this client is the owner
        bool isOwner = NetworkManager.Singleton.LocalClientId == _ownerClientId.Value;
        transform.position = isOwner ? _localSpawnPos.Value : _remoteSpawnPos.Value;

        FaceTarget();
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_initialised) return;

        _lifetimeTimer += Time.deltaTime;

        float step = _speed.Value * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position, _targetPoint.Value, step);

        FaceTarget();

        bool arrived  = Vector3.Distance(transform.position, _targetPoint.Value) < 0.05f;
        bool timedOut = _lifetimeTimer >= maxLifetime;

        if ((arrived || timedOut) && IsServer)
        {
            NetworkObject netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void FaceTarget()
    {
        Vector3 direction = _targetPoint.Value - transform.position;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction);
    }
}