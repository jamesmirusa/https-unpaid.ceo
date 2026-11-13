using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// Every tap answers in the same frame: the button sinks (class <see cref="PressedClass"/>, styled in Tokens.uss),
    /// a soft tap plays and the phone ticks. A closed button answers too — a muted "closed" sound and a small shake —
    /// so a tap is never silent (its status line already says why it is closed).
    /// One listener on the root, trickling down, so every button gets it, including ones built later.
    /// </summary>
    public static class Feel
    {
        public const string PressedClass = "feel--pressed";

        public static void Attach(VisualElement root)
        {
            root.RegisterCallback<PointerDownEvent>(e => Press(e.target as VisualElement), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => Release(), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCancelEvent>(_ => Release(), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerCaptureOutEvent>(_ => Release(), TrickleDown.TrickleDown);
        }

        static Button _pressed;

        /// <summary>The button under a press (the element itself or its nearest Button ancestor).</summary>
        public static Button ButtonOf(VisualElement e)
        {
            for (var v = e; v != null; v = v.parent) if (v is Button b) return b;
            return null;
        }

        public static void Press(VisualElement target)
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
            b.AddToClassList(PressedClass);
            UiSound.Play(UiSound.Tap);
            Haptics.Play(Haptics.Kind.Light);
        }

        public static void Release()
        {
            if (_pressed == null) return;
            _pressed.RemoveFromClassList(PressedClass);
            _pressed = null;
        }

        /// <summary>A short side-to-side "no" (≈0.24 s).</summary>
        public static void Shake(VisualElement e)
        {
            float[] steps = { 10, -8, 6, -3, 0 };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = steps[i];
                e.schedule.Execute(() => e.style.translate = new Translate(x, 0)).StartingIn(i * 48L);
            }
        }
    }
}
