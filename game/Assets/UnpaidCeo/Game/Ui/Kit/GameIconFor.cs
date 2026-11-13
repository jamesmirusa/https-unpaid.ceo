namespace UnpaidCeo.Game.Ui.Kit
{
    /// <summary>Which game icon stands for a deed, a week mode or a gig (one table, so rooms and the Week screen agree).</summary>
    public static class GameIconFor
    {
        public static string Action(string freeAction) => freeAction switch
        {
            "overtime" => "emote_work",
            "night_shift" => "energy",
            "rest" => "mood_slept",
            "study" => "knowledge",
            "certificate_course" => "lesson",
            "networking" => "tone_business",
            "family_time" => "close_ones",
            "hobby" => "mood_coffee",
            "gray_scheme" => "tone_firm",
            "loan_shark_loan" => "mood_loan_shark",
            "reserve_transfer" => "reserve",
            "pitch_night" => "reputation",
            "budget_review" => "profit",
            _ => "action_token",
        };

        public static string Mode(string mode) => mode switch
        {
            "hustle" => "energy",
            "balance" => "action_token",
            "study" => "lesson",
            "family" => "close_ones",
            _ => "action_token",
        };

        /// <summary>Service icon (tinted) for where the week's work goes.</summary>
        public static string Focus(string industryOrEven) => industryOrEven switch
        {
            "gig_delivery" => "truck",
            "gig_resale" => "arrows-clockwise",
            "gig_ai_freelance" => "robot",
            _ => "scales",
        };
    }
}
