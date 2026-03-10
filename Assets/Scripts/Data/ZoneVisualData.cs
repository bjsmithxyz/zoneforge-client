using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(fileName = "ZoneVisualData", menuName = "ZoneForge/ZoneVisualData")]
public class ZoneVisualData : ScriptableObject
{
    [Header("Identity")]
    public string ZoneTypeName = "Default";

    [Header("Default Tiles")]
    public TileBase DefaultGroundTile;
    public TileBase DefaultDecorationTile;

    [Header("Atmosphere")]
    public Color AmbientLightColor = Color.white;
    [Range(0f, 2f)]
    public float AmbientLightIntensity = 1f;
}
