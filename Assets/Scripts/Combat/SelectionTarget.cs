using UnityEngine;

/// <summary>
/// Marks a GameObject as click-selectable as a combat target.
/// EnemyManager + PlayerManager attach one of these to each spawned capsule.
/// CombatInputHandler raycasts against the trigger collider and reads
/// (TargetId, IsEnemy) to set the active target.
///
/// The collider is a trigger so projectiles (Rigidbody + OnCollisionEnter)
/// fly through without bouncing — triggers don't fire OnCollisionEnter.
/// </summary>
public class SelectionTarget : MonoBehaviour
{
    public ulong TargetId;
    public bool  IsEnemy;

    /// <summary>Adds the marker + a trigger SphereCollider to <paramref name="go"/>.</summary>
    public static SelectionTarget Attach(GameObject go, ulong targetId, bool isEnemy, float radius = 0.6f)
    {
        var marker = go.AddComponent<SelectionTarget>();
        marker.TargetId = targetId;
        marker.IsEnemy  = isEnemy;
        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = radius;
        col.center    = new Vector3(0f, 0.5f, 0f); // capsule center
        return marker;
    }
}
