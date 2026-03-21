using UnityEngine;
using SpacetimeDB.Types;

/// <summary>
/// Attached to the root Zone GameObject in a zone scene.
/// Holds server-side zone metadata. The 3D tile grid is managed separately
/// by the tile placement system — no Tilemaps used in the game client.
/// </summary>
public class ZoneController : MonoBehaviour
{
    [Header("Zone Identity (synced from server)")]
    public ulong ZoneId;
    public string ZoneName;
    public uint GridWidth;
    public uint GridHeight;

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
        GridWidth = zone.TerrainWidth;
        GridHeight = zone.TerrainHeight;
        gameObject.name = $"Zone_{zone.Name}";
    }

    void OnDrawGizmosSelected()
    {
        if (GridWidth == 0 || GridHeight == 0) return;

        // Draw zone bounds as a wire cube on the X/Z plane (Y = up)
        Gizmos.color = Color.cyan;
        Vector3 size = new Vector3(GridWidth, 0.1f, GridHeight);
        Vector3 centre = transform.position + new Vector3(GridWidth * 0.5f, 0f, GridHeight * 0.5f);
        Gizmos.DrawWireCube(centre, size);
    }
}
