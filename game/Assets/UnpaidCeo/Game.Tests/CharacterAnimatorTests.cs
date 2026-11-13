using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.Hero;
using State = UnpaidCeo.Game.Hero.CharacterAnimator.State;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>Characters play idle / walk / cheer / think clips; the hero reacts to the month and strolls by home.</summary>
    public class CharacterAnimatorTests
    {
        GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        static AnimationClip Clip(string name, float x, float length = 0.5f)
        {
            var c = new AnimationClip { name = name };
            c.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Constant(0, length, x));
            return c;
        }

        CharacterAnimator Make(Transform? parent = null, bool allClips = true)
        {
            var go = new GameObject("Character");
            if (parent != null) go.transform.SetParent(parent, false);
            else _root = go;
            new GameObject("Bone").transform.SetParent(go.transform, false);
            var a = go.AddComponent<CharacterAnimator>();
            if (allClips) a.SetClips(Clip("idle", 0), Clip("walk", 1), Clip("cheer", 2, 0.3f), Clip("think", 3, 0.3f));
            else a.SetClips(Clip("idle", 0), null, null, null);
            return a;
        }

        static float BoneX(CharacterAnimator a) => a.transform.Find("Bone").localPosition.x;

        static IEnumerator Wait(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        [UnityTest]
        public IEnumerator Starts_in_idle()
        {
            var a = Make();
            yield return Wait(0.2f);
            Assert.AreEqual(State.Idle, a.Current);
            Assert.AreEqual(0f, BoneX(a), 0.05f);
        }

        [UnityTest]
        public IEnumerator Walk_blends_into_the_walk_pose()
        {
            var a = Make();
            yield return null;
            a.Play(State.Walk);
            yield return Wait(0.6f);
            Assert.AreEqual(State.Walk, a.Current);
            Assert.AreEqual(1f, BoneX(a), 0.05f);
        }

        [UnityTest]
        public IEnumerator Cheer_plays_once_then_returns_to_idle()
        {
            var a = Make();
            yield return null;
            a.PlayOnce(State.Cheer);
            yield return Wait(0.1f);
            Assert.AreEqual(State.Cheer, a.Current);
            yield return Wait(1.0f);
            Assert.AreEqual(State.Idle, a.Current);
            Assert.AreEqual(0f, BoneX(a), 0.05f);
        }

        [UnityTest]
        public IEnumerator Missing_clips_are_ignored()
        {
            var a = Make(allClips: false);
            yield return null;
            a.Play(State.Walk);
            a.PlayOnce(State.Cheer);
            yield return Wait(0.2f);
            Assert.AreEqual(State.Idle, a.Current);
        }

        HeroView MakeHero(out CharacterAnimator animator)
        {
            var hero = new GameObject("Hero").AddComponent<HeroView>();
            _root = hero.gameObject;
            animator = Make(hero.transform);
            return hero;
        }

        [UnityTest]
        public IEnumerator Hero_cheers_after_a_profitable_month_and_thinks_after_a_loss()
        {
            var hero = MakeHero(out var animator);
            yield return null;
            hero.React(new MonthReport { NetProfit = Money.FromDollars(120) });
            yield return null;
            Assert.AreEqual(State.Cheer, animator.Current);
            yield return Wait(1.0f);
            hero.React(new MonthReport { NetProfit = Money.FromDollars(-40) });
            yield return null;
            Assert.AreEqual(State.Think, animator.Current);
        }

        [UnityTest]
        public IEnumerator Hero_thinks_while_choosing()
        {
            var hero = MakeHero(out var animator);
            yield return null;
            hero.Think();
            yield return null;
            Assert.AreEqual(State.Think, animator.Current);
        }

        [UnityTest]
        public IEnumerator Hero_strolls_in_front_of_home_and_rests_at_the_ends()
        {
            var hero = MakeHero(out var animator);
            var stroll = hero.gameObject.AddComponent<HeroStroll>();
            stroll.Configure(halfWidth: 1f, speed: 4f, restSeconds: 0.2f);
            var start = animator.transform.localPosition;
            bool walked = false, rested = false;
            float end = Time.time + 2f;
            while (Time.time < end)
            {
                yield return null;
                if (animator.Current == State.Walk && (animator.transform.localPosition - start).sqrMagnitude > 0.01f) walked = true;
                if (walked && animator.Current == State.Idle) rested = true;
            }
            Assert.IsTrue(walked, "never walked");
            Assert.IsTrue(rested, "never rested at an end");
            Assert.LessOrEqual(Mathf.Abs(animator.transform.localPosition.x), 1.01f);
        }
    }
}
