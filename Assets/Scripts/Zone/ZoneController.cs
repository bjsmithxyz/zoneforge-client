using UnityEngine;
using UnityEngine.Tilemaps;
using SpacetimeDB.Types;

/// <summary>
/// Attached to the root Grid GameObject in a zone scene.
/// Holds server-side zone metadata and references to the three tilemap layers.
/// </summary>
public class ZoneController : MonoBehaviour
{
    [Header("Zone Identity (synced from server)")]
    public ulong ZoneId;
    public string ZoneName;
    public uint GridWidth;
    public uint GridHeight;

    [Header("Tilemap Layers")]
    public Tilemap GroundLayer;
    public Tilemap DecorationLayer;
    public Tilemap CollisionLayer;

    [Header("Visual Config")]
    public ZoneVisualData VisualData;

    /// <summary>
    /// Populate this controller from a server Zone row.
    /// Call this after subscribing and receiving zone data.
    /// </summary>
    public void InitFromServerData(Zone zone)
    {
        ZoneId = zone.Id;
        ZoneName = zone.Name;
        GridWidth = zone.GridWidth;
        GridHeight = zone.GridHeight;
        gameObject.name = $"Zone_{zone.Name}";
    }

    void OnDrawGizmosSelected()
    {
        if (GridWidth == 0 || GridHeight == 0) return;

        // Draw zone bounds as a wire cube centred on the grid origin
        Gizmos.color = Color.cyan;
        Vector3 size = new Vector3(GridWidth, GridHeight, 0.1f);
        Vector3 centre = transform.position + new Vector3(GridWidth * 0.5f, GridHeight * 0.5f, 0f);
        Gizmos.DrawWireCube(centre, size);
    }
}
