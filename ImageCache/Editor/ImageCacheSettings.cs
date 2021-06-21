using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[CustomEditor(typeof(ImageCacheSettings))]
public class ImageCacheSettingsEditor : UnityEditor.Editor
{
    
    [MenuItem("Tools/Get image cache settings")]
    public static void GetImageCacheSettings()
    {
        var settings = ImageCacheSettings.Instance;

        if (settings == null)
        {
            AssetDatabase.CreateFolder("Assets/", "Resources");
            settings = new ImageCacheSettings();
            settings.matchingPatterns.Add(@".*\.(jpg)$");
            AssetDatabase.CreateAsset(settings, "Assets/Resources/ImageCache.asset");
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

    void CacheImages()
    {
        Debug.Log("Start caching images");
        AssetDatabase.DeleteAsset("Assets/Resources/ImageCache");
        AssetDatabase.CreateFolder("Assets/Resources", "ImageCache");
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
    }

    void CacheImage(string path)
    {
        var importer = (TextureImporter) TextureImporter.GetAtPath(path);
        importer.maxTextureSize = 32;
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();

        var clone_path = "Assets/Resources/ImageCache/" + AssetDatabase.AssetPathToGUID(path) + ".bytes";
        AssetDatabase.CopyAsset(path, clone_path);
        ((ImageCacheSettings)target).mapItems.Add(new ImageCacheSettings.MapItem()
        {
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path),
            binarySprite = AssetDatabase.LoadAssetAtPath<TextAsset>(clone_path)
        });
    }
}