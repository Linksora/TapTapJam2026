#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 Assets/美术合集 下的所有图片镜像同步到 Assets/Resources/Art（保留子目录结构），
/// 供运行时按文件名 Resources.Load 使用。
/// - 新增/改动的图片：复制过去并强制按 Sprite(2D) 导入；
/// - 源文件已删除的：同步删除 Resources/Art 里的对应文件；
/// - 只处理图片，不动美术合集里的其他文件。
/// </summary>
public static class ArtSyncTool
{
    private const string SourceRoot = "Assets/美术合集";
    private const string DestResourceRoot = "Assets/Resources/Art";
    private static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg" };

    [MenuItem("Tools/同步美术到 Resources")]
    public static void Sync()
    {
        if (!Directory.Exists(SourceRoot))
        {
            Debug.LogError($"[ArtSync] 找不到源目录 {SourceRoot}");
            return;
        }

        Directory.CreateDirectory(DestResourceRoot);

        int copied = 0;
        int skipped = 0;
        var sourceFiles = new HashSet<string>();

        foreach (string sourcePath in EnumerateImages(SourceRoot))
        {
            string relative = Path.GetRelativePath(SourceRoot, sourcePath);
            sourceFiles.Add(relative);
            string destPath = Combine(DestResourceRoot, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(destPath));

            if (File.Exists(destPath) && File.GetLastWriteTimeUtc(destPath) >= File.GetLastWriteTimeUtc(sourcePath))
            {
                // 目标已是最新（注意：内容变更通常也会刷新修改时间）。
                skipped++;
                continue;
            }

            // 只复制图片本身，不复制 .meta，避免源/目标出现重复 GUID。
            File.Copy(sourcePath, destPath, true);
            copied++;
        }

        AssetDatabase.Refresh();

        // 导入完成后统一把新图设置为 Sprite。
        int configured = 0;
        foreach (string relative in sourceFiles)
        {
            string destPath = Combine(DestResourceRoot, relative);
            if (ConfigureAsSprite(destPath))
            {
                configured++;
            }
        }

        int deleted = DeleteOrphans(sourceFiles);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ArtSync] 同步完成：复制/更新 {copied} 张，配置 Sprite {configured} 张，跳过未变 {skipped} 张，清理失效 {deleted} 张。");
    }

    /// <summary>
    /// 一次性修复：把 Resources/Art 下所有已导入 Sprite 的 Mesh Type 强制改成 Full Rect。
    /// 已切片（Multiple）的立绘也适用，只改网格类型不动切片数据。
    /// </summary>
    [MenuItem("Tools/美术全部改用 Full Rect 网格")]
    public static void ForceFullRectMesh()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture", new[] { DestResourceRoot });
        int changed = 0;
        try
        {
            AssetDatabase.StartAssetEditing();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer
                    || importer.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.FullRect)
                {
                    continue;
                }

                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
                changed++;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        Debug.Log($"[ArtSync] 已将 {changed} 张 Sprite 改为 Full Rect 网格。");
    }

    private static IEnumerable<string> EnumerateImages(string root)
    {
        foreach (string path in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (System.Array.IndexOf(ImageExtensions, extension) >= 0)
            {
                yield return path.Replace('\\', '/');
            }
        }
    }

    private static bool ConfigureAsSprite(string assetPath)
    {
        if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
        {
            return false;
        }

        bool changed = false;
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

        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;

        // UI 图一律用 Full Rect：Tight 网格会触发 Burst AOT 编译 FindTightRectJob，
        // Unity 6 在部分 macOS arm64 机器上 burst-lld 链接失败（弹 bcl/burst-lld-21 报错）。
        // Unity 6 移除了 TextureImporter.spriteMeshType 公开属性，改走 TextureImporterSettings。
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        if (settings.spriteMeshType != SpriteMeshType.FullRect)
        {
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
            return true;
        }

        return false;
    }

    private static int DeleteOrphans(HashSet<string> sourceFiles)
    {
        if (!Directory.Exists(DestResourceRoot))
        {
            return 0;
        }

        int deleted = 0;
        foreach (string destPath in Directory.GetFiles(DestResourceRoot, "*.*", SearchOption.AllDirectories))
        {
            string normalized = destPath.Replace('\\', '/');
            string extension = Path.GetExtension(normalized).ToLowerInvariant();
            if (System.Array.IndexOf(ImageExtensions, extension) < 0)
            {
                continue;
            }

            string relative = Path.GetRelativePath(DestResourceRoot, normalized).Replace('\\', '/');
            if (sourceFiles.Contains(relative))
            {
                continue;
            }

            AssetDatabase.DeleteAsset(normalized);
            deleted++;
        }

        return deleted;
    }

    private static string Combine(string root, string relative)
    {
        return (root + "/" + relative.Replace('\\', '/')).Replace("//", "/");
    }
}
#endif
