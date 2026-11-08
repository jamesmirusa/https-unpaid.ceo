using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnpaidCeo.Game.Bootstrap;

namespace UnpaidCeo.Editor
{
    /// <summary>
    /// Builds Scenes/Boot.unity (the only scene in the player build; districts load through Addressables)
    /// and makes the content JSON addressable for <see cref="GameBootstrap"/>.
    /// Run: -executeMethod UnpaidCeo.Editor.BootSceneBuilder.BuildAll
    /// </summary>
    public static class BootSceneBuilder
    {
        public const string ScenePath = "Assets/UnpaidCeo/Scenes/Boot.unity";
        public const string YarnProjectPath = "Assets/UnpaidCeo/Yarn/Chapter1.yarnproject";
        const string ContentDir = "Assets/UnpaidCeo/Content/";

        [MenuItem("Unpaid CEO/Build Boot Scene")]
        public static void BuildAll()
        {
            MarkContentAddressable();
            BuildScene();
        }

        public static void MarkContentAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
            var group = settings.FindGroup("content") ?? settings.CreateGroup("content", false, false, true, null,
                typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            group.GetSchema<BundledAssetGroupSchema>().BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackTogether;
            foreach (var file in GameBootstrap.ContentFiles)
            {
                var guid = AssetDatabase.AssetPathToGUID(ContentDir + file + ".json");
                if (string.IsNullOrEmpty(guid)) throw new System.IO.FileNotFoundException(ContentDir + file + ".json");
                settings.CreateOrMoveEntry(guid, group, false, false).SetAddress(GameBootstrap.ContentAddress(file), false);
            }
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.BatchModification, null, true, true);
            AssetDatabase.SaveAssets();
        }

        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var boot = new GameObject("Game");
            var bootstrap = boot.AddComponent<GameBootstrap>();
            var so = new SerializedObject(bootstrap);
            so.FindProperty("yarnProject").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(YarnProjectPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            var events = new GameObject("EventSystem");
            var eventSystem = System.Type.GetType("UnityEngine.EventSystems.EventSystem, UnityEngine.UI");
            if (eventSystem != null) events.AddComponent(eventSystem);
            var module = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (module != null) events.AddComponent(module);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
        }
    }
}
