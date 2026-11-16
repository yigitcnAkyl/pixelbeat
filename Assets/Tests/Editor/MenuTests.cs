using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PixelBeat.Core;
using PixelBeat.Gameplay;
using PixelBeat.UI;
using UnityEngine;

namespace PixelBeat.Tests
{
    /// <summary>Arayüz (GDD §11): skor kaydı, ekran yığını / geri tuşu, ayarlar, kaydırma, şarkı kataloğu.</summary>
    public class MenuTests
    {
        // ---------- ScoreBook ----------

        private static readonly string Key = ScoreBook.Key("lumiere", MapType.Vocal, SongDifficulty.Normal);

        [Test]
        public void ScoreBook_FirstResultIsRecord_HigherScoreReplaces_LowerKeepsBest()
        {
            var book = new ScoreBook();
            Assert.IsTrue(book.Submit(Key, 500000, Grade.C, 120, false, false), "ilk sonuç rekor");
            Assert.IsFalse(book.Submit(Key, 400000, Grade.D, 300, true, false), "düşük skor rekor değil");
            var r = book.Get(Key);
            Assert.AreEqual(500000, r.score, "skor en iyisi kalır");
            Assert.AreEqual(Grade.C, r.grade, "derece en iyi skorunki");
            Assert.AreEqual(300, r.maxCombo, "max combo ayrıca en iyisi");
            Assert.IsTrue(r.fullCombo, "FC bir kez kazanılınca kalır");

            Assert.IsTrue(book.Submit(Key, 900000, Grade.A, 50, false, false), "daha yüksek skor rekor");
            r = book.Get(Key);
            Assert.AreEqual(900000, r.score);
            Assert.AreEqual(Grade.A, r.grade);
            Assert.AreEqual(300, r.maxCombo);
            Assert.IsTrue(r.fullCombo, "FC kaybolmaz");
            Assert.IsFalse(book.Submit(Key, 900000, Grade.A, 10, false, true), "eşit skor rekor değil");
            Assert.IsTrue(book.Get(Key).allPerfect, "AP eşit skorda bile işlenir");
        }

        [Test]
        public void ScoreBook_KeysSeparateTypeAndDifficulty_BestGradePerSong()
        {
            var book = new ScoreBook();
            book.Submit(ScoreBook.Key("a", MapType.Vocal, SongDifficulty.Easy), 950000, Grade.S, 1, false, false);
            book.Submit(ScoreBook.Key("a", MapType.Melody, SongDifficulty.Hard), 700000, Grade.B, 1, false, false);
            book.Submit(ScoreBook.Key("ab", MapType.Melody, SongDifficulty.Hard), 100, Grade.D, 1, false, false);
            Assert.AreEqual(3, book.Count);
            Assert.AreEqual(Grade.S, book.BestGrade("a"));
            Assert.AreEqual(Grade.D, book.BestGrade("ab"), "önek çakışması karışmaz");
            Assert.IsNull(book.BestGrade("yok"));
            Assert.IsNull(book.Get("a", MapType.Vocal, SongDifficulty.Hard));
        }

        [Test]
        public void ScoreBook_JsonRoundTrip_AndFile()
        {
            var book = new ScoreBook();
            book.Submit(Key, 812345, Grade.A, 222, true, false);
            var back = ScoreBook.FromJson(book.ToJson());
            var r = back.Get(Key);
            Assert.AreEqual(812345, r.score);
            Assert.AreEqual(Grade.A, r.grade);
            Assert.AreEqual(222, r.maxCombo);
            Assert.IsTrue(r.fullCombo);

            string dir = Path.Combine(Path.GetTempPath(), "pixelbeat_scorebook_test");
            string path = Path.Combine(dir, ScoreBook.FileName);
            try
            {
                book.Save(path);
                Assert.AreEqual(812345, ScoreBook.Load(path).Get(Key).score);
                Assert.IsFalse(File.Exists(path + ".tmp"), "geçici dosya kalmaz");
            }
            finally
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
        }

