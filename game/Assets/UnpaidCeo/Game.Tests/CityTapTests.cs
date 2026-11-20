using NUnit.Framework;
using UnityEngine;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Owner, 2026-11-19: "a building on the map can't be tapped to go in" — what counts as a tap, and whose building it is.</summary>
    public class CityTapTests
    {
        [Test]
        public void A_short_still_press_is_a_tap_and_a_drag_or_a_long_press_is_not()
        {
            var tap = new TapDetector(maxMove: 12f, maxSeconds: 0.35f);
            tap.Press(new Vector2(100, 100), 1.00f);
            Assert.IsTrue(tap.Release(new Vector2(105, 103), 1.20f), "a light touch that barely moves is a tap");

            tap.Press(new Vector2(100, 100), 2.00f);
            Assert.IsFalse(tap.Release(new Vector2(160, 100), 2.10f), "a drag pans the camera, it is not a tap");

            tap.Press(new Vector2(100, 100), 3.00f);
            Assert.IsFalse(tap.Release(new Vector2(101, 100), 3.80f), "a long press is not a tap");

            Assert.IsFalse(tap.Release(new Vector2(100, 100), 4.00f), "a release without a press is nothing");
        }

        [Test]
        public void A_drag_that_comes_back_to_where_it_started_is_still_a_drag()
        {
            var tap = new TapDetector(maxMove: 12f, maxSeconds: 0.35f);
            tap.Press(new Vector2(100, 100), 1.00f);
            tap.Move(new Vector2(150, 100));
            Assert.IsFalse(tap.Release(new Vector2(101, 100), 1.20f));
        }

        [Test]
        public void A_tap_on_any_part_of_a_building_finds_its_place_and_home_is_the_house_by_the_home_spot()
        {
            var district = new GameObject("district");
            var anchor = new GameObject("HomeAnchor");
            try
            {
                var bodega = Child(district, "building_bodega_0", new Vector3(10, 0, 8));
                var facade = Child(bodega, "facade_LOD0", new Vector3(10, 2, 4));
                var home = Child(district, "building_brownstone_row_0", new Vector3(-6, 0, 8));
                var neighbour = Child(district, "building_brownstone_row_1", new Vector3(-20, 0, 8));
                anchor.transform.position = new Vector3(-6, 0, 3);

                var map = Places.FindBuildings(district.GetComponentsInChildren<Transform>(true), anchor.transform);
                Assert.AreEqual("bodega", Places.PlaceOf(facade.transform, map), "a wall of the bodega is the bodega");
                Assert.AreEqual("bodega", Places.PlaceOf(bodega.transform, map));
                Assert.AreEqual(Places.Home, Places.PlaceOf(home.transform, map), "the house by the home spot is home");
                Assert.IsNull(Places.PlaceOf(neighbour.transform, map), "a neighbour's house is not a place");
                Assert.IsNull(Places.PlaceOf(null, map));
            }
            finally
            {
                Object.DestroyImmediate(district);
                Object.DestroyImmediate(anchor);
            }
        }

        static GameObject Child(GameObject parent, string name, Vector3 at)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform);
            go.transform.position = at;
            return go;
        }
    }
}
