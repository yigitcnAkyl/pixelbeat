using System.Collections.Generic;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>
    /// Şarkı detayı (GDD §11): ad, sanatçı, süre; harita türü sekmeleri (VOKAL / MELODI, sadece katalogda olanlar);
    /// KOLAY / NORMAL / ZOR satırları (her birinde en yüksek skor + derece, harita yoksa YOK); BASLA.
    /// Son seçilen tür ve zorluk PlayerPrefs'te hatırlanır.
    /// </summary>
    public class SongDetailScreen : UiScreen
    {
        private const string LastTypeKey = "lastMapType";
        private const string LastDifficultyKey = "lastDifficulty";
        private const int RowW = 320, RowH = 44, RowGap = 10;

        private static readonly string[] DifficultyNames = { "", "KOLAY", "NORMAL", "ZOR" };

        private PixelButton back;
        private PixelText header;
        private SpriteRenderer backdrop;
        private PixelText songTitle, artist, duration, typeLabel;
        private readonly Dictionary<MapType, PixelButton> tabs = new Dictionary<MapType, PixelButton>();
        private readonly PixelButton[] diffRows = new PixelButton[3];
        private readonly PixelText[] diffLabels = new PixelText[3];
        private readonly PixelText[] diffScores = new PixelText[3];
        private readonly PixelText[] diffGrades = new PixelText[3];
        private PixelButton start;

        private SongEntry song;
        private MapType type;
        private SongDifficulty difficulty;

        protected override void Build()
        {
            (back, header) = Header("SARKI");
            backdrop = Backdrop();
            songTitle = Text("Song Title");
            artist = Text("Artist");
            duration = Text("Duration");
            typeLabel = Text("Type Label");
            tabs[MapType.Vocal] = Button("VOKAL", PaletteColor.Ash);
            tabs[MapType.Melody] = Button("MELODI", PaletteColor.Ash);
            for (int i = 0; i < 3; i++)
            {
                diffRows[i] = Button("", PaletteColor.Ash);
                diffLabels[i] = Text($"Label {i}");
                diffScores[i] = Text($"Score {i}");
                diffGrades[i] = Text($"Grade {i}");
            }
            start = Button("BASLA", PaletteColor.Lime, Ctx.Skin.IconPlay);
        }

        public override void Show()
        {
            song = Ctx.SelectedSong;
            if (song != null)
            {
                var types = song.AvailableTypes();
                var lastType = (MapType)PlayerPrefs.GetInt(LastTypeKey, (int)MapType.Vocal);
                type = types.Contains(lastType) ? lastType : types.Count > 0 ? types[0] : MapType.Vocal;
                difficulty = (SongDifficulty)PlayerPrefs.GetInt(LastDifficultyKey, (int)SongDifficulty.Normal);
                FixDifficulty();
            }
            base.Show();
        }

        /// <summary>Seçili zorluk bu türde yoksa ilk mevcut zorluğa geçer.</summary>
        private void FixDifficulty()
        {
            if (song.Has(type, difficulty)) return;
            foreach (var d in SongEntry.Difficulties)
                if (song.Has(type, d))
                {
                    difficulty = d;
                    return;
                }
        }

        public override void Layout()
        {
            LayoutHeader(back, header, "SARKI");
            LayoutBackdrop(backdrop);
            if (song == null) return;

            int y = HeaderBottom - 20 - PixelFont.UiGlyphHeight * 2;
            songTitle.Set(song.Title?.ToUpperInvariant() ?? "?", 0, y, PixelText.Align.Center, Palette.Get(PaletteColor.White), 2);
            y -= 10 + PixelFont.UiGlyphHeight;
            artist.Set(string.IsNullOrEmpty(song.Artist) ? "-" : song.Artist.ToUpperInvariant(), 0, y, PixelText.Align.Center,
                Palette.Get(PaletteColor.Haze));
            y -= 6 + PixelFont.UiGlyphHeight;
            duration.Set(SongEntry.FormatDuration(song.DurationSec), 0, y, PixelText.Align.Center, Palette.Get(PaletteColor.Cyan));

            // Tür sekmeleri
            y -= 30 + PixelFont.UiGlyphHeight;
            typeLabel.Set("HARITA", 0, y, PixelText.Align.Center, Palette.Get(PaletteColor.Steel));
            y -= 8 + PixelButton.Height;
            var types = song.AvailableTypes();
            const int tabW = 140, tabGap = 12;
            int total = types.Count * tabW + (types.Count - 1) * tabGap;
            int x = -total / 2;
            foreach (var kv in tabs) kv.Value.Visible = types.Contains(kv.Key);
            foreach (var t in types)
            {
                var tab = tabs[t];
                bool selected = t == type;
                tab.SetColor(selected ? PaletteColor.Cyan : PaletteColor.Ash, selected ? 0.7f : 0f);
                tab.Place(x, y, tabW);
                x += tabW + tabGap;
            }

            // Zorluk satırları
            y -= 24;
            for (int i = 0; i < 3; i++)
            {
                var d = SongEntry.Difficulties[i];
                bool has = song.Has(type, d);
                bool selected = has && d == difficulty;
                y -= RowH;
                var row = diffRows[i];
                row.SetColor(selected ? PaletteColor.Magenta : PaletteColor.Ash, selected ? 0.7f : 0f);
                row.Place(-RowW / 2, y, RowW, RowH);
                int ty = y + (RowH - PixelFont.UiGlyphHeight) / 2;
                diffLabels[i].Set(DifficultyNames[(int)d], -RowW / 2 + 16, ty, PixelText.Align.Left,
                    Palette.Get(selected ? PaletteColor.Magenta : has ? PaletteColor.Haze : PaletteColor.Steel));

                var rec = Ctx.Scores.Get(song.Id, type, d);
                string scoreText = !has ? "YOK" : rec != null ? rec.score.ToString("0000000") : "-------";
                diffScores[i].Set(scoreText, RowW / 2 - 50, ty, PixelText.Align.Right,
                    Palette.Get(has ? PaletteColor.White : PaletteColor.Steel));
                diffGrades[i].Visible = rec != null && has;
                if (rec != null && has)
                    diffGrades[i].Set(rec.grade.ToString() + (rec.allPerfect ? "+" : ""), RowW / 2 - 16, ty, PixelText.Align.Right, GradeColor(rec.grade));
                y -= RowGap;
            }

            // BASLA
            var l = Ctx.Layout;
            const int sw = 220, sh = 44;
            start.Visible = song.Has(type, difficulty);
            start.Place(-sw / 2, Mathf.Min(y - 30 - sh, l.SafeBottomPx + 60), sw, sh);
        }

        protected override void OnTap(Vector2 px)
        {
            if (back.Contains(px))
            {
                Ctx.Router.Back();
                return;
            }
            if (song == null) return;

            foreach (var kv in tabs)
            {
                if (!kv.Value.Contains(px) || kv.Key == type) continue;
                type = kv.Key;
                PlayerPrefs.SetInt(LastTypeKey, (int)type);
                FixDifficulty();
                Ctx.Sfx.Click();
                Layout();
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                var d = SongEntry.Difficulties[i];
                if (!diffRows[i].Contains(px) || !song.Has(type, d)) continue;
                difficulty = d;
                PlayerPrefs.SetInt(LastDifficultyKey, (int)d);
                Ctx.Sfx.Click();
                Layout();
                return;
            }

            if (start.Contains(px))
            {
                PlayerPrefs.Save();
                Ctx.Router.StartGame(song, song.Get(type, difficulty));
            }
        }
    }
}
