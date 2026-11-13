using NUnit.Framework;
using UnpaidCeo.Editor;

namespace UnpaidCeo.Editor.Tests
{
    /// <summary>
    /// A prefab used by several districts lives in one shared Addressables group instead of a copy per district bundle:
    /// the Bronx's are downloaded at boot (city_common), Brooklyn and Queens share city_ch2 (after DistrictBuilder.BuildAll).
    /// </summary>
    public class DistrictGroupsTests
    {
        [TestCase("prop_street_lamp", DistrictBuilder.CommonGroup)]
        [TestCase("building_garden_lot", DistrictBuilder.CommonGroup)]
        [TestCase("building_pawn_shop", DistrictBuilder.CommonGroup)]
        [TestCase("building_bakery", DistrictBuilder.Chapter2Group)]
        [TestCase("building_office", DistrictBuilder.Chapter2Group)]
        [TestCase("prop_coffee_cart", DistrictBuilder.Chapter2Group)]
        [TestCase("building_warehouse", DistrictBuilder.Chapter2Group)]
        public void Shared_prefabs_live_in_a_shared_group(string prefab, string group) =>
            Assert.AreEqual(group, DistrictBuilder.SharedGroupOf(prefab));

        [TestCase("building_brownstone_row")]
        [TestCase("building_queens_house")]
        [TestCase("building_barbershop")]
        public void Single_district_prefabs_stay_in_their_scene_bundle(string prefab) =>
            Assert.IsNull(DistrictBuilder.SharedGroupOf(prefab));
    }
}
