using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// A car on the avenue: drives along its nose and, past the edge of the district, comes back in on the other side.
    /// The district builder puts it on the taxis (which must not be static, or they are batched into the scene).
    /// </summary>
    public sealed class StreetCar : MonoBehaviour
    {
        [SerializeField] float speed = 7f;
        [SerializeField] float halfLength = 68f;

        public void Configure(float speed, float halfLength)
        {
            this.speed = speed;
            this.halfLength = halfLength;
        }

        public void Step(float seconds)
        {
            var p = transform.position + transform.forward * (speed * seconds);
            if (p.x > halfLength) p.x = -halfLength;
            else if (p.x < -halfLength) p.x = halfLength;
            if (p.z > halfLength) p.z = -halfLength;
            else if (p.z < -halfLength) p.z = halfLength;
            transform.position = p;
        }

        void Update() => Step(Time.deltaTime);
    }
}
