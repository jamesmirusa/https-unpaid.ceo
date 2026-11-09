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

        /// <param name="size">Largest horizontal side in metres (a cart or truck is smaller than the lot); 0 = the lot's footprint.</param>
        /// <param name="yaw">Turn in degrees so the model's front faces the street.</param>
        public void Place(GameObject prefab, string businessId, float size = 0f, float yaw = 0f)
        {
            Clear();
            if (prefab == null) return;
            Building = Instantiate(prefab, transform);
            Building.name = prefab.name;
            Building.transform.localPosition = Vector3.zero;
            Building.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            ModelFit.ToFootprint(Building, size > 0f ? Mathf.Min(size, footprint) : footprint);
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
