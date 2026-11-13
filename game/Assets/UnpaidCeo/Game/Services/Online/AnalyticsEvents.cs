#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace UnpaidCeo.Game.Services.Online
{
    /// <summary>The player's analytics choice on this device: never asked, granted or declined.</summary>
    public static class AnalyticsConsent
    {
        public const string Key = "unpaid.analytics_consent";

        public static bool Asked => PlayerPrefs.HasKey(Key);
        public static bool Granted => PlayerPrefs.GetInt(Key, 0) == 1;

        public static void Set(bool on)
        {
            PlayerPrefs.SetInt(Key, on ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    /// <summary>
    /// The spec §7.7 events, one helper each. Recording goes through <see cref="IGameAnalytics"/>, which drops everything
    /// until the player consents. <c>purchase</c> and <c>ad_rewarded</c> are declared for stage 2D.
    /// </summary>
    public static class AnalyticsEvents
    {
        public const string TutorialStepEvent = "tutorial_step", MonthCompletedEvent = "month_completed",
            ChapterUnlockedEvent = "chapter_unlocked", IndustryChosenEvent = "industry_chosen",
            BusinessOpenedEvent = "business_opened", BankruptEvent = "bankrupt", LessonViewedEvent = "lesson_viewed",
            QuizPassedEvent = "quiz_passed", SignupEvent = "signup", PurchaseEvent = "purchase",
            AdRewardedEvent = "ad_rewarded", GraphicsModeEvent = "graphics_mode", LanguageEvent = "language";

        public static readonly string[] All =
        {
            TutorialStepEvent, MonthCompletedEvent, ChapterUnlockedEvent, IndustryChosenEvent, BusinessOpenedEvent,
            BankruptEvent, LessonViewedEvent, QuizPassedEvent, SignupEvent, PurchaseEvent, AdRewardedEvent,
            GraphicsModeEvent, LanguageEvent,
        };

        public static void TutorialStep(IGameAnalytics a, int step) => a.Record(TutorialStepEvent, Args(("step", step)));
        public static void MonthCompleted(IGameAnalytics a, int month, int chapter) => a.Record(MonthCompletedEvent, Args(("month", month), ("chapter", chapter)));
        public static void ChapterUnlocked(IGameAnalytics a, int chapter) => a.Record(ChapterUnlockedEvent, Args(("chapter", chapter)));
        public static void IndustryChosen(IGameAnalytics a, string id) => a.Record(IndustryChosenEvent, Args(("industry", id)));
        public static void BusinessOpened(IGameAnalytics a, string id, int units) => a.Record(BusinessOpenedEvent, Args(("industry", id), ("units", units)));
        public static void Bankrupt(IGameAnalytics a, int month) => a.Record(BankruptEvent, Args(("month", month)));
        public static void LessonViewed(IGameAnalytics a, string id) => a.Record(LessonViewedEvent, Args(("lesson", id)));
        public static void QuizPassed(IGameAnalytics a, string id) => a.Record(QuizPassedEvent, Args(("lesson", id)));
        public static void Signup(IGameAnalytics a, string method) => a.Record(SignupEvent, Args(("method", method)));
        public static void GraphicsMode(IGameAnalytics a, string mode) => a.Record(GraphicsModeEvent, Args(("mode", mode)));
        public static void Language(IGameAnalytics a, string code) => a.Record(LanguageEvent, Args(("language", code)));

        static Dictionary<string, object> Args(params (string Key, object Value)[] pairs)
        {
            var d = new Dictionary<string, object>();
            foreach (var (k, v) in pairs) d[k] = v;
            return d;
        }
    }
}
