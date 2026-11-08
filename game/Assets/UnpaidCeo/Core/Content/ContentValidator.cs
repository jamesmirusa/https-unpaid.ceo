using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Economy;

namespace UnpaidCeo.Core.Content
{
    public sealed class ValidationResult
    {
        public List<string> Errors { get; } = new List<string>();
        public bool IsValid => Errors.Count == 0;
    }

    public static class ContentValidator
    {
        public static ValidationResult Validate(ContentDb db)
        {
            var r = new ValidationResult();

            foreach (var g in db.IndustryList.GroupBy(i => i.Id).Where(g => g.Count() > 1))
                r.Errors.Add($"industry {g.Key}: duplicate id");
            foreach (var i in db.IndustryList)
            {
                if (string.IsNullOrEmpty(i.Id)) r.Errors.Add("industry: missing id");
                if (i.Price.Cents < 0) r.Errors.Add($"industry {i.Id}: negative price");
                if (i.UnitCost.Cents < 0) r.Errors.Add($"industry {i.Id}: negative unitCost");
                if (i.RefPrice.Cents <= 0) r.Errors.Add($"industry {i.Id}: refPrice must be > 0");
                if (i.MarketingK.Cents <= 0) r.Errors.Add($"industry {i.Id}: marketingK must be > 0");
                if (i.Kind == IndustryKind.Gig && i.CapacityPerWorkPoint <= 0)
                    r.Errors.Add($"industry {i.Id}: gig needs capacityPerWorkPoint > 0");
                if (i.Kind == IndustryKind.Business && i.CapacityPerUnit <= 0)
                    r.Errors.Add($"industry {i.Id}: business needs capacityPerUnit > 0");
            }

            var b = db.Balance;
            if (b.TaxRate < 0 || b.TaxRate > 1) r.Errors.Add("balance.taxRate: must be within [0, 1]");
            if (b.EventsPerMonthWeights == null || b.EventsPerMonthWeights.Length != 3 ||
                b.EventsPerMonthWeights.Any(w => w < 0) || b.EventsPerMonthWeights.Sum() <= 0)
                r.Errors.Add("balance.eventsPerMonthWeights: need 3 non-negative weights with a positive sum");
            if (b.AutopilotHoursPerMonth <= 0) r.Errors.Add("balance.autopilotHoursPerMonth: must be > 0");
            if (b.AutopilotMaxMonths < 0) r.Errors.Add("balance.autopilotMaxMonths: must be >= 0");
            foreach (var id in b.Chapter1Gigs.Where(id => !db.Industries.ContainsKey(id)))
                r.Errors.Add($"balance.chapter1Gigs: unknown industry '{id}'");

            foreach (var g in db.FreeActionList.GroupBy(a => a.Id).Where(g => g.Count() > 1))
                r.Errors.Add($"freeAction {g.Key}: duplicate id");
            foreach (var a in db.FreeActionList.Where(a => a.ActionPoints < 0))
                r.Errors.Add($"freeAction {a.Id}: negative actionPoints");

            foreach (var g in db.NpcList.GroupBy(n => n.Id).Where(g => g.Count() > 1))
                r.Errors.Add($"npc {g.Key}: duplicate id");

            foreach (var g in db.Storylets.GroupBy(x => x.Id).Where(g => g.Count() > 1))
                r.Errors.Add($"storylet {g.Key}: duplicate id");
            foreach (var st in db.Storylets)
            {
                if (string.IsNullOrEmpty(st.Id)) r.Errors.Add("storylet: missing id");
                if (string.IsNullOrEmpty(st.YarnNode)) r.Errors.Add($"storylet {st.Id}: missing yarnNode");
                if (st.Weight <= 0) r.Errors.Add($"storylet {st.Id}: weight must be > 0");
                if (st.Choices == null || st.Choices.Count == 0) r.Errors.Add($"storylet {st.Id}: needs at least one choice");
            }

            return r;
        }
    }
}
