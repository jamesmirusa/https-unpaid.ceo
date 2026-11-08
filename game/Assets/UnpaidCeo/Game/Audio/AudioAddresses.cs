namespace UnpaidCeo.Game.Audio
{
    /// <summary>Addressables keys for streamed audio. Voice lines use Yarn Spinner's own address format.</summary>
    public static class AudioAddresses
    {
        public const string MusicDay = "music_day";
        public const string MusicNeon = "music_neon";

        /// <summary>Same format as Yarn Spinner's Localization.GetAddressForLine (guarded by an editor test).</summary>
        public static string Line(string lineId, string lang) => "line_" + lang + "_" + lineId.Replace("line:", "");

        public static string Lesson(string lessonId, string lang) => "lesson_" + lang + "_" + lessonId;

        /// <summary>Voice exists for English and Russian only; every other UI language hears English.</summary>
        public static string VoiceLanguage(string uiLanguage) => uiLanguage == "ru" ? "ru" : "en";
    }
}
