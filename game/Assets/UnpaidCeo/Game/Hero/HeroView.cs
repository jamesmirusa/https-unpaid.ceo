using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Core.Primitives;
using UnpaidCeo.Game.City;

namespace UnpaidCeo.Game.Hero
{
    /// <summary>
    /// Shows the player's hero: loads the species/outfit model through Addressables (so heroes are not in the
    /// initial web download), sizes it to 1.6 m and applies the chosen colour as a tint.
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        public const float Height = 1.6f;
        public static readonly Color[] Tints =
        {
            Color.white, new Color(1f, 0.9f, 0.78f), new Color(0.82f, 0.9f, 1f), new Color(0.86f, 1f, 0.88f),
        };

        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        AsyncOperationHandle<GameObject> _handle;
        MaterialPropertyBlock _block;

        public GameObject Model { get; private set; }
        public string LoadedAddress { get; private set; }

        public static string Address(HeroAppearance a)
        {
            int outfit = Mathf.Clamp(a.Outfit, 0, HeroCreator.Outfits.Length - 1);
            string species = System.Array.IndexOf(HeroCreator.Species, a.Species) >= 0 ? a.Species : HeroCreator.Species[0];
            return $"hero_{species}_{HeroCreator.Outfits[outfit]}";
        }

        public IEnumerator Apply(HeroAppearance a)
        {
            var address = Address(a);
            if (address != LoadedAddress)
            {
                Release();
                _handle = Addressables.InstantiateAsync(address, transform);
                yield return _handle;
                if (_handle.Status != AsyncOperationStatus.Succeeded) { Debug.LogError($"Hero '{address}' failed to load"); yield break; }
                Model = _handle.Result;
                Model.name = address;
                Model.transform.localPosition = Vector3.zero;
                Model.transform.localRotation = Quaternion.identity;
                ModelFit.ToHeight(Model, Height);
                LoadedAddress = address;
            }
            Tint(a.ColorIndex);
        }

        /// <summary>Cheers after a month that made money, thinks after one that did not.</summary>
        public void React(MonthReport report) =>
            GetComponentInChildren<CharacterAnimator>()?.PlayOnce(report.NetProfit > Money.Zero ? CharacterAnimator.State.Cheer : CharacterAnimator.State.Think);

        /// <summary>Plays the think clip, e.g. while the player picks a dialogue option.</summary>
        public void Think() => GetComponentInChildren<CharacterAnimator>()?.PlayOnce(CharacterAnimator.State.Think);

        void Tint(int index)
        {
            if (Model == null) return;
            _block ??= new MaterialPropertyBlock();
            var tint = Tints[Mathf.Clamp(index, 0, Tints.Length - 1)];
            foreach (var r in Model.GetComponentsInChildren<Renderer>())
            {
                r.GetPropertyBlock(_block);
                _block.SetColor(BaseColor, tint);
                r.SetPropertyBlock(_block);
            }
        }

        void Release()
        {
            if (_handle.IsValid()) Addressables.ReleaseInstance(_handle);
            Model = null;
            LoadedAddress = null;
        }

        void OnDestroy() => Release();
    }
}
