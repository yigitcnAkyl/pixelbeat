using System.Collections.Generic;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using UnityEngine;

namespace PixelBeat.UI.Menu
{
    /// <summary>
    /// Şarkı listesi (GDD §11): katalogdaki şarkılar dikey, kaydırılabilir liste. Her satırda ad, sanatçı, süre ve
    /// o şarkıdaki en iyi derece. Satırlar liste alanı kadar bir SpriteMask ile kırpılır (arka plan görünür kalır).
    /// Satıra dokununca şarkı detayı açılır.
    /// </summary>
    public class SongListScreen : UiScreen
    {
        private const int RowH = 64;
        private const int RowGap = 12;
        private const int RowW = 320;
        /// <summary>Liste kenarı ile ilk/son satır arası (satır halesi kırpılmasın).</summary>
        private const int EdgePad = 6;

        private PixelButton back;
        private PixelText title;
        private Transform rowsRoot;
        private SpriteMask mask;
        private PixelText empty;
        private readonly List<Row> rows = new List<Row>();
        private readonly ScrollModel scroll = new ScrollModel();
        private int listTop, listBottom;
        private bool pressedInList;

        private class Row
        {
            public SongEntry Song;
            public PixelButton Frame;
            public PixelText Title, Artist, Duration, Grade;
        }

        protected override void Build()
        {
            (back, title) = Header("SARKILAR");
            empty = Text("Empty");

            rowsRoot = new GameObject("Rows").transform;
            rowsRoot.SetParent(Root, false);
            var maskGo = new GameObject("List Mask");
            maskGo.transform.SetParent(Root, false);
            mask = maskGo.AddComponent<SpriteMask>();
            mask.sprite = Ctx.Playfield.PixelSprite;

            if (Ctx.Catalog == null) return;
            foreach (var song in Ctx.Catalog.Songs)
            {
                if (song == null) continue;
                var row = new Row
                {
                    Song = song,
                    Frame = new PixelButton(Ctx.Playfield, rowsRoot, "", Order + 1, PaletteColor.Cyan),
                    Title = PixelText.Create("Title", rowsRoot, Order + 2),
                    Artist = PixelText.Create("Artist", rowsRoot, Order + 2),
                    Duration = PixelText.Create("Duration", rowsRoot, Order + 2),
                    Grade = PixelText.Create("Grade", rowsRoot, Order + 2),
                };
                rows.Add(row);
            }
            // Satırlar sadece liste alanında (maskenin içinde) görünür.
            foreach (var sr in rowsRoot.GetComponentsInChildren<SpriteRenderer>(true))
                sr.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
        }

        public override void Layout()
        {
            var l = Ctx.Layout;
            LayoutHeader(back, title, "SARKILAR");
            listTop = HeaderBottom - 12;
            listBottom = l.SafeBottomPx + 12;
            // Maske: hale taşmaları da görünsün diye liste alanının yanlarında pay.
            Playfield.PlaceRect(mask.transform, l.LeftPx, listBottom, l.RightPx - l.LeftPx, listTop - listBottom,
                Ctx.Playfield.PixelSprite.rect.width);

            scroll.SetSizes(rows.Count * (RowH + RowGap) - RowGap + 2 * EdgePad, listTop - listBottom);
            empty.Visible = rows.Count == 0;
            if (rows.Count == 0) empty.Set("KATALOG BOS", 0, 0, PixelText.Align.Center, Palette.Get(PaletteColor.Haze));
            LayoutRows();
        }

        private void LayoutRows()
        {
            int x = -RowW / 2;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = rows[i];
                int y = listTop - EdgePad - (i + 1) * RowH - i * RowGap + Mathf.RoundToInt(scroll.Offset);
                bool visible = y + RowH > listBottom && y < listTop;
                r.Frame.Visible = visible;
                r.Title.Visible = r.Artist.Visible = r.Duration.Visible = r.Grade.Visible = visible;
                if (!visible) continue;

                r.Frame.Place(x, y, RowW, RowH);
                int pad = 14, gh = PixelFont.UiGlyphHeight;
                r.Title.Set(r.Song.Title?.ToUpperInvariant() ?? "?", x + pad, y + RowH - pad - gh, PixelText.Align.Left, Palette.Get(PaletteColor.White));
                r.Artist.Set(string.IsNullOrEmpty(r.Song.Artist) ? "-" : r.Song.Artist.ToUpperInvariant(), x + pad, y + pad,
                    PixelText.Align.Left, Palette.Get(PaletteColor.Haze));
                r.Duration.Set(SongEntry.FormatDuration(r.Song.DurationSec), x + RowW - pad, y + RowH - pad - gh, PixelText.Align.Right,
                    Palette.Get(PaletteColor.Cyan));
                var best = Ctx.Scores.BestGrade(r.Song.Id);
                r.Grade.Set(best.HasValue ? best.Value.ToString() : "-", x + RowW - pad, y + pad, PixelText.Align.Right,
                    best.HasValue ? GradeColor(best.Value) : Palette.Get(PaletteColor.Steel));
            }
        }

        public override void OnPointerDown(Vector2 px)
        {
            pressedInList = px.y < listTop && px.y > listBottom;
            if (pressedInList) scroll.BeginDrag(px.y);
        }

        public override void OnDrag(Vector2 px)
        {
            if (!pressedInList) return;
            scroll.Drag(px.y);
            LayoutRows();
        }

        public override void OnPointerUp(Vector2 px, bool tap, float velocityY)
        {
            if (pressedInList) scroll.EndDrag(tap ? 0f : velocityY);
            pressedInList = false;
            base.OnPointerUp(px, tap, velocityY);
        }

        public override void Tick(float dt)
        {
            if (scroll.Dragging || Mathf.Approximately(scroll.Velocity, 0f)) return;
            scroll.Tick(dt);
            LayoutRows();
        }

        protected override void OnTap(Vector2 px)
        {
            if (back.Contains(px))
            {
                Ctx.Router.Back();
                return;
            }
            if (px.y >= listTop || px.y <= listBottom) return;
            foreach (var r in rows)
            {
                if (!r.Frame.Contains(px)) continue;
                Ctx.SelectedSong = r.Song;
                Ctx.Router.Navigate(ScreenId.SongDetail);
                return;
            }
        }
    }
}
