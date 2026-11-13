using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// The "+$51" / "−10" that rises from a number and fades (≈0.9 s). Lives in <paramref name="host"/> (which should be
    /// position: relative); never takes taps; removes itself.
    /// </summary>
    public static class FloatingDelta
    {
        public const string Class = "delta", Positive = "delta--positive", Negative = "delta--negative", Fly = "delta--fly";
        public const long LifeMs = 1000;

        public static Label Show(VisualElement host, string text, bool good)
        {
            var l = new Label(text) { pickingMode = PickingMode.Ignore, name = "delta" };
            l.AddToClassList(Class);
            l.AddToClassList(UiRoot.ValueClass);
            l.AddToClassList(good ? Positive : Negative);
            host.Add(l);
            l.schedule.Execute(() => l.AddToClassList(Fly)).StartingIn(30);   // next frame: the transition runs
            l.schedule.Execute(l.RemoveFromHierarchy).StartingIn(LifeMs);
            return l;
        }
    }
}
