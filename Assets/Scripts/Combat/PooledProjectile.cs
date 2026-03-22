using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to all projectile prefabs. Self-returns to pool on collision or timeout.
/// Requires a Rigidbody (gravity disabled) on the same GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PooledProjectile : MonoBehaviour
{
    [Tooltip("Must match the pool key registered in ZoneForgePoolManager")]
    public string poolKey = "projectile_fireball";
    public float speed = 12f;
    public float maxLifetime = 5f;

    [Tooltip("VFX pool key to spawn on impact. Leave empty to skip.")]
    public string impactVfxKey = "vfx_impact_fire";

    private Rigidbody _rb;
    private bool _hasReturned;
    private Coroutine _timeoutCoroutine;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
    }

    void OnEnable()
    {
        _hasReturned = false;
        _timeoutCoroutine = StartCoroutine(TimeoutReturn());
    }

    void OnDisable()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }
        _rb.linearVelocity = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
    }

    /// <summary>Aim and launch toward a world-space target position.</summary>
    public void Launch(Vector3 origin, Vector3 targetPos)
    {
        transform.position = origin;
        Vector3 dir = (targetPos - origin).normalized;
        transform.forward = dir;
        _rb.linearVelocity = dir * speed;
    }

    void OnCollisionEnter(Collision collision)
    {
        ReturnToPool(collision.contacts.Length > 0 ? collision.contacts[0].point : transform.position);
    }

    private IEnumerator TimeoutReturn()
    {
        yield return new WaitForSeconds(maxLifetime);
        ReturnToPool(transform.position);
    }

    private void ReturnToPool(Vector3 impactPoint)
    {
        if (_hasReturned) return;
        _hasReturned = true;

        // Spawn impact VFX
        if (!string.IsNullOrEmpty(impactVfxKey) && ZoneForgePoolManager.Instance != null)
        {
            var vfx = ZoneForgePoolManager.Instance.Get(impactVfxKey);
            if (vfx != null)
                vfx.transform.position = impactPoint;
        }

        if (ZoneForgePoolManager.Instance != null)
            ZoneForgePoolManager.Instance.Return(poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }
}
