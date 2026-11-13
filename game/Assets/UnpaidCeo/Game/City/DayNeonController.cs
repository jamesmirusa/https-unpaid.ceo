using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnpaidCeo.Game.City
{
    public enum TimeOfDay { Day, Night, Evening }

    /// <summary>
    /// Day = tilt-shift diorama, Night = neon-noir. One blend value drives the house shader's emission
    /// (global _UnpaidNight), the sun, ambient light, the day/night post-processing volumes and the street lamps.
    /// </summary>
    public sealed class DayNeonController : MonoBehaviour
    {
        public const string NightProperty = "_UnpaidNight";
        static readonly int NightId = Shader.PropertyToID(NightProperty);

        [SerializeField] Light sun;
        [SerializeField] Volume dayVolume;
        [SerializeField] Volume nightVolume;
        [SerializeField] List<Light> lamps = new List<Light>();
        [SerializeField] Color daySunLate = new Color(1f, 0.9f, 0.76f), nightSun = new Color(0.45f, 0.5f, 0.9f);
        [SerializeField] float daySunLevel = 0.86f, nightSunIntensity = 0.18f, lampIntensity = 1.8f;
        [SerializeField] Color daySky = new Color(0.4f, 0.46f, 0.58f), nightSky = new Color(0.12f, 0.08f, 0.25f);
        [SerializeField] Color dayGround = new Color(0.22f, 0.2f, 0.18f), nightGround = new Color(0.06f, 0.05f, 0.1f);

        public float NightAmount { get; private set; }
        public Light Sun => sun;

        // Stage 5B: the evening stop between day and neon, and the look by quality level.
        [SerializeField] Color eveningSun = new Color(1f, 0.62f, 0.36f), eveningSky = new Color(0.42f, 0.32f, 0.34f);
        [SerializeField] float eveningSunIntensity = 0.75f;
        Volume _evening, _look;
        /// <summary>Split toning (warm light, cool shade), a light vignette and a touch of contrast; on in Optimal and Max.</summary>
        public Volume LookVolume => _look != null ? _look : _look = MakeVolume("volume_look", LookProfile(), 50f);
        Volume EveningVolume => _evening != null ? _evening : _evening = MakeVolume("volume_evening", EveningProfile(), 40f);

        Volume MakeVolume(string name, VolumeProfile profile, float priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = priority;
            v.sharedProfile = profile;
            return v;
        }

        static VolumeProfile LookProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "look_quality";
            var split = p.Add<UnityEngine.Rendering.Universal.SplitToning>(true);
            split.highlights.Override(new Color(1f, 0.78f, 0.55f));
            split.shadows.Override(new Color(0.42f, 0.55f, 0.85f));
            split.balance.Override(-12f);
            var vig = p.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vig.intensity.Override(0.18f);
            vig.smoothness.Override(0.5f);
            var c = p.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            c.contrast.Override(8f);
            c.postExposure.Override(-0.22f);   // the pale sidewalks no longer blow out at noon
            return p;
        }

        static VolumeProfile EveningProfile()
        {
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            p.name = "look_evening";
            var c = p.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            c.colorFilter.Override(new Color(1f, 0.86f, 0.74f));
            c.postExposure.Override(-0.15f);
            c.saturation.Override(10f);
            return p;
        }

        void OnDisable() { UnpaidCeo.Game.Graphics.GraphicsModeService.Changed -= OnMode; }
        bool _lookOn = true;
        /// <summary>The look fades out toward the neon night, which keeps its own grade.</summary>
        void ApplyLook() => LookVolume.weight = _lookOn ? 1f - NightAmount : 0f;
        void OnMode(UnpaidCeo.Core.Platform.GraphicsMode m) { _lookOn = UnpaidCeo.Game.Graphics.GraphicsModeService.PostProcessing(m); ApplyLook(); }

        public void Configure(Light s, Volume day, Volume night, List<Light> streetLamps)
        {
            sun = s;
            dayVolume = day;
            nightVolume = night;
            lamps = streetLamps;
        }

        void OnEnable()
        {
            UnpaidCeo.Game.Graphics.GraphicsModeService.Changed -= OnMode;
            UnpaidCeo.Game.Graphics.GraphicsModeService.Changed += OnMode;
            OnMode(UnpaidCeo.Game.Graphics.GraphicsModeService.Current);
            SetBlend(NightAmount);
        }

        public void Set(TimeOfDay t) => SetBlend(t == TimeOfDay.Night ? 1f : t == TimeOfDay.Evening ? 0.5f : 0f);

        /// <summary>Day → evening (0.5) → night: three colour stops instead of a straight grey blend.</summary>
        static Color Stops(Color day, Color evening, Color night, float n) =>
            n <= 0.5f ? Color.Lerp(day, evening, n * 2f) : Color.Lerp(evening, night, (n - 0.5f) * 2f);
        static float Stops(float day, float evening, float night, float n) =>
            n <= 0.5f ? Mathf.Lerp(day, evening, n * 2f) : Mathf.Lerp(evening, night, (n - 0.5f) * 2f);

        public void SetBlend(float night)
        {
            NightAmount = Mathf.Clamp01(night);
            Shader.SetGlobalFloat(NightId, NightAmount);
            if (sun != null)
            {
                sun.color = Stops(daySunLate, eveningSun, nightSun, NightAmount);
                sun.intensity = Stops(daySunLevel, eveningSunIntensity, nightSunIntensity, NightAmount);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Stops(daySky, eveningSky, nightSky, NightAmount);
            EveningVolume.weight = 1f - Mathf.Abs(NightAmount * 2f - 1f);
            ApplyLook();
            RenderSettings.ambientEquatorColor = Color.Lerp((daySky + dayGround) * 0.5f, (nightSky + nightGround) * 0.5f, NightAmount);
            RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, NightAmount);
            if (dayVolume != null) dayVolume.weight = 1f - NightAmount;
            if (nightVolume != null) nightVolume.weight = NightAmount;
            foreach (var l in lamps) if (l != null) l.intensity = lampIntensity * Mathf.SmoothStep(0f, 1f, (NightAmount - 0.3f) / 0.5f);
        }
    }
}
