using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>Opens a panel with the popup bounce (class <see cref="UiRoot.PopupInClass"/> one frame later) and a soft pop.</summary>
    public static class PopIn
    {
        public static void Play(VisualElement panel, string sound = UiSound.Pop)
        {
            panel.RemoveFromClassList(UiRoot.PopupInClass);
            panel.schedule.Execute(() => panel.AddToClassList(UiRoot.PopupInClass)).StartingIn(16);
            if (sound != null) UiSound.Play(sound);
        }
    }
}
