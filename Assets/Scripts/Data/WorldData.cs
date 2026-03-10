using UnityEngine;

[CreateAssetMenu(fileName = "WorldData", menuName = "ZoneForge/WorldData")]
public class WorldData : ScriptableObject
{
    public string WorldName = "ZoneForge World";
    public uint StartingZoneId = 1;
}
