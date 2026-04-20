using UnityEngine;
using UnityEngine.Rendering;

namespace ZoneForge.Data
{
    [CreateAssetMenu(menuName = "ZoneForge/Mood Preset", fileName = "MoodPreset")]
    public class MoodPreset : ScriptableObject
    {
        [Tooltip("Must match server Zone.mood_preset_id. 0 is the fallback.")]
        public uint Id;

        public string DisplayName;

        [Header("Sun (curves indexed by minutes-of-day 0..1440)")]
        public AnimationCurve SunPitchCurve = AnimationCurve.Linear(0, -30, 1440, 330);
        public AnimationCurve SunYawCurve = AnimationCurve.Linear(0, 0, 1440, 0);

        [Header("Ambient & Fog")]
        public Gradient AmbientColorGradient;
        public Gradient FogColorGradient;
        public AnimationCurve FogDensityCurve = AnimationCurve.Constant(0, 1440, 0.01f);

        [Header("Post-FX")]
        public VolumeProfile PostFxProfile;

        [Header("Audio")]
        public AudioClip BaseAmbientClip;
    }
}
