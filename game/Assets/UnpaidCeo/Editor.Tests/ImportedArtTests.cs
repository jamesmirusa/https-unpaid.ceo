using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnpaidCeo.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>Every generated prefab in Art/Prefabs stays within the art budget (brief Task 13, Step 2).</summary>
    public class ImportedArtTests
    {
        const string Prefabs = "Assets/UnpaidCeo/Art/Prefabs";

        static List<GameObject> Load(string prefix, params string[] more)
        {
            var prefixes = new[] { prefix }.Concat(more).ToArray();
            return AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => prefixes.Any(x => System.IO.Path.GetFileName(p).StartsWith(x)))
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>)
                .ToList();
        }

        static void AssertBudget(List<GameObject> prefabs, AssetKind kind, int minimum)
        {
            Assert.GreaterOrEqual(prefabs.Count, minimum, $"expected at least {minimum} {kind} prefabs in {Prefabs}");
            var errors = prefabs.SelectMany(p => AssetBudgetValidator.Check(p, kind)).ToList();
            Assert.IsEmpty(errors, string.Join("\n", errors));
            var oversized = prefabs.SelectMany(p => p.GetComponentsInChildren<Renderer>(true))
                .SelectMany(r => r.sharedMaterials).Where(m => m != null)
                .Select(m => m.GetTexture("_BaseMap")).Where(t => t != null && t.width > GlbPrefabBuilder.MaxTextureSize(kind))
                .Select(t => $"{t.name}: {t.width}px").Distinct().ToList();
            Assert.IsEmpty(oversized, "textures above the web cap:\n" + string.Join("\n", oversized));
        }

        [Test] public void All_imported_buildings_within_budget() => AssertBudget(Load("building_"), AssetKind.Building, 12);
        [Test] public void All_imported_props_within_budget() => AssertBudget(Load("prop_"), AssetKind.Prop, 14);
        [Test] public void All_characters_within_budget() => AssertBudget(Load("hero_", "npc_"), AssetKind.Character, 11);

        static readonly string[] KeyBuildings =
        {
            "building_bodega", "building_walkup", "building_laundromat", "building_pawn_shop",
            "building_coffee_shop", "building_barbershop", "building_bakery", "building_car_wash",
        };

        [Test]
        public void Every_character_has_idle_walk_cheer_think()
        {
            var characters = Load("hero_", "npc_");
            Assert.GreaterOrEqual(characters.Count, 11);
            foreach (var p in characters)
            {
                var animator = p.GetComponent<UnpaidCeo.Game.Hero.CharacterAnimator>();
                Assert.IsNotNull(animator, $"{p.name} has no CharacterAnimator");
                foreach (UnpaidCeo.Game.Hero.CharacterAnimator.State s in System.Enum.GetValues(typeof(UnpaidCeo.Game.Hero.CharacterAnimator.State)))
                    Assert.IsNotNull(animator.Clip(s), $"{p.name} is missing its {s} clip");
            }
        }

        [Test]
        public void Key_buildings_and_characters_have_normal_maps()
        {
            var prefabs = Load("hero_", "npc_");
            prefabs.AddRange(KeyBuildings.Select(n => AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{n}.prefab")));
            foreach (var p in prefabs)
            {
                Assert.IsNotNull(p);
                var mats = p.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null).ToList();
                Assert.IsTrue(mats.Count > 0 && mats.All(m => m.GetTexture("_BumpMap") != null && m.IsKeywordEnabled("_NORMALMAP")),
                    $"{p.name} has a material without a normal map");
            }
        }

        [Test]
        public void Every_hero_species_has_both_outfits()
        {
            var names = Load("hero_").Select(p => p.name).ToList();
            foreach (var species in new[] { "raccoon", "fox", "cat" })
                foreach (var outfit in new[] { "hoodie", "blazer" })
                    CollectionAssert.Contains(names, $"hero_{species}_{outfit}");
        }
    }
}
