using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>
    /// A generated room comes in any orientation (its walls even at an angle to the model's axes); the builder turns it so
    /// its two walls stand on the far sides, -X and +Z, square to the axes.
    /// </summary>
    public class InteriorOrientationTests
    {
        static void Box(List<Vector3> v, List<int> t, Vector3 min, Vector3 max)
        {
            int o = v.Count;
            for (int i = 0; i < 8; i++) v.Add(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
            int[] q = { 0,2,3,1, 4,5,7,6, 0,1,5,4, 2,6,7,3, 0,4,6,2, 1,3,7,5 };
            for (int f = 0; f < 6; f++)
            {
                int a = o + q[f * 4], b = o + q[f * 4 + 1], c = o + q[f * 4 + 2], d = o + q[f * 4 + 3];
                t.AddRange(new[] { a, b, c, a, c, d });
            }
        }

        /// <summary>A 10 m room: floor slab, a tall wall along -X and a long one along +Z, a counter in the middle.</summary>
        static (List<Vector3>, List<int>) Room()
        {
            var v = new List<Vector3>(); var t = new List<int>();
            Box(v, t, new Vector3(-5f, -1f, -5f), new Vector3(5f, 0f, 5f));
            Box(v, t, new Vector3(-5f, 0f, -5f), new Vector3(-4.7f, 5f, 5f));
            Box(v, t, new Vector3(-5f, 0f, 4.7f), new Vector3(5f, 5f, 5f));
            Box(v, t, new Vector3(-1f, 0f, 1f), new Vector3(1.5f, 1.2f, 2f));
            return (v, t);
        }

        [TestCase(0f)] [TestCase(30f)] [TestCase(45f)] [TestCase(90f)] [TestCase(137f)] [TestCase(-100f)] [TestCase(200f)]
        public void Any_turn_of_the_room_is_undone_to_walls_at_minus_x_and_plus_z(float turned)
        {
            var (v, t) = Room();
            var q = Quaternion.Euler(0f, turned, 0f);
            for (int i = 0; i < v.Count; i++) v[i] = q * v[i];
            float yaw = InteriorBuilder.WallYaw(v, t);
            var back = Quaternion.Euler(0f, yaw, 0f) * q;
            var wallLeft = back * new Vector3(-4.85f, 2.5f, 0f);
            var wallBack = back * new Vector3(0f, 2.5f, 4.85f);
            Assert.AreEqual(-4.85f, wallLeft.x, 0.2f, $"turned {turned}: yaw {yaw} puts the tall wall at -X ({wallLeft})");
            Assert.AreEqual(4.85f, wallBack.z, 0.2f, $"turned {turned}: yaw {yaw} puts the long wall at +Z ({wallBack})");
        }
    }
}
