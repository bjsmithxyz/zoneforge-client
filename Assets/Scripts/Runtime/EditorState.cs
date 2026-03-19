using System;

/// <summary>
/// Global editor state — tracks which zone is currently being edited.
/// </summary>
public static class EditorState
{
    public static ulong ActiveZoneId { get; private set; }
    public static bool HasActiveZone => ActiveZoneId != 0;

    public static event Action<ulong> OnActiveZoneChanged;

    public static void SetActiveZone(ulong zoneId)
    {
        if (ActiveZoneId == zoneId) return;
        ActiveZoneId = zoneId;
        OnActiveZoneChanged?.Invoke(zoneId);
    }
}
