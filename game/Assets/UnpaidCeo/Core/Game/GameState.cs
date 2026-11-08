using System.Collections.Generic;
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
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public ulong Seed { get; set; }
        /// <summary>The month currently being played, starting at 1.</summary>
        public int Month { get; set; }
        public int Chapter { get; set; } = 1;
        public Money Cash { get; set; }
        public Money Reserve { get; set; }
        public Money LifetimeEarnings { get; set; }
        public LifeStats Life { get; set; } = new LifeStats();
        public int StudyXp { get; set; }
        public FactStore Facts { get; set; } = new FactStore();
        public NpcRelations Npcs { get; set; } = new NpcRelations();
        public List<BusinessState> Businesses { get; set; } = new List<BusinessState>();
        public List<LoanState> Loans { get; set; } = new List<LoanState>();
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
        public MonthDecisions LastDecisions { get; set; }
    }
}
