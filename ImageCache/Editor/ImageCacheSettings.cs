using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;

[CustomEditor(typeof(ImageCacheSettings))]
public class ImageCacheSettingsEditor : UnityEditor.Editor
{

    [MenuItem("Tools/Get image cache settings")]
    public static void GetImageCacheSettings()
    {
        var settings = ImageCacheSettings.Instance;

        if (settings == null)
        {
            //AssetDatabase.CreateFolder("Assets/", "Resources");
            settings = new ImageCacheSettings();
            settings.matchingPatterns.Add(@".*\.(jpg)$");
            AssetDatabase.CreateAsset(settings, "Assets/ImageCache.asset");
        }

        EditorGUIUtility.PingObject(settings);
        Selection.activeObject = settings;
    }

    [MenuItem("Tools/Remove duplicated pngs")]
    public static void RemoveDuplicatedPNGs()
    {
        var paths = new List<string>(AssetDatabase.GetAllAssetPaths());
        foreach (var path in paths)
        {
            if (path.EndsWith(".jpg"))
            {
                var png_path = path.Substring(0, path.Length - 4) + ".png";
                if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(png_path) != null)
                    AssetDatabase.DeleteAsset(png_path);
            }
        }
    }

    public override void OnInspectorGUI()
    {
        if (GUILayout.Button("Cache images")) CacheImages();
        GUILayout.Space(32);
        DrawDefaultInspector();
    }

    private AddressableAssetGroup addressable_group;
    private AddressableAssetSettings addressable_settings;

    void CacheImages()
    {
        var target = (ImageCacheSettings) base.target;
        this.addressable_settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var group = addressable_settings.FindGroup(target.addressableGroupName);
        if (group == null)
        {
            Debug.LogError("Addressable group \"" + target.addressableGroupName + "\" not found.");
            return;
        }

        var entries = new HashSet<AddressableAssetEntry>(group.entries);
        foreach (var entry in entries) group.RemoveAssetEntry(entry);
        this.addressable_group = group;

        AssetDatabase.DeleteAsset("Assets/ImageCache");
        AssetDatabase.CreateFolder("Assets", "ImageCache");
        ((ImageCacheSettings)target).mapItems = new List<ImageCacheSettings.MapItem>();
        var patterns = ((ImageCacheSettings)target).matchingPatterns;

        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            foreach (var regex in patterns)
            {
                if (Regex.IsMatch(path, regex))
                {
                    CacheImage(path);
                    break;
                }
            }
        }

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        
        //AddressablesPlayerBuildResult result;
        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        bool success = string.IsNullOrEmpty(result.Error);
        if (!success)
            Debug.LogError("Addressables build error encountered: " + result.Error);
    }

    void CacheImage(string path)
    {
        var importer = (TextureImporter) TextureImporter.GetAtPath(path);
        importer.maxTextureSize = 32;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var clone_path = "Assets/ImageCache/" + AssetDatabase.AssetPathToGUID(path) + ".bytes";
        AssetDatabase.CopyAsset(path, clone_path);
        var guid = AssetDatabase.AssetPathToGUID(clone_path);
        var new_entry = addressable_settings.CreateOrMoveEntry(guid, addressable_group);

        ((ImageCacheSettings)target).mapItems.Add(new ImageCacheSettings.MapItem()
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path),
            binarySpriteReference = new UnityEngine.AddressableAssets.AssetReference(guid)
        });
    }
};