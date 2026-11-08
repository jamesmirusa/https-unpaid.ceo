using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Hero;
using State = UnpaidCeo.Game.Hero.CharacterAnimator.State;

namespace UnpaidCeo.Game.Tests
{
    /// <summary>The real hero models load through Addressables with their four clips and play them.</summary>
    public class HeroAnimationSmokeTests
    {
        GameObject? _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.Destroy(_root);
        }

        [UnityTest]
        public IEnumerator Every_hero_loads_with_clips_and_cheers()
        {
            var hero = new GameObject("Hero").AddComponent<HeroView>();
            _root = hero.gameObject;
            foreach (var species in HeroCreator.Species)
                for (int outfit = 0; outfit < HeroCreator.Outfits.Length; outfit++)
                {
                    yield return hero.Apply(new HeroAppearance { Species = species, Outfit = outfit });
                    Assert.IsNotNull(hero.Model, HeroView.Address(new HeroAppearance { Species = species, Outfit = outfit }));
                    var animator = hero.Model!.GetComponent<CharacterAnimator>();
                    Assert.IsNotNull(animator, hero.LoadedAddress);
                    foreach (State s in System.Enum.GetValues(typeof(State)))
                        Assert.IsNotNull(animator.Clip(s), $"{hero.LoadedAddress} {s}");
                }

            hero.React(new MonthReport { NetProfit = UnpaidCeo.Core.Primitives.Money.FromDollars(10) });
            yield return null;
            Assert.AreEqual(State.Cheer, hero.Model!.GetComponent<CharacterAnimator>().Current);
        }
    }
}
