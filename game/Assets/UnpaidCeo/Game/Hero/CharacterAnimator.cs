using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UnpaidCeo.Game.Hero
{
    /// <summary>
    /// Plays a character's four clips (idle, walk, cheer, think) through a small playable mixer, no controller asset
    /// needed. Looping states crossfade; a one-shot returns to the looping state when it ends. Missing clips are skipped,
    /// so a character with only an idle clip simply stays idle.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public sealed class CharacterAnimator : MonoBehaviour
    {
        public enum State { Idle, Walk, Cheer, Think }

        public const float FadeSeconds = 0.25f;
        const int Count = 4;

        [SerializeField] AnimationClip? idle;
        [SerializeField] AnimationClip? walk;
        [SerializeField] AnimationClip? cheer;
        [SerializeField] AnimationClip? think;

        PlayableGraph _graph;
        AnimationMixerPlayable _mixer;
        readonly AnimationClipPlayable[] _playables = new AnimationClipPlayable[Count];
        readonly float[] _weights = new float[Count];
        State _loop = State.Idle;
        float _oneShotEnds = -1f;

        public State Current { get; private set; } = State.Idle;

        public AnimationClip? Clip(State s) => s switch
        {
            State.Idle => idle,
            State.Walk => walk,
            State.Cheer => cheer,
            _ => think,
        };

        public void SetClips(AnimationClip? idleClip, AnimationClip? walkClip, AnimationClip? cheerClip, AnimationClip? thinkClip)
        {
            idle = idleClip;
            walk = walkClip;
            cheer = cheerClip;
            think = thinkClip;
            if (_graph.IsValid()) Build();
        }

        /// <summary>Switches to a looping state (idle or walk); it waits for a running one-shot to finish.</summary>
        public void Play(State s)
        {
            if (Clip(s) == null) return;
            _loop = s;
            if (_oneShotEnds >= 0f || Current == s) return;
            Switch(s);
        }

        /// <summary>Plays a clip once from the start, then returns to the looping state.</summary>
        public void PlayOnce(State s)
        {
            var clip = Clip(s);
            if (clip == null) return;
            Switch(s);
            _oneShotEnds = Time.time + clip.length;
        }

        void OnEnable() => Build();

        void OnDisable()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        void Build()
        {
            if (_graph.IsValid()) _graph.Destroy();
            var animator = GetComponent<Animator>();
            animator.runtimeAnimatorController = null;
            _graph = PlayableGraph.Create($"{name}-character");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, Count);
            for (int i = 0; i < Count; i++)
            {
                _playables[i] = default;
                var clip = Clip((State)i);
                if (clip == null) continue;
                _playables[i] = AnimationClipPlayable.Create(_graph, clip);
                _graph.Connect(_playables[i], 0, _mixer, i);
            }
            AnimationPlayableOutput.Create(_graph, "character", animator).SetSourcePlayable(_mixer);
            if (Clip(Current) == null) Current = State.Idle;
            for (int i = 0; i < Count; i++) _weights[i] = i == (int)Current ? 1f : 0f;
            ApplyWeights();
            _graph.Play();
        }

        void Switch(State s)
        {
            Current = s;
            var p = _playables[(int)s];
            if (p.IsValid()) p.SetTime(0);
        }

        void Update()
        {
            if (_oneShotEnds >= 0f && Time.time >= _oneShotEnds)
            {
                _oneShotEnds = -1f;
                Switch(Clip(_loop) != null ? _loop : State.Idle);
            }
            float step = Time.deltaTime / FadeSeconds;
            for (int i = 0; i < Count; i++) _weights[i] = Mathf.MoveTowards(_weights[i], i == (int)Current ? 1f : 0f, step);
            ApplyWeights();
        }

        void ApplyWeights()
        {
            if (!_mixer.IsValid()) return;
            for (int i = 0; i < Count; i++) _mixer.SetInputWeight(i, _playables[i].IsValid() ? _weights[i] : 0f);
        }
    }
}
