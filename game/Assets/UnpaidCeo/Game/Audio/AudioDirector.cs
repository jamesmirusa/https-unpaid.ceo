#nullable enable
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnpaidCeo.Game.City;

namespace UnpaidCeo.Game.Audio
{
    /// <summary>
    /// One place for game audio. Both music loops run all the time and an equal-power crossfade follows the
    /// day/neon light, so switching never restarts a track. The day layer is the district's own theme
    /// (<see cref="AudioAddresses.DistrictMusic"/>); the neon layer is shared. A voice line ducks the music while it plays.
    /// Timing is driven by <see cref="Tick"/> (unscaled time), so pausing the game does not freeze the mix.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const float CrossfadeSeconds = 2.5f;
        public const float VoiceDuck = 0.35f;
        const float DuckSeconds = 0.25f;

        public float MusicVolume = 0.55f;
        public float VoiceVolume = 1f;

        AudioSource? _day, _night, _voice, _sfx;
        AudioClip? _pendingDay;
        AsyncOperationHandle<AudioClip> _dayHandle;
        string _dayAddress = "";
        Func<float> _nightAmount = () => 0f;
        float _mix, _duck = 1f, _voiceLeft;
        bool _musicStarted;

        public float DayVolume => _day != null ? _day.volume : 0f;
        public float NightVolume => _night != null ? _night.volume : 0f;
        public bool MusicPlaying => _musicStarted && _day != null && _night != null && _day.loop && _night.loop;
        public bool VoicePlaying => _voiceLeft > 0f;
        public string? DayTheme => _day != null && _day.clip != null ? _day.clip.name : null;
        public string? NightTheme => _night != null && _night.clip != null ? _night.clip.name : null;

        /// <summary>Puts a district theme on the day layer; the neon loop and the mix keep running. A null clip is ignored.</summary>
        public void SetDayTheme(AudioClip? clip)
        {
            if (clip == null) return;
            EnsureSources();
            if (!_musicStarted) { _pendingDay = clip; return; }
            if (_day!.clip == clip) return;
            _day.clip = clip;
            _day.Play();
            Apply();
        }

        /// <summary>Streams the day theme at <paramref name="address"/> and swaps it in, releasing the previous one.</summary>
        public IEnumerator LoadDayTheme(string address)
        {
            if (address == _dayAddress) yield break;
            _dayAddress = address;
            var h = Addressables.LoadAssetAsync<AudioClip>(address);
            yield return h;
            if (h.Status != AsyncOperationStatus.Succeeded || address != _dayAddress)
            {
                if (h.Status != AsyncOperationStatus.Succeeded) Debug.LogWarning(address + " failed to load; the current theme keeps playing.");
                if (h.IsValid()) Addressables.Release(h);
                yield break;
            }
            SetDayTheme(h.Result);
            if (_dayHandle.IsValid()) Addressables.Release(_dayHandle);
            _dayHandle = h;
        }

        void Awake() => EnsureSources();

        void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>Starts both loops; <paramref name="nightAmount"/> is polled every frame (0 = day, 1 = neon night).</summary>
        public void Configure(AudioClip day, AudioClip night, Func<float>? nightAmount)
        {
            EnsureSources();
            _nightAmount = nightAmount ?? (() => 0f);
            _mix = Mathf.Clamp01(_nightAmount());
            _day!.clip = _pendingDay != null ? _pendingDay : day;
            _pendingDay = null;
            _night!.clip = night;
            if (FindFirstObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            _day.Play();
            _night.Play();
            _musicStarted = true;
            Apply();
        }

        /// <summary>Loads both themes through Addressables and follows the city's light.</summary>
        public IEnumerator LoadMusic(DayNeonController? light) => LoadMusic(() => light != null ? light.NightAmount : 0f);

        /// <summary>Loads the day theme at <paramref name="dayAddress"/> and the neon theme, and follows <paramref name="nightAmount"/> (0 day … 1 neon), which may switch districts.</summary>
        public IEnumerator LoadMusic(Func<float> nightAmount, string dayAddress = AudioAddresses.MusicDay)
        {
            _dayAddress = dayAddress;
            var day = Addressables.LoadAssetAsync<AudioClip>(dayAddress);
            var night = Addressables.LoadAssetAsync<AudioClip>(AudioAddresses.MusicNeon);
            yield return day;
            yield return night;
            if (day.Status == AsyncOperationStatus.Succeeded && night.Status == AsyncOperationStatus.Succeeded)
            {
                _dayHandle = day;
                Configure(day.Result, night.Result, nightAmount);
            }
            else
                Debug.LogWarning("Music themes failed to load; the game continues without music.");
        }

        public void Tick(float dt)
        {
            _mix = Mathf.MoveTowards(_mix, Mathf.Clamp01(_nightAmount()), dt / CrossfadeSeconds);
            _voiceLeft = Mathf.Max(0f, _voiceLeft - dt);
            _duck = Mathf.MoveTowards(_duck, VoicePlaying ? VoiceDuck : 1f, dt / DuckSeconds * (1f - VoiceDuck));
            Apply();
        }

        /// <summary>Plays a voice line, replacing any line still playing. A null clip is ignored.</summary>
        public void PlayVoice(AudioClip? clip)
        {
            EnsureSources();
            StopVoice();
            if (clip == null) return;
            _voice!.clip = clip;
            _voice.volume = VoiceVolume;
            _voice.Play();
            _voiceLeft = clip.length;
        }

        public void StopVoice()
        {
            if (_voice != null) _voice.Stop();
            _voiceLeft = 0f;
        }

        public void PlaySfx(AudioClip? clip, float volume = 1f)
        {
            EnsureSources();
            if (clip != null) _sfx!.PlayOneShot(clip, volume);
        }

        void Apply()
        {
            if (_day == null || _night == null) return;
            var angle = _mix * Mathf.PI * 0.5f;
            _day.volume = MusicVolume * _duck * Mathf.Cos(angle);
            _night.volume = MusicVolume * _duck * Mathf.Sin(angle);
        }

        void EnsureSources()
        {
            if (_day != null) return;
            _day = Source("Music Day", true);
            _night = Source("Music Neon", true);
            _voice = Source("Voice", false);
            _sfx = Source("Sfx", false);
        }

        AudioSource Source(string name, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }
    }
}
