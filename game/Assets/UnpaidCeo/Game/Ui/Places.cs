using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnpaidCeo.Game.Ui
{
    /// <summary>What a button at a place does.</summary>
    public enum PlaceActionKind
    {
        /// <summary>Puts one of the month's free actions into the plan (and takes it back out).</summary>
        Plan,
        /// <summary>Opens another screen (the month plan, the report, lessons…).</summary>
        Open,
        /// <summary>Opens the bank's loan dialog.</summary>
        Loan,
    }

    public sealed class PlaceAction
    {
        /// <summary>Unique among all places; the button is "place-action-&lt;Id&gt;" and its text "place.action.&lt;Id&gt;".</summary>
        public string Id;
        public PlaceActionKind Kind;
        /// <summary>Id in free_actions.json (Plan).</summary>
        public string FreeAction;
        /// <summary>A <see cref="UiRoot"/> tab (Open).</summary>
        public string Tab;
        public int MinChapter = 1;
    }

    /// <summary>
    /// A place on the block: who hosts it, which buildings carry its marker, what can be done there.
    /// Strings: place.&lt;id&gt;.title, .host, .line.1-3. Picture: Addressable "place_&lt;id&gt;".
    /// </summary>
    public sealed class PlaceDef
    {
        public string Id;
        /// <summary>Id in npcs.json; the trust shown is the relationship with this character.</summary>
        public string Host;
        /// <summary>Building prefab names; the first one found in a district carries the marker.</summary>
        public string[] Buildings = new string[0];
        public PlaceAction[] Actions = new PlaceAction[0];
    }

    /// <summary>
    /// The places of the city. Every free action of the month has a place where it is done (a test keeps it so):
    /// rest at home, overtime at Tony's, networking at the bar, a loan at the pawnshop, the reserve at the bank…
    /// </summary>
    public static class Places
    {
        public const string Home = "home";
        public const int LinesPerPlace = 3;
        /// <summary>Shop-sign height on the shop front (row houses are ~9 m deep, their fronts ~4.5 m from the centre).</summary>
        const float MarkerHeight = 3.2f, TowardStreet = 5f;

        static PlaceAction Plan(string freeAction) => new PlaceAction { Id = freeAction, Kind = PlaceActionKind.Plan, FreeAction = freeAction };
        static PlaceAction Open(string id, string tab, int minChapter = 1) => new PlaceAction { Id = id, Kind = PlaceActionKind.Open, Tab = tab, MinChapter = minChapter };

        public static IReadOnlyList<PlaceDef> All { get; } = new List<PlaceDef>
        {
            new PlaceDef { Id = Home, Host = "mentor",
                Actions = new[] { Plan("rest"), Plan("family_time"), Plan("sunday_dinner"), Open("journal", UiRoot.Journal) } },
            new PlaceDef { Id = "bodega", Host = "tony", Buildings = new[] { "building_bodega" },
                Actions = new[] { Plan("overtime"), Plan("night_shift"), Plan("weekend_shift") } },
            new PlaceDef { Id = "bar", Host = "best_friend", Buildings = new[] { "building_pizza", "building_bakery" },
                Actions = new[] { Plan("networking"), Plan("pitch_night"), Plan("hobby") } },
            new PlaceDef { Id = "office", Host = "cofounder_sam", Buildings = new[] { "building_laundromat", "building_barbershop" },
                Actions = new[] { Open("plan", UiRoot.Decisions), Open("report", UiRoot.Company), Open("business", UiRoot.Industry, minChapter: 2) } },
            new PlaceDef { Id = "pawnshop", Host = "loan_shark", Buildings = new[] { "building_pawn_shop" },
                Actions = new[] { Plan("loan_shark_loan"), Plan("gray_scheme") } },
            new PlaceDef { Id = "bank", Host = "banker_lee", Buildings = new[] { "building_bank", "building_check_cashing" },
                Actions = new[] { Plan("reserve_transfer"), Plan("budget_review"), new PlaceAction { Id = "loan", Kind = PlaceActionKind.Loan, MinChapter = 2 } } },
            new PlaceDef { Id = "academy", Host = "mentor", Buildings = new[] { "building_academy", "building_church" },
                Actions = new[] { Plan("study"), Plan("certificate_course"), Open("lessons", UiRoot.Academy) } },
        };

        public static PlaceDef Find(string id) => All.FirstOrDefault(p => p.Id == id);

        /// <summary>Addressable address of a place's 4K picture, loaded when the player walks in.</summary>
        public static string ArtAddress(string id) => "place_" + id;
        /// <summary>The card's small picture in the list of places (768x512).</summary>
        public static string ThumbAddress(string id) => "place_thumb_" + id;

        /// <summary>
        /// Where each place's marker hangs in a district: over the street side of its building (the row faces the
        /// avenue at z = 0), at home for Home. A place with no building in the district gets no marker; the list of
        /// places still has it.
        /// </summary>
        public static Dictionary<string, Vector3> FindSpots(IEnumerable<Transform> all, Transform home)
        {
            var spots = new Dictionary<string, Vector3>();
            var list = all.Where(t => t != null).ToList();
            foreach (var place in All)
            {
                var at = place.Id == Home ? home : list.FirstOrDefault(t => place.Buildings.Any(b => t.name.StartsWith(b)));
                if (at == null) continue;
                var p = at.position;
                spots[place.Id] = new Vector3(p.x, MarkerHeight, p.z + (p.z > 0f ? -TowardStreet : TowardStreet));
            }
            return spots;
        }

        const string BuildingPrefix = "building_";
        /// <summary>How near the home spot the hero's house stands (row houses are ~8 m wide, the spot is on its stoop).</summary>
        const float HomeReach = 12f;

        /// <summary>
        /// Each place's building by its top transform — the same building that carries its sign (see <see cref="FindSpots"/>);
        /// Home is the house nearest the home spot. A tap on any part of a building finds its place with <see cref="PlaceOf"/>.
        /// </summary>
        public static Dictionary<Transform, string> FindBuildings(IEnumerable<Transform> all, Transform home)
        {
            var map = new Dictionary<Transform, string>();
            var list = all.Where(t => t != null).ToList();
            foreach (var place in All)
            {
                if (place.Id == Home) continue;
                var at = list.FirstOrDefault(t => place.Buildings.Any(b => t.name.StartsWith(b)));
                if (at != null) map[BuildingRoot(at)] = place.Id;
            }
            if (home != null)
            {
                Transform best = null;
                float nearest = HomeReach;
                foreach (var root in list.Where(t => t.name.StartsWith(BuildingPrefix)).Select(BuildingRoot).Distinct())
                {
                    if (map.ContainsKey(root)) continue;
                    float d = Vector2.Distance(new Vector2(root.position.x, root.position.z), new Vector2(home.position.x, home.position.z));
                    if (d < nearest) { nearest = d; best = root; }
                }
                if (best != null) map[best] = Home;
            }
            return map;
        }

        /// <summary>The place whose building was hit (a wall, a roof, an awning of it), or null.</summary>
        public static string PlaceOf(Transform hit, IReadOnlyDictionary<Transform, string> buildings)
        {
            for (var t = hit; t != null; t = t.parent)
                if (buildings.TryGetValue(t, out var id)) return id;
            return null;
        }

        /// <summary>The outermost "building_…" transform above (or at) a part of a building.</summary>
        static Transform BuildingRoot(Transform part)
        {
            var root = part;
            for (var t = part.parent; t != null; t = t.parent)
                if (t.name.StartsWith(BuildingPrefix)) root = t;
            return root;
        }
    }
}
