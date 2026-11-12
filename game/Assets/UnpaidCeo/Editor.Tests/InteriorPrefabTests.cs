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
