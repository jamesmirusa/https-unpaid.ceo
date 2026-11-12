using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// The rooms behind the places' signs. A generated room (Art/Imported/Buildings/room_&lt;id&gt;.glb, an isometric
    /// two-wall cutaway, imported to Art/Prefabs/room_&lt;id&gt; by ArtImportTool) is turned so its two walls stand on the
    /// far sides (-X and +Z, the camera looks in from the open corner), scaled to <see cref="Interiors.Footprint"/>,
    /// centred, and lifted so the top of its floor is y = 0 (home falls back to the furniture props). Each room becomes the
    /// Addressable "interior_&lt;id&gt;" (its own bundle in the "interiors" group), and a preview with two stand-ins at the
    /// conversation spots is rendered to tools/art/stage4/interiors/preview_&lt;id&gt;.jpg.
    ///   tools/unity-run.sh UnpaidCeo.Editor.InteriorBuilder.BuildAll
    /// </summary>
    public static class InteriorBuilder
    {
        public const string Out = "Assets/UnpaidCeo/Art/Prefabs/Interiors";
        public const string Group = "interiors";
        const string Prefabs = "Assets/UnpaidCeo/Art/Prefabs";
        const string Materials = "Assets/UnpaidCeo/Art/Materials";
        const string PreviewDir = "../tools/art/stage4/interiors";

        /// <summary>Turn after the automatic wall detection, for a room whose walls it reads wrong (degrees).</summary>
        static readonly Dictionary<string, float> ExtraYaw = new Dictionary<string, float>();

        [MenuItem("Unpaid CEO/Art/Build interiors")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Out);
            Directory.CreateDirectory(PreviewDir);
            var report = new List<string>();
            foreach (var place in Places.All)
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = new GameObject(Interiors.RoomAddress(place.Id));
                bool ok = BuildGenerated(place.Id, root.transform, report) || (place.Id == Places.Home && BuildHome(root.transform));
                if (!ok) { Object.DestroyImmediate(root); continue; }
                PlaceTalkSpot(place.Id, root.transform, report);
                var path = $"{Out}/{root.name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                MakeAddressable(path, root.name);
                Preview(place.Id, root);
                Object.DestroyImmediate(root);
                report.Add($"{place.Id}: ok");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[interiors] " + string.Join("; ", report));
        }

        static bool BuildGenerated(string id, Transform root, List<string> report)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/room_{id}.prefab");
            if (src == null) { report.Add($"{id}: no room_{id} prefab"); return false; }
            var room = (GameObject)PrefabUtility.InstantiatePrefab(src, root);
            room.transform.localPosition = Vector3.zero;
            room.transform.localRotation = Quaternion.identity;
            foreach (var c in room.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

            // The two walls are where the tall part of the mesh is: turn that corner to (-X, +Z).
            var points = Vertices(room);
            var b = BoundsOf(points);
            float wallLine = b.min.y + 0.55f * b.size.y;
            var tall = points.Where(p => p.y > wallLine).ToList();
            var corner = tall.Count == 0 ? new Vector3(-1f, 0f, 1f) : new Vector3(tall.Average(p => p.x) - b.center.x, 0f, tall.Average(p => p.z) - b.center.z);
            float yaw = Vector3.SignedAngle(corner, new Vector3(-1f, 0f, 1f), Vector3.up);
            if (ExtraYaw.TryGetValue(id, out var extra)) yaw += extra;
            room.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            ModelFit.ToFootprint(room, Interiors.Footprint);
            var fitted = ModelFit.RendererBounds(room);
            room.transform.position -= new Vector3(fitted.center.x, 0f, fitted.center.z);

            // The top of the floor where people stand: a ray down onto the open middle (a rug or tiles count as floor).
            fitted = ModelFit.RendererBounds(room);
            var layout = InteriorLayout.For(new Bounds(new Vector3(0f, fitted.center.y, 0f), fitted.size));
            var colliders = new List<MeshCollider>();
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders.Add(mc); }
            Physics.SyncTransforms();
            var floors = new List<float>();
            foreach (var at in new[] { layout.HeroSpot, layout.HostSpot, (layout.HeroSpot + layout.HostSpot) * 0.5f })
                if (Physics.Raycast(new Vector3(at.x, fitted.max.y + 5f, at.z), Vector3.down, out var hit, fitted.size.y + 20f))
                    floors.Add(hit.point.y);
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            float floor = floors.Count > 0 ? floors.Min() : fitted.min.y;
            room.transform.position -= new Vector3(0f, floor, 0f);
            report.Add($"{id}: yaw {yaw:0} floor {floor:0.00} size {fitted.size.x:0.0}x{fitted.size.y:0.0}x{fitted.size.z:0.0}");
            return true;
        }

        /// <summary>
        /// Marks where the two meet (<see cref="Interiors.TalkSpot"/>): the nearest point to the middle of the floor where
        /// both stand on clear floor (not in a bench or behind a counter) and nothing hides them from the camera.
        /// </summary>
        static void PlaceTalkSpot(string id, Transform root, List<string> report)
        {
            var colliders = new List<MeshCollider>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders.Add(mc); }
            Physics.SyncTransforms();
            var preferred = InteriorLayout.DefaultMiddle(ModelFit.RendererBounds(root.gameObject));
            Vector3? best = null;
            float bestScore = float.MaxValue;
            for (float t = -2.5f; t <= 3.5f; t += 0.25f)
                for (float side = -2.5f; side <= 2.5f; side += 0.25f)
                {
                    var middle = preferred + InteriorLayout.Toward * t + InteriorLayout.Side * side;
                    float score = t * t + 1.5f * side * side;   // stay central across the view rather than in depth
                    if (score >= bestScore) continue;
                    var l = InteriorLayout.At(middle);
                    if (ClearFloor(l.HeroSpot) && ClearFloor(l.HostSpot) && InView(l.CameraPosition, l.HeroSpot) && InView(l.CameraPosition, l.HostSpot))
                    {
                        best = middle;
                        bestScore = score;
                    }
                }
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            var spot = new GameObject(Interiors.TalkSpot).transform;
            spot.SetParent(root, false);
            spot.localPosition = best ?? preferred;
            report.Add(best == null ? $"{id}: NO clear talk spot, using the middle" : $"{id}: talk spot {spot.localPosition.x:0.0},{spot.localPosition.z:0.0}");
        }

        /// <summary>Clear floor for a person: rays down within their reach land on the floor, not on furniture.</summary>
        public static bool ClearFloor(Vector3 at)
        {
            const float Reach = 0.65f;   // a 2.6 m character is about a metre across with its arms
            for (int i = 0; i <= 8; i++)
            {
                var p = at + (i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * Reach);
                if (!Physics.Raycast(new Vector3(p.x, at.y + 20f, p.z), Vector3.down, out var hit, 40f)) return false;   // off the floor
                if (hit.point.y > at.y + 0.2f || hit.point.y < at.y - 0.3f) return false;
            }
            return true;
        }

        /// <summary>Nothing between the camera and a person standing there (knees, chest and head).</summary>
        public static bool InView(Vector3 camera, Vector3 at)
        {
            var across = Vector3.Cross(Vector3.up, (at - camera).normalized).normalized * 0.55f;
            foreach (var h in new[] { 0.45f, 1.0f, 1.6f, 2.3f })   // feet may hide behind a box, not knees behind a bench
                foreach (var side in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
                {
                    var d = at + Vector3.up * h + across * side - camera;
                    if (Physics.Raycast(camera, d.normalized, d.magnitude - 0.6f)) return false;
                }
            return true;
        }

        /// <summary>Fallback for the hero's room while it has no generated room: the furniture props at the rooms' scale.</summary>
        static bool BuildHome(Transform root)
        {
            float w = Interiors.Footprint, d = Interiors.Footprint * 0.84f, h = 4.6f;
            var floor = Mat("interior_home_floor", new Color(0.66f, 0.46f, 0.3f));
            var wall = Mat("interior_home_wall", new Color(0.86f, 0.8f, 0.7f));
            var trim = Mat("interior_home_trim", new Color(0.36f, 0.27f, 0.2f));
            Block("floor", root, new Vector3(0f, -0.15f, 0f), new Vector3(w, 0.3f, d), floor);
            Block("base", root, new Vector3(0f, -0.65f, 0f), new Vector3(w + 0.2f, 0.7f, d + 0.2f), trim);
            Block("wall_back", root, new Vector3(0f, h / 2f, d / 2f + 0.1f), new Vector3(w, h, 0.2f), wall);
            Block("wall_left", root, new Vector3(-w / 2f - 0.1f, h / 2f, 0f), new Vector3(0.2f, h, d), wall);
            Place("prop_bed", root, new Vector3(-3.0f, 0f, 2.3f), 90f, 3.2f);
            Place("prop_desk", root, new Vector3(2.6f, 0f, 3.5f), 180f, 2.1f);
            Place("prop_sofa", root, new Vector3(-0.2f, 0f, 3.4f), 180f, 2.6f);
            Place("prop_kitchenette", root, new Vector3(-4.2f, 0f, -1.7f), 90f, 2.4f);
            Place("prop_shoe_rack", root, new Vector3(-4.4f, 0f, -3.5f), 90f, 1.2f);
            var ac = Place("prop_window_ac", root, new Vector3(1.0f, 0f, 4.1f), 180f, 1.1f);
            ac.transform.position += Vector3.up * 2.8f;
            return true;
        }

        static GameObject Place(string prefab, Transform parent, Vector3 position, float yaw, float footprint)
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{prefab}.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(src, parent);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ModelFit.ToFootprint(go, footprint);
            var p = go.transform.position;
            ModelFit.Ground(go);
            go.transform.position = new Vector3(p.x, go.transform.position.y, p.z);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return go;
        }

        static void Block(string name, Transform parent, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(Materials);
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(AssetBudgetValidator.StylizedShaderName)); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static List<Vector3> Vertices(GameObject go)
        {
            var list = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                foreach (var v in mf.sharedMesh.vertices) list.Add(m.MultiplyPoint3x4(v));
            }
            return list;
        }

        static Bounds BoundsOf(List<Vector3> points)
        {
            if (points.Count == 0) return new Bounds();
            var b = new Bounds(points[0], Vector3.zero);
            foreach (var p in points) b.Encapsulate(p);
            return b;
        }

        static void MakeAddressable(string assetPath, string address)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(Group) ?? settings.CreateGroup(Group, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            // One bundle per room: the web build downloads a room when the player first walks into it.
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        /// <summary>The room through the conversation camera, with two 2.6 m stand-ins where the hero and the host stand.</summary>
        static void Preview(string id, GameObject room)
        {
            var b = ModelFit.RendererBounds(room);
            var talk = room.transform.Find(Interiors.TalkSpot);
            var layout = talk != null ? InteriorLayout.At(talk.position) : InteriorLayout.For(b);
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var lamp = new GameObject("lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 16f;
            lamp.intensity = 0.6f;
            lamp.color = new Color(1f, 0.86f, 0.68f);
            lamp.transform.position = layout.HostSpot + Vector3.up * 5f + (layout.CameraPosition - layout.HostSpot).normalized * 2f;
            var people = new List<GameObject>();
            foreach (var (spot, color) in new[] { (layout.HeroSpot, new Color(0.9f, 0.5f, 0.2f)), (layout.HostSpot, new Color(0.3f, 0.5f, 0.9f)) })
            {
                var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                c.transform.localScale = new Vector3(1f, UnpaidCeo.Game.Hero.HeroView.Height / 2f, 1f);
                c.transform.position = spot + Vector3.up * UnpaidCeo.Game.Hero.HeroView.Height / 2f;
                var m = new Material(Shader.Find(AssetBudgetValidator.StylizedShaderName));
                m.SetColor("_BaseColor", color);
                c.GetComponent<Renderer>().sharedMaterial = m;
                people.Add(c);
            }
            var camGo = new GameObject("cam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = layout.FieldOfView;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.11f, 0.12f, 0.2f);
            camGo.AddComponent<UniversalAdditionalCameraData>();
            foreach (var (name, w, h) in new[] { ("phone", 540, 960), ("wide", 1280, 720) })
            {
                cam.transform.position = layout.CameraPosition;
                cam.transform.LookAt(layout.LookAt);
                var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.aspect = w / (float)h;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                for (int i = 0; i < 2; i++)
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes($"{PreviewDir}/preview_{id}_{name}.jpg", tex.EncodeToJPG(85));
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }
            // A view of the whole room from higher up, to check the walls and the floor.
            cam.transform.position = new Vector3(14f, 13f, -14f);
            cam.transform.LookAt(new Vector3(0f, 1f, 0f));
            cam.aspect = 1f;
            var rt2 = new RenderTexture(800, 800, 24) { antiAliasing = 4 };
            cam.targetTexture = rt2;
            var req2 = new RenderPipeline.StandardRequest { destination = rt2 };
            for (int i = 0; i < 2; i++)
                if (RenderPipeline.SupportsRenderRequest(cam, req2)) RenderPipeline.SubmitRenderRequest(cam, req2); else cam.Render();
            RenderTexture.active = rt2;
            var t2 = new Texture2D(800, 800, TextureFormat.RGB24, false);
            t2.ReadPixels(new Rect(0, 0, 800, 800), 0, 0);
            t2.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"{PreviewDir}/preview_{id}_room.jpg", t2.EncodeToJPG(85));
            cam.targetTexture = null;
            Object.DestroyImmediate(t2);
            Object.DestroyImmediate(rt2);
            foreach (var p in people) Object.DestroyImmediate(p);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(sun.gameObject);
            Object.DestroyImmediate(lamp.gameObject);
        }
    }
}
