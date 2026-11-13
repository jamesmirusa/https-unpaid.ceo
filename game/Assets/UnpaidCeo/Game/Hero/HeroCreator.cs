using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.Ui;

namespace UnpaidCeo.Game.Hero
{
    /// <summary>
    /// Create-your-hero panel (Resources/UI/HeroCreator.uxml): species, outfit and colour. A random hero is
    /// preselected and "Done" works immediately, so onboarding can pass this step in one tap.
    /// </summary>
    public sealed class HeroCreator
    {
        public static readonly string[] Species = { "raccoon", "fox", "cat" };
        public static readonly string[] Outfits = { "hoodie", "blazer" };

        readonly HeroView _view;
        readonly VisualElement _species, _outfits, _colors;

        public HeroAppearance Result { get; private set; }
        public event Action<HeroAppearance> Confirmed;

        public HeroCreator(VisualElement root, HeroView view, int seed)
        {
            _view = view;
            var rng = new System.Random(seed);
            Result = new HeroAppearance { Species = Species[rng.Next(Species.Length)], Outfit = rng.Next(Outfits.Length), ColorIndex = 0 };

            var asset = Resources.Load<VisualTreeAsset>("UI/HeroCreator");
            var panel = asset.Instantiate();
            root.Add(panel.childCount == 1 ? panel[0] : panel);
            UiText.Bind(root.Q<Label>("hero-title"), "hero.title");
            UiText.Bind(root.Q<Label>("hero-species-caption"), "hero.species");
            UiText.Bind(root.Q<Label>("hero-outfit-caption"), "hero.outfit");
            UiText.Bind(root.Q<Label>("hero-color-caption"), "hero.color");
            var done = UiText.Bind(root.Q<Button>("hero-done"), "hero.done");
            done.clicked += Confirm;

            _species = root.Q("hero-species");
            foreach (var s in Species)
            {
                var b = UiText.Bind(new Button(() => Choose(s, Result.Outfit, Result.ColorIndex)) { name = "species-" + s }, "hero.species." + s);
                b.AddToClassList("choice-button");
                _species.Add(b);
            }
            _outfits = root.Q("hero-outfit");
            for (int i = 0; i < Outfits.Length; i++)
            {
                int outfit = i;
                var b = UiText.Bind(new Button(() => Choose(Result.Species, outfit, Result.ColorIndex)) { name = "outfit-" + Outfits[i] }, "hero.outfit." + Outfits[i]);
                b.AddToClassList("choice-button");
                _outfits.Add(b);
            }
            _colors = root.Q("hero-color");
            for (int i = 0; i < HeroView.Tints.Length; i++)
            {
                int color = i;
                var swatch = new Button(() => Choose(Result.Species, Result.Outfit, color)) { name = "color-" + i };
                swatch.AddToClassList("swatch");
                swatch.style.backgroundColor = HeroView.Tints[i];
                _colors.Add(swatch);
            }
            Refresh();
            Show();
        }

        public void Choose(string species, int outfit, int color)
        {
            Result = new HeroAppearance { Species = species, Outfit = outfit, ColorIndex = color };
            Refresh();
            Show();
        }

        public void Confirm() => Confirmed?.Invoke(Result);

        void Show()
        {
            if (_view != null && _view.isActiveAndEnabled) _view.StartCoroutine(_view.Apply(Result));
        }

        void Refresh()
        {
            for (int i = 0; i < Species.Length; i++) _species[i].EnableInClassList("choice-button--selected", Species[i] == Result.Species);
            for (int i = 0; i < Outfits.Length; i++) _outfits[i].EnableInClassList("choice-button--selected", i == Result.Outfit);
            for (int i = 0; i < _colors.childCount; i++) _colors[i].EnableInClassList("swatch--selected", i == Result.ColorIndex);
        }
    }
}
