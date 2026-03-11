using UnityEngine;

[CreateAssetMenu(fileName = "ZoneVisualData", menuName = "ZoneForge/ZoneVisualData")]
public class ZoneVisualData : ScriptableObject
{
    [Header("Identity")]
    public string ZoneTypeName = "Default";

    [Header("Default Tile Prefabs")]
    public GameObject DefaultGroundTilePrefab;
    public GameObject DefaultDecorationTilePrefab;

    [Header("Atmosphere")]
    public Color AmbientLightColor = Color.white;
    [Range(0f, 2f)]
    public float AmbientLightIntensity = 1f;
}
