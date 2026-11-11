using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Game.City;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The city is alive: the hero stands where the camera sees it, people walk the sidewalks, cars drive.</summary>
    public class StreetLifeTests
    {
        readonly List<GameObject> _made = new List<GameObject>();

        GameObject Make(string name)
        {
            var go = new GameObject(name);
            _made.Add(go);
            return go;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made) if (go != null) Object.Destroy(go);
            _made.Clear();
        }

        static bool OnSidewalk(Vector3 p) =>
            Mathf.Abs(Mathf.Abs(p.z) - StreetLife.SidewalkOffset) <= 1.6f || Mathf.Abs(Mathf.Abs(p.x) - StreetLife.SidewalkOffset) <= 1.6f;

        [Test]
        public void The_hero_stands_on_the_sidewalk_in_front_of_home()
        {
            var home = Make("home").transform;
            home.SetPositionAndRotation(new Vector3(11f, 0f, 13.5f), Quaternion.Euler(0f, 180f, 0f));   // the row faces the avenue
            var spot = StreetLife.HomeFront(home);
            Assert.AreEqual(11f, spot.x, 0.01f);
            Assert.That(spot.z, Is.InRange(5.2f, 7.8f), "on the sidewalk, not inside the building");
            Assert.That(spot.y, Is.InRange(0.1f, 0.3f), "on top of the sidewalk");
            Assert.GreaterOrEqual(HeroView.Height, 2.4f, "big enough to be seen from the city camera");
        }

        [Test]
        public void The_camera_frames_the_hero()
        {
            var rig = Make("cam").AddComponent<CameraRig>();
            rig.Frame(new Vector3(11f, 0f, 4f), 48f);
            Assert.AreEqual(new Vector3(11f, 0f, 4f), rig.Focus);
            Assert.AreEqual(48f, rig.Distance, 0.01f);
            var toFocus = (rig.Focus - rig.transform.position).normalized;
            Assert.Greater(Vector3.Dot(toFocus, rig.transform.forward), 0.999f, "the camera looks at the point");
            Assert.AreEqual(48f, Vector3.Distance(rig.Focus, rig.transform.position), 0.05f);

            rig.Frame(new Vector3(500f, 0f, 0f), 1f);
            Assert.LessOrEqual(rig.Focus.x, 60f, "framing stays inside the district");
            Assert.GreaterOrEqual(rig.Distance, 20f, "and never dives into the ground");
        }

        [UnityTest]
        public IEnumerator People_walk_the_sidewalks()
        {
            var life = Make("life").AddComponent<StreetLife>();
            var looks = new List<string>();
            life.Begin(seed: 7, count: 10, spawn: (address, parent) =>
            {
                looks.Add(address);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                body.transform.SetParent(parent, false);
                return body;
            });
            yield return null;

            Assert.AreEqual(10, life.Walkers.Count);
            Assert.GreaterOrEqual(looks.Distinct().Count(), 3, "a crowd, not ten copies of one character");
            foreach (var w in life.Walkers) Assert.IsTrue(OnSidewalk(w.transform.position), $"{w.name} starts off the sidewalk at {w.transform.position}");

            var before = life.Walkers.Select(w => w.transform.position).ToList();
            for (int i = 0; i < 40; i++) foreach (var w in life.Walkers) w.Step(0.1f);
            int moved = 0;
            for (int i = 0; i < before.Count; i++)
            {
                var p = life.Walkers[i].transform.position;
                if ((p - before[i]).magnitude > 0.5f) moved++;
                Assert.IsTrue(OnSidewalk(p), $"{life.Walkers[i].name} left the sidewalk at {p}");
            }
            Assert.GreaterOrEqual(moved, 8, "people actually walk");
        }

        [Test]
        public void A_walker_turns_round_at_the_end_of_its_lane()
        {
            var w = Make("walker").AddComponent<Pedestrian>();
            w.Configure(new Vector3(0f, 0.16f, 6.5f), new Vector3(4f, 0.16f, 6.5f), speed: 2f, restSeconds: 0f, startAt: 0.5f);
            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 100; i++)
            {
                w.Step(0.1f);
                min = Mathf.Min(min, w.transform.position.x);
                max = Mathf.Max(max, w.transform.position.x);
            }
            Assert.AreEqual(0f, min, 0.05f, "walks to one end");
            Assert.AreEqual(4f, max, 0.05f, "and back to the other");
        }

        [Test]
        public void A_street_car_drives_along_the_avenue_and_comes_back_round()
        {
            var car = Make("taxi").AddComponent<StreetCar>();
            car.transform.SetPositionAndRotation(new Vector3(60f, 0.02f, -2.4f), Quaternion.Euler(0f, 90f, 0f));   // heading +x
            car.Configure(speed: 10f, halfLength: 68f);
            car.Step(0.5f);
            Assert.AreEqual(65f, car.transform.position.x, 0.01f, "drives forward");
            car.Step(0.5f);
            Assert.Less(car.transform.position.x, -60f, "past the edge of the district it re-enters on the other side");
            Assert.AreEqual(-2.4f, car.transform.position.z, 0.01f, "and keeps its lane");
        }
    }
}
