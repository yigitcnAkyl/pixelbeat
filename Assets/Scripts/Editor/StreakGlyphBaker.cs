using System.IO;
using PixelBeat.UI;
using UnityEditor;
using UnityEngine;

namespace PixelBeat.EditorTools
{
    /// <summary>
    /// StreakGlyphSet için döndürülmüş pixel sprite'ları üretir: "PERFECT" (tek parça) ve 0–9 rakamları (normal + büyüme ölçeği).
    /// PNG'ler Point / sıkıştırmasız / PPU 16 / merkez pivot olarak içe aktarılır.
    /// </summary>
    public static class StreakGlyphBaker
    {
        private const string DefaultAssetPath = "Assets/Data/StreakGlyphs.asset";
        private const float PixelsPerUnit = 16f;

        [MenuItem("PixelBeat/Bake Streak Glyphs")]
        public static void BakeDefault()
        {
            var set = AssetDatabase.LoadAssetAtPath<StreakGlyphSet>(DefaultAssetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<StreakGlyphSet>();
                AssetDatabase.CreateAsset(set, DefaultAssetPath);
            }
            Bake(set);
            Selection.activeObject = set;
        }

        public static void Bake(StreakGlyphSet set)
        {
            string folder = set.OutputFolder.TrimEnd('/');
            Directory.CreateDirectory(folder);
            float angle = set.AngleDeg;

            var label = BakeOne(PixelFont.Rasterize(StreakGlyphSet.LabelText, set.LabelScale), angle, $"{folder}/streak_perfect.png");

            var digits = new Sprite[10];
            var bumpDigits = new Sprite[10];
            for (int d = 0; d < 10; d++)
            {
                string s = d.ToString();
                digits[d] = BakeOne(PixelFont.Rasterize(s, set.NumberScale), angle, $"{folder}/streak_digit_{d}.png");
                bumpDigits[d] = BakeOne(PixelFont.Rasterize(s, set.BumpScale), angle, $"{folder}/streak_digit_{d}_bump.png");
            }

            set.SetBaked(angle, label, digits, bumpDigits);
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();
            Debug.Log($"[StreakGlyphBaker] {angle}° ile 21 sprite üretildi → {folder}");
        }

        private static Sprite BakeOne(bool[,] mask, float angle, string path)
        {
            var rotated = PixelRotator.RotateClockwise(mask, angle);
            int w = rotated.GetLength(0), h = rotated.GetLength(1);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new Color32[w * h];
            var white = new Color32(255, 255, 255, 255);
            var clear = new Color32(0, 0, 0, 0);
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                pixels[y * w + x] = rotated[x, y] ? white : clear;
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Center; // boyutlar çift → merkez tam piksel sınırında
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }

    [CustomEditor(typeof(StreakGlyphSet))]
    public class StreakGlyphSetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var set = (StreakGlyphSet)target;

            EditorGUILayout.Space();
            if (set.NeedsRebake)
                EditorGUILayout.HelpBox("Sprite'lar şimdiki açıyla üretilmemiş. Aşağıdaki butona bas.", MessageType.Warning);

            if (GUILayout.Button("Sprite'ları üret", GUILayout.Height(28)))
                StreakGlyphBaker.Bake(set);
        }
    }
}
