using Unity.Netcode;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ScopeProjectile.cs
//  Attach to your projectile prefab alongside a NetworkObject.
//
//  The server calls Initialise() immediately after spawning. The projectile
//  then moves toward the target point at a fixed speed on every client.
//  It destroys itself (server-side, despawning for all clients) either when
//  it arrives at the target or after a safety lifetime expires.
// ─────────────────────────────────────────────────────────────────────────────
public class ScopeProjectile : NetworkBehaviour
{
    [Header("Lifetime")]
    [SerializeField] private float maxLifetime = 10f; // despawn safety net

    // Synced so late-joining clients also get the correct values
    private NetworkVariable<Vector3> _targetPoint  = new NetworkVariable<Vector3>();
    private NetworkVariable<float>   _speed        = new NetworkVariable<float>();

    private bool  _initialised;
    private float _lifetimeTimer;

    // ─────────────────────────────────────────────────────────────────────────
    //  Called by ScopeSystem on the server right after Spawn()
    // ─────────────────────────────────────────────────────────────────────────
    public void Initialise(Vector3 targetPoint, float speed)
    {
        _targetPoint.Value = targetPoint;
        _speed.Value       = speed;
        _initialised       = true;

        // Point the transform at the target immediately on the server
        FaceTarget();
    }

    // ─────────────────────────────────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        // On clients the NetworkVariables arrive shortly after spawn;
        // once they have non-zero speed we treat the projectile as initialised
        _targetPoint.OnValueChanged += (_, __) => OnVariableReady();
        _speed.OnValueChanged       += (_, __) => OnVariableReady();
    }

    private void OnVariableReady()
    {
        if (_speed.Value > 0f)
        {
            _initialised = true;
            FaceTarget();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (!_initialised) return;

        _lifetimeTimer += Time.deltaTime;

        // Move toward target at constant speed
        float step = _speed.Value * Time.deltaTime;
        transform.position = Vector3.MoveTowards(
            transform.position, _targetPoint.Value, step);

        // Keep facing the target while in flight (handles any spawn offset)
        FaceTarget();

        // Despawn when arrived or lifetime exceeded — server only
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
