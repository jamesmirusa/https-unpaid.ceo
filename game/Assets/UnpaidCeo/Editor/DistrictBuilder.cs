using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnpaidCeo.Game.City;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Builds Scenes/Bronx.unity (Addressable "district_bronx") and Scenes/Room.unity (Addressable "room") from the
    /// generated prefabs: streets, buildings, lots, props, street lamps, day/night volumes, camera and controllers.
    /// Deterministic — re-run after re-importing art.
    /// </summary>
    public static class DistrictBuilder
    {
        const string Prefabs = "Assets/UnpaidCeo/Art/Prefabs";
        const string Scenes = "Assets/UnpaidCeo/Scenes";
        const string Settings = "Assets/UnpaidCeo/Settings";
        const string Materials = "Assets/UnpaidCeo/Art/Materials";

        [MenuItem("Unpaid CEO/Build district scenes")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(Scenes);
            Directory.CreateDirectory(Settings + "/Volumes");
            BuildBronx();
            BuildRoom();
            MarkCharactersAddressable();
            AssetDatabase.SaveAssets();
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

        static void BuildBronx()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Bronx").transform;
            var world = new GameObject("World").transform;
            world.SetParent(root);

            var asphalt = Mat("city_asphalt", new Color(0.23f, 0.25f, 0.29f));
            var sidewalk = Mat("city_sidewalk", new Color(0.72f, 0.7f, 0.66f));
            var ground = Mat("city_ground", new Color(0.55f, 0.52f, 0.47f));
            var pad = Mat("city_lot_pad", new Color(0.86f, 0.78f, 0.55f));
            var paint = Mat("city_road_paint", new Color(0.95f, 0.82f, 0.3f));

            Block("ground", world, new Vector3(0, -0.1f, 0), new Vector3(140, 0.2f, 90), ground);
            Block("avenue", world, new Vector3(0, 0.01f, 0), new Vector3(140, 0.02f, 10), asphalt);
            Block("street", world, new Vector3(0, 0.012f, 0), new Vector3(10, 0.02f, 90), asphalt);
            for (int i = -6; i <= 6; i++) Block("lane", world, new Vector3(i * 10 + 2.5f, 0.025f, 0), new Vector3(4, 0.01f, 0.25f), paint);
            foreach (float z in new[] { -6.5f, 6.5f }) Block("sidewalk", world, new Vector3(0, 0.08f, z), new Vector3(140, 0.16f, 3), sidewalk);
            foreach (float x in new[] { -6.5f, 6.5f }) Block("sidewalk", world, new Vector3(x, 0.08f, 0), new Vector3(3, 0.16f, 90), sidewalk);

            // Buildings: north row faces the avenue (south), south row faces north.
            var buildings = new GameObject("Buildings").transform;
            buildings.SetParent(root);
            Transform home = null;
            var north = new (string name, float x)[] { ("building_church", -43), ("building_check_cashing", -32), ("building_pawn_shop", -21), ("building_laundromat", -10),
                ("building_walkup", 11), ("building_bodega", 21), ("building_pizza", 32), ("building_garden_lot", 43) };
            foreach (var (name, x) in north)
            {
                // Tripo P2 put the bodega's storefront on the model's +X side; turn it to face the avenue.
                float yaw = name == "building_bodega" ? 270f : 180f;
                var b = Place(name, buildings, new Vector3(x, 0, 13.5f), yaw, footprint: name == "building_walkup" ? 8f : 9.5f);
                if (name == "building_walkup") home = b.transform;
            }
            Place("building_garage", buildings, new Vector3(43, 0, -13.5f), 0f, footprint: 9.5f);
            Place("building_court", buildings, new Vector3(-43, 0, -13.5f), 0f, footprint: 10f);
            Place("building_bus_shelter", buildings, new Vector3(-16, 0.16f, -6.8f), 0f, footprint: 3.6f);
            Place("building_el_subway", buildings, new Vector3(0, 0, -30f), 90f, footprint: 22f);

            // Lots for the player's businesses (south row).
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
                lot.Configure($"bronx_lot_{i + 1}", 9f, marker);
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
            Place("prop_stoop", props, new Vector3(11f, 0.16f, 8.2f), 180f, height: 1.4f);
            Place("prop_delivery_bike", props, new Vector3(14.2f, 0.16f, 7.4f), 90f, footprint: 1.8f);
            Place("prop_hydrant", props, new Vector3(5.6f, 0.16f, 7.6f), 0f, height: 0.9f);
            Place("prop_hydrant", props, new Vector3(-26f, 0.16f, -7.6f), 0f, height: 0.9f);
            Place("prop_trash_cans", props, new Vector3(17.5f, 0.16f, 7.8f), 0f, height: 1.1f);
            Place("prop_trash_cans", props, new Vector3(-13f, 0.16f, 7.8f), 0f, height: 1.1f);
            Place("prop_newspaper_box", props, new Vector3(-4.8f, 0.16f, 7.6f), 180f, height: 1.2f);
            Place("prop_food_cart", props, new Vector3(4.8f, 0.16f, -7.4f), 0f, footprint: 2.4f);
            Place("prop_taxi", props, new Vector3(-24f, 0.02f, -2.4f), 90f, footprint: 4.6f);
            Place("prop_taxi", props, new Vector3(30f, 0.02f, 2.4f), -90f, footprint: 4.6f);

            var lighting = new GameObject("Lighting").transform;
            lighting.SetParent(root);
            var sun = Sun(lighting);
            var day = GlobalVolume(lighting, "volume_day", Profile("Bronx_Day", false), 1f);
            var night = GlobalVolume(lighting, "volume_night", Profile("Bronx_Night", true), 0f);

            var camGo = new GameObject("CityCamera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(root);
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.62f, 0.72f, 0.86f);
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camGo.AddComponent<CameraRig>().Snap();

            var controllers = new GameObject("Controllers").transform;
            controllers.SetParent(root);
            var catalog = Catalog();
            var city = controllers.gameObject.AddComponent<CityView>();
            city.Configure(catalog, lots, home);
            var dayNeon = controllers.gameObject.AddComponent<DayNeonController>();
            dayNeon.Configure(sun, day, night, lamps);
            var timelapse = controllers.gameObject.AddComponent<TimelapseDirector>();
            timelapse.Configure(dayNeon, city, sun.transform);

            var path = $"{Scenes}/Bronx.unity";
            EditorSceneManager.SaveScene(scene, path);
            MakeAddressable(path, CityView.BronxAddress, "district_bronx");
        }

        static CityCatalog Catalog()
        {
            var path = $"{Settings}/CityCatalog.asset";
            var c = AssetDatabase.LoadAssetAtPath<CityCatalog>(path);
            if (c == null) { c = ScriptableObject.CreateInstance<CityCatalog>(); AssetDatabase.CreateAsset(c, path); }
            c.defaultBuilding = Prefab("building_bodega");
            c.entries = new List<CityCatalog.Entry>
            {
                new CityCatalog.Entry { industryId = "resale_shop", building = Prefab("building_pawn_shop") },
                new CityCatalog.Entry { industryId = "laundromat", building = Prefab("building_laundromat") },
                new CityCatalog.Entry { industryId = "pizza", building = Prefab("building_pizza") },
                new CityCatalog.Entry { industryId = "check_cashing", building = Prefab("building_check_cashing") },
                new CityCatalog.Entry { industryId = "repair_shop", building = Prefab("building_garage") },
            };
            EditorUtility.SetDirty(c);
            return c;
        }

        static void BuildRoom()
        {
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
            GlobalVolume(lights, "volume_day", AssetDatabase.LoadAssetAtPath<VolumeProfile>($"{Settings}/Volumes/Bronx_Day.asset"), 1f);

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
            MakeAddressable(path, CityView.RoomAddress, "district_bronx");
        }

        /// <summary>Heroes and NPCs load on demand (address = prefab name), keeping them out of the initial web download.</summary>
        static void MarkCharactersAddressable()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Prefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = Path.GetFileNameWithoutExtension(path);
                if (name.StartsWith("hero_") || name.StartsWith("npc_")) MakeAddressable(path, name, "characters");
            }
        }

        static void MakeAddressable(string assetPath, string address, string groupName)
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup(groupName) ?? settings.CreateGroup(groupName, false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), group);
            entry.address = address;
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, entry, true);
        }

        /// <summary>Renders the built Bronx scene from its city camera into tools/art/bronx-day.jpg and bronx-night.jpg for review.</summary>
        public static void RenderBronxPreview()
        {
            EditorSceneManager.OpenScene($"{Scenes}/Bronx.unity");
            var rig = Object.FindFirstObjectByType<CameraRig>();
            rig.Snap();
            var cam = rig.GetComponent<Camera>();
            var dn = Object.FindFirstObjectByType<DayNeonController>();
            var shots = new[] { ("warmup", 0f, false), ("bronx-day", 0f, false), ("bronx-night", 1f, false), ("bronx-closeup", 0f, true) };
            foreach (var (name, night, close) in shots)
            {
                dn.SetBlend(night);
                if (close) { cam.transform.position = new Vector3(5f, 14f, -12f); cam.transform.LookAt(new Vector3(21f, 3f, 13.5f)); }
                var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                var req = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, req)) RenderPipeline.SubmitRenderRequest(cam, req); else cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                if (name != "warmup") File.WriteAllBytes($"../tools/art/{name}.jpg", tex.EncodeToJPG(85));
                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
            }
            dn.SetBlend(0f);
        }
    }
}
