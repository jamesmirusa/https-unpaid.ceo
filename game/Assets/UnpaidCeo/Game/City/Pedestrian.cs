using UnityEngine;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.City
{
    /// <summary>Someone walking a stretch of sidewalk: to one end, a short rest, then back. Its model is a child (it may arrive later).</summary>
    public sealed class Pedestrian : MonoBehaviour
    {
        const float TurnDegreesPerSecond = 360f;

        Vector3 _a, _b;
        float _speed = 1.2f, _rest = 2f;
        float _t;            // 0 at a, 1 at b
        float _direction = 1f;
        float _resting;
        CharacterAnimator _animator;

        /// <param name="startAt">0..1 along the lane.</param>
        public void Configure(Vector3 a, Vector3 b, float speed, float restSeconds, float startAt, bool towardB = true)
        {
            _a = a;
            _b = b;
            _speed = speed;
            _rest = restSeconds;
            _t = Mathf.Clamp01(startAt);
            _direction = towardB ? 1f : -1f;
            transform.position = Vector3.Lerp(_a, _b, _t);
            Face(instant: true);
        }

        public void Step(float seconds)
        {
            if (_animator == null) _animator = GetComponentInChildren<CharacterAnimator>();
            if (_resting > 0f)
            {
                _resting -= seconds;
                _animator?.Play(CharacterAnimator.State.Idle);
                return;
            }
            float length = Vector3.Distance(_a, _b);
            if (length < 0.01f) return;
            _t += _direction * _speed * seconds / length;
            if (_t >= 1f || _t <= 0f)
            {
                _t = Mathf.Clamp01(_t);
                _direction = -_direction;
                _resting = _rest;
            }
            transform.position = Vector3.Lerp(_a, _b, _t);
            Face(instant: false, seconds);
            _animator?.Play(CharacterAnimator.State.Walk);
        }

        void Face(bool instant, float seconds = 0f)
        {
            var heading = (_b - _a) * _direction;
            heading.y = 0f;
            if (heading.sqrMagnitude < 1e-6f) return;
            var goal = Quaternion.LookRotation(heading);
            transform.rotation = instant ? goal : Quaternion.RotateTowards(transform.rotation, goal, TurnDegreesPerSecond * seconds);
        }

        void Update() => Step(Time.deltaTime);
    }
}
