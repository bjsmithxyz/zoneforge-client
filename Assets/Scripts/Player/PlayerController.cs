using UnityEngine;
using UnityEngine.AI;
using SpacetimeDB;
using SpacetimeDB.Types;

/// <summary>
/// Attached to each player capsule. isLocal=true: reads WASD input, predicts
/// movement, throttles MovePlayer reducer calls. isLocal=false: lerps to server pos.
/// </summary>
public class PlayerController : MonoBehaviour
{
    public bool IsLocal { get; private set; }

    [SerializeField] private float _speed = 5f;

    private Camera _camera;
    private NavMeshAgent _agent;

    // Local player: throttled reducer sends
    private Vector3 _lastSentPosition;
    private float _sendTimer;
    private const float SendInterval = 0.1f;

    // Local player: reconciliation
    private Vector3 _reconcileTarget;
    private float _reconcileSpeed;
    private bool _reconciling;

    // Remote player: lerp target
    private Vector3 _targetPosition;

    /// <summary>
    /// Call immediately after AddComponent. Sets initial position and wires
    /// up the MovePlayer error callback for local players.
    /// </summary>
    public void Init(Player player, bool isLocal)
    {
        IsLocal = isLocal;
        float spawnTerrainY = TerrainRenderer.GetSurfaceHeight(player.PositionX, player.PositionY);
        Vector3 spawnPos = new Vector3(player.PositionX, spawnTerrainY + 1f, player.PositionY);
        transform.position = spawnPos;
        _lastSentPosition = spawnPos;
        _targetPosition = spawnPos;
        _sendTimer = 0f;
        _camera = Camera.main;

        if (isLocal)
            SpacetimeDBManager.Conn.Reducers.OnMovePlayer += OnMovePlayerResult;
    }

    void OnDestroy()
    {
        if (IsLocal && SpacetimeDBManager.Conn != null)
            SpacetimeDBManager.Conn.Reducers.OnMovePlayer -= OnMovePlayerResult;
    }

    /// <summary>
    /// Called by PlayerManager once the NavMesh is baked and a NavMeshAgent
    /// has been configured on this GameObject. From this point on, all local
    /// movement goes through the agent so it respects terrain and boundaries.
    /// </summary>
    public void SetAgent(NavMeshAgent agent)
    {
        _agent = agent;
        // Snap to the nearest NavMesh point at current position
        _agent.Warp(transform.position);
    }

    void OnMovePlayerResult(ReducerEventContext ctx, float newX, float newY)
    {
        if (ctx.Event.CallerIdentity != SpacetimeDBManager.LocalIdentity) return;
        if (ctx.Event.Status is Status.Failed(var reason))
            Debug.LogWarning($"[PlayerController] MovePlayer failed: {reason}");
    }

    void Update()
    {
        if (IsLocal) UpdateLocal();
        else UpdateRemote();
    }

