using UnityEngine;

namespace UnpaidCeo.Game.City
{
    /// <summary>An empty pad in the district where a business's building appears when it opens.</summary>
    public sealed class LotView : MonoBehaviour
    {
        [SerializeField] string lotId;
        [SerializeField] float footprint = 9f;
        [SerializeField] GameObject emptyMarker;

        public string LotId => lotId;
        public float Footprint => footprint;
        public GameObject Building { get; private set; }
        public string BusinessId { get; private set; }

        public void Configure(string id, float size, GameObject marker)
        {
            lotId = id;
            footprint = size;
            emptyMarker = marker;
        }

        public void Place(GameObject prefab, string businessId)
        {
            Clear();
            if (prefab == null) return;
            Building = Instantiate(prefab, transform);
            Building.name = prefab.name;
            Building.transform.localPosition = Vector3.zero;
            Building.transform.localRotation = Quaternion.identity;
            ModelFit.ToFootprint(Building, footprint);
            BusinessId = businessId;
            if (emptyMarker != null) emptyMarker.SetActive(false);
        }

        public void Clear()
        {
            if (Building != null)
            {
                if (Application.isPlaying) Destroy(Building);
                else DestroyImmediate(Building);
            }
            Building = null;
            BusinessId = null;
            if (emptyMarker != null) emptyMarker.SetActive(true);
        }
    }
}
