using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Per-enemy component. Lerps toward the last server-reported position each Update
/// (smooths 500ms tick intervals). Plays a scale-to-zero death animation when
/// is_dead becomes true, matching the 3s server despawn window.
/// </summary>
public class EnemyController : MonoBehaviour
{
    private Vector3 _targetPosition;
    private bool    _isDead;
    private float   _deathTimer;

    private const float DeathAnimDuration = 2.5f; // completes before 3s despawn fires
    private const float LerpSpeed         = 8f;   // units/sec toward server position

    public void Init(Enemy enemy)
    {
        var pos = new Vector3(enemy.PositionX, TerrainRenderer.GetSurfaceHeight(enemy.PositionX, enemy.PositionY) + 0.75f, enemy.PositionY);
        transform.position = pos;
        _targetPosition    = pos;
        _isDead            = enemy.IsDead;
    }

    public void ReceiveUpdate(Enemy enemy)
    {
        if (!_isDead && enemy.IsDead)
        {
            _isDead      = true;
            _deathTimer  = DeathAnimDuration;
        }

        if (!_isDead)
            _targetPosition = new Vector3(enemy.PositionX, TerrainRenderer.GetSurfaceHeight(enemy.PositionX, enemy.PositionY) + 0.75f, enemy.PositionY);
    }

    void Update()
    {
        if (_isDead)
        {
            _deathTimer = Mathf.Max(0f, _deathTimer - Time.deltaTime);
            float t = 1f - _deathTimer / DeathAnimDuration;
            transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t);
            return;
        }

        transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * LerpSpeed);
    }
}
