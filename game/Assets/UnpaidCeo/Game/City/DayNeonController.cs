using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnpaidCeo.Game.City
{
    public enum TimeOfDay { Day, Night }

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
        [SerializeField] Color daySun = new Color(1f, 0.95f, 0.86f), nightSun = new Color(0.45f, 0.5f, 0.9f);
        [SerializeField] float daySunIntensity = 1.0f, nightSunIntensity = 0.18f, lampIntensity = 1.8f;
        [SerializeField] Color daySky = new Color(0.4f, 0.46f, 0.58f), nightSky = new Color(0.12f, 0.08f, 0.25f);
        [SerializeField] Color dayGround = new Color(0.22f, 0.2f, 0.18f), nightGround = new Color(0.06f, 0.05f, 0.1f);

        public float NightAmount { get; private set; }

        public void Configure(Light s, Volume day, Volume night, List<Light> streetLamps)
        {
            sun = s;
            dayVolume = day;
            nightVolume = night;
            lamps = streetLamps;
        }

        void OnEnable() => SetBlend(NightAmount);

        public void Set(TimeOfDay t) => SetBlend(t == TimeOfDay.Night ? 1f : 0f);

        public void SetBlend(float night)
        {
            NightAmount = Mathf.Clamp01(night);
            Shader.SetGlobalFloat(NightId, NightAmount);
            if (sun != null)
            {
                sun.color = Color.Lerp(daySun, nightSun, NightAmount);
                sun.intensity = Mathf.Lerp(daySunIntensity, nightSunIntensity, NightAmount);
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(daySky, nightSky, NightAmount);
            RenderSettings.ambientEquatorColor = Color.Lerp((daySky + dayGround) * 0.5f, (nightSky + nightGround) * 0.5f, NightAmount);
            RenderSettings.ambientGroundColor = Color.Lerp(dayGround, nightGround, NightAmount);
            if (dayVolume != null) dayVolume.weight = 1f - NightAmount;
            if (nightVolume != null) nightVolume.weight = NightAmount;
            foreach (var l in lamps) if (l != null) l.intensity = lampIntensity * Mathf.SmoothStep(0f, 1f, (NightAmount - 0.3f) / 0.5f);
        }
    }
}