    void UpdateLocal()
    {
        // 1. Apply WASD input immediately (client-side prediction)
        Vector3 dir = GetCameraRelativeInput();
        if (dir.sqrMagnitude > 0.001f)
        {
            if (_agent != null)
                _agent.Move(dir * _speed * Time.deltaTime);  // NavMesh constrains movement
            else
                transform.position += dir * _speed * Time.deltaTime;
            // Active input takes priority — cancel any in-progress reconciliation
            // so the server's lagged position doesn't fight the new direction.
            _reconciling = false;
        }

        // 2. Apply reconciliation correction
        if (_reconciling)
        {
            if (_agent != null)
            {
                // Warp was already issued in ReceiveServerPosition; just clear the flag
                _reconciling = false;
            }
            else
            {
                transform.position = Vector3.MoveTowards(
                    transform.position, _reconcileTarget, _reconcileSpeed * Time.deltaTime);
                if (Vector3.Distance(transform.position, _reconcileTarget) < 0.001f)
                    _reconciling = false;
            }
        }

        // Keep Y snapped to terrain surface every frame until NavMeshAgent takes over.
        // Without this, the player stays at spawn-time Y=1 (terrain not yet loaded)
        // until they move, leaving them clipped through terrain while standing still.
        if (_agent == null)
            EnforceY();

        // 3. Throttled reducer send (10 Hz, only if moved)
        _sendTimer += Time.deltaTime;
        if (_sendTimer >= SendInterval)
        {
            _sendTimer = 0f;
            Vector3 pos = transform.position;
            float dx = pos.x - _lastSentPosition.x;
            float dz = pos.z - _lastSentPosition.z;
            if (dx * dx + dz * dz > 0.01f * 0.01f)
            {
                // Clamp to zone bounds before sending — the NavMeshAgent normally
                // prevents out-of-bounds movement, but may not be active yet while
                // the NavMesh is still baking on first load.
                float sendX = pos.x;
                float sendZ = pos.z;
                foreach (var zone in SpacetimeDBManager.Conn.Db.Zone.Iter())
                {
                    if (zone.Id != SpacetimeDBManager.CurrentZoneId) continue;
                    sendX = Mathf.Clamp(pos.x, 0f, (float)zone.TerrainWidth  - 1f);
                    sendZ = Mathf.Clamp(pos.z, 0f, (float)zone.TerrainHeight - 1f);
                    break;
                }
                SpacetimeDBManager.Conn.Reducers.MovePlayer(sendX, sendZ);
                _lastSentPosition = pos;
            }
        }
    }

    void UpdateRemote()
    {
        transform.position = Vector3.Lerp(
            transform.position, _targetPosition, Time.deltaTime * 10f);
        EnforceY();
    }

    /// <summary>
    /// Called by PlayerManager when the server sends a new position for this player.
    /// </summary>
    public void ReceiveServerPosition(Player newPlayer)
    {
        float terrainY = TerrainRenderer.GetSurfaceHeight(newPlayer.PositionX, newPlayer.PositionY);
        Vector3 serverPos = new Vector3(newPlayer.PositionX, terrainY + 1f, newPlayer.PositionY);

        if (!IsLocal)
        {
            _targetPosition = serverPos;
            return;
        }

        // Compare XZ only — the server stores no Y, so serverPos.y is always 1f.
        // Including Y in the distance check causes false reconciliation when
        // NavMeshAgent places the player at a different height (e.g. above terrain).
        float dx = transform.position.x - serverPos.x;
        float dz = transform.position.z - serverPos.z;
        float dist = Mathf.Sqrt(dx * dx + dz * dz);
        // Only reconcile when the player is not pressing keys. While moving, prediction
        // is authoritative — the server's lagged position would fight the new direction.
        bool hasInput = Mathf.Abs(Input.GetAxisRaw("Horizontal")) > 0f
                     || Mathf.Abs(Input.GetAxisRaw("Vertical")) > 0f;
        // Threshold is 2× the max expected drift (5 u/s × 0.1 s send interval = 0.5 u).
        // Corrections below 1.0 m are ignored to prevent jitter from normal round-trip latency.
        if (dist > 1.0f && !hasInput)
        {
            if (_agent != null)
            {
                // Warp snaps to the nearest NavMesh point at the server XZ position
                _agent.Warp(new Vector3(serverPos.x, 0f, serverPos.z));
                _reconciling = false;
            }
            else
            {
                // Start or restart reconciliation
                _reconcileTarget = serverPos;
                _reconcileSpeed  = dist / 0.2f;  // covers gap in 0.2 s
                _reconciling     = true;
            }
        }
        else
        {
            // Cancel any in-progress reconciliation — server agrees closely enough
            _reconciling = false;
        }
    }

    // Keep Y at terrain surface + 1 (capsule center 1 unit above ground).
    void EnforceY()
    {
        float terrainY = TerrainRenderer.GetSurfaceHeight(transform.position.x, transform.position.z);
        transform.position = new Vector3(transform.position.x, terrainY + 1f, transform.position.z);
    }

    Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (h == 0f && v == 0f) return Vector3.zero;

        if (_camera == null) return Vector3.zero;

        Vector3 forward = _camera.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return Vector3.zero;
        forward.Normalize();

        Vector3 right = _camera.transform.right;
        right.y = 0f;
        right.Normalize();

        return (forward * v + right * h).normalized;
    }
}
