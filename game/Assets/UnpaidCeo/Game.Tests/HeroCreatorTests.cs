using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.Tests
{
    public class HeroCreatorTests
    {
        GameObject _host;

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.Destroy(_host);
        }

        HeroView NewView()
        {
            _host = new GameObject("hero");
            return _host.AddComponent<HeroView>();
        }

        [UnityTest]
        public IEnumerator Default_hero_ready_in_one_tap()
        {
            yield return LocalizationSettings.InitializationOperation;
            var root = new VisualElement();
            var view = NewView();
            var creator = new HeroCreator(root, view, seed: 7);
            var done = root.Q<Button>("hero-done");
            Assert.IsNotNull(done);
            Assert.IsTrue(done.enabledSelf, "Done must be tappable right away");
            Assert.Contains(creator.Result.Species, HeroCreator.Species.ToList(), "a random species is preselected");

            HeroAppearance confirmed = null;
            creator.Confirmed += a => confirmed = a;
            creator.Confirm();
            Assert.IsNotNull(confirmed);
            Assert.AreEqual(creator.Result.Species, confirmed.Species);
        }

        [Test]
        public void Choices_change_the_result()
        {
            var creator = new HeroCreator(new VisualElement(), null, seed: 1);
            creator.Choose("fox", 1, 2);
            Assert.AreEqual("fox", creator.Result.Species);
            Assert.AreEqual(1, creator.Result.Outfit);
            Assert.AreEqual(2, creator.Result.ColorIndex);
            Assert.AreEqual("hero_fox_blazer", HeroView.Address(creator.Result));
        }

        [UnityTest]
        public IEnumerator All_species_and_outfits_load()
        {
            var view = NewView();
            foreach (var species in HeroCreator.Species)
                for (int outfit = 0; outfit < HeroCreator.Outfits.Length; outfit++)
                {
                    var a = new HeroAppearance { Species = species, Outfit = outfit, ColorIndex = outfit };
                    yield return view.Apply(a);
                    Assert.IsNotNull(view.Model, $"{HeroView.Address(a)} did not load");
                    Assert.AreEqual(HeroView.Address(a), view.LoadedAddress);
                    Assert.IsNotNull(view.Model.GetComponentInChildren<SkinnedMeshRenderer>(), $"{HeroView.Address(a)} is not a rigged character");
                    Assert.AreEqual(1, view.GetComponentsInChildren<SkinnedMeshRenderer>().Length, "the previous hero must be replaced");
                }
        }
    }
}
