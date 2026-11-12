#nullable enable
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// The 3D rooms behind the places' signs: an isometric cutaway room per place (Addressable "interior_&lt;id&gt;",
    /// built by Editor/InteriorBuilder with its floor at y = 0 and its two walls on the far sides, -X and +Z) and the
    /// character model of each host.
    /// </summary>
    public static class Interiors
    {
        /// <summary>Rooms are built far below the street, so neither the city nor its lights show inside.</summary>
        public static readonly Vector3 Origin = new Vector3(0f, -400f, 0f);
        /// <summary>A room's larger side: the street's characters (2.6 m, big heads) fit a ~6 m real shop at this size.</summary>
        public const float Footprint = 10f;

        public static string RoomAddress(string placeId) => "interior_" + placeId;

        /// <summary>A room's child marking where the hero and the host meet: clear floor the camera sees (found by the builder).</summary>
        public const string TalkSpot = "talk_spot";

        /// <summary>The host's character prefab, or null while it has no model yet (the conversation shows its portrait).</summary>
        public static string HostModel(string npcId) => npcId switch
        {
            "mentor" => "npc_mentor_raccoon",
            "tony" => "npc_tony_bulldog",
            "best_friend" => "npc_jay_otter",
            "loan_shark" => "npc_mr_fins_shark",
            "fat_cat" => "npc_whiskerton_cat",
            "banker_lee" => "npc_ms_lee_owl",
            "cofounder_sam" => "npc_sam_beaver",
            _ => null,
        };
    }

    /// <summary>
    /// Where the hero and the host stand and where the camera looks from, in the room's own space (floor at y = 0).
    /// They face each other across the open middle of the floor, turned a little toward the camera, which looks in from
    /// the open corner (+X, -Z) and keeps both in the upper part of a phone screen, above the conversation panel.
    /// </summary>
    public readonly struct InteriorLayout
    {
        public readonly Vector3 HeroSpot, HostSpot, CameraPosition, LookAt;
        public readonly Quaternion HeroFacing, HostFacing;
        public readonly float FieldOfView;

        InteriorLayout(Vector3 hero, Vector3 host, Quaternion heroFacing, Quaternion hostFacing, Vector3 cam, Vector3 look, float fov)
        {
            HeroSpot = hero; HostSpot = host; HeroFacing = heroFacing; HostFacing = hostFacing;
            CameraPosition = cam; LookAt = look; FieldOfView = fov;
        }

        /// <summary>Screen right as seen from the open corner, and the way toward the camera.</summary>
        public static readonly Vector3 Side = new Vector3(1f, 0f, 1f).normalized, Toward = new Vector3(1f, 0f, -1f).normalized;
        const float HalfGap = 1.2f, TurnToCamera = 0.55f;
        /// <summary>A low camera, like a customer's eye: the back corner's wall tops stay in the frame above the two.</summary>
        const float CameraBack = 16f, CameraUp = 4f, LookHeight = 0.5f, Fov = 28f;

        /// <summary>The middle of the floor, nudged toward the open corner: furniture lines the two walls.</summary>
        public static Vector3 DefaultMiddle(Bounds room) =>
            new Vector3(room.center.x, 0f, room.center.z) + Toward * (0.15f * Mathf.Min(room.size.x, room.size.z));

        public static InteriorLayout For(Bounds room) => At(DefaultMiddle(room));

        /// <summary>The two meet at <paramref name="middle"/> (on the floor, y = 0) and the camera frames them there.</summary>
        public static InteriorLayout At(Vector3 middle)
        {
            middle.y = 0f;
            var hero = middle - Side * HalfGap;
            var host = middle + Side * HalfGap;
            var heroFacing = Quaternion.LookRotation((Side + Toward * TurnToCamera).normalized, Vector3.up);
            var hostFacing = Quaternion.LookRotation((-Side + Toward * TurnToCamera).normalized, Vector3.up);
            var cam = middle + Toward * CameraBack + Vector3.up * CameraUp;
            return new InteriorLayout(hero, host, heroFacing, hostFacing, cam, middle + Vector3.up * LookHeight, Fov);
        }
    }

    /// <summary>
    /// Shows one place's room with the hero and the host in it, far from the street, through its own camera (the city's
    /// rests meanwhile). It owns what it is given and destroys it on <see cref="Clear"/>.
    /// </summary>
    public sealed class InteriorStage : MonoBehaviour
    {
        Camera? _camera, _city;
        Light? _lamp;
        Volume? _look;
        GameObject? _room, _hero, _host;
        Transform? _heroSpot, _hostSpot;

        public Camera Camera => _camera != null ? _camera : _camera = MakeCamera();
        public GameObject? Hero => _hero;
        public GameObject? Host => _host;
        public bool Showing => _room != null;
        public InteriorLayout Layout { get; private set; }

        /// <summary>
        /// The room's look over the district's volumes while inside: no tilt-shift blur (it is tuned for the street camera
        /// 40 m up and smears a room seen from 18 m), softer bloom and a touch less exposure for the generated textures,
        /// which already carry their own light.
        /// </summary>
        Volume Look => _look != null ? _look : _look = MakeLook();

        Volume MakeLook()
        {
            var go = new GameObject("interior_look");
            go.transform.SetParent(transform, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.priority = 100f;
            v.weight = 1f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "interior_look";
            profile.Add<DepthOfField>(true).mode.Override(DepthOfFieldMode.Off);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.08f);
            bloom.threshold.Override(1.6f);
            profile.Add<ColorAdjustments>(true).postExposure.Override(-0.2f);
            v.sharedProfile = profile;
            v.enabled = false;
            return v;
        }

        Camera MakeCamera()
        {
            var go = new GameObject("InteriorCamera");
            go.transform.SetParent(transform, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.12f, 0.2f);   // the navy of the UI chrome around a lit dollhouse
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 80f;
            go.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            cam.enabled = false;
            return cam;
        }

        /// <param name="room">The room (its floor at y = 0); owned from now on.</param>
        /// <param name="hero">The hero's model, or null; owned from now on.</param>
        /// <param name="host">The host's model, or null (no model yet); owned from now on.</param>
        /// <param name="cityCamera">Disabled while inside, enabled again by <see cref="Clear"/>.</param>
        public void Show(GameObject room, GameObject? hero, GameObject? host, Camera? cityCamera)
        {
            Clear();
            transform.position = Interiors.Origin;
            _room = room;
            room.transform.SetParent(transform, false);
            room.transform.localPosition = Vector3.zero;

            var talk = room.transform.Find(Interiors.TalkSpot);
            if (talk != null) Layout = InteriorLayout.At(transform.InverseTransformPoint(talk.position));
            else
            {
                var local = ModelFit.RendererBounds(room);
                local.center -= transform.position;
                Layout = InteriorLayout.For(local);
            }

            _heroSpot = Spot("hero_spot", Layout.HeroSpot, Layout.HeroFacing);
            _hostSpot = Spot("host_spot", Layout.HostSpot, Layout.HostFacing);
            _hero = Stand(hero, _heroSpot);
            _host = Stand(host, _hostSpot);

            if (_lamp == null)
            {
                _lamp = new GameObject("interior_lamp").AddComponent<Light>();
                _lamp.transform.SetParent(transform, false);
                _lamp.type = LightType.Point;
                _lamp.range = 16f;
                _lamp.intensity = 0.6f;   // a warm fill on the faces; the district's sun still lights the room
                _lamp.color = new Color(1f, 0.86f, 0.68f);
            }
            _lamp.transform.localPosition = Layout.HostSpot + Vector3.up * 5f + (Layout.CameraPosition - Layout.HostSpot).normalized * 2f;
            _lamp.enabled = true;
            Look.enabled = true;

            var cam = Camera;
            cam.fieldOfView = Layout.FieldOfView;
            cam.transform.localPosition = Layout.CameraPosition;
            cam.transform.LookAt(transform.TransformPoint(Layout.LookAt));
            _city = cityCamera;
            if (_city != null) _city.enabled = false;
            cam.enabled = true;
        }

        Transform Spot(string name, Vector3 at, Quaternion facing)
        {
            var t = new GameObject(name).transform;
            t.SetParent(transform, false);
            t.localPosition = at;
            t.localRotation = facing;
            return t;
        }

        static GameObject? Stand(GameObject? model, Transform spot)
        {
            if (model == null) return null;
            model.transform.SetParent(spot, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            ModelFit.ToHeight(model, HeroView.Height);
            return model;
        }

        /// <summary>The host's clip for a moment of the talk: cheer for a deal, think while the player chooses.</summary>
        public void HostReacts(CharacterAnimator.State state) => _host?.GetComponentInChildren<CharacterAnimator>()?.PlayOnce(state);
        public void HeroReacts(CharacterAnimator.State state) => _hero?.GetComponentInChildren<CharacterAnimator>()?.PlayOnce(state);

        public void Clear()
        {
            foreach (var go in new[] { _room, _hero, _host }) if (go != null) Destroy(go);
            foreach (var t in new[] { _heroSpot, _hostSpot }) if (t != null) Destroy(t.gameObject);
            _room = _hero = _host = null;
            if (_lamp != null) _lamp.enabled = false;
            if (_look != null) _look.enabled = false;
            if (_camera != null) _camera.enabled = false;
            if (_city != null) _city.enabled = true;
            _city = null;
        }

        void OnDestroy()
        {
            if (_city != null) _city.enabled = true;
            if (_look != null && _look.sharedProfile != null) Destroy(_look.sharedProfile);
        }
    }
}
