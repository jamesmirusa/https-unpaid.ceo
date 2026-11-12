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
    }
}