        [Test]
        public void ScoreBook_CorruptOrMissingFile_OpensEmpty()
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            Assert.AreEqual(0, ScoreBook.FromJson("{ bu json değil").Count);
            Assert.AreEqual(0, ScoreBook.FromJson("").Count);
            Assert.AreEqual(0, ScoreBook.Load(Path.Combine(Path.GetTempPath(), "yok_pixelbeat", "scores.json")).Count);
        }

        // ---------- Ekran yığını / geri tuşu ----------

        private enum Scr { Main, List, Detail, Settings, Calibration, Game }

        [Test]
        public void ScreenStack_BackReturnsPrevious_RootMeansQuit()
        {
            var s = new ScreenStack<Scr>();
            s.Reset(Scr.Main);
            s.Push(Scr.List);
            s.Push(Scr.Detail);
            s.Push(Scr.Game);
            Assert.AreEqual(Scr.Game, s.Current);

            Assert.IsTrue(s.TryBack(out var p));
            Assert.AreEqual(Scr.Detail, p, "oyundan geri → şarkı detayı");
            Assert.IsTrue(s.TryBack(out p));
            Assert.AreEqual(Scr.List, p);
            Assert.IsTrue(s.TryBack(out p));
            Assert.AreEqual(Scr.Main, p);
            Assert.IsFalse(s.TryBack(out p), "ana menüde geri = çıkış");
            Assert.AreEqual(Scr.Main, s.Current);
        }

        [Test]
        public void ScreenStack_RevisitingScreen_DoesNotGrowHistory()
        {
            var s = new ScreenStack<Scr>();
            s.Reset(Scr.Main);
            s.Push(Scr.Settings);
            s.Push(Scr.Calibration);
            s.Push(Scr.Settings); // kalibrasyondan ayarlara "ileri" gidilse bile döngü birikmez
            Assert.AreEqual(2, s.Count);
            Assert.IsTrue(s.TryBack(out var p));
            Assert.AreEqual(Scr.Main, p);
        }

        // ---------- Ayarlar ----------

        private sealed class MemoryStore : ISettingsStore
        {
            public readonly Dictionary<string, float> Values = new Dictionary<string, float>();
            public int Saves;
            public bool Has(string key) => Values.ContainsKey(key);
            public float GetFloat(string key, float fallback) => Values.TryGetValue(key, out var v) ? v : fallback;
            public void SetFloat(string key, float value) => Values[key] = value;
            public void Save() => Saves++;
        }

        [Test]
        public void Settings_DefaultsSaveLoadAndClamp()
        {
            var store = new MemoryStore();
            var d = GameSettings.Load(store);
            Assert.AreEqual(GameSettings.DefaultScrollSec, d.NoteScrollSec);
            Assert.AreEqual(GameSettings.DefaultMusicVolume, d.MusicVolume);
            Assert.AreEqual(GameSettings.DefaultSfxVolume, d.SfxVolume);

            d.NoteScrollSec = 0.65f;
            d.MusicVolume = 0.3f;
            d.SfxVolume = 0f;
            d.Save(store);
            Assert.AreEqual(1, store.Saves);
            var back = GameSettings.Load(store);
            Assert.AreEqual(0.65f, back.NoteScrollSec, 1e-6);
            Assert.AreEqual(0.3f, back.MusicVolume, 1e-6);
            Assert.AreEqual(0f, back.SfxVolume, 1e-6);

            // Dışarıdan bozulmuş değerler kırpılır.
            store.Values[GameSettings.ScrollKey] = 99f;
            store.Values[GameSettings.MusicKey] = -1f;
            store.Values[GameSettings.SfxKey] = float.NaN;
            var clamped = GameSettings.Load(store);
            Assert.AreEqual(GameSettings.MaxScrollSec, clamped.NoteScrollSec);
            Assert.AreEqual(0f, clamped.MusicVolume);
            Assert.That(clamped.SfxVolume, Is.InRange(0f, 1f));
            clamped.NoteScrollSec = 0.01f;
            Assert.AreEqual(GameSettings.MinScrollSec, clamped.NoteScrollSec);
        }

