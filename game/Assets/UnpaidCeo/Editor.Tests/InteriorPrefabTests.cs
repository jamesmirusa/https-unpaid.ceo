using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>The built rooms: the two people stand on clear floor (not in a bench or a counter) where the camera sees them.</summary>
    public class InteriorPrefabTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        static IEnumerable<string> Ids => Places.All.Select(p => p.Id);

        [TestCaseSource(nameof(Ids))]
        public void The_room_has_a_talk_spot_on_clear_floor_that_the_camera_sees(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InteriorBuilder.Out}/{Interiors.RoomAddress(id)}.prefab");
            Assert.IsNotNull(prefab, id + " has no room");
            var room = (GameObject)Object.Instantiate(prefab);
            _made.Add(room);
            var spot = room.transform.Find(Interiors.TalkSpot);
            Assert.IsNotNull(spot, id + ": the builder marks where the two meet");
            Assert.AreEqual(0f, spot.position.y, 1e-3f, "on the floor");
            foreach (var mf in room.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
            Physics.SyncTransforms();

            var layout = InteriorLayout.At(spot.position);
            foreach (var at in new[] { layout.HeroSpot, layout.HostSpot })
            {
                Assert.IsTrue(InteriorBuilder.ClearFloor(at), $"{id}: something stands where a person should ({at})");
                Assert.IsTrue(InteriorBuilder.InView(layout.CameraPosition, at), $"{id}: furniture hides a person from the camera ({at})");
            }
        }

        /// <summary>
        /// A crack in a generated floor shows the board under it: in the floor's own colour it reads as a seam, in a fixed
        /// dark brown it reads as a hole beside the people (the bar's pale planks).
        /// </summary>
        [TestCaseSource(nameof(Ids))]
        public void A_crack_in_the_floor_shows_the_floors_own_colour(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InteriorBuilder.Out}/{Interiors.RoomAddress(id)}.prefab");
            Assert.IsNotNull(prefab, id + " has no room");
            var backing = prefab.transform.Find("floor_backing");
            if (backing == null) Assert.Ignore(id + " is not a generated room");
            var board = backing.GetComponent<Renderer>().sharedMaterial.GetColor("_BaseColor");

            var room = (GameObject)Object.Instantiate(prefab);
            _made.Add(room);
            var floor = Color.black;
            var spot = room.transform.Find(Interiors.TalkSpot).position;
            var shell = room.GetComponentsInChildren<MeshFilter>().First(mf => mf.name != "floor_backing" && mf.sharedMesh != null);
            shell.gameObject.AddComponent<MeshCollider>().sharedMesh = shell.sharedMesh;
            Physics.SyncTransforms();
            Assert.IsTrue(Physics.Raycast(spot + Vector3.up * 3f, Vector3.down, out var hit, 6f), id + ": floor under the talk spot");
            var tex = shell.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap");
            var rt = RenderTexture.GetTemporary(256, 256, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _made.Add(read);
            floor = read.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);

            float Lum(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            Assert.Greater(Lum(board), 0.45f * Lum(floor), $"{id}: board {board} under a floor of {floor}");
            Assert.Less(Lum(board), 1.1f * Lum(floor) + 0.02f, $"{id}: board {board} under a floor of {floor}");
        }

        /// <summary>
        /// Image-to-3D flattened the bank's black and cream marble checkerboard into blank white, an empty stage under the
        /// two; the builder's floor painter puts the concept's checkerboard back.
        /// </summary>
        [Test]
        public void The_banks_floor_is_black_and_cream_marble()
        {
            var (lum, _) = SampleFloor("bank");
            float dark = lum.Count(l => l < 0.25f) / (float)lum.Count, light = lum.Count(l => l > 0.6f) / (float)lum.Count;
            Assert.That(dark, Is.InRange(0.3f, 0.7f), "share of black marble");
            Assert.That(light, Is.InRange(0.3f, 0.7f), "share of cream marble");
        }

        /// <summary>
        /// The academy's parquet came back as one flat orange (brightness ± 0.005); its planks show again, each in its own
        /// tone of oak. The floor has about 30 texels a metre, so the planks' tones carry the pattern more than their seams.
        /// </summary>
        [Test]
        public void The_academys_floor_is_parquet()
        {
            var (lum, _) = SampleFloor("academy");
            float mean = lum.Average();
            float spread = Mathf.Sqrt(lum.Sum(l => (l - mean) * (l - mean)) / lum.Count);
            Assert.Greater(spread, 0.025f, "planks in their own tones");
        }

        /// <summary>
        /// The base colour under 3 m lines across the floor around the talk spot (both directions, 1.5 cm a step): each
        /// sample's brightness and each step's change in it. Furniture tops are skipped.
        /// </summary>
        (List<float> lum, List<float> steps) SampleFloor(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InteriorBuilder.Out}/{Interiors.RoomAddress(id)}.prefab");
            Assert.IsNotNull(prefab, id + " has no room");
            var room = (GameObject)Object.Instantiate(prefab);
            _made.Add(room);
            var spot = room.transform.Find(Interiors.TalkSpot).position;
            var shell = room.GetComponentsInChildren<MeshFilter>().First(mf => mf.name != "floor_backing" && mf.sharedMesh != null);
            shell.gameObject.AddComponent<MeshCollider>().sharedMesh = shell.sharedMesh;
            Physics.SyncTransforms();
            var read = ReadBack(shell.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), 2048);
            var lum = new List<float>();
            var steps = new List<float>();
            foreach (var dir in new[] { Vector3.right, Vector3.forward })
                foreach (float offset in new[] { -0.6f, 0f, 0.6f })
                {
                    float? last = null;
                    var across = Vector3.Cross(Vector3.up, dir) * offset;
                    for (float s = -1.5f; s <= 1.5f; s += 0.015f)
                    {
                        if (!Physics.Raycast(spot + across + dir * s + Vector3.up * 3f, Vector3.down, out var hit, 3.4f) || hit.point.y > 0.15f)
                        {
                            last = null;
                            continue;
                        }
                        var c = read.GetPixelBilinear(hit.textureCoord.x, hit.textureCoord.y);
                        float l = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                        lum.Add(l);
                        if (last != null) steps.Add(Mathf.Abs(l - last.Value));
                        last = l;
                    }
                }
            Assert.Greater(lum.Count, 600, id + ": floor around the talk spot");
            return (lum, steps);
        }

        Texture2D ReadBack(Texture tex, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var read = new Texture2D(size, size, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            _made.Add(read);
            return read;
        }

        /// <summary>
        /// A generated room is one textured shell: the house shader's warm rim (meant for silhouettes on the street) and its
        /// three light bands turn its pale floors and walls into banded, chrome-looking streaks seen from inside.
        /// </summary>
        [TestCaseSource(nameof(Ids))]
        public void The_rooms_surfaces_are_lit_softly_with_no_rim(string id)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{InteriorBuilder.Out}/{Interiors.RoomAddress(id)}.prefab");
            Assert.IsNotNull(prefab, id + " has no room");
            var shell = prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.HasProperty("_RimColor") && m.GetTexture("_BaseMap") != null)
                .ToList();
            Assert.IsNotEmpty(shell, id + ": the room's textured materials");
            foreach (var m in shell)
            {
                var rim = m.GetColor("_RimColor");
                Assert.LessOrEqual(Mathf.Max(rim.r, rim.g, rim.b), 0.05f, $"{id}: {m.name} rim");
                Assert.GreaterOrEqual(m.GetFloat("_Bands"), GlbPrefabBuilder.InteriorBands, $"{id}: {m.name} bands");
            }
        }
    }
}
