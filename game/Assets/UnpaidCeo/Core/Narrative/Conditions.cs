using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnpaidCeo.Core.Life;
using UnpaidCeo.Core.Primitives;

namespace UnpaidCeo.Core.Narrative
{
    /// <summary>A storylet precondition parsed from JSON (see plan Task 7 for the grammar).</summary>
    [JsonConverter(typeof(ConditionJsonConverter))]
    public abstract class Condition
    {
        public JObject Raw { get; private set; }
        public abstract bool Evaluate(NarrativeContext c);

        public static Condition Parse(JObject j)
        {
            Condition c;
            if (j["fact"] != null) c = new FactCondition((string)j["fact"], true);
            else if (j["notFact"] != null) c = new FactCondition((string)j["notFact"], false);
            else if (j["stat"] != null)
            {
                var stat = (string)j["stat"];
                if (!LifeStats.IsKnown(stat)) throw new FormatException($"unknown stat '{stat}' in condition {j.ToString(Formatting.None)}");
                c = new CompareCondition(ctx => ctx.Life.Get(stat), Op(j), (long)j["value"]);
            }
            else if (j["npc"] != null)
            {
                var npc = (string)j["npc"];
                c = new CompareCondition(ctx => ctx.Npcs.Get(npc), Op(j), (long)j["value"]);
            }
            else if (j["cash"] is JObject cash) c = new CompareCondition(ctx => ctx.Cash.Cents, Op(cash), (long)cash["value"]);
            else if (j["sinceFact"] != null)
                c = new SinceFactCondition((string)j["sinceFact"], (int?)j["min"] ?? 0, (int?)j["max"] ?? int.MaxValue);
            else if (j["chapter"] != null) c = new CompareCondition(ctx => ctx.Chapter, "==", (long)j["chapter"]);
            else if (j["industry"] != null)
            {
                var id = (string)j["industry"];
                c = new FuncCondition(ctx => ctx.IndustryIds.Contains(id));
            }
            else throw new FormatException($"unknown condition {j.ToString(Formatting.None)}");
            c.Raw = j;
            return c;
        }

        static string Op(JObject j)
        {
            var op = (string)j["op"] ?? "==";
            if (op != ">=" && op != ">" && op != "<=" && op != "<" && op != "==" && op != "!=")
                throw new FormatException($"unknown operator '{op}' in {j.ToString(Formatting.None)}");
            return op;
        }
    }

    sealed class FactCondition : Condition
    {
        readonly string _id; readonly bool _expected;
        public FactCondition(string id, bool expected) { _id = id; _expected = expected; }
        public override bool Evaluate(NarrativeContext c) => c.Facts.Has(_id) == _expected;
    }

    sealed class CompareCondition : Condition
    {
        readonly Func<NarrativeContext, long> _get; readonly string _op; readonly long _value;
        public CompareCondition(Func<NarrativeContext, long> get, string op, long value) { _get = get; _op = op; _value = value; }

        public override bool Evaluate(NarrativeContext c)
        {
            long v = _get(c);
            switch (_op)
            {
                case ">=": return v >= _value;
                case ">": return v > _value;
                case "<=": return v <= _value;
                case "<": return v < _value;
                case "!=": return v != _value;
                default: return v == _value;
            }
        }
    }

    sealed class SinceFactCondition : Condition
    {
        readonly string _id; readonly int _min, _max;
        public SinceFactCondition(string id, int min, int max) { _id = id; _min = min; _max = max; }

        public override bool Evaluate(NarrativeContext c)
        {
            var set = c.Facts.MonthSet(_id);
            if (set == null) return false;
            int since = c.Month - set.Value;
            return since >= _min && since <= _max;
        }
    }

    sealed class FuncCondition : Condition
    {
        readonly Func<NarrativeContext, bool> _f;
        public FuncCondition(Func<NarrativeContext, bool> f) { _f = f; }
        public override bool Evaluate(NarrativeContext c) => _f(c);
    }

    public sealed class ConditionJsonConverter : JsonConverter<Condition>
    {
        public override void WriteJson(JsonWriter writer, Condition value, JsonSerializer serializer) => value.Raw.WriteTo(writer);
        public override Condition ReadJson(JsonReader reader, Type objectType, Condition existingValue, bool hasExistingValue, JsonSerializer serializer) =>
            Condition.Parse(JObject.Load(reader));
    }
}
