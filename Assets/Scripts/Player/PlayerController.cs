using UnityEngine;
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
        Vector3 spawnPos = new Vector3(player.PositionX, 1f, player.PositionY);
        transform.position = spawnPos;
        _lastSentPosition = spawnPos;
        _targetPosition = spawnPos;

        if (isLocal)
            SpacetimeDBManager.Conn.Reducers.OnMovePlayer += OnMovePlayerResult;
    }

    void OnDestroy()
    {
        if (IsLocal && SpacetimeDBManager.Conn != null)
            SpacetimeDBManager.Conn.Reducers.OnMovePlayer -= OnMovePlayerResult;
    }

    void OnMovePlayerResult(ReducerEventContext ctx, float newX, float newY)
    {
        if (ctx.Event.Status is Status.Failed failedStatus)
            Debug.LogWarning($"[PlayerController] MovePlayer failed: {failedStatus.Message}");
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
            transform.position += dir * _speed * Time.deltaTime;
            EnforceY();
        }

        // 2. Apply reconciliation correction (MoveTowards at fixed speed)
        if (_reconciling)
        {
            transform.position = Vector3.MoveTowards(
                transform.position, _reconcileTarget, _reconcileSpeed * Time.deltaTime);
            EnforceY();
            if (Vector3.Distance(transform.position, _reconcileTarget) < 0.001f)
                _reconciling = false;
        }

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
                SpacetimeDBManager.Conn.Reducers.MovePlayer(pos.x, pos.z);
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
        Vector3 serverPos = new Vector3(newPlayer.PositionX, 1f, newPlayer.PositionY);

        if (!IsLocal)
        {
            _targetPosition = serverPos;
            return;
        }

        float dist = Vector3.Distance(transform.position, serverPos);
        if (dist > 1.0f)
        {
            // Start or restart reconciliation
            _reconcileTarget = serverPos;
            _reconcileSpeed = dist / 0.2f; // covers gap in 0.2 s
            _reconciling = true;
        }
        else
        {
            // Cancel any in-progress reconciliation — server agrees closely enough
            _reconciling = false;
        }
    }

    // Force Y to 1.0f after every position write (camera is parented here)
    void EnforceY() =>
        transform.position = new Vector3(transform.position.x, 1f, transform.position.z);

    static Vector3 GetCameraRelativeInput()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (h == 0f && v == 0f) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return Vector3.zero;
        forward.Normalize();

        Vector3 right = cam.transform.right;
        right.y = 0f;
        right.Normalize();

        return (forward * v + right * h).normalized;
    }
}
