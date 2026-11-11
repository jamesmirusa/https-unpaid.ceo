using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnpaidCeo.Game.Hero;

namespace UnpaidCeo.Game.City
{
    /// <summary>
    /// People on the district's sidewalks. Every district is built on the same plan (an avenue along X, a side street
    /// along Z, sidewalks 6.5 m from each centre line, the row of houses 13.5 m north of the avenue), so the lanes are
    /// known without scene data. Walkers appear at once and move; their models stream in through Addressables.
    /// </summary>
    public sealed class StreetLife : MonoBehaviour
    {
        /// <summary>Sidewalk centre lines: z = ±6.5 along the avenue, x = ±6.5 along the side street.</summary>
        public const float SidewalkOffset = 6.5f;
        public const float SidewalkTop = 0.16f;
        public const float WalkerHeight = 2.2f;
        const float AvenueHalf = 58f, SideFrom = 9.5f, SideTo = 40f, Curb = 0.9f;

        /// <summary>Character prefabs (Addressables) used for the crowd.</summary>
        public static readonly string[] Looks =
        {
            "hero_cat_blazer", "hero_fox_hoodie", "hero_raccoon_blazer", "hero_cat_hoodie", "hero_fox_blazer", "hero_raccoon_hoodie",
        };

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        readonly List<Pedestrian> _walkers = new List<Pedestrian>();
        readonly List<AsyncOperationHandle<GameObject>> _handles = new List<AsyncOperationHandle<GameObject>>();

        public IReadOnlyList<Pedestrian> Walkers => _walkers;

        /// <summary>Someone who lives on the block and stands at their place: where, facing where, and the name tag's string key.</summary>
        public readonly struct Resident
        {
            public readonly string Address;
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly string TagKey;
            public Resident(string address, Vector3 position, Quaternion rotation, string tagKey) { Address = address; Position = position; Rotation = rotation; TagKey = tagKey; }
        }

        /// <summary>A resident standing in the scene.</summary>
        public sealed class Standing
        {
            public string Address;
            public string TagKey;
            public Transform Transform;
        }

        readonly List<Standing> _residents = new List<Standing>();
        public IReadOnlyList<Standing> Residents => _residents;

        /// <summary>
        /// Who stands on the district's block: the raccoon mentor beside the hero's door (every district: he moves with
        /// the hero), and in the Bronx Tony in front of his bodega next door.
        /// </summary>
        public static IReadOnlyList<Resident> ResidentsFor(string district, Transform home)
        {
            var list = new List<Resident>();
            if (home == null) return list;
            var p = home.position + home.forward * 6.1f + home.right * 3.6f;   // the building side of the sidewalk
            list.Add(new Resident("npc_mentor_raccoon", new Vector3(p.x, SidewalkTop, p.z), home.rotation, "city.mentor"));
            if (district == "bronx")
                list.Add(new Resident("npc_tony_bulldog", new Vector3(21.5f, SidewalkTop, 7.4f), Quaternion.Euler(0f, 180f, 0f), "city.tony"));
            return list;
        }

        /// <summary>Where the hero stands: on the sidewalk straight out of the home's front door.</summary>
        public static Vector3 HomeFront(Transform home)
        {
            var p = home.position + home.forward * 7f;
            return new Vector3(p.x, SidewalkTop, p.z);
        }

        /// <summary>The stretches people walk: both avenue sidewalks end to end, and the side street's four arms.</summary>
        public static IReadOnlyList<(Vector3 a, Vector3 b)> Lanes()
        {
            var lanes = new List<(Vector3, Vector3)>();
            foreach (float z in new[] { SidewalkOffset - Curb, -(SidewalkOffset - Curb) })
                lanes.Add((new Vector3(-AvenueHalf, SidewalkTop, z), new Vector3(AvenueHalf, SidewalkTop, z)));
            foreach (float x in new[] { -SidewalkOffset, SidewalkOffset })
            {
                lanes.Add((new Vector3(x, SidewalkTop, SideFrom), new Vector3(x, SidewalkTop, SideTo)));
                lanes.Add((new Vector3(x, SidewalkTop, -SideFrom), new Vector3(x, SidewalkTop, -SideTo)));
            }
            return lanes;
        }

        /// <param name="spawn">Makes a walker's model under its parent (tests); by default models load through Addressables.</param>
        public void Begin(int seed, int count = 10, Func<string, Transform, GameObject> spawn = null, IReadOnlyList<Resident> residents = null)
        {
            var random = new System.Random(seed);
            var lanes = Lanes();
            for (int i = 0; i < count; i++)
            {
                // Two of every three walk the avenue (the lanes the camera opens on), the rest the side street.
                var (a, b) = i % 3 != 2 ? lanes[i % 2] : lanes[2 + (i / 3) % 4];
                var go = new GameObject($"walker_{i + 1}");
                go.transform.SetParent(transform, false);
                var walker = go.AddComponent<Pedestrian>();
                walker.Configure(a, b, speed: 1.1f + (float)random.NextDouble() * 0.7f, restSeconds: 1f + (float)random.NextDouble() * 3f,
                    startAt: (float)random.NextDouble(), towardB: random.Next(2) == 0);
                _walkers.Add(walker);

                string look = Looks[(i + seed) % Looks.Length];
                var tint = HeroView.Tints[(i / Looks.Length + i) % HeroView.Tints.Length];
                if (spawn != null) Dress(spawn(look, go.transform), tint, WalkerHeight);
                else StartCoroutine(Load(look, go.transform, tint, WalkerHeight));
            }

            if (residents == null) return;
            foreach (var r in residents)
            {
                var go = new GameObject("resident_" + r.Address);
                go.transform.SetParent(transform, false);
                go.transform.SetPositionAndRotation(r.Position, r.Rotation);
                _residents.Add(new Standing { Address = r.Address, TagKey = r.TagKey, Transform = go.transform });
                if (spawn != null) Dress(spawn(r.Address, go.transform), Color.white, HeroView.Height);
                else StartCoroutine(Load(r.Address, go.transform, Color.white, HeroView.Height));
            }
        }

        IEnumerator Load(string address, Transform parent, Color tint, float height)
        {
            var handle = Addressables.InstantiateAsync(address, parent);
            _handles.Add(handle);
            yield return handle;
            if (handle.Status == AsyncOperationStatus.Succeeded) Dress(handle.Result, tint, height);
        }

        static void Dress(GameObject model, Color tint, float height)
        {
            if (model == null) return;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            ModelFit.ToHeight(model, height);
            var block = new MaterialPropertyBlock();
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(block);
                block.SetColor(BaseColor, tint);
                r.SetPropertyBlock(block);
            }
        }

        void OnDestroy()
        {
            foreach (var h in _handles) if (h.IsValid()) Addressables.ReleaseInstance(h);
            _handles.Clear();
        }
    }
}
