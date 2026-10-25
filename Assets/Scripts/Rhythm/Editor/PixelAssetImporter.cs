#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace RhythmGame.Editor
{
    /// <summary>
    /// Assets/Sprites/Sliced altındaki tüm görselleri Sprite (PPU: 32, Point Filter, No Compression)
    /// olarak yapılandırır.
    /// </summary>
    public static class PixelAssetImporter
    {
        [MenuItem("Rhythm Game/🎨 Setup All Sliced Cyberpunk Sprites", false, 15)]
        public static void SetupAllSlicedSprites()
        {
            string[] folders = new string[] { "Assets/Sprites/Sliced/Buttons", "Assets/Sprites/Sliced/Gameplay" };
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", folders);

            int count = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    bool modified = false;
                    if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; modified = true; }
                    if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; modified = true; }
                    if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; modified = true; }
                    if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; modified = true; }
                    if (importer.mipmapEnabled) { importer.mipmapEnabled = false; modified = true; }
                    if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; modified = true; }
                    if (Mathf.Abs(importer.spritePixelsPerUnit - 32f) > 0.01f) { importer.spritePixelsPerUnit = 32f; modified = true; }

                    if (modified)
                    {
                        importer.SaveAndReimport();
                        count++;
                    }
                }
            }

            if (count > 0)
            {
                Debug.Log($"<color=#00ff88><b>[PixelAssetImporter]</b> {count} adet Cyberpunk piksel sprite'ı (PPU: 32, Point Filter) yapılandırıldı!</color>");
            }
        }
    }
}
#endif
