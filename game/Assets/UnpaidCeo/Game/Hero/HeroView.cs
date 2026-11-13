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
    /// initial web download), sizes it and applies the chosen colour as a tint.
    /// </summary>
    public sealed class HeroView : MonoBehaviour
    {
        /// <summary>Taller than life (the row houses are ~12 m): from the city camera a 1.6 m hero is a few pixels.</summary>
        public const float Height = 2.6f;
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
                ModelFit.Stand(Model, Height);
                LoadedAddress = address;
            }
            Tint(a.ColorIndex);
        }

        /// <summary>Cheers after a month that made money, thinks after one that did not.</summary>
        public void React(MonthReport report) =>
            GetComponentInChildren<CharacterAnimator>()?.PlayOnce(report.NetProfit > Money.Zero ? CharacterAnimator.State.Cheer : CharacterAnimator.State.Think);

        /// <summary>Plays the think clip, e.g. while the player picks a dialogue option.</summary>
        public void Think() => GetComponentInChildren<CharacterAnimator>()?.PlayOnce(CharacterAnimator.State.Think);

        /// <summary>A copy of the hero as it looks now (model, clips, tint) for a room; the caller owns and destroys it.</summary>
        public GameObject CopyForStage()
        {
            if (Model == null) return null;
            var copy = Instantiate(Model);
            copy.name = Model.name;
            TintModel(copy, _tint);
            return copy;
        }

        int _tint;

        void Tint(int index)
        {
            _tint = index;
            if (Model != null) TintModel(Model, index);
        }

        void TintModel(GameObject model, int index)
        {
            _block ??= new MaterialPropertyBlock();
            var tint = Tints[Mathf.Clamp(index, 0, Tints.Length - 1)];
            foreach (var r in model.GetComponentsInChildren<Renderer>())
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
