using System.IO;
using PixelBeat.Core;
using PixelBeat.UI;
using UnityEditor;
using UnityEngine;

namespace PixelBeat.EditorTools.NeonArt
{
    /// <summary>
    /// *PixelBeat > Bake Neon Sprites*: NeonPixelArt desenlerini PNG olarak Assets/Art/Neon/ altına yazar
    /// (Point, sıkıştırmasız, PPU 16, 9-slice kenarları, Full Rect mesh), additive materyali oluşturur ve NeonSkin'i doldurur.
    /// </summary>
    public static class NeonSpriteBaker
    {
        public const string Folder = "Assets/Art/Neon";
        public const string SkinPath = "Assets/Data/NeonSkin.asset";
        public const string MaterialPath = "Assets/Art/Materials/NeonAdditive.mat";
        private const float Ppu = 16f;

        [MenuItem("PixelBeat/Bake Neon Sprites")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null)
            {
                var shader = Shader.Find("PixelBeat/SpriteAdditive");
                mat = new Material(shader) { name = "NeonAdditive" };
                AssetDatabase.CreateAsset(mat, MaterialPath);
            }

            var heads = new Sprite[NeonSkin.PaletteSize];
            var bodies = new Sprite[NeonSkin.PaletteSize];
            var tails = new Sprite[NeonSkin.PaletteSize];
            var glows = new Sprite[NeonSkin.PaletteSize];
            var pressed = new Sprite[NeonSkin.PaletteSize];
            var flashes = new Sprite[NeonSkin.PaletteSize];

            foreach (var c in NeonSkin.AccentColors)
            {
                string n = c.ToString().ToLowerInvariant();
                int i = (int)c;
                heads[i] = Write(NeonPixelArt.NoteHead(c), $"note_head_{n}", NeonPixelArt.NoteHeadSpec);
                bodies[i] = Write(NeonPixelArt.HoldBody(c), $"hold_body_{n}", NeonPixelArt.HoldBodySpec);
                tails[i] = Write(NeonPixelArt.HoldTail(c), $"hold_tail_{n}", NeonPixelArt.HoldTailSpec);
                glows[i] = Write(NeonPixelArt.NoteGlow(c), $"note_glow_{n}", NeonPixelArt.NoteGlowSpec);
                pressed[i] = Write(NeonPixelArt.KeyPad(c, Palette.DarkOf(c), PaletteColor.White), $"keypad_pressed_{n}", NeonPixelArt.KeyPadSpec);
                flashes[i] = Write(NeonPixelArt.LaneFlash(c), $"lane_flash_{n}", NeonPixelArt.LaneFlashSpec);
            }

            var lane = Write(NeonPixelArt.LanePanel(), "lane_panel", NeonPixelArt.LanePanelSpec);
            var hitIdle = Write(NeonPixelArt.HitLine(PaletteColor.Haze, PaletteColor.Steel), "hitline_idle", NeonPixelArt.HitLineSpec);
            var hitBeat = Write(NeonPixelArt.HitLine(PaletteColor.White, PaletteColor.Haze), "hitline_beat", NeonPixelArt.HitLineSpec);
            var keyIdle = Write(NeonPixelArt.KeyPad(PaletteColor.Steel, PaletteColor.Night, PaletteColor.Slate), "keypad_idle", NeonPixelArt.KeyPadSpec);
            var p1 = Write(NeonPixelArt.Solid(1, PaletteColor.White), "particle_1", new NeonPixelArt.Spec { W = 1, H = 1 });
            var p2 = Write(NeonPixelArt.Solid(2, PaletteColor.White), "particle_2", new NeonPixelArt.Spec { W = 2, H = 2 });
            var sky = Write(NeonPixelArt.Sky(), "sky", new NeonPixelArt.Spec { W = NeonPixelArt.SkyWidth, H = NeonPixelArt.SkyHeight },
                new Vector2(0.5f, NeonPixelArt.SkyHorizon / (float)NeonPixelArt.SkyHeight));

            var skin = AssetDatabase.LoadAssetAtPath<NeonSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<NeonSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            skin.SetBaked(mat, lane, hitIdle, hitBeat, keyIdle, p1, p2, sky, heads, bodies, tails, glows, pressed, flashes);
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            Debug.Log($"[NeonSpriteBaker] Sprite'lar üretildi → {Folder}, skin → {SkinPath}");
        }

        /// <summary>Tuvali PNG'ye yazar ve sprite olarak içe aktarır.</summary>
        public static Sprite Write(PixelCanvas canvas, string name, NeonPixelArt.Spec spec, Vector2? pivot = null)
        {
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, ToTexture(canvas).EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Ppu;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.spriteBorder = new Vector4(spec.L, spec.B, spec.R, spec.T);

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; // Tiled/Sliced çizim için gerekli
            if (pivot.HasValue)
            {
                settings.spriteAlignment = (int)SpriteAlignment.Custom;
                settings.spritePivot = pivot.Value;
            }
            else
            {
                settings.spriteAlignment = (int)SpriteAlignment.Center;
            }
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        public static Texture2D ToTexture(PixelCanvas canvas)
        {
            var tex = new Texture2D(canvas.Width, canvas.Height, TextureFormat.RGBA32, false);
            var pixels = new Color32[canvas.Width * canvas.Height];
            for (int y = 0; y < canvas.Height; y++)
            for (int x = 0; x < canvas.Width; x++)
            {
                int idx = canvas[x, y];
                pixels[y * canvas.Width + x] = idx < 0 ? new Color32(0, 0, 0, 0) : Palette.Colors[idx];
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
