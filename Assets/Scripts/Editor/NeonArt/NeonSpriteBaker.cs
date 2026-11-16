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

            // Eski (180 px dönemi) PNG'ler: artık üretilmeyenler silinir ki klasörde karışmasın.
            foreach (var old in new[] { "note_head_", "hold_body_", "hold_tail_", "note_glow_", "keypad_pressed_", "lane_flash_" })
            foreach (var c in NeonSkin.AccentColors)
                AssetDatabase.DeleteAsset($"{Folder}/{old}{c.ToString().ToLowerInvariant()}.png");
            AssetDatabase.DeleteAsset($"{Folder}/keypad_idle.png");

            var b = new NeonSkin.BakedSet { Additive = mat };
            var simple = new NeonPixelArt.Spec();

            foreach (var c in NeonSkin.AccentColors)
            {
                string n = c.ToString().ToLowerInvariant();
                int i = (int)c;
                b.NoteHead[i] = Write(NeonPixelArt.Diamond(c), $"note_{n}", simple);
                b.NoteGlow[i] = Write(NeonPixelArt.DiamondHalo(c), $"note_halo_{n}", simple);
                b.HoldTail[i] = Write(NeonPixelArt.Diamond(c, NeonPixelArt.TailSize), $"hold_tail_{n}", simple);
                b.TailGlow[i] = Write(NeonPixelArt.DiamondHalo(c, NeonPixelArt.TailSize), $"hold_tail_halo_{n}", simple);
                b.HoldBody[i] = Write(NeonPixelArt.Beam(c), $"hold_beam_{n}", NeonPixelArt.BeamSpec);
                b.HoldGlow[i] = Write(NeonPixelArt.BeamGlow(c), $"hold_beam_glow_{n}", NeonPixelArt.BeamGlowSpec);
                b.KeyPadIdle[i] = Write(NeonPixelArt.KeyPadIdle(c), $"key_{n}", NeonPixelArt.KeyPadSpec);
                b.KeyPadPressed[i] = Write(NeonPixelArt.KeyPadPressed(c), $"key_pressed_{n}", NeonPixelArt.KeyPadSpec);
                b.KeyPadGlow[i] = Write(NeonPixelArt.FrameGlow(c), $"key_glow_{n}", NeonPixelArt.KeyGlowSpec);
                b.ButtonFrame[i] = Write(NeonPixelArt.ButtonFrame(c), $"button_{n}", NeonPixelArt.ButtonSpec);
                b.ButtonGlow[i] = b.KeyPadGlow[i];
                b.LaneFlash[i] = Write(NeonPixelArt.LaneFlash(c), $"flash_{n}", NeonPixelArt.LaneFlashSpec);
            }

            var wordDefs = new (string text, PaletteColor[] colors)[]
            {
                ("PERFECT!", new[] { PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta }),
                ("GREAT!", new[] { PaletteColor.Lime, PaletteColor.Cyan }),
                ("GOOD!", new[] { PaletteColor.Gold, PaletteColor.Orange }),
                ("MISS!", new[] { PaletteColor.Red, PaletteColor.Magenta }),
            };
            for (int i = 0; i < wordDefs.Length; i++)
            {
                var word = NeonPixelArt.Word(wordDefs[i].text, wordDefs[i].colors);
                string n = wordDefs[i].text.TrimEnd('!').ToLowerInvariant();
                b.Words[i] = Write(word, $"word_{n}", simple, Vector2.zero);
                b.WordGlows[i] = Write(NeonPixelArt.WordGlow(word, wordDefs[i].colors[0]), $"word_{n}_glow", simple, Vector2.zero);
            }

            // Sonuç etiketleri: derece harfleri büyük (7x), FAILED 4x, YENI REKOR! 3x, rozetler 2x.
            var labelDefs = new (string text, string file, int scale, PaletteColor[] colors)[]
            {
                ("S", "grade_s", 7, new[] { PaletteColor.White, PaletteColor.Gold, PaletteColor.Orange }),
                ("A", "grade_a", 7, new[] { PaletteColor.Cyan, PaletteColor.Violet, PaletteColor.Magenta }),
                ("B", "grade_b", 7, new[] { PaletteColor.Lime, PaletteColor.Cyan }),
                ("C", "grade_c", 7, new[] { PaletteColor.Gold, PaletteColor.Orange }),
                ("D", "grade_d", 7, new[] { PaletteColor.Red, PaletteColor.Plum }),
                ("FAILED", "failed", 4, new[] { PaletteColor.Red, PaletteColor.Magenta }),
                ("YENI REKOR!", "new_record", 3, new[] { PaletteColor.White, PaletteColor.Gold, PaletteColor.Orange }),
                ("FULL COMBO", "full_combo", 2, new[] { PaletteColor.Gold, PaletteColor.Orange }),
                ("ALL PERFECT", "all_perfect", 2, new[] { PaletteColor.Cyan, PaletteColor.Magenta }),
            };
            for (int i = 0; i < labelDefs.Length; i++)
            {
                var d = labelDefs[i];
                var label = NeonPixelArt.Word(d.text, d.scale, d.colors);
                b.Labels[i] = Write(label, $"label_{d.file}", simple, Vector2.zero);
                b.LabelGlows[i] = Write(NeonPixelArt.WordGlow(label, d.colors[d.colors.Length > 2 ? 1 : 0]), $"label_{d.file}_glow", simple, Vector2.zero);
            }
            b.IconBack = Write(NeonPixelArt.IconBack(PaletteColor.Cyan), "icon_back", simple);

            var logo = NeonPixelArt.Logo();
            b.Logo = Write(logo, "logo", simple);
            b.LogoGlow = Write(NeonPixelArt.GlowAround(logo, PaletteColor.Magenta, NeonPixelArt.LogoGlowMargin), "logo_glow", simple);
            b.IconPlay = Write(NeonPixelArt.IconPlay(PaletteColor.Cyan), "icon_play", simple);
            b.IconGear = Write(NeonPixelArt.IconGear(PaletteColor.Magenta), "icon_gear", simple);

            b.LanePanel = Write(NeonPixelArt.LanePanel(), "lane_panel", NeonPixelArt.LanePanelSpec);
            b.HitLineIdle = Write(NeonPixelArt.HitLine(PaletteColor.Haze, PaletteColor.Steel), "hitline_idle", NeonPixelArt.HitLineSpec);
            b.HitLineBeat = Write(NeonPixelArt.HitLine(PaletteColor.White, PaletteColor.Haze), "hitline_beat", NeonPixelArt.HitLineSpec);
            b.Particle1 = Write(NeonPixelArt.Solid(2, PaletteColor.White), "particle_1", new NeonPixelArt.Spec { W = 2, H = 2 });
            b.Particle2 = Write(NeonPixelArt.Solid(3, PaletteColor.White), "particle_2", new NeonPixelArt.Spec { W = 3, H = 3 });
            b.Sky = Write(NeonPixelArt.Sky(), "sky", new NeonPixelArt.Spec { W = NeonPixelArt.SkyWidth, H = NeonPixelArt.SkyHeight },
                new Vector2(0.5f, NeonPixelArt.SkyHorizon / (float)NeonPixelArt.SkyHeight));

            var skin = AssetDatabase.LoadAssetAtPath<NeonSkin>(SkinPath);
            if (skin == null)
            {
                skin = ScriptableObject.CreateInstance<NeonSkin>();
                AssetDatabase.CreateAsset(skin, SkinPath);
            }
            skin.SetBaked(b);
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
