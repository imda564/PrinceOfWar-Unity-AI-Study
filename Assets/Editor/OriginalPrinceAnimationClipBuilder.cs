using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OriginalPrinceAnimationClipBuilder
{
    private const string SourceRoot = "Assets/Resources/OriginalPrince/ExtractedAnimations/Units";
    private const string OutputRoot = "Assets/OriginalPrinceAnimationClips";
    private const float FrameRate = 24f;
    private const float PixelsPerUnit = 96f;

    [MenuItem("Tools/Prince Of War/Build Extracted Animation Clips")]
    public static void BuildClips()
    {
        if (!Directory.Exists(SourceRoot))
        {
            Debug.LogError("Missing extracted animation folder: " + SourceRoot);
            return;
        }

        Directory.CreateDirectory(OutputRoot);

        var clipCount = 0;
        foreach (var unitDirectory in Directory.GetDirectories(SourceRoot).OrderBy(Path.GetFileName))
        {
            var unitName = Path.GetFileName(unitDirectory);
            var unitOutput = Path.Combine(OutputRoot, unitName).Replace("\\", "/");
            Directory.CreateDirectory(unitOutput);

            foreach (var stateDirectory in Directory.GetDirectories(unitDirectory).OrderBy(Path.GetFileName))
            {
                var stateName = Path.GetFileName(stateDirectory);
                var sprites = LoadStateSprites(stateDirectory);
                if (sprites.Count == 0)
                {
                    Debug.LogWarning($"No sprites found for {unitName}/{stateName}");
                    continue;
                }

                var clip = CreateSpriteClip(sprites, stateName == "idle" || stateName == "move");
                var clipPath = Path.Combine(unitOutput, stateName + ".anim").Replace("\\", "/");
                if (File.Exists(clipPath))
                {
                    AssetDatabase.DeleteAsset(clipPath);
                }

                AssetDatabase.CreateAsset(clip, clipPath);
                clipCount++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Built {clipCount} Prince Of War animation clips in {OutputRoot}");
    }

    private static List<Sprite> LoadStateSprites(string stateDirectory)
    {
        var sprites = new List<Sprite>();
        var framePaths = Directory.GetFiles(stateDirectory, "*.png")
            .OrderBy(path => ExtractFrameNumber(Path.GetFileNameWithoutExtension(path)));

        foreach (var path in framePaths)
        {
            var assetPath = path.Replace("\\", "/");
            ConfigureSpriteImporter(assetPath);

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        return sprites;
    }

    private static void ConfigureSpriteImporter(string assetPath)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        var changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit))
        {
            importer.spritePixelsPerUnit = PixelsPerUnit;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AnimationClip CreateSpriteClip(IReadOnlyList<Sprite> sprites, bool loop)
    {
        var clip = new AnimationClip
        {
            frameRate = FrameRate
        };

        var binding = new EditorCurveBinding
        {
            path = string.Empty,
            type = typeof(SpriteRenderer),
            propertyName = "m_Sprite"
        };

        var keyframes = new ObjectReferenceKeyframe[sprites.Count];
        for (var index = 0; index < sprites.Count; index++)
        {
            keyframes[index] = new ObjectReferenceKeyframe
            {
                time = index / FrameRate,
                value = sprites[index]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        return clip;
    }

    private static int ExtractFrameNumber(string frameName)
    {
        return int.TryParse(frameName, out var number) ? number : 0;
    }
}
