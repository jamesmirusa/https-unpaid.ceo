using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>A consequence of a choice or free action, parsed from JSON. Applied by EffectApplier (Task 8).</summary>
    [JsonConverter(typeof(EffectJsonConverter))]
    public abstract class Effect
    {
        public JObject Raw { get; private set; }

        public static Effect Parse(JObject j)
        {
            Effect e;
            if (j["cash"] != null) e = new CashEffect(Money.FromCents((long)j["cash"]));
            else if (j["stat"] != null)
            {
                var stat = (string)j["stat"];
                if (!LifeStats.IsKnown(stat)) throw new FormatException($"unknown stat '{stat}' in effect {j.ToString(Formatting.None)}");
                e = new StatEffect(stat, (int)j["delta"]);
            }
            else if (j["npc"] != null) e = new NpcEffect((string)j["npc"], (int)j["delta"]);
            else if (j["setFact"] != null) e = new SetFactEffect((string)j["setFact"]);
            else if (j["clearFact"] != null) e = new ClearFactEffect((string)j["clearFact"]);
            else if (j["reserve"] != null) e = new ReserveEffect(Money.FromCents((long)j["reserve"]));
            else if (j["lesson"] != null) e = new LessonEffect((string)j["lesson"]);
            else if (j["studyXp"] != null) e = new StudyXpEffect((int)j["studyXp"]);
            else throw new FormatException($"unknown effect {j.ToString(Formatting.None)}");
            e.Raw = j;
            return e;
        }
    }

    public sealed class CashEffect : Effect { public Money Amount { get; } public CashEffect(Money a) { Amount = a; } }
    public sealed class StatEffect : Effect { public string Stat { get; } public int Delta { get; } public StatEffect(string s, int d) { Stat = s; Delta = d; } }
    public sealed class NpcEffect : Effect { public string Npc { get; } public int Delta { get; } public NpcEffect(string n, int d) { Npc = n; Delta = d; } }
    public sealed class SetFactEffect : Effect { public string FactId { get; } public SetFactEffect(string id) { FactId = id; } }
    public sealed class ClearFactEffect : Effect { public string FactId { get; } public ClearFactEffect(string id) { FactId = id; } }
    /// <summary>Moves money from cash into the reserve account (negative moves it back).</summary>
    public sealed class ReserveEffect : Effect { public Money Amount { get; } public ReserveEffect(Money a) { Amount = a; } }
    public sealed class LessonEffect : Effect { public string LessonId { get; } public LessonEffect(string id) { LessonId = id; } }
    public sealed class StudyXpEffect : Effect { public int Amount { get; } public StudyXpEffect(int a) { Amount = a; } }

    public sealed class EffectJsonConverter : JsonConverter<Effect>
    {
        public override void WriteJson(JsonWriter writer, Effect value, JsonSerializer serializer) => value.Raw.WriteTo(writer);
        public override Effect ReadJson(JsonReader reader, Type objectType, Effect existingValue, bool hasExistingValue, JsonSerializer serializer) =>
            Effect.Parse(JObject.Load(reader));
    }
}
