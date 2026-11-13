using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnpaidCeo.Core.Economy;
using UnpaidCeo.Core.Game;
using UnpaidCeo.Game.City;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Builds the district scenes from the generated prefabs — Bronx (chapter 1), Brooklyn (2) and Queens (3), Addressables
    /// "district_{name}" — on one shared block layout: an avenue and a cross street, a row of buildings facing the avenue,
    /// six lots for the player's units opposite, landmarks, street props, lamps, day/night volumes, camera and controllers.
    /// Also builds Scenes/Room.unity. A prefab used by more than one district goes to a shared group so no bundle carries
    /// a copy: "city_common" when the Bronx uses it (downloaded at boot), "city_ch2" when only Brooklyn and Queens do.
    /// Deterministic — re-run after re-importing art.
    /// </summary>
    public static class DistrictBuilder
    {
        const string Prefabs = "Assets/UnpaidCeo/Art/Prefabs";
        const string Scenes = "Assets/UnpaidCeo/Scenes";
        const string Settings = "Assets/UnpaidCeo/Settings";
        const string Materials = "Assets/UnpaidCeo/Art/Materials";

        public const string CharactersGroup = "characters";
        public const string CommonGroup = "city_common";
        public const string Chapter2Group = "city_ch2";
        public const string RoomGroup = "room";
        const string BrooklynAddress = "district_brooklyn", QueensAddress = "district_queens";

        /// <summary>Prefab name → Addressable groups whose scenes (or catalogs) reference it.</summary>
        static readonly Dictionary<string, HashSet<string>> Users = new Dictionary<string, HashSet<string>>();
        static string _building = "";

        [MenuItem("Unpaid CEO/Build district scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Scenes);
            Directory.CreateDirectory(Settings + "/Volumes");
            Users.Clear();
            _dayProfile = _nightProfile = null;
            var bronx = BronxCatalog();
            var chapter2 = Chapter2Catalog();
            BuildDistrict(Bronx(bronx));
            BuildDistrict(Brooklyn(chapter2));
            BuildDistrict(Queens(chapter2));
            BuildRoom();
            AssignSharedGroups();
            MarkCharactersAddressable();
            AssetDatabase.SaveAssets();
        }

        // ---------------------------------------------------------------- districts

        sealed class District
        {
            public string Name, Address, GroundMaterial, Home;
            public Color Ground, Sky;
            public CityCatalog Catalog;
            /// <summary>The row facing the avenue (z = 13.5): prefab, x, yaw, footprint.</summary>
            public (string prefab, float x, float yaw, float footprint)[] Row;
            public (string prefab, Vector3 at, float yaw, float footprint)[] Landmarks;
            /// <summary>Street props: size is a footprint, or a height when <c>byHeight</c>.</summary>
            public (string prefab, Vector3 at, float yaw, float size, bool byHeight)[] Props;
        }

        static readonly (string, Vector3, float, float, bool)[] BronxProps =
        {
            ("prop_stoop", new Vector3(11f, 0.16f, 8.2f), 180f, 1.4f, true),
            ("prop_delivery_bike", new Vector3(14.2f, 0.16f, 7.4f), 90f, 1.8f, false),
            ("prop_hydrant", new Vector3(5.6f, 0.16f, 7.6f), 0f, 0.9f, true),
            ("prop_hydrant", new Vector3(-26f, 0.16f, -7.6f), 0f, 0.9f, true),
            ("prop_trash_cans", new Vector3(17.5f, 0.16f, 7.8f), 0f, 1.1f, true),
            ("prop_trash_cans", new Vector3(-13f, 0.16f, 7.8f), 0f, 1.1f, true),
            ("prop_newspaper_box", new Vector3(-4.8f, 0.16f, 7.6f), 180f, 1.2f, true),
            ("prop_food_cart", new Vector3(4.8f, 0.16f, -7.4f), 0f, 2.4f, false),
            ("prop_taxi", new Vector3(-24f, 0.02f, -2.4f), 90f, 4.6f, false),
            ("prop_taxi", new Vector3(30f, 0.02f, 2.4f), -90f, 4.6f, false),
        };

        static District Bronx(CityCatalog catalog) => new District
        {
            Name = "Bronx", Address = CityView.BronxAddress, Catalog = catalog,
            GroundMaterial = "city_ground", Ground = new Color(0.55f, 0.52f, 0.47f), Sky = new Color(0.62f, 0.72f, 0.86f),
            Home = "building_walkup",
            Row = new[]
            {
                // Stage 4 key buildings (Tripo P2 from GPT Image concepts): the academy hall and the bank stand where the places open.
                ("building_academy", -43f, 180f, 9.5f), ("building_bank", -32f, 270f, 9.5f), ("building_pawn_shop", -21f, 180f, 9.5f),
                ("building_laundromat", -10f, 180f, 9.5f), ("building_walkup", 11f, 180f, 8f),
                // Tripo P2 put the bodega's storefront on the model's +X side; turn it to face the avenue.
                ("building_bodega", 21f, 270f, 9.5f), ("building_pizza", 32f, 180f, 9.5f), ("building_garden_lot", 43f, 180f, 9.5f),
            },
            Landmarks = new[]
            {
                ("building_garage", new Vector3(43, 0, -13.5f), 0f, 9.5f), ("building_court", new Vector3(-43, 0, -13.5f), 0f, 10f),
                ("building_bus_shelter", new Vector3(-16, 0.16f, -6.8f), 0f, 3.6f), ("building_el_subway", new Vector3(0, 0, -30f), 90f, 22f),
            },
            Props = BronxProps,
        };

        /// <summary>Chapter 2: brownstone blocks around a bakery, a barbershop and a car wash, under the elevated line.</summary>
        static District Brooklyn(CityCatalog catalog) => new District
        {
            Name = "Brooklyn", Address = BrooklynAddress, Catalog = catalog,
            GroundMaterial = "city_ground_brooklyn", Ground = new Color(0.58f, 0.51f, 0.45f), Sky = new Color(0.66f, 0.74f, 0.88f),
            Home = "building_brownstone_row",
            Row = new[]
            {
                ("building_brownstone_row", -43f, 270f, 9.5f), ("building_bakery", -32f, 180f, 9.5f), ("building_barbershop", -21f, 180f, 9.5f),
                ("building_brownstone_row", -10f, 270f, 9.5f), ("building_brownstone_row", 11f, 270f, 9.5f), ("building_car_wash", 21f, 180f, 9.5f),
                ("building_brownstone_row", 32f, 270f, 9.5f), ("building_garden_lot", 43f, 180f, 9.5f),
            },
            Landmarks = new[]
            {
                ("building_walkup", new Vector3(-43, 0, -13.5f), 0f, 8f), ("building_garage", new Vector3(43, 0, -13.5f), 0f, 9.5f),
                ("building_bus_shelter", new Vector3(-16, 0.16f, -6.8f), 0f, 3.6f), ("building_el_subway", new Vector3(0, 0, -30f), 90f, 22f),
            },
            Props = new[]
            {
                ("prop_delivery_bike", new Vector3(-27.5f, 0.16f, 7.4f), 90f, 1.8f, false),
                ("prop_hydrant", new Vector3(-5.6f, 0.16f, 7.6f), 0f, 0.9f, true),
                ("prop_hydrant", new Vector3(26f, 0.16f, -7.6f), 0f, 0.9f, true),
                ("prop_trash_cans", new Vector3(-36f, 0.16f, 7.8f), 0f, 1.1f, true),
                ("prop_trash_cans", new Vector3(16f, 0.16f, -7.8f), 0f, 1.1f, true),
                ("prop_newspaper_box", new Vector3(4.8f, 0.16f, 7.6f), 180f, 1.2f, true),
                ("prop_taxi", new Vector3(18f, 0.02f, -2.4f), 90f, 4.6f, false),
                ("prop_taxi", new Vector3(-34f, 0.02f, 2.4f), -90f, 4.6f, false),
            },
        };

        /// <summary>Chapter 3: detached Queens houses with porches, a laundromat and a car wash, under the 7 train.</summary>
        static District Queens(CityCatalog catalog) => new District
        {
            Name = "Queens", Address = QueensAddress, Catalog = catalog,
            GroundMaterial = "city_ground_queens", Ground = new Color(0.5f, 0.55f, 0.42f), Sky = new Color(0.6f, 0.76f, 0.9f),
            Home = "building_queens_house",
            Row = new[]
            {
                ("building_queens_house", -43f, 270f, 9f), ("building_car_wash", -32f, 180f, 9.5f), ("building_queens_house", -21f, 270f, 9f),
                ("building_laundromat", -10f, 180f, 9.5f), ("building_queens_house", 11f, 270f, 9f), ("building_queens_house", 21f, 270f, 9f),
                ("building_bakery", 32f, 180f, 9.5f), ("building_garden_lot", 43f, 180f, 9.5f),
            },
            Landmarks = new[]
            {
                ("building_church", new Vector3(-43, 0, -13.5f), 0f, 9.5f), ("building_garage", new Vector3(43, 0, -13.5f), 0f, 9.5f),
                ("building_bus_shelter", new Vector3(-16, 0.16f, -6.8f), 0f, 3.6f), ("building_el_subway", new Vector3(0, 0, -30f), 90f, 22f),
            },
            Props = new[]
            {
                ("prop_delivery_bike", new Vector3(8f, 0.16f, 7.4f), 90f, 1.8f, false),
                ("prop_hydrant", new Vector3(16f, 0.16f, 7.6f), 0f, 0.9f, true),
                ("prop_hydrant", new Vector3(-30f, 0.16f, -7.6f), 0f, 0.9f, true),
                ("prop_trash_cans", new Vector3(-24.5f, 0.16f, 7.8f), 0f, 1.1f, true),
                ("prop_trash_cans", new Vector3(26.5f, 0.16f, 7.8f), 0f, 1.1f, true),
                ("prop_newspaper_box", new Vector3(-4.8f, 0.16f, -7.6f), 0f, 1.2f, true),
                ("prop_food_cart", new Vector3(-4.8f, 0.16f, 7.4f), 180f, 2.4f, false),
                ("prop_taxi", new Vector3(-12f, 0.02f, 2.4f), -90f, 4.6f, false),
                ("prop_taxi", new Vector3(24f, 0.02f, -2.4f), 90f, 4.6f, false),
            },
        };

        static void BuildDistrict(District d)
        {
            _building = d.Address;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(d.Name).transform;
            var world = new GameObject("World").transform;
            world.SetParent(root);

            var asphalt = Mat("city_asphalt", new Color(0.23f, 0.25f, 0.29f));
            var sidewalk = Mat("city_sidewalk", new Color(0.72f, 0.7f, 0.66f));
            var ground = Mat(d.GroundMaterial, d.Ground);
            var pad = Mat("city_lot_pad", new Color(0.86f, 0.78f, 0.55f));
            var paint = Mat("city_road_paint", new Color(0.95f, 0.82f, 0.3f));

            Block("ground", world, new Vector3(0, -0.1f, 0), new Vector3(140, 0.2f, 90), ground);
            Block("avenue", world, new Vector3(0, 0.01f, 0), new Vector3(140, 0.02f, 10), asphalt);
            Block("street", world, new Vector3(0, 0.012f, 0), new Vector3(10, 0.02f, 90), asphalt);
            for (int i = -6; i <= 6; i++) Block("lane", world, new Vector3(i * 10 + 2.5f, 0.025f, 0), new Vector3(4, 0.01f, 0.25f), paint);
            foreach (float z in new[] { -6.5f, 6.5f }) Block("sidewalk", world, new Vector3(0, 0.08f, z), new Vector3(140, 0.16f, 3), sidewalk);
            foreach (float x in new[] { -6.5f, 6.5f }) Block("sidewalk", world, new Vector3(x, 0.08f, 0), new Vector3(3, 0.16f, 90), sidewalk);

            // The row faces the avenue (south); landmarks stand at the ends of the lot row and behind it.
            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(root);
            Transform home = null;
            foreach (var (prefab, x, yaw, footprint) in d.Row)
            {
                var b = Place(prefab, buildings, new Vector3(x, 0, 13.5f), yaw, footprint: footprint);
                if (home == null && prefab == d.Home) home = b.transform;
            }
            foreach (var (prefab, at, yaw, footprint) in d.Landmarks) Place(prefab, buildings, at, yaw, footprint: footprint);

            // Lots for the player's units (south row).
            var lotsRoot = new GameObject("Lots").transform;
            lotsRoot.SetParent(root);
            var lots = new List<LotView>();
            float[] lotX = { -32f, -21f, -10f, 11f, 21f, 32f };
            for (int i = 0; i < lotX.Length; i++)
            {
                var lotGo = new GameObject($"lot_{i + 1}");
                lotGo.transform.SetParent(lotsRoot, false);
                lotGo.transform.position = new Vector3(lotX[i], 0f, -13.5f);
                var marker = Block("pad", lotGo.transform, lotGo.transform.position + Vector3.up * 0.06f, new Vector3(8.5f, 0.12f, 8.5f), pad);
                var lot = lotGo.AddComponent<LotView>();
                lot.Configure($"{d.Name.ToLowerInvariant()}_lot_{i + 1}", 9f, marker);
                lots.Add(lot);
            }

            // Street life and lamps.
            var props = new GameObject("Props").transform;
            props.SetParent(root);
            var lamps = new List<Light>();
            for (int i = -5; i <= 5; i++)
            {
                if (i == 0) continue;
                foreach (float z in new[] { -7.6f, 7.6f })
                {
                    var lampGo = Place("prop_street_lamp", props, new Vector3(i * 12f, 0.16f, z), z > 0 ? 180f : 0f, height: 4.2f);
                    var light = new GameObject("lamp_light").AddComponent<Light>();
                    light.transform.SetParent(lampGo.transform, false);
                    light.transform.position = lampGo.transform.position + Vector3.up * 4f;
                    light.type = LightType.Point;
                    light.range = 7f;
                    light.color = new Color(1f, 0.78f, 0.45f);
                    light.intensity = 0f;
                    light.shadows = LightShadows.None;
                    lamps.Add(light);
                }
            }
            int cars = 0;
            foreach (var (prefab, at, yaw, size, byHeight) in d.Props)
            {
                var prop = Place(prefab, props, at, yaw, footprint: byHeight ? 0 : size, height: byHeight ? size : 0);
                if (prefab != "prop_taxi") continue;
                // Cars drive: a static object is batched into the scene mesh and could not move.
                foreach (var t in prop.GetComponentsInChildren<Transform>(true)) t.gameObject.isStatic = false;
                prop.AddComponent<StreetCar>().Configure(speed: 6f + 2.5f * (cars++ % 3), halfLength: 68f);
            }

            var lighting = new GameObject("Lighting").transform;
            lighting.SetParent(root);
            var sun = Sun(lighting);
            var day = GlobalVolume(lighting, "volume_day", DayProfile(), 1f);
            var night = GlobalVolume(lighting, "volume_night", NightProfile(), 0f);

            var camGo = new GameObject("CityCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = d.Sky;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.AddComponent<CameraRig>().Snap();

            var controllers = new GameObject("Controllers").transform;
            controllers.SetParent(root);
            var city = controllers.gameObject.AddComponent<CityView>();
            city.Configure(d.Catalog, lots, home);
            var dayNeon = controllers.gameObject.AddComponent<DayNeonController>();
            dayNeon.Configure(sun, day, night, lamps);
            var timelapse = controllers.gameObject.AddComponent<TimelapseDirector>();
            timelapse.Configure(dayNeon, city, sun.transform);

            var path = $"{Scenes}/{d.Name}.unity";
            EditorSceneManager.SaveScene(scene, path);
            MakeAddressable(path, d.Address, d.Address);
        }

        // ---------------------------------------------------------------- catalogs

        static CityCatalog Catalog(string file, GameObject fallback, List<CityCatalog.Entry> entries, params string[] users)
        {
            var path = $"{Settings}/{file}.asset";
            var c = AssetDatabase.LoadAssetAtPath<CityCatalog>(path);
            if (c == null) { c = ScriptableObject.CreateInstance<CityCatalog>(); AssetDatabase.CreateAsset(c, path); }
            c.defaultBuilding = fallback;
            c.entries = entries;
            foreach (var u in users)
            {
                Use(fallback.name, u);
                foreach (var e in entries) Use(e.building.name, u);
            }
            EditorUtility.SetDirty(c);
            return c;
        }

        static CityCatalog.Entry E(string industry, string prefab, int minUnits = 1, float footprint = 0f, float yaw = 0f) =>
            new CityCatalog.Entry { industryId = industry, building = Prefab(prefab), minUnits = minUnits, footprint = footprint, yaw = yaw };

        /// <summary>Chapter 1 (the Bronx): the stage-1 lot entries; chapter-1 players only run gigs.</summary>
        static CityCatalog BronxCatalog() => Catalog("CityCatalog", Prefab("building_bodega"), new List<CityCatalog.Entry>
        {
            E("resale_shop", "building_pawn_shop"), E("laundromat", "building_laundromat"), E("pizza", "building_pizza"),
            E("check_cashing", "building_check_cashing"), E("repair_shop", "building_garage"),
        }, CityView.BronxAddress);

        /// <summary>
        /// Chapters 2–3: the six businesses; carts and trucks keep their own size and become buildings as units open
        /// (coffee cart → coffee shop at 2, food truck → pizzeria at 3, box truck → warehouse at 2).
        /// </summary>
        static CityCatalog Chapter2Catalog() => Catalog("CityCatalog_Chapter2", Prefab("building_bodega"), new List<CityCatalog.Entry>
        {
            E("coffee_cart", "prop_coffee_cart", 1, 3.6f), E("coffee_cart", "building_coffee_shop", 2),
            E("resale_store", "building_pawn_shop"),
            // The truck parks along the avenue with its serving window to the street.
            E("food_truck", "prop_food_truck", 1, 7f, 90f), E("food_truck", "building_pizza", 3),
            E("ai_agency", "building_office"),
            E("cleaning", "building_cleaning_co", yaw: 90f),
            E("logistics_box_truck", "prop_box_truck", 1, 7.5f), E("logistics_box_truck", "building_warehouse", 2, yaw: 90f),
        }, BrooklynAddress, QueensAddress);

        // ---------------------------------------------------------------- building blocks

        static void Use(string prefab, string group)
        {
            if (!Users.TryGetValue(prefab, out var set)) Users[prefab] = set = new HashSet<string>();
            set.Add(group);
        }

        static GameObject Prefab(string name)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{Prefabs}/{name}.prefab");
            if (p == null) throw new FileNotFoundException("missing prefab " + name);
            return p;
        }

        static Material Mat(string name, Color color)
        {
            Directory.CreateDirectory(Materials);
            var path = $"{Materials}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find(AssetBudgetValidator.StylizedShaderName)); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Block(string name, Transform parent, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            go.isStatic = true;
            return go;
        }

        static GameObject Place(string prefab, Transform parent, Vector3 position, float yaw, float footprint = 0, float height = 0)
        {
            Use(prefab, _building);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Prefab(prefab), parent);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            if (footprint > 0) ModelFit.ToFootprint(go, footprint);
            else if (height > 0) ModelFit.ToHeight(go, height);
            var p = go.transform.position;
            ModelFit.Ground(go);
            go.transform.position = new Vector3(p.x, go.transform.position.y + position.y, p.z);
            go.isStatic = true;
            return go;
        }

        static VolumeProfile _dayProfile, _nightProfile;

        /// <summary>One day and one night grade shared by every district (built once per <see cref="BuildAll"/>).</summary>
        static VolumeProfile DayProfile() => _dayProfile != null ? _dayProfile : _dayProfile = Profile("Bronx_Day", false);
        static VolumeProfile NightProfile() => _nightProfile != null ? _nightProfile : _nightProfile = Profile("Bronx_Night", true);

        static VolumeProfile Profile(string name, bool night)
        {
            var path = $"{Settings}/Volumes/{name}.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(night ? 1.4f : 0.25f);
            bloom.threshold.Override(night ? 0.9f : 1.3f);
            bloom.scatter.Override(night ? 0.75f : 0.6f);
            if (night) bloom.tint.Override(new Color(1f, 0.55f, 0.85f));
            var dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(85f);
            dof.gaussianEnd.Override(150f);
            dof.gaussianMaxRadius.Override(1.2f);
            var color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(night ? 18f : 12f);
            color.postExposure.Override(night ? -0.2f : 0.05f);
            if (night) color.colorFilter.Override(new Color(0.78f, 0.74f, 1f));
            if (night) { var v = profile.Add<Vignette>(true); v.intensity.Override(0.3f); }
            foreach (var c in profile.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, profile); }
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static Volume GlobalVolume(Transform parent, string name, VolumeProfile profile, float weight)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<Volume>();
            v.isGlobal = true;
            v.sharedProfile = profile;
            v.weight = weight;
            return v;
        }

        static Light Sun(Transform parent)
        {
            var sun = new GameObject("sun").AddComponent<Light>();
            sun.transform.SetParent(parent, false);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.0f;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            return sun;
        }

        static void BuildRoom()
        {
            _building = RoomGroup;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Room").transform;
            var floor = Mat("room_floor", new Color(0.66f, 0.46f, 0.3f));
            var wall = Mat("room_wall", new Color(0.86f, 0.8f, 0.7f));
            Block("floor", root, new Vector3(0, -0.05f, 0), new Vector3(6, 0.1f, 5), floor);
            Block("wall_back", root, new Vector3(0, 1.5f, 2.55f), new Vector3(6, 3, 0.1f), wall);
            Block("wall_left", root, new Vector3(-3.05f, 1.5f, 0), new Vector3(0.1f, 3, 5), wall);
            var props = new GameObject("Props").transform;
            props.SetParent(root);
            Place("prop_bed", props, new Vector3(-1.9f, 0, 1.4f), 90f, footprint: 2f);
            Place("prop_desk", props, new Vector3(1.6f, 0, 2f), 180f, footprint: 1.3f);
            Place("prop_kitchenette", props, new Vector3(-2.5f, 0, -1.2f), 90f, footprint: 1.5f);
            Place("prop_sofa", props, new Vector3(1.6f, 0, -0.8f), -90f, footprint: 1.7f);
            Place("prop_shoe_rack", props, new Vector3(2.6f, 0, -2.1f), -90f, footprint: 0.8f);
            Place("prop_window_ac", props, new Vector3(0.2f, 1.6f, 2.45f), 180f, footprint: 0.7f);

            var lights = new GameObject("Lighting").transform;
            lights.SetParent(root);
            var sun = Sun(lights);
            sun.intensity = 0.9f;
            var lamp = new GameObject("room_lamp").AddComponent<Light>();
            lamp.transform.SetParent(lights, false);
            lamp.transform.position = new Vector3(0.5f, 2.6f, 0.2f);
            lamp.type = LightType.Point;
            lamp.range = 7f;
            lamp.intensity = 1.4f;
            lamp.color = new Color(1f, 0.82f, 0.6f);
            GlobalVolume(lights, "volume_day", DayProfile(), 1f);

            var spawn = new GameObject("hero_spawn").transform;
            spawn.SetParent(root);
            spawn.position = new Vector3(0.1f, 0, 0.2f);
            spawn.rotation = Quaternion.Euler(0, 200f, 0);

            var camGo = new GameObject("RoomCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 32f;
            cam.transform.position = new Vector3(5.2f, 5.4f, -6.2f);
            cam.transform.LookAt(new Vector3(0, 0.8f, 0.3f));
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.12f, 0.1f, 0.16f);
            camGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;

            root.gameObject.AddComponent<RoomScene>().Configure(spawn, cam);

            var path = $"{Scenes}/Room.unity";
            EditorSceneManager.SaveScene(scene, path);
            // Its own group: opening the room never pulls a district bundle.
            MakeAddressable(path, CityView.RoomAddress, RoomGroup);
        }

        // ---------------------------------------------------------------- addressables

        /// <summary>
        /// Prefabs referenced from more than one group become explicit entries of a shared group, so each bundle
        /// references them instead of carrying a copy: "city_common" when the Bronx uses them, else "city_ch2".
        /// </summary>
        static void AssignSharedGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            foreach (var name in new[] { CommonGroup, Chapter2Group })
            {
                var group = settings.FindGroup(name);
                if (group == null) continue;
                foreach (var e in group.entries.ToList()) settings.RemoveAssetEntry(e.guid, false);
            }
            foreach (var pair in Users.OrderBy(p => p.Key, System.StringComparer.Ordinal))
            {
                if (pair.Value.Count < 2) continue;
                MakeAddressable($"{Prefabs}/{pair.Key}.prefab", "city/" + pair.Key, pair.Value.Contains(CityView.BronxAddress) ? CommonGroup : Chapter2Group);
            }
        }

        /// <summary>Which shared group a prefab went to in the last <see cref="BuildAll"/> (null = only one scene uses it).</summary>
        public static string SharedGroupOf(string prefab)
        {
            var entry = AddressableAssetSettingsDefaultObject.GetSettings(false)?.FindAssetEntry(AssetDatabase.AssetPathToGUID($"{Prefabs}/{prefab}.prefab"));
            return entry != null && (entry.parentGroup.Name == CommonGroup || entry.parentGroup.Name == Chapter2Group) ? entry.parentGroup.Name : null;
        }

        /// <summary>Heroes and NPCs load on demand (address = prefab name), keeping them out of the initial web download.</summary>
        public static void MarkCharactersAddressable()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("hero_") || name.StartsWith("npc_")) MakeAddressable(path, name, CharactersGroup);
            }
        }

        static void MakeAddressable(string assetPath, string address, string groupName)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            // One bundle per character: the web build downloads only the hero the player picked (~2 MB, not ~18 MB).
            if (groupName == CharactersGroup)
                group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        // ---------------------------------------------------------------- previews

        static readonly (string scene, (string industry, int units)[] businesses, Vector3 closeFrom, Vector3 closeAt)[] Previews =
        {
            ("Bronx", new (string, int)[0], new Vector3(5f, 14f, -12f), new Vector3(21f, 3f, 13.5f)),
            ("Brooklyn", new[] { ("coffee_cart", 1), ("resale_store", 1), ("food_truck", 1), ("ai_agency", 1), ("cleaning", 1), ("logistics_box_truck", 1) },
                new Vector3(4f, 16f, 10f), new Vector3(-6f, 2f, -13.5f)),
            ("Queens", new[] { ("coffee_cart", 2), ("resale_store", 1), ("food_truck", 3), ("ai_agency", 1), ("cleaning", 1), ("logistics_box_truck", 2) },
                new Vector3(4f, 16f, 10f), new Vector3(-6f, 2f, -13.5f)),
        };

        /// <summary>
        /// Renders each district from its city camera into tools/art/{bronx,brooklyn,queens}-{day,night,closeup}.jpg for
        /// review; Brooklyn shows the six businesses at one unit, Queens at the unit counts where they become buildings.
        /// </summary>
        public static void RenderPreviews()
        {
            foreach (var (sceneName, businesses, closeFrom, closeAt) in Previews)
            {
                EditorSceneManager.OpenScene($"{Scenes}/{sceneName}.unity");
                var state = new GameState { Chapter = 2 };
                for (int i = 0; i < businesses.Length; i++)
                    state.Businesses.Add(new BusinessState { Id = "preview_" + i, IndustryId = businesses[i].industry, Units = businesses[i].units });
                Object.FindFirstObjectByType<CityView>().Sync(state);
                var rig = Object.FindFirstObjectByType<CameraRig>();
                rig.Snap();
                var cam = rig.GetComponent<Camera>();
                var dn = Object.FindFirstObjectByType<DayNeonController>();
                string file = sceneName.ToLowerInvariant();
                var shots = new List<(string, float, bool)> { ("warmup", 0f, false), ("day", 0f, false), ("night", 1f, false), ("closeup", 0f, true) };
                if (sceneName != "Bronx") shots.Add(("row", 0f, true)); else shots.Add(("west", 0f, true));
                foreach (var (name, night, close) in shots)
                {
                    dn.SetBlend(night);
                    if (name == "row") { cam.transform.position = new Vector3(-2f, 14f, -10f); cam.transform.LookAt(new Vector3(11f, 3f, 13.5f)); }
                    else if (name == "west") { cam.transform.position = new Vector3(-30f, 12f, -10f); cam.transform.LookAt(new Vector3(-36f, 4f, 13.5f)); }
                    else if (close) { cam.transform.position = closeFrom; cam.transform.LookAt(closeAt); }
                    var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
                    cam.targetTexture = rt;
                    var req = new RenderPipeline.StandardRequest { destination = rt };
                    if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    if (name != "warmup") File.WriteAllBytes($"../tools/art/{file}-{name}.jpg", tex.EncodeToJPG(85));
                    cam.targetTexture = null;
                    Object.DestroyImmediate(tex);
                    Object.DestroyImmediate(rt);
                }
                dn.SetBlend(0f);
            }
        }

        /// <summary>
        /// Each chapter-2 catalog model on one Brooklyn lot, seen from the avenue (north), into
        /// tools/art/previews/lot_{prefab}.jpg — to check that every front faces the street (Entry.yaw).
        /// </summary>
        public static void RenderLotPreviews()
        {
            EditorSceneManager.OpenScene($"{Scenes}/Brooklyn.unity");
            var city = Object.FindFirstObjectByType<CityView>();
            var cam = Object.FindFirstObjectByType<CameraRig>().GetComponent<Camera>();
            var lot = city.Lots[0]; // a lone business takes the first lot
            var target = lot.transform.position + Vector3.up * 2.5f;
            cam.transform.position = target + new Vector3(7f, 9f, 15f);
            cam.transform.LookAt(target);
            foreach (var e in city.Catalog.entries.Prepend(default))
            {
                var state = new GameState { Chapter = 2 };
                if (e.building != null) state.Businesses.Add(new BusinessState { Id = "preview", IndustryId = e.industryId, Units = Mathf.Max(1, e.minUnits) });
                city.Sync(state);
                var rt = new RenderTexture(800, 450, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(800, 450, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 800, 450), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                if (e.building != null) File.WriteAllBytes($"../tools/art/previews/lot_{e.building.name}.jpg", tex.EncodeToJPG(85));
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }
        }
    }
}
