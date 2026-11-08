using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        public Transform HomeAnchor => homeAnchor;

        public void Configure(CityCatalog c, List<LotView> l, Transform home)
        {
            catalog = c;
            lots = l;
            homeAnchor = home;
        }

        public void Sync(GameState s)
        {
            var byLot = s.Businesses.Where(b => !string.IsNullOrEmpty(b.LotId))
                .GroupBy(b => b.LotId).ToDictionary(g => g.Key, g => g.First());
            foreach (var lot in lots)
            {
                if (byLot.TryGetValue(lot.LotId, out var business))
                {
                    if (lot.BusinessId != business.Id) lot.Place(catalog != null ? catalog.BuildingFor(business.IndustryId) : null, business.Id);
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
