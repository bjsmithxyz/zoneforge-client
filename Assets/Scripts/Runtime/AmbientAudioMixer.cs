using UnityEngine;
using UnityEngine.Audio;
using ZoneForge.Data;

namespace ZoneForge.Runtime
{
    public class AmbientAudioMixer : MonoBehaviour
    {
        [SerializeField] private AudioMixer _mixer;
        [SerializeField] private AudioMixerGroup _baseGroup;
        [SerializeField] private AudioMixerGroup _weatherGroup;
        [SerializeField] private AudioMixerGroup _timeGroup;

        [Header("Weather clips (index by WeatherKind ordinal)")]
        [SerializeField] private AudioClip _rainClip;
        [SerializeField] private AudioClip _stormClip;
        [SerializeField] private AudioClip _fogClip;
        [SerializeField] private AudioClip _snowClip;

        [Header("Time clips")]
        [SerializeField] private AudioClip _dayClip;
        [SerializeField] private AudioClip _nightClip;

        private AudioSource _baseSource, _weatherSource, _timeSource;
        private const float FadeSeconds = 1.5f;

        void Awake()
        {
            _baseSource = NewLoopingSource(_baseGroup);
            _weatherSource = NewLoopingSource(_weatherGroup);
            _timeSource = NewLoopingSource(_timeGroup);
        }

        private AudioSource NewLoopingSource(AudioMixerGroup group)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.loop = true;
            src.playOnAwake = false;
            src.outputAudioMixerGroup = group;
            return src;
        }

        public void ApplyMood(MoodPreset preset)
        {
            Crossfade(_baseSource, preset != null ? preset.BaseAmbientClip : null);
        }

        public void ApplyWeather(int weatherKindOrdinal)
        {
            AudioClip clip = weatherKindOrdinal switch
            {
                1 => _rainClip,
                2 => _stormClip,
                3 => _fogClip,
                4 => _snowClip,
                _ => null,
            };
            Crossfade(_weatherSource, clip);
        }

        public void ApplyTimeOfDay(int minutesOfDay)
        {
            bool isDay = minutesOfDay >= 360 && minutesOfDay < 1080;
            Crossfade(_timeSource, isDay ? _dayClip : _nightClip);
        }

        private void Crossfade(AudioSource src, AudioClip nextClip)
        {
            if (src.clip == nextClip) return;
            if (nextClip == null)
            {
                src.Stop();
                src.clip = null;
                return;
            }
            src.clip = nextClip;
            src.Play();
        }
    }
}
