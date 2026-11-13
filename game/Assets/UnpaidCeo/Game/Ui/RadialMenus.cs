using System.Collections.Generic;
using System.Linq;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Narrative;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>
    /// Which items a target in the city offers (stage 5B, "tap anything"). Built from the places table and the week's
    /// shared <see cref="ChoiceState"/> (through <see cref="DecisionsScreen"/>): the rules are the cards' rules, only the
    /// way to tap changes. Targets: "place:&lt;id&gt;", "resident:&lt;address&gt;", "hero", "room:&lt;id&gt;".
    /// </summary>
    public static class RadialMenus
    {
        /// <summary>Which place a resident of the street belongs to.</summary>
        public static string PlaceOfResident(string address) => address switch
        {
            "npc_tony_bulldog" => "bodega",
            "npc_mentor_raccoon" => "academy",
            _ => null,
        };

        /// <summary>The hero's own things: sleep, study, call mom, the week, the journal.</summary>
        static readonly string[] HeroDeeds = { "rest", "study", "family_time" };

        public static string TitleKey(string target)
        {
            if (target == "hero") return "city.you";
            var (kind, id) = Split(target);
            if (kind == "resident") return Places.Find(PlaceOfResident(id) ?? "")?.Host is string host ? "npc." + host : null;
            return $"place.{id}.title";
        }

        public static List<RadialItem> For(string target, UiRoot ui)
        {
            var items = new List<RadialItem>();
            var plan = ui.DecisionsPanel;
            if (plan == null) return items;
            if (target == "hero")
            {
                foreach (var d in HeroDeeds) items.Add(Deed(d, plan, () => plan.Plan(d)));
                items.RemoveAll(i => i.State == ChoiceState.Locked);
                items.Add(new RadialItem { Id = "call_mom", LabelKey = "radial.call_mom", GameIcon = "close_ones", HintKey = "radial.hint.call_mom",
                    Pending = ui.State != null && HostScenes.HasScene(ui.State, ui.Engine.Content, "mom"), Run = () => ui.Talk("mom") });
                items.Add(new RadialItem { Id = "week", LabelKey = "dock.decisions", ServiceIcon = "calendar-dots", HintKey = "radial.hint.week", Run = () => ui.Open(UiRoot.Decisions) });
                items.Add(new RadialItem { Id = "journal", LabelKey = "hud.journal", ServiceIcon = "book-open-text", HintKey = "radial.hint.journal", Run = () => ui.Open(UiRoot.Journal) });
                return items;
            }
            var (kind, id) = Split(target);
            string placeId = kind == "resident" ? PlaceOfResident(id) : id;
            var place = placeId != null ? Places.Find(placeId) : null;
            if (place == null) return items;
            bool room = kind == "room";
            if (kind == "resident" || room)
                items.Add(new RadialItem { Id = "talk", LabelKey = "visit.talk", ServiceIcon = "chat-circle-dots", First = true, HintKey = "radial.hint.talk",
                    Pending = ui.State != null && HostScenes.HasScene(ui.State, ui.Engine.Content, place.Host),
                    Run = room ? (System.Action)(() => ui.Visit.Talk()) : () => ui.OpenPlace(place.Id) });
            else
                items.Add(new RadialItem { Id = "enter", LabelKey = "radial.enter", ServiceIcon = "door-open", First = true, HintKey = "radial.hint.enter", Run = () => ui.OpenPlace(place.Id) });
            foreach (var a in place.Actions.Where(a => ui.Chapter >= a.MinChapter))
            {
                var act = a;
                switch (a.Kind)
                {
                    case PlaceActionKind.Plan:
                        items.Add(Deed(a.FreeAction, plan, room ? (System.Action)(() => ui.Visit.Choose(act.Id)) : () => plan.Plan(act.FreeAction)));
                        break;
                    case PlaceActionKind.Loan:
                        items.Add(new RadialItem { Id = a.Id, LabelKey = "place.action." + a.Id, GameIcon = "loan", HintKey = "place.action." + a.Id,
                            Run = room ? (System.Action)(() => ui.Visit.Choose(act.Id)) : () => ui.Loan.Open(Core.Primitives.Money.Zero) });
                        break;
                    case PlaceActionKind.Open:
                        items.Add(new RadialItem { Id = a.Id, LabelKey = "place.action." + a.Id, ServiceIcon = TabIcon(a.Tab), HintKey = "place.action." + a.Id,
                            Run = room ? (System.Action)(() => ui.Visit.Choose(act.Id)) : () => ui.Open(act.Tab) });
                        break;
                }
            }
            items.RemoveAll(i => i.State == ChoiceState.Locked);   // not offered until a relationship opens it
            return items;
        }

        static RadialItem Deed(string freeAction, DecisionsScreen plan, System.Action run)
        {
            var def = plan.FreeAction(freeAction);
            var state = plan.State(freeAction);
            return new RadialItem
            {
                Id = freeAction, LabelKey = "action." + freeAction, GameIcon = Kit.GameIconFor.Action(freeAction),
                State = state, WeeksUntilOpen = plan.WeeksUntilOpen(freeAction),
                Recommended = state == ChoiceState.Open && plan.Recommended(freeAction),
                Confirm = def != null && def.Confirm, Cost = def?.Cost ?? default, Effects = def?.Effects, Run = run,
            };
        }

        static string TabIcon(string tab) => tab switch
        {
            UiRoot.Journal => "book-open-text", UiRoot.Decisions => "calendar-dots", UiRoot.Company => "chart-bar",
            UiRoot.Academy => "graduation-cap", UiRoot.Industry => "storefront", _ => "arrow-right",
        };

        static (string kind, string id) Split(string target)
        {
            int i = target.IndexOf(':');
            return i < 0 ? (target, null) : (target.Substring(0, i), target.Substring(i + 1));
        }

        /// <summary>
        /// The badge over a target: "emote_work" when one of its earning deeds is open now, "emote_lesson" over the
        /// academy while a lesson is new; null for none. ("!" and the level heart come with the dialogue step.)
        /// </summary>
        public static string Emote(string target, UiRoot ui)
        {
            var plan = ui.DecisionsPanel;
            if (plan == null || ui.RunMode || !ui.GuideEnabled) return null;   // no badge where a tap would not open a menu
            var (kind, id) = Split(target);
            string placeId = kind == "resident" ? PlaceOfResident(id) : id;
            var place = placeId != null ? Places.Find(placeId) : null;
            if (place == null) return null;
            if (ui.State != null && HostScenes.HasScene(ui.State, ui.Engine.Content, place.Host)) return "emote_scene";
            if (place.Id == "academy" && ui.NewLesson) return "emote_lesson";
            foreach (var a in place.Actions)
            {
                if (a.Kind != PlaceActionKind.Plan || plan.State(a.FreeAction) != ChoiceState.Open) continue;
                var def = plan.FreeAction(a.FreeAction);
                if (def != null && def.Effects.Any(e => e is Core.Narrative.CashEffect c && c.Amount.Cents > 0)) return "emote_work";
            }
            return null;
        }
    }
}
