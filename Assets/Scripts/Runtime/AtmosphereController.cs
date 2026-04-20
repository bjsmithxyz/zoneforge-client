using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using SpacetimeDB.Types;
using ZoneForge.Data;

namespace ZoneForge.Runtime
{
    public class AtmosphereController : MonoBehaviour
    {
        [SerializeField] private Light _sun;
        [SerializeField] private Volume _postFxVolume;
        [SerializeField] private AmbientAudioMixer _audioMixer;
        [SerializeField] private Transform _weatherVfxParent;

        public ulong CurrentZoneId { get; private set; }

        private MoodPreset _currentPreset;
        private int _cachedMinutes = 480;
        private GameObject _currentWeatherVfx;
        private WeatherKind _currentWeather = WeatherKind.Clear;
        private Dictionary<WeatherKind, GameObject> _vfxPrefabs;

        void Awake()
        {
            _vfxPrefabs = new Dictionary<WeatherKind, GameObject>
            {
                [WeatherKind.Rain] = Resources.Load<GameObject>("WeatherVFX/WeatherVFX_Rain"),
                [WeatherKind.Fog] = Resources.Load<GameObject>("WeatherVFX/WeatherVFX_Fog"),
                [WeatherKind.Storm] = Resources.Load<GameObject>("WeatherVFX/WeatherVFX_Storm"),
                [WeatherKind.Snow] = Resources.Load<GameObject>("WeatherVFX/WeatherVFX_Snow"),
            };
        }

        public void SetZone(ulong zoneId, uint moodPresetId)
        {
            CurrentZoneId = zoneId;
            _currentPreset = MoodPresetRegistry.Get(moodPresetId);
            if (_audioMixer != null) _audioMixer.ApplyMood(_currentPreset);
        }

        public void OnWorldClockChanged(ushort minutesOfDay)
        {
            _cachedMinutes = minutesOfDay;
            if (_audioMixer != null) _audioMixer.ApplyTimeOfDay(minutesOfDay);
        }

        public void OnWeatherChanged(WeatherKind kind, float intensity)
        {
            if (kind == _currentWeather) return;
            _currentWeather = kind;

            if (_currentWeatherVfx != null)
            {
                Destroy(_currentWeatherVfx);
                _currentWeatherVfx = null;
            }
            if (kind != WeatherKind.Clear && _vfxPrefabs.TryGetValue(kind, out var prefab) && prefab != null)
            {
                _currentWeatherVfx = Instantiate(prefab, _weatherVfxParent != null ? _weatherVfxParent : transform);
            }
            if (_audioMixer != null) _audioMixer.ApplyWeather((int)kind);
        }

        void Update()
        {
            if (_currentPreset == null || _sun == null) return;
            float t = _cachedMinutes;
            float normalized = _cachedMinutes / 1440f;

            _sun.transform.localRotation = Quaternion.Euler(
                _currentPreset.SunPitchCurve.Evaluate(t),
                _currentPreset.SunYawCurve.Evaluate(t),
                0f);

            RenderSettings.ambientLight = _currentPreset.AmbientColorGradient.Evaluate(normalized);
            RenderSettings.fogColor = _currentPreset.FogColorGradient.Evaluate(normalized);
            RenderSettings.fogDensity = _currentPreset.FogDensityCurve.Evaluate(t);

            if (_postFxVolume != null && _currentPreset.PostFxProfile != null)
            {
                _postFxVolume.profile = _currentPreset.PostFxProfile;
            }
        }
    }
}
