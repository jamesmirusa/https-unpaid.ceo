using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>Which building prefab stands on a lot for each industry.</summary>
    [CreateAssetMenu(menuName = "Unpaid CEO/City Catalog")]
    public sealed class CityCatalog : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string industryId;
            public GameObject building;
        }

        public GameObject defaultBuilding;
        public List<Entry> entries = new List<Entry>();

        public GameObject BuildingFor(string industryId)
        {
            foreach (var e in entries) if (e.industryId == industryId && e.building != null) return e.building;
            return defaultBuilding;
        }
    }
}
