using System.IO;
using UnityEditor;
using UnityEngine;

namespace PixelBeat.EditorTools.NeonArt
{
    /// <summary>
    /// *PixelBeat > AI Görsel İçe Aktar*: Assets/Art/Incoming/ içindeki bir görseli seçilen piksel boyutuna
    /// nearest-neighbor ile küçültür, GDD paletine indirger ve Point / sıkıştırmasız / PPU 16 sprite olarak Assets/Art/ altına kaydeder.
    /// Kaynak dosya ham okunur (import ayarlarından bağımsız).
    /// </summary>
    public class PixelArtImportWindow : EditorWindow
    {
        private const string IncomingFolder = "Assets/Art/Incoming";

        [SerializeField] private Texture2D source;
        [SerializeField] private int targetWidth = 180;
        [SerializeField] private int targetHeight;
        [SerializeField] private bool keepAspect = true;
        [SerializeField] private int alphaThreshold = 128;
        [SerializeField] private bool dither;
        [SerializeField] private string outputFolder = "Assets/Art/Imported";
        [SerializeField] private string outputName = "";
        [SerializeField] private SpriteAlignment pivot = SpriteAlignment.Center;

        private Texture2D preview;
        private Texture2D sourcePreview;
        private string status = "";

        [MenuItem("PixelBeat/AI Görsel İçe Aktar")]
        public static void Open() => GetWindow<PixelArtImportWindow>("AI Görsel İçe Aktar");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox($"Görseli {IncomingFolder}/ içine koy, buradan seç. Sonuç sadece GDD paletindeki 16 rengi kullanır.", MessageType.None);

            EditorGUI.BeginChangeCheck();
            source = (Texture2D)EditorGUILayout.ObjectField("Kaynak görsel", source, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck() && source != null)
            {
                if (string.IsNullOrEmpty(outputName)) outputName = source.name;
                LoadSourcePreview();
            }

            if (source != null && !AssetDatabase.GetAssetPath(source).StartsWith(IncomingFolder))
                EditorGUILayout.HelpBox($"Kaynak {IncomingFolder}/ dışında; yine de çalışır.", MessageType.Warning);

            targetWidth = Mathf.Max(1, EditorGUILayout.IntField("Hedef genişlik (px)", targetWidth));
            keepAspect = EditorGUILayout.Toggle("Oranı koru", keepAspect);
            using (new EditorGUI.DisabledScope(keepAspect))
                targetHeight = Mathf.Max(1, EditorGUILayout.IntField("Hedef yükseklik (px)", keepAspect ? AspectHeight() : targetHeight));
            alphaThreshold = EditorGUILayout.IntSlider("Alfa eşiği", alphaThreshold, 0, 255);
            dither = EditorGUILayout.Toggle(new GUIContent("Dither (2x2 Bayer)", "Renk geçişlerinde bant yerine dama doku."), dither);
            pivot = (SpriteAlignment)EditorGUILayout.EnumPopup(new GUIContent("Pivot", "Arka plan için ufuk çizgisini pivot yapmak istersen Custom'ı import ayarlarından düzenle."), pivot);
            outputFolder = EditorGUILayout.TextField("Çıktı klasörü", outputFolder);
            outputName = EditorGUILayout.TextField("Çıktı adı", outputName);

            using (new EditorGUI.DisabledScope(source == null))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Önizle")) Convert(save: false);
                if (GUILayout.Button("Dönüştür ve kaydet", GUILayout.Height(24))) Convert(save: true);
                EditorGUILayout.EndHorizontal();
            }

            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            DrawPreview(sourcePreview, "Önce");
            DrawPreview(preview, "Sonra");
            EditorGUILayout.EndHorizontal();
        }

        private int AspectHeight()
        {
            if (sourcePreview == null) return targetHeight > 0 ? targetHeight : targetWidth;
            return Mathf.Max(1, Mathf.RoundToInt(targetWidth * sourcePreview.height / (float)sourcePreview.width));
        }

        private static void DrawPreview(Texture2D tex, string label)
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField(label);
            if (tex != null)
            {
                var r = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true));
                EditorGUI.DrawTextureTransparent(r, tex, ScaleMode.ScaleToFit);
            }
            EditorGUILayout.EndVertical();
        }

        private void LoadSourcePreview()
        {
            if (sourcePreview != null) DestroyImmediate(sourcePreview);
            sourcePreview = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            sourcePreview.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
            sourcePreview.filterMode = FilterMode.Point;
        }

        private void Convert(bool save)
        {
            if (sourcePreview == null) LoadSourcePreview();
            int w = targetWidth;
            int h = keepAspect ? AspectHeight() : targetHeight;

            var resized = PaletteImageTools.ResizeNearest(sourcePreview.GetPixels32(), sourcePreview.width, sourcePreview.height, w, h);
            var quantized = PaletteImageTools.Quantize(resized, w, (byte)alphaThreshold, dither);

            if (preview != null) DestroyImmediate(preview);
            preview = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            preview.SetPixels32(quantized);
            preview.Apply();
            status = $"{sourcePreview.width}x{sourcePreview.height} → {w}x{h}";

            if (!save) return;

            Directory.CreateDirectory(outputFolder);
            string path = $"{outputFolder.TrimEnd('/')}/{outputName}.png";
            File.WriteAllBytes(path, preview.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            status += $"\nKaydedildi: {path}";
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }

        private void OnDestroy()
        {
            if (preview != null) DestroyImmediate(preview);
            if (sourcePreview != null) DestroyImmediate(sourcePreview);
        }
    }
}
