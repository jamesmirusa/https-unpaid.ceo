using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// Every tap answers: the button sinks the moment the finger touches it (class <see cref="PressedClass"/>, styled in
    /// Tokens.uss), and the soft tap and the phone's tick come when the finger lifts — so a scroll that starts on a card
    /// stays quiet: moving more than <see cref="DragSlop"/> px lifts the card back up and cancels the tap.
    /// A closed button answers too — a muted "closed" sound and a small shake — so a tap is never silent.
    /// One listener per panel root, trickling down, so every button gets it, including ones built later.
    /// </summary>
    public static class Feel
    {
        public const string PressedClass = "feel--pressed";
        public const float DragSlop = 12f;

        public static void Attach(VisualElement root)
        {
            root.RegisterCallback<PointerDownEvent>(e => { if (e.button <= 0) Press(e.target as VisualElement, e.position); }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerMoveEvent>(e => Move(e.position), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => Release(commit: true), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(_ => Release(), TrickleDown.TrickleDown);
        }

        static Button _pressed;
        static Vector2 _start;

        /// <summary>The button under a press (the element itself or its nearest Button ancestor).</summary>
        public static Button ButtonOf(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent) if (v is Button b) return b;
            return null;
        }

        public static void Press(VisualElement target, Vector2 position)
        {
            var b = ButtonOf(target);
            if (b == null) return;
            if (!b.enabledInHierarchy)
            {
                UiSound.Play(UiSound.Closed);
                Haptics.Play(Haptics.Kind.Warning);
                Shake(b);
                return;
            }
            Release();
            _pressed = b;
            _start = position;
            b.AddToClassList(PressedClass);
        }

        /// <summary>A drag past the slop is a scroll, not a tap: the card comes back up and stays quiet.</summary>
        public static void Move(Vector2 position)
        {
            if (_pressed != null && (position - _start).sqrMagnitude > DragSlop * DragSlop) Release();
        }

        /// <param name="commit">The finger lifted on a press that was not a drag: play the tap.</param>
        public static void Release(bool commit = false)
        {
            if (_pressed == null) return;
            _pressed.RemoveFromClassList(PressedClass);
            _pressed = null;
            if (!commit) return;
            UiSound.Play(UiSound.Tap);
            Haptics.Play(Haptics.Kind.Light);
        }

        /// <summary>A short side-to-side "no" (≈0.24 s); leaves no inline style behind.</summary>
        public static void Shake(VisualElement e)
        {
            float[] steps = { 10, -8, 6, -3 };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = steps[i];
                e.schedule.Execute(() => e.style.translate = new Translate(x, 0)).StartingIn(i * 48L);
            }
            e.schedule.Execute(() => e.style.translate = StyleKeyword.Null).StartingIn(steps.Length * 48L);
        }
    }
}
