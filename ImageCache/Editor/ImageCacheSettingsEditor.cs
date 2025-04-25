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
using System.IO;
using System.Linq;

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
        var target = (ImageCacheSettings)base.target;
        this.addressable_settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        var group = addressable_settings.FindGroup(target.addressableGroupName);
        if (group == null)
        {
            Debug.LogError("Addressable group \"" + target.addressableGroupName + "\" not found.");
            return;
        }

        //var entries = new HashSet<AddressableAssetEntry>(group.entries);
        //foreach (var entry in entries) group.RemoveAssetEntry(entry);
        this.addressable_group = group;

        if (AssetDatabase.IsValidFolder("Assets/ImageCache") == false)
            AssetDatabase.CreateFolder("Assets", "ImageCache");
        var patterns = target.matchingPatterns;


        List<ImageCacheSettings.MapItem> existing_items = target.mapItems ?? new List<ImageCacheSettings.MapItem>();
        existing_items.RemoveAll(i => i == null || i.sprite == null || i.binarySpriteReference == null);

        var map_items = existing_items.ToDictionary(item =>
        {
            return AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(item.sprite));
        });

        foreach (var path in AssetDatabase.GetAllAssetPaths())
        {
            foreach (var regex in patterns)
            {
                if (Regex.IsMatch(path, regex))
                {
                    var guid = AssetDatabase.AssetPathToGUID(path);
                    map_items.TryGetValue(guid, out var map_item);
                    map_items[guid] = CacheImage(path, map_item);
                    break;
                }
            }
        }

        target.mapItems = map_items.Values.ToList();
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();



        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
        bool success = string.IsNullOrEmpty(result.Error);
        if (!success)
            Debug.LogError("Addressables build error encountered: " + result.Error);

        HashSet<string> existing_filename = new HashSet<string>();
        foreach (var item in target.mapItems)
        {
            var sprite = item.sprite;
            if (sprite == null) continue;

            if (existing_filename.Contains(sprite.name))
            {
                var path = AssetDatabase.GetAssetPath(item.sprite);
                Debug.LogError("Sprite name already exist: " + sprite.name + " in " + path);
            } else
                existing_filename.Add(sprite.name);
        }
    }

    ImageCacheSettings.MapItem CacheImage(string path, ImageCacheSettings.MapItem map_item)
    {

        long new_modified_timestamp = File.GetLastWriteTime(path).Ticks;
        if (map_item != null && map_item.binarySpriteReference != null && new_modified_timestamp == map_item.lastModifiedTimestamp)
        {
            Debug.Log(path + " is already cached.");
            return map_item;
        }

        var importer = (TextureImporter)TextureImporter.GetAtPath(path);
        importer.maxTextureSize = 32;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var clone_path = "Assets/ImageCache/" + AssetDatabase.AssetPathToGUID(path) + ".bytes";
        AssetDatabase.CopyAsset(path, clone_path);
        var guid = AssetDatabase.AssetPathToGUID(clone_path);
        var new_entry = addressable_settings.CreateOrMoveEntry(guid, addressable_group);

        map_item = map_item ?? new ImageCacheSettings.MapItem();
        map_item.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        map_item.binarySpriteReference = new UnityEngine.AddressableAssets.AssetReference(guid);
        map_item.lastModifiedTimestamp = new_modified_timestamp;

        return map_item;

    }
};