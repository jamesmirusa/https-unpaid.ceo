using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>
    /// Icons as images: service icons (Phosphor, MIT; white, tinted by USS through -unity-background-image-tint-color)
    /// and game icons (generated, full colour). An icon never takes taps — the button around it does.
    /// </summary>
    public static class Icons
    {
        public const string ServiceFolder = "UI/Icons/", GameFolder = "UI/GameIcons/";

        /// <summary>Every service icon the UI names (a test checks each file exists).</summary>
        public static readonly string[] ServiceNames =
        {
            "map-trifold", "calendar-dots", "chart-bar", "graduation-cap", "trophy", "book-open-text", "gear-six", "x",
            "caret-left", "caret-right", "lock-simple", "check", "arrow-right", "fast-forward", "lightning", "coins",
            "heart", "users-three", "star", "door-open", "chat-circle-dots", "hand-coins", "warning", "info", "clock",
            "sparkle", "minus", "plus", "house-line", "briefcase", "scales", "book-open", "truck", "arrows-clockwise",
            "robot", "storefront", "user", "piggy-bank", "bell",
        };

        /// <summary>Every game icon (the owner's set: tools/art/stage5b/icons/README.md).</summary>
        public static readonly string[] GameNames =
        {
            "money", "energy", "close_ones", "knowledge", "reputation", "trust", "trust_cold", "action_token", "rent", "food",
            "loan", "salary", "reserve", "lesson", "money_date", "profit", "tone_friendly", "tone_business", "tone_joke",
            "tone_firm", "emote_scene", "emote_work", "emote_lesson", "emote_level", "mood_slept", "mood_loan_shark",
            "mood_burnout", "mood_coffee", "chapter_trophy", "place_shop", "level_bonus", "advice",
        };

        static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D Load(string path)
        {
            if (!Cache.TryGetValue(path, out var t) || t == null) Cache[path] = t = Resources.Load<Texture2D>(path);
            return t;
        }

        /// <summary>A tinted service icon (class "icon" + <paramref name="cls"/>).</summary>
        public static VisualElement Service(string name, string cls = null) => Make(ServiceFolder + name, "icon", cls, name);

        /// <summary>A full-colour game icon (class "game-icon" + <paramref name="cls"/>).</summary>
        public static VisualElement Game(string name, string cls = null) => Make(GameFolder + name, "game-icon", cls, name);

        static VisualElement Make(string path, string baseClass, string cls, string name)
        {
            var e = new VisualElement { name = "icon-" + name, pickingMode = PickingMode.Ignore };
            e.AddToClassList(baseClass);
            if (cls != null) e.AddToClassList(cls);
            var tex = Load(path);
            if (tex != null) e.style.backgroundImage = new StyleBackground(tex);
            else Debug.LogWarning("icon missing: " + path);
            return e;
        }

        /// <summary>
        /// Puts an icon on a text button without touching its text (bound labels keep working): the icon sits in the
        /// button's padding, placed by <paramref name="placement"/> ("icon--top" above the text, "icon--lead" before it).
        /// Replaces an icon added before.
        /// </summary>
        public static T Decorate<T>(T button, VisualElement icon, string placement) where T : VisualElement
        {
            button.Q(className: "icon-slot")?.RemoveFromHierarchy();
            icon.AddToClassList("icon-slot");
            icon.AddToClassList(placement);
            button.AddToClassList("has-" + placement);
            button.Insert(0, icon);
            return button;
        }
    }
}
