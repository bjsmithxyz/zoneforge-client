using System.Collections;
using UnityEngine;

/// <summary>
/// Attach to all pooled VFX prefabs. Automatically returns to pool when
/// the particle system finishes. Requires a ParticleSystem on the same GO.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class PooledVFX : MonoBehaviour
{
    [Tooltip("Must match the pool key registered in ZoneForgePoolManager")]
    public string poolKey = "vfx_impact_generic";

    private ParticleSystem _ps;
    private Coroutine _returnCoroutine;

    void Awake() => _ps = GetComponent<ParticleSystem>();

    void OnEnable()
    {
        _ps.Play();
        _returnCoroutine = StartCoroutine(WaitAndReturn());
    }

    void OnDisable()
    {
        if (_returnCoroutine != null)
        {
            StopCoroutine(_returnCoroutine);
            _returnCoroutine = null;
        }
        _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private IEnumerator WaitAndReturn()
    {
        yield return new WaitUntil(() => !_ps.IsAlive(true));
        if (ZoneForgePoolManager.Instance != null)
            ZoneForgePoolManager.Instance.Return(poolKey, gameObject);
        else
            gameObject.SetActive(false);
    }
}
