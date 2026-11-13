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
            MakeContactShadow();
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

            // The two walls: square them to the axes and turn their corner to (-X, +Z).
            var (points, triangles) = Geometry(room);
            float yaw = SeenFromTheOpenCorner(room, WallYaw(points, triangles));
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
            int cut = CutSlab(id, room, SlabKept);
            // A board just under the floor in the floor's own (slightly shadowed) colour: a crack in a generated floor reads
            // as a seam between planks, not as a hole onto the stage behind.
            var back = ModelFit.RendererBounds(room);
            var board = FloorColour(room) * BoardShade;
            board.a = 1f;
            Block("floor_backing", root, new Vector3(back.center.x, -0.12f, back.center.z),
                new Vector3(Mathf.Max(0.1f, back.size.x - 1f), 0.02f, Mathf.Max(0.1f, back.size.z - 1f)), Mat($"interior_floor_backing_{id}", board));
            report.Add($"{id}: yaw {yaw:0} floor {floor:0.00} size {fitted.size.x:0.0}x{fitted.size.y:0.0}x{fitted.size.z:0.0} slab -{cut} tris");
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
            // Strict: both whole people in view; relaxed (a crowded room): their upper bodies, a box may hide the knees.
            Vector3? best = null;
            string tier = "";
            int floorOk = 0;
            var probe = new GameObject("framing_probe").AddComponent<Camera>();
            foreach (var (name, lowest, framed) in new[] { ("", 0.45f, true), (" (floor edge shows)", 0.45f, false), (" (knees may hide)", 1.2f, true), (" (knees may hide, floor edge shows)", 1.2f, false) })
            {
                float bestScore = float.MaxValue;
                for (float t = -3f; t <= 2.5f; t += 0.25f)
                    for (float side = -2.5f; side <= 2.5f; side += 0.25f)
                    {
                        var middle = preferred + InteriorLayout.Toward * t + InteriorLayout.Side * side;
                        // Stay central across the view; forward (toward the camera) brings the floor's front edge into the frame.
                        float score = (t > 0f ? 3f : 1f) * t * t + 1.5f * side * side;
                        if (score >= bestScore) continue;
                        var l = InteriorLayout.At(middle);
                        if (!ClearFloor(l.HeroSpot) || !ClearFloor(l.HostSpot)) continue;
                        floorOk++;
                        if (InView(l.CameraPosition, l.HeroSpot, lowest) && InView(l.CameraPosition, l.HostSpot, lowest) && (!framed || FloorReachesThePanel(probe, l)))
                        {
                            best = middle;
                            bestScore = score;
                        }
                    }
                if (best != null) { tier = name; break; }
            }
            Object.DestroyImmediate(probe.gameObject);
            var spot = new GameObject(Interiors.TalkSpot).transform;
            spot.SetParent(root, false);
            spot.localPosition = best ?? preferred;
            // The floor under each person: generated floors are not flat, so each stands on what is really under them.
            var layout = InteriorLayout.At(spot.localPosition);
            foreach (var (marker, at) in new[] { (Interiors.HeroFloor, layout.HeroSpot), (Interiors.HostFloor, layout.HostSpot) })
            {
                var m = new GameObject(marker).transform;
                m.SetParent(spot, true);
                m.position = new Vector3(at.x, FloorUnder(at), at.z);
            }
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            report.Add(best == null ? $"{id}: NO clear talk spot ({floorOk} on clear floor), using the middle" : $"{id}: talk spot {spot.localPosition.x:0.0},{spot.localPosition.z:0.0}{tier}");
        }

        /// <summary>The top of the conversation panel on a phone (it covers the bottom quarter of the screen).</summary>
        public const float PanelTop = 0.25f;

        /// <summary>
        /// On a phone (430x900 to 1080x1920) the floor reaches down to the conversation panel: along its top edge the camera
        /// sees floor or furniture, not the room's slab or the empty background under it.
        /// </summary>
        static bool FloorReachesThePanel(Camera probe, InteriorLayout l)
        {
            probe.transform.position = l.CameraPosition;
            probe.transform.LookAt(l.LookAt);
            probe.fieldOfView = l.FieldOfView;
            foreach (var aspect in new[] { 430f / 900f, 1080f / 1920f })
            {
                probe.aspect = aspect;
                foreach (var x in new[] { 0.15f, 0.5f, 0.85f })   // the corners may look past the room, like a dollhouse
                {
                    var ray = probe.ViewportPointToRay(new Vector3(x, PanelTop, 0f));
                    if (!Physics.Raycast(ray, out var hit, 200f)) return false;
                    // Floor (facing up) or furniture standing on it; the slab's side faces the camera at floor level or below.
                    if (hit.normal.y < 0.5f && hit.point.y < 0.3f) return false;
                }
            }
            return true;
        }

        /// <summary>The floor height under a person's feet: the median of short rays down around the spot.</summary>
        static float FloorUnder(Vector3 at)
        {
            var hits = new List<float>();
            for (int i = 0; i <= 8; i++)
            {
                var p = at + (i == 0 ? Vector3.zero : Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 0.3f);
                if (Physics.Raycast(new Vector3(p.x, at.y + 3f, p.z), Vector3.down, out var hit, 6f)) hits.Add(hit.point.y);
            }
            if (hits.Count == 0) return at.y;
            hits.Sort();
            return hits[hits.Count / 2];
        }

        public const string ContactShadowPath = "Assets/UnpaidCeo/Resources/" + InteriorStage.ContactShadowMaterial + ".mat";

        /// <summary>The soft ellipse under people's feet: a radial alpha texture on a transparent unlit material.</summary>
        static void MakeContactShadow()
        {
            var dir = Path.GetDirectoryName(ContactShadowPath)!;
            Directory.CreateDirectory(dir);
            var texPath = Path.Combine(dir, "contact_shadow.png").Replace('\\', '/');
            if (!File.Exists(texPath))
            {
                const int N = 128;
                var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01(1f - d);
                        a = a * a * (3f - 2f * a);   // smooth falloff, darkest under the feet
                        tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                File.WriteAllBytes(texPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(texPath);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ContactShadowPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(mat, ContactShadowPath);
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            mat.SetColor("_BaseColor", new Color(0.05f, 0.04f, 0.06f, 0.6f));
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
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
        public static bool InView(Vector3 camera, Vector3 at) => InView(camera, at, 0.45f);

        /// <param name="lowest">The lowest height checked: below it (feet) low clutter may hide them.</param>
        public static bool InView(Vector3 camera, Vector3 at, float lowest)
        {
            var across = Vector3.Cross(Vector3.up, (at - camera).normalized).normalized * 0.55f;
            foreach (var h in new[] { 0.45f, 1.0f, 1.6f, 2.3f })   // feet may hide behind a box, not knees behind a bench
            {
                if (h < lowest) continue;
                foreach (var side in new[] { -1f, -0.5f, 0f, 0.5f, 1f })
                {
                    var d = at + Vector3.up * h + across * side - camera;
                    if (Physics.Raycast(camera, d.normalized, d.magnitude - 0.6f)) return false;
                }
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

        /// <summary>The board under a floor is a little darker than the floor: a crack is a shadowed seam.</summary>
        const float BoardShade = 0.85f;
        static readonly Color NoFloorColour = new Color(0.13f, 0.1f, 0.09f);

        /// <summary>
        /// The colour of a generated room's floor around its middle: the base map under a 3×3 grid of rays (furniture tops
        /// skipped), the median by brightness so a rug or a dark seam does not decide it.
        /// </summary>
        public static Color FloorColour(GameObject room)
        {
            var colliders = new List<MeshCollider>();
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders.Add(mc); }
            Physics.SyncTransforms();
            var b = ModelFit.RendererBounds(room);
            var middle = InteriorLayout.DefaultMiddle(b);
            var samples = new List<Color>();
            var readable = new Dictionary<Texture, Texture2D>();
            try
            {
                for (int i = -1; i <= 1; i++)
                    for (int j = -1; j <= 1; j++)
                    {
                        var at = middle + InteriorLayout.Side * (i * 1.2f) + InteriorLayout.Toward * (j * 1.2f);
                        if (!Physics.Raycast(new Vector3(at.x, b.max.y + 1f, at.z), Vector3.down, out var hit, b.size.y + 5f)) continue;
                        if (hit.point.y > 0.3f) continue;   // a table or a bed, not the floor (the builder levels the floor to 0)
                        var renderer = hit.collider.GetComponent<Renderer>();
                        var material = renderer != null ? renderer.sharedMaterial : null;
                        var tex = material != null ? material.GetTexture("_BaseMap") : null;
                        if (tex == null) continue;
                        if (!readable.TryGetValue(tex, out var read)) readable[tex] = read = Readable(tex, 256);
                        samples.Add(read.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y));
                    }
            }
            finally
            {
                foreach (var mc in colliders) Object.DestroyImmediate(mc);
                foreach (var t in readable.Values) Object.DestroyImmediate(t);
            }
            if (samples.Count == 0) return NoFloorColour;
            samples.Sort((x, y) => x.grayscale.CompareTo(y.grayscale));
            return samples[samples.Count / 2];
        }

        /// <summary>A small readable sRGB copy of any texture (imported textures are not readable).</summary>
        static Texture2D Readable(Texture source, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return read;
        }

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(Materials);
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(AssetBudgetValidator.StylizedShaderName)); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            GlbPrefabBuilder.LightSoftly(m);   // flat colours inside a room take the same light as its textured shell
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// The turn (degrees about Y) that squares a room's walls to the axes and stands them on the far sides, -X and +Z.
        /// The walls' direction comes from the horizontal normals of the upper part of the mesh (angles folded to 90°, so
        /// both walls agree); of the four square turns, the one with most wall at -X and +Z wins.
        /// </summary>
        public static float WallYaw(IList<Vector3> v, IList<int> t)
        {
            var b = BoundsOf(v as List<Vector3> ?? v.ToList());
            float wallLine = b.min.y + 0.45f * b.size.y;
            double c4 = 0, s4 = 0;
            var tall = new List<Vector3>();
            for (int i = 0; i + 2 < t.Count; i += 3)
            {
                Vector3 a = v[t[i]], p = v[t[i + 1]], q = v[t[i + 2]];
                if ((a.y + p.y + q.y) / 3f < wallLine) continue;
                var n = Vector3.Cross(p - a, q - a);
                float area = n.magnitude * 0.5f;
                if (area < 1e-8f) continue;
                n /= area * 2f;
                if (Mathf.Abs(n.y) > 0.35f) continue;
                double ang = Mathf.Atan2(n.z, n.x) * 4.0;
                c4 += area * System.Math.Cos(ang);
                s4 += area * System.Math.Sin(ang);
            }
            foreach (var p in v) if (p.y > wallLine) tall.Add(p);
            float square = (float)(System.Math.Atan2(s4, c4) / 4.0 * Mathf.Rad2Deg);
            float best = square, bestScore = float.MinValue;
            for (int k = 0; k < 4; k++)
            {
                float yaw = square + 90f * k;
                var r = Quaternion.Euler(0f, yaw, 0f);
                var rb = BoundsOf(v.Select(x => r * x).ToList());
                float score = 0f;
                foreach (var p in tall)
                {
                    var x = r * p;
                    float u = (x.x - rb.center.x) / Mathf.Max(rb.extents.x, 1e-4f), w = (x.z - rb.center.z) / Mathf.Max(rb.extents.z, 1e-4f);
                    if (u < -0.7f) score++;
                    if (w > 0.7f) score++;
                    if (u > 0.7f) score--;
                    if (w < -0.7f) score--;
                }
                if (score > bestScore) { bestScore = score; best = yaw; }
            }
            best = Mathf.Repeat(best + 180f, 360f) - 180f;
            return best;
        }

        /// <summary>How much of the diorama's base stays under the floor: a floor's thickness, not a pedestal.</summary>
        public const float SlabKept = 0.25f;
        /// <summary>A face whose highest point is this far under the floor belongs to the pedestal, not the room.</summary>
        public const float PedestalTop = 0.08f;

        /// <summary>
        /// Generated rooms stand on a thick pedestal (the diorama's base). Everything more than <paramref name="kept"/> below
        /// the floor is flattened to that depth (faces wholly under it are dropped): under the floor's front edge the camera
        /// sees the dark stage behind the room, not a white block.
        /// The room keeps its own mesh asset (Art/Meshes/interior_&lt;id&gt;_n), LOD0 only: the conversation camera is fixed and close.
        /// </summary>
        static int CutSlab(string id, GameObject room, float kept)
        {
            int removed = 0, n = 0;
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
            {
                var src = mf.sharedMesh;
                if (src == null) continue;
                var toRoot = mf.transform.localToWorldMatrix;   // the wrapper root stands at the origin
                var v = src.vertices;
                var heights = new float[v.Length];
                for (int i = 0; i < v.Length; i++) heights[i] = toRoot.MultiplyPoint3x4(v[i]).y;
                var map = new int[v.Length];
                for (int i = 0; i < map.Length; i++) map[i] = -1;
                var keep = new List<int>();
                var subs = new List<List<int>>();
                for (int sm = 0; sm < src.subMeshCount; sm++)
                {
                    var idx = src.lodCount > 1 ? src.GetIndices(sm, 0) : src.GetIndices(sm);
                    var list = new List<int>();
                    for (int i = 0; i + 2 < idx.Length; i += 3)
                    {
                        int a = idx[i], b = idx[i + 1], c = idx[i + 2];
                        // Wholly under the floor (the pedestal's top around an inset floor, its foot): dropped.
                        if (Mathf.Max(heights[a], heights[b], heights[c]) < -PedestalTop) { removed++; continue; }
                        foreach (var k in new[] { a, b, c })
                        {
                            if (map[k] < 0) { map[k] = keep.Count; keep.Add(k); }
                            list.Add(map[k]);
                        }
                    }
                    subs.Add(list);
                }
                var mesh = new Mesh { name = $"interior_{id}_{n}", indexFormat = keep.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
                // The pedestal's sides run from the floor's edge down to its foot: their lower vertices come up to the cut,
                // so what remains under the floor's edge is a rim as thick as a floor.
                var toLocal = toRoot.inverse;
                mesh.SetVertices(keep.Select(k =>
                {
                    if (heights[k] >= -kept) return v[k];
                    var w = toRoot.MultiplyPoint3x4(v[k]);
                    w.y = -kept;
                    return toLocal.MultiplyPoint3x4(w);
                }).ToList());
                var normals = src.normals; if (normals.Length == v.Length) mesh.SetNormals(keep.Select(k => normals[k]).ToList());
                var tangents = src.tangents; if (tangents.Length == v.Length) mesh.SetTangents(keep.Select(k => tangents[k]).ToList());
                var uv = src.uv; if (uv.Length == v.Length) mesh.SetUVs(0, keep.Select(k => uv[k]).ToList());
                var colors = src.colors; if (colors.Length == v.Length) mesh.SetColors(keep.Select(k => colors[k]).ToList());
                mesh.subMeshCount = subs.Count;
                for (int sm = 0; sm < subs.Count; sm++) mesh.SetTriangles(subs[sm], sm);
                mesh.RecalculateBounds();
                var path = $"{MeshesDir}/interior_{id}_{n++}.asset";
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (old != null) { EditorUtility.CopySerialized(mesh, old); Object.DestroyImmediate(mesh); mesh = old; }
                else AssetDatabase.CreateAsset(mesh, path);
                mf.sharedMesh = mesh;
            }
            return removed;
        }

        const string MeshesDir = "Assets/UnpaidCeo/Art/Meshes";

        /// <summary>
        /// Of the four square turns around <paramref name="yaw"/>, the one where the conversation camera (from +X, -Z, above)
        /// sees most of the floor: a generated room may carry an extra wall or tall shelving that fools the wall count.
        /// </summary>
        static float SeenFromTheOpenCorner(GameObject room, float yaw)
        {
            var colliders = new List<MeshCollider>();
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; colliders.Add(mc); }
            float best = yaw;
            int bestSeen = -1;
            for (int k = 0; k < 4; k++)
            {
                float candidate = Mathf.Repeat(yaw + 90f * k + 180f, 360f) - 180f;
                room.transform.localRotation = Quaternion.Euler(0f, candidate, 0f);
                Physics.SyncTransforms();
                var b = ModelFit.RendererBounds(room);
                var eye = b.center + new Vector3(1f, 0.8f, -1f).normalized * b.extents.magnitude * 2.5f;
                int seen = 0;
                for (int i = -2; i <= 2; i++)
                    for (int j = -2; j <= 2; j++)
                    {
                        var target = new Vector3(b.center.x + i * 0.15f * b.size.x, b.min.y + 0.4f * b.size.y, b.center.z + j * 0.15f * b.size.z);
                        var d = target - eye;
                        // Seen: nothing in the way short of the target (the room is still in its generated units here).
                        if (!Physics.Raycast(eye, d.normalized, out var hit, d.magnitude * 2f) || hit.distance > d.magnitude - 0.12f * b.size.magnitude) seen++;
                    }
                if (seen > bestSeen) { bestSeen = seen; best = candidate; }
            }
            foreach (var mc in colliders) Object.DestroyImmediate(mc);
            room.transform.localRotation = Quaternion.identity;
            return best;
        }

        static (List<Vector3>, List<int>) Geometry(GameObject go)
        {
            var points = new List<Vector3>();
            var tris = new List<int>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var m = mf.transform.localToWorldMatrix;
                int o = points.Count;
                foreach (var p in mf.sharedMesh.vertices) points.Add(m.MultiplyPoint3x4(p));
                var mesh = mf.sharedMesh;
                var idx = mesh.lodCount > 1 ? mesh.GetIndices(0, 0) : mesh.triangles;   // LOD0 only
                foreach (var i in idx) tris.Add(o + i);
            }
            return (points, tris);
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
            sun.intensity = 1.25f;
            sun.color = new Color(1f, 0.95f, 0.88f);
            sun.shadowStrength = 0.75f;
            var view = layout.LookAt - layout.CameraPosition;   // the same key light as InteriorStage
            sun.transform.rotation = Quaternion.Euler(52f, Mathf.Atan2(view.x, view.z) * Mathf.Rad2Deg + 35f, 0f);
            var lamp = new GameObject("lamp").AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.range = 16f;
            lamp.intensity = 0.5f;
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
