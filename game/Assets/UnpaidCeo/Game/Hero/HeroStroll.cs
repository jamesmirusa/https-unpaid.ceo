using UnityEngine;
using State = UnpaidCeo.Game.Hero.CharacterAnimator.State;

namespace UnpaidCeo.Game.Hero
{
    /// <summary>
    /// Walks the hero back and forth in front of home along the anchor's local X axis, resting and facing the street
    /// at each end. It waits while a cheer or think plays, and a hero without a walk clip just stands.
    /// </summary>
    public sealed class HeroStroll : MonoBehaviour
    {
        [SerializeField] float halfWidth = 1.8f;
        [SerializeField] float speed = 0.9f;
        [SerializeField] float restSeconds = 4f;
        const float TurnDegreesPerSecond = 360f;

        float _direction = 1f;
        float _restUntil;

        public void Configure(float halfWidth, float speed, float restSeconds)
        {
            this.halfWidth = halfWidth;
            this.speed = speed;
            this.restSeconds = restSeconds;
        }

        void Update()
        {
            var a = GetComponentInChildren<CharacterAnimator>();
            if (a == null || a.Current == State.Cheer || a.Current == State.Think) return;
            var t = a.transform;

            if (Time.time < _restUntil || a.Clip(State.Walk) == null)
            {
                a.Play(State.Idle);
                t.localRotation = Quaternion.RotateTowards(t.localRotation, Quaternion.identity, TurnDegreesPerSecond * Time.deltaTime);
                return;
            }

            var p = t.localPosition;
            float goal = _direction * halfWidth;
            if (Mathf.Abs(goal - p.x) < 0.005f)
            {
                _restUntil = Time.time + restSeconds;
                _direction = -_direction;
                a.Play(State.Idle);
                return;
            }

            a.Play(State.Walk);
            p.x = Mathf.MoveTowards(p.x, goal, speed * Time.deltaTime);
            t.localPosition = p;
            var facing = Quaternion.Euler(0f, goal > t.localPosition.x ? 90f : -90f, 0f);
            t.localRotation = Quaternion.RotateTowards(t.localRotation, facing, TurnDegreesPerSecond * Time.deltaTime);
        }
    }
}
