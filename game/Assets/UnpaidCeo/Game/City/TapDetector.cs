using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// A tap on the street: a press that barely moves and lets go quickly. A drag pans the camera and a long press does
    /// nothing, so neither is a tap — also a drag that wanders off and comes back to where it started.
    /// </summary>
    public sealed class TapDetector
    {
        bool _down;
        Vector2 _start;
        float _startTime, _travel;

        public TapDetector(float maxMove, float maxSeconds)
        {
            MaxMove = maxMove;
            MaxSeconds = maxSeconds;
        }

        /// <summary>How far (pixels) the pointer may travel and still tap; the game scales it with the screen.</summary>
        public float MaxMove { get; set; }
        public float MaxSeconds { get; }

        public void Press(Vector2 at, float time)
        {
            _down = true;
            _start = at;
            _startTime = time;
            _travel = 0f;
        }

        public void Move(Vector2 at)
        {
            if (_down) _travel = Mathf.Max(_travel, Vector2.Distance(_start, at));
        }

        /// <summary>The press ends; true when it was a tap.</summary>
        public bool Release(Vector2 at, float time)
        {
            if (!_down) return false;
            Move(at);
            _down = false;
            return _travel <= MaxMove && time - _startTime <= MaxSeconds;
        }

        /// <summary>Forgets the press (it began on the interface, or a second finger came down to pinch).</summary>
        public void Cancel() => _down = false;
    }
}