        // ---------- Kaydırma ----------

        [Test]
        public void Scroll_ClampsToContent_AndFlingDecays()
        {
            var s = new ScrollModel();
            s.SetSizes(1000, 400);
            Assert.AreEqual(600, s.MaxOffset);

            s.BeginDrag(0);
            s.Drag(-50); // parmak aşağı: başın üstüne çıkılamaz
            Assert.AreEqual(0, s.Offset);
            s.Drag(250);
            Assert.AreEqual(250, s.Offset);
            s.Drag(5000);
            Assert.AreEqual(600, s.Offset, "sona sınırlı");

            s.BeginDrag(0);
            s.Drag(-300);
            Assert.AreEqual(300, s.Offset);
            s.EndDrag(800);
            float before = s.Offset;
            for (int i = 0; i < 120; i++) s.Tick(1f / 60f);
            Assert.Greater(s.Offset, before, "fırlatma devam eder");
            Assert.LessOrEqual(s.Offset, s.MaxOffset);
            Assert.AreEqual(0f, s.Velocity, "zamanla durur");

            var small = new ScrollModel();
            small.SetSizes(100, 400);
            Assert.AreEqual(0, small.MaxOffset, "içerik sığıyorsa kaymaz");
        }

        [Test]
        public void Scroll_TapThreshold()
        {
            Assert.IsTrue(ScrollModel.IsTap(new Vector2(0, 0), new Vector2(5, 5)));
            Assert.IsFalse(ScrollModel.IsTap(new Vector2(0, 0), new Vector2(0, 12)));
        }

        // ---------- Şarkı kataloğu ----------

        [Test]
        public void Catalog_ListsAvailableTypesInOrder_AndFindsMaps()
        {
            var a = new TextAsset("{}");
            var maps = new List<MapEntry>
            {
                new MapEntry(MapType.Melody, SongDifficulty.Hard, a),
                new MapEntry(MapType.Melody, SongDifficulty.Easy, a),
                new MapEntry(MapType.Vocal, SongDifficulty.Normal, null), // beatmap yok → sayılmaz
            };
            var song = new SongEntry("x", "X", "Y", null, maps);
            CollectionAssert.AreEqual(new[] { MapType.Melody }, song.AvailableTypes());
            Assert.IsTrue(song.Has(MapType.Melody, SongDifficulty.Hard));
            Assert.IsFalse(song.Has(MapType.Melody, SongDifficulty.Normal));
            Assert.IsFalse(song.Has(MapType.Vocal, SongDifficulty.Normal));
            Assert.AreEqual(0, song.DurationSec);

            maps.Add(new MapEntry(MapType.Vocal, SongDifficulty.Easy, a));
            CollectionAssert.AreEqual(new[] { MapType.Vocal, MapType.Melody }, song.AvailableTypes(), "Vokal önce");

            var catalog = ScriptableObject.CreateInstance<SongCatalog>();
            catalog.SetSongs(new List<SongEntry> { song });
            Assert.AreSame(song, catalog.Find("x"));
            Assert.IsNull(catalog.Find("z"));
            Object.DestroyImmediate(catalog);
        }

        [Test]
        public void Duration_FormatsMinutesSeconds()
        {
            Assert.AreEqual("3:42", SongEntry.FormatDuration(222.1));
            Assert.AreEqual("0:05", SongEntry.FormatDuration(5));
            Assert.AreEqual("0:00", SongEntry.FormatDuration(-3));
        }

        [Test]
        public void ProjectCatalog_LumiereHasSixMaps()
        {
            var catalog = UnityEditor.AssetDatabase.LoadAssetAtPath<SongCatalog>("Assets/Data/SongCatalog.asset");
            Assert.IsNotNull(catalog, "Assets/Data/SongCatalog.asset");
            var song = catalog.Find("lumiere");
            Assert.IsNotNull(song);
            foreach (var t in new[] { MapType.Vocal, MapType.Melody })
            foreach (var d in SongEntry.Difficulties)
                Assert.IsTrue(song.Has(t, d), $"{t} {d}");
        }
    }
}
