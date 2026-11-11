using System.Collections.Generic;
using Newtonsoft.Json;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Narrative;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Game
{
    public sealed class HeroAppearance
    {
        public string Species { get; set; } = "raccoon";
        public int Outfit { get; set; }
        public int ColorIndex { get; set; }
    }

    public sealed class JournalEntry
    {
        public int Month { get; set; }
        /// <summary>"decision" or "consequence".</summary>
        public string Kind { get; set; }
        public string TextKey { get; set; }
        public Dictionary<string, string> Args { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>Everything that defines a running game. Serialized whole for saves and hashed for replay checks.</summary>
    public sealed class GameState
    {
        /// <summary>2: chapters 2–3 (loans, demand modifiers, staff). 3: the turn is a week (stage 4B).</summary>
        public const int CurrentVersion = 3;
        public const int WeeksPerMonth = 4;

        public int Version { get; set; } = CurrentVersion;
        public ulong Seed { get; set; }
        /// <summary>The week currently being played, starting at 1; four weeks make a month.</summary>
        public int Week { get; set; }
        /// <summary>The month the week falls in (kept in step with <see cref="Week"/> by the engine): facts, cooldowns and seasonality count months.</summary>
        public int Month { get; set; }
        /// <summary>1–4 within the month.</summary>
        [JsonIgnore] public int WeekOfMonth => Week <= 0 ? 1 : (Week - 1) % WeeksPerMonth + 1;
        public static int MonthOf(int week) => week <= 0 ? 1 : (week - 1) / WeeksPerMonth + 1;
        public static int FirstWeekOf(int month) => (month - 1) * WeeksPerMonth + 1;
        /// <summary>Action points left in the month's pool (refilled on its first week).</summary>
        public int ActionPointsLeft { get; set; }
        /// <summary>Free actions already taken this month: each at most once a month.</summary>
        public List<string> ActionsThisMonth { get; set; } = new List<string>();
        /// <summary>Week whose scenes were already rolled (0 = none yet).</summary>
        public int ScenesRolledWeek { get; set; }
        public int Chapter { get; set; } = 1;
        /// <summary>Where the chapter is played: bronx, brooklyn or queens. Derived, not saved.</summary>
        [JsonIgnore] public string District => Chapters.District(Chapter);
        public Money Cash { get; set; }
        public Money Reserve { get; set; }
        public Money LifetimeEarnings { get; set; }
        public LifeStats Life { get; set; } = new LifeStats();
        public int StudyXp { get; set; }
        public FactStore Facts { get; set; } = new FactStore();
        public NpcRelations Npcs { get; set; } = new NpcRelations();
        public List<BusinessState> Businesses { get; set; } = new List<BusinessState>();
        public List<LoanState> Loans { get; set; } = new List<LoanState>();
        public List<DemandModifier> Modifiers { get; set; } = new List<DemandModifier>();
        public HeroAppearance Hero { get; set; } = new HeroAppearance();
        /// <summary>Month whose scenes were already rolled (0 = none yet).</summary>
        public int ScenesRolledMonth { get; set; }
        public Dictionary<string, int> StoryletLastMonth { get; set; } = new Dictionary<string, int>();
        /// <summary>Once-only storylets already shown: id → month.</summary>
        public Dictionary<string, int> StoryletsSeen { get; set; } = new Dictionary<string, int>();
        public List<string> PendingStorylets { get; set; } = new List<string>();
        public List<string> PendingLessons { get; set; } = new List<string>();
        public Dictionary<string, int> LessonsSeen { get; set; } = new Dictionary<string, int>();
        /// <summary>Founder skill XP: sales, operations, finance, marketing, leadership.</summary>
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
        /// <summary>First answer given to each quiz question: "lessonId/questionIndex" → option index.</summary>
        public Dictionary<string, int> QuizAnswers { get; set; } = new Dictionary<string, int>();
        public PcgState Rng { get; set; }
        public List<JournalEntry> Journal { get; set; } = new List<JournalEntry>();
        public List<DecisionLogEntry> Log { get; set; } = new List<DecisionLogEntry>();
        public int MonthsInDeepDebt { get; set; }
        /// <summary>Consecutive month ends with at least the chapter-3 team (Balance.Chapter3EmployeesGoal).</summary>
        public int TeamMonths { get; set; }
        public MonthDecisions LastDecisions { get; set; }
    }
}
