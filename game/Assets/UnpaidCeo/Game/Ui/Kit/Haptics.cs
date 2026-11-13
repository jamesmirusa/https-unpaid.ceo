#nullable enable
using System;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// A light touch on the phone when the interface answers (iPhone: UIImpactFeedbackGenerator through
    /// Plugins/iOS/UnpaidHaptics.mm). Nothing on the web or in the editor. Follows the sound switch's "quiet" choice.
    /// </summary>
    public static class Haptics
    {
        public enum Kind { Light = 0, Medium = 1, Success = 2, Warning = 3 }

        /// <summary>Where haptics go; null = the device.</summary>
        public static Action<Kind>? Sink;

        public static void Play(Kind kind)
        {
            if (Sink != null) { Sink(kind); return; }
#if UNITY_IOS && !UNITY_EDITOR
            if (!UiSound.Muted) UnpaidHapticsPlay((int)kind);
#endif
        }

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void UnpaidHapticsPlay(int kind);
#endif
    }
}
