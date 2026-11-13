namespace UnpaidCeo.Game.Audio
{
    /// <summary>Addressables keys for streamed audio. Voice lines use Yarn Spinner's own address format.</summary>
    public static class AudioAddresses
    {
        public const string MusicDay = "music_day";
        public const string MusicNeon = "music_neon";

        /// <summary>Day theme of a district (Chapters.District): the Bronx keeps the chapter-1 theme. Neon nights share <see cref="MusicNeon"/>.</summary>
        public static string DistrictMusic(string district) => district == "bronx" ? MusicDay : "music_" + district;

        /// <summary>Same format as Yarn Spinner's Localization.GetAddressForLine (guarded by an editor test).</summary>
        public static string Line(string lineId, string lang) => "line_" + lang + "_" + lineId.Replace("line:", "");

        public static string Lesson(string lessonId, string lang) => "lesson_" + lang + "_" + lessonId;

        /// <summary>Voice exists for English and Russian only; every other UI language hears English.</summary>
        public static string VoiceLanguage(string uiLanguage) => uiLanguage == "ru" ? "ru" : "en";
    }
}
