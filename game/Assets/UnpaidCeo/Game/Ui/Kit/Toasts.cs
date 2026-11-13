using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>A short note at the top (2 s): slides in, stays, fades. A new toast replaces the one showing.</summary>
    public static class Toasts
    {
        public const string Class = "toast", In = "toast--in";
        public const long ShowMs = 2000;

        public static Label Show(VisualElement root, string key, string icon = null)
        {
            root.Q<Label>("toast")?.RemoveFromHierarchy();
            var t = UiText.Label(key, Class);
            t.name = "toast";
            t.pickingMode = PickingMode.Ignore;
            if (icon != null) Icons.Decorate(t, Icons.Game(icon), "icon--lead");
            root.Add(t);
            t.BringToFront();
            t.schedule.Execute(() => t.AddToClassList(In)).StartingIn(30);
            t.schedule.Execute(() => t.RemoveFromClassList(In)).StartingIn(ShowMs);
            t.schedule.Execute(t.RemoveFromHierarchy).StartingIn(ShowMs + 400);
            UiSound.Play(UiSound.Pop);
            return t;
        }
    }
}
