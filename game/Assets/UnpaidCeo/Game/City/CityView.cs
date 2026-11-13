using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;

namespace UnpaidCeo.Game.City
{
    /// <summary>Keeps the district in step with the game: a business with a lot gets its building, a closed one frees the lot.</summary>
    public sealed class CityView : MonoBehaviour
    {
        public const string BronxAddress = "district_bronx";
        public const string RoomAddress = "room";

        [SerializeField] CityCatalog catalog;
        [SerializeField] List<LotView> lots = new List<LotView>();
        [SerializeField] Transform homeAnchor;

        public IReadOnlyList<LotView> Lots => lots;
        public CityCatalog Catalog => catalog;
        public Transform HomeAnchor => homeAnchor;

        public const string WindowGlowProperty = "_WindowGlow";
        static readonly int WindowGlowId = Shader.PropertyToID(WindowGlowProperty);

        /// <summary>Stage 5B · the block's buildings light their windows in the evening (props, cars and people do not).</summary>
        void Start() { foreach (var root in gameObject.scene.GetRootGameObjects()) LightWindows(root); }

        static bool InBuilding(Transform t)
        {
            for (var p = t; p != null; p = p.parent) if (p.name.StartsWith("building_")) return true;
            return false;
        }

        public static void LightWindows(GameObject root)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (!InBuilding(r.transform)) continue;
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.HasProperty(WindowGlowId)) m.SetFloat(WindowGlowId, 1f);
            }
        }

        public void Configure(CityCatalog c, List<LotView> l, Transform home)
        {
            catalog = c;
            lots = l;
            homeAnchor = home;
        }

        /// <summary>
        /// A business with a fixed lot stands there; one without takes a lot once its first unit opens (the lot it
        /// already stands on, else the first free one). The model follows the number of units (cart → shop).
        /// </summary>
        public void Sync(GameState s)
        {
            var byLot = new Dictionary<string, BusinessState>();
            foreach (var b in s.Businesses)
                if (!string.IsNullOrEmpty(b.LotId) && !byLot.ContainsKey(b.LotId)) byLot[b.LotId] = b;
            var pending = new List<BusinessState>();
            foreach (var b in s.Businesses.Where(b => string.IsNullOrEmpty(b.LotId) && b.Units > 0))
            {
                var current = lots.FirstOrDefault(l => l.BusinessId == b.Id && !byLot.ContainsKey(l.LotId));
                if (current != null) byLot[current.LotId] = b; else pending.Add(b);
            }
            foreach (var b in pending)
            {
                var free = lots.FirstOrDefault(l => !byLot.ContainsKey(l.LotId));
                if (free == null) break;
                byLot[free.LotId] = b;
            }

            foreach (var lot in lots)
            {
                if (byLot.TryGetValue(lot.LotId, out var business))
                {
                    var prefab = catalog != null ? catalog.BuildingFor(business.IndustryId, business.Units) : null;
                    bool same = lot.BusinessId == business.Id && (prefab == null ? lot.Building == null : lot.Building != null && lot.Building.name == prefab.name);
                    if (!same && catalog != null)
                        lot.Place(prefab, business.Id, catalog.FootprintFor(business.IndustryId, business.Units), catalog.YawFor(business.IndustryId, business.Units));
                    else if (!same) lot.Place(null, business.Id);
                }
                else if (lot.Building != null) lot.Clear();
            }
        }

        /// <summary>Where money floats up during the month timelapse: occupied lots, or home while the player only has gigs.</summary>
        public IEnumerable<Vector3> MoneyAnchors()
        {
            var occupied = lots.Where(l => l.Building != null).Select(l => ModelFit.RendererBounds(l.Building)).ToList();
            if (occupied.Count == 0 && homeAnchor != null) return new[] { homeAnchor.position };
            return occupied.Select(b => new Vector3(b.center.x, b.max.y, b.center.z));
        }
    }
}
