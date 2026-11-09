using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// Which model stands on a lot for each industry, by number of units open: a coffee cart becomes a coffee
    /// shop at two units, a box truck a warehouse. Vehicles and carts keep their own size on the lot.
    /// </summary>
    [CreateAssetMenu(menuName = "Unpaid CEO/City Catalog")]
    public sealed class CityCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string industryId;
            [Tooltip("Smallest number of open units that shows this model (0 or 1 = from the first unit).")]
            public int minUnits;
            public GameObject building;
            [Tooltip("Largest horizontal side on the lot in metres; 0 = the lot's own footprint.")]
            public float footprint;
            [Tooltip("Turn on the lot (degrees) so the model's front faces the street; generated models differ.")]
            public float yaw;
        }

        public GameObject defaultBuilding;
        public List<Entry> entries = new List<Entry>();

        public GameObject BuildingFor(string industryId) => BuildingFor(industryId, 1);

        public GameObject BuildingFor(string industryId, int units) => TryFind(industryId, units, out var e) ? e.building : defaultBuilding;

        /// <summary>Footprint for the model <see cref="BuildingFor(string, int)"/> returns; 0 = the lot decides.</summary>
        public float FootprintFor(string industryId, int units) => TryFind(industryId, units, out var e) ? e.footprint : 0f;

        /// <summary>Turn on the lot for the model <see cref="BuildingFor(string, int)"/> returns.</summary>
        public float YawFor(string industryId, int units) => TryFind(industryId, units, out var e) ? e.yaw : 0f;

        bool TryFind(string industryId, int units, out Entry best)
        {
            best = default;
            int open = Mathf.Max(1, units), bestMin = 0;
            bool found = false;
            foreach (var e in entries)
            {
                int min = Mathf.Max(1, e.minUnits);
                if (e.industryId != industryId || e.building == null || min > open || (found && min <= bestMin)) continue;
                best = e;
                bestMin = min;
                found = true;
            }
            return found;
        }
    }
}
