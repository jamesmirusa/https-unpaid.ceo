#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// The interface's sounds: one soft family (tools/audio/ui_sounds.py), loaded from Resources/Audio/Sfx and played
    /// on one 2D source. <see cref="Sink"/> can be swapped (tests record what played). Muted by the settings switch.
    /// </summary>
    public static class UiSound
    {
        public const string Tap = "ui_tap", Pop = "ui_pop", Coin = "ui_coin", Success = "ui_success",
            Closed = "ui_closed", Whoosh = "ui_whoosh", Fanfare = "ui_fanfare";
        public static readonly string[] All = { Tap, Pop, Coin, Success, Closed, Whoosh, Fanfare };
        public const string MutePrefsKey = "unpaidceo.sound.muted";

        /// <summary>Where sounds go; null = the default player.</summary>
        public static Action<string>? Sink;
        public static float Volume = 0.7f;

        static AudioSource? _source;
        static readonly Dictionary<string, AudioClip?> Clips = new Dictionary<string, AudioClip?>();
        static float _lastTap;

        public static bool Muted
        {
            get => PlayerPrefs.GetInt(MutePrefsKey, 0) == 1;
            set { PlayerPrefs.SetInt(MutePrefsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static AudioClip? Clip(string kind)
        {
            if (!Clips.TryGetValue(kind, out var c)) Clips[kind] = c = Resources.Load<AudioClip>("Audio/Sfx/" + kind);
            return c;
        }

        public static void Play(string kind)
        {
            if (Sink != null) { Sink(kind); return; }
            if (Muted) return;
            // taps can come in bursts (a drag over buttons): keep them from stacking into a buzz
            if (kind == Tap) { if (Time.unscaledTime - _lastTap < 0.05f) return; _lastTap = Time.unscaledTime; }
            var clip = Clip(kind);
            if (clip == null) return;
            if (_source == null)
            {
                var go = new GameObject("UI Sound");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _source = go.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
                if (UnityEngine.Object.FindFirstObjectByType<AudioListener>() == null) go.AddComponent<AudioListener>();
            }
            _source.PlayOneShot(clip, Volume);
        }
    }
}
