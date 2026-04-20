using System.Collections.Generic;
using UnityEngine;

namespace ZoneForge.Data
{
    public static class MoodPresetRegistry
    {
        private static Dictionary<uint, MoodPreset> _byId;
        private static MoodPreset _fallback;

        public static MoodPreset Get(uint id)
        {
            EnsureLoaded();
            return _byId.TryGetValue(id, out var preset) ? preset : _fallback;
        }

        public static IReadOnlyCollection<MoodPreset> All
        {
            get { EnsureLoaded(); return _byId.Values; }
        }

        public static void Reload()
        {
            _byId = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_byId != null) return;
            _byId = new Dictionary<uint, MoodPreset>();
            var assets = Resources.LoadAll<MoodPreset>("MoodPresets");
            foreach (var asset in assets)
            {
                if (_byId.ContainsKey(asset.Id))
                {
                    Debug.LogWarning($"Duplicate MoodPreset id {asset.Id}: {asset.name}");
                    continue;
                }
                _byId[asset.Id] = asset;
                if (asset.Id == 0) _fallback = asset;
            }
            if (_fallback == null && assets.Length > 0) _fallback = assets[0];
            if (_fallback == null) Debug.LogError("No MoodPreset assets found in Resources/MoodPresets/");
        }
    }
}
