using System.Collections.Generic;
using System.Linq;

namespace UnpaidCeo.Core.Content
{
    /// <summary>A CEO Academy card: unlocked by a fact, explained by the mentor, checked by a 3-question quiz.</summary>
    public sealed class LessonDef
    {
        public string Id { get; set; }
        public string TriggerFact { get; set; }
        public string TitleKey { get; set; }
        public string BodyKey { get; set; }
        public List<QuizQuestion> Quiz { get; set; } = new List<QuizQuestion>();
        public SkillReward SkillReward { get; set; } = new SkillReward();
    }

    public sealed class QuizQuestion
    {
        public string QuestionKey { get; set; }
        public List<string> OptionKeys { get; set; } = new List<string>();
        public int CorrectIndex { get; set; }
    }

    /// <summary>XP granted to one founder skill for each quiz question answered right on the first try.</summary>
    public sealed class SkillReward
    {
        public string Skill { get; set; }
        public int Xp { get; set; }
    }

    /// <summary>The five founder skills (spec §5).</summary>
    public static class FounderSkills
    {
        public static readonly IReadOnlyList<string> All = new[] { "sales", "operations", "finance", "marketing", "leadership" };
        public static bool IsKnown(string skill) => All.Contains(skill);
    }
}
