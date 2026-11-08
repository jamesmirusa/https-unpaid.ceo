using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnpaidCeo.Core.Format;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// The month in 5–8 seconds: the sun crosses day → night → day while the month's revenue floats up
    /// above the block in a few pops. Runs on unscaled time so pausing the game does not stall it.
    /// </summary>
    public sealed class TimelapseDirector : MonoBehaviour
    {
        public const float MinSeconds = 5f, MaxSeconds = 8f;

        [SerializeField] DayNeonController dayNeon;
        [SerializeField] CityView city;
        [SerializeField] Transform sun;
        [SerializeField] int pops = 4;

        public bool IsPlaying { get; private set; }
        public int FloatingTextsShown { get; private set; }
        public string Language { get; set; } = "en";
        public event Action Finished;

        public void Configure(DayNeonController dn, CityView c, Transform sunTransform)
        {
            dayNeon = dn;
            city = c;
            sun = sunTransform;
        }

        public void Play(MonthReport report, float seconds = 6f)
        {
            StopAllCoroutines();
            StartCoroutine(Run(report, Mathf.Clamp(seconds, MinSeconds, MaxSeconds)));
        }

        IEnumerator Run(MonthReport report, float seconds)
        {
            IsPlaying = true;
            FloatingTextsShown = 0;
            float startNight = dayNeon != null ? dayNeon.NightAmount : 0f;
            var startRotation = sun != null ? sun.rotation : Quaternion.identity;
            var anchors = city != null ? city.MoneyAnchors().ToList() : new List<Vector3> { transform.position };
            if (anchors.Count == 0) anchors.Add(transform.position);
            long perPop = report != null && pops > 0 ? report.Revenue.Cents / pops : 0;
            int popped = 0;

            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                float k = t / seconds;
                if (dayNeon != null) dayNeon.SetBlend(Mathf.Sin(k * Mathf.PI));
                if (sun != null) sun.rotation = Quaternion.AngleAxis(k * 360f, Vector3.up) * startRotation;
                while (perPop > 0 && popped < pops && k >= (popped + 0.5f) / pops)
                {
                    var at = anchors[popped % anchors.Count] + Vector3.up * 2f;
                    FloatingText.Spawn(at, "+" + MoneyFormat.Compact(Money.FromCents(perPop), Language), new Color(0.24f, 0.86f, 0.52f));
                    popped++;
                    FloatingTextsShown++;
                }
                yield return null;
            }

            if (dayNeon != null) dayNeon.SetBlend(startNight);
            if (sun != null) sun.rotation = startRotation;
            IsPlaying = false;
            Finished?.Invoke();
        }
    }

    /// <summary>A world-space number that rises, faces the camera and fades out.</summary>
    public sealed class FloatingText : MonoBehaviour
    {
        const float Life = 1.6f;
        TextMesh _text;
        float _age;
        Color _color;

        public static FloatingText Spawn(Vector3 position, string text, Color color)
        {
            var go = new GameObject("floating-text");
            go.transform.position = position;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.fontSize = 64;
            tm.characterSize = 0.12f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.fontStyle = FontStyle.Bold;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = tm.font.material;
            var f = go.AddComponent<FloatingText>();
            f._text = tm;
            f._color = color;
            return f;
        }

        void Update()
        {
            _age += Time.unscaledDeltaTime;
            transform.position += Vector3.up * (Time.unscaledDeltaTime * 2.5f);
            var cam = Camera.main;
            if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
            _text.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(1f - (_age - Life * 0.6f) / (Life * 0.4f)));
            if (_age >= Life) Destroy(gameObject);
        }
    }
}
