using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>The hero's room in the walk-up: where the month starts and the hero stands between decisions.</summary>
    public sealed class RoomScene : MonoBehaviour
    {
        [SerializeField] Transform heroSpawn;
        [SerializeField] Camera roomCamera;

        public Transform HeroSpawn => heroSpawn;
        public Camera Camera => roomCamera;

        public void Configure(Transform spawn, Camera cam)
        {
            heroSpawn = spawn;
            roomCamera = cam;
        }
    }
}
