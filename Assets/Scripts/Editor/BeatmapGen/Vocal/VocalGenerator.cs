using System;
using System.Collections.Generic;
using PixelBeat.EditorTools.BeatmapGen.Melody;

namespace PixelBeat.EditorTools.BeatmapGen.Vocal
{
    public enum NoteSource
    {
        Vocal,
        Instrument,
    }

    /// <summary>Vokal modu girdileri (hepsi analiz edilmiş; Unity'den bağımsız).</summary>
    public sealed class VocalInputs
    {
        /// <summary>Vokal MIDI'sinin notaları.</summary>
        public IReadOnlyList<MidiNote> VocalMidi;
        /// <summary>Vokal kapısının bulduğu vokal aktif bölümleri.</summary>
        public IReadOnlyList<Span> Sections;
        /// <summary>Vokal stem'indeki onset zamanları (hece bölme).</summary>
        public IReadOnlyList<double> VocalOnsets;
        /// <summary>Tam mix MIDI'sinin §7.1 melodi hattı (enstrümantal aralar için).</summary>
        public IReadOnlyList<MelodyNote> InstrumentLine;
        /// <summary>Orijinal şarkıdan beat zamanları.</summary>
        public IReadOnlyList<double> BeatTimes;
        public double SongDurationSec;
        public double OffsetSec;
    }

    public sealed class VocalResult
    {
        public GeneratorResult Map;
        /// <summary>Map.Notes ile aynı sırada her notanın kaynağı.</summary>
        public List<NoteSource> Sources = new List<NoteSource>();
        /// <summary>Vokal bölgeleri: vokal bölümleri + heceler, 4 sn'den kısa boşluklar dahil birleşik. Burada sadece vokal notası olur.</summary>
        public List<Span> VocalRegions = new List<Span>();
        /// <summary>Enstrüman dolgusunun girebildiği aralıklar (≥ 4 sn susma, sınırlardan 0.5 sn içeride).</summary>
        public List<Span> FillRegions = new List<Span>();
        public int GatedOut;
        public int SimultaneousDropped;
        public int OutliersDropped;
        public int SplitNotes;
        public int Syllables;
        public int VocalNotes;
        public int InstrumentNotes;
        /// <summary>Her vokal ↔ dolgu geçişinde (sınır zamanı, önceki 4 sn nota/sn, sonraki 4 sn nota/sn).</summary>
        public List<(double time, double before, double after)> Transitions = new List<(double, double, double)>();
    }

    /// <summary>
    /// Vokal modu (GDD §7.2). Unity'ye bağımlı değil, belirleyici.
    /// Vokal: kapı → tek ses → aykırılar → hece bölme → seyreltme (cümle başları korunur).
    /// Kaynak önceliği: vokal bölgelerinde (vokalin ≥ 4 sn susmadığı her yer) sadece vokal notası; enstrüman (§7.1 melodi hattı)
    /// sadece ≥ 4 sn susmalarda, sınırlardan 0.5 sn içeride; geçişte hedef yoğunluk komşu vokal yoğunluğundan rampalanır.
    /// Akor (Zor): vokal bölgesinde vurgulu hecenin kendisi ikinci şeride (enstrüman notası kullanılmaz).
    /// </summary>
    public static class VocalGenerator
    {
        /// <summary>Vokal bu kadar ve daha uzun susarsa enstrüman dolgusu girer; daha kısa boşluklar boş kalır.</summary>
        public const double FillMinSilenceSec = 4.0;
        /// <summary>Vokal bölgesi ile dolgu arasında en az bu kadar boşluk.</summary>
        public const double BoundaryGapSec = 0.5;
        /// <summary>Dolgu yoğunluğu, sınırda komşu vokal yoğunluğundan bu sürede zorluk hedefine geçer.</summary>
        public const double RampSec = 10.0;
        /// <summary>Komşu vokal yoğunluğu, dolgu sınırının dışındaki bu kadar süreden ölçülür (geçiş raporuyla aynı pencere).</summary>
        public const double NeighbourDensitySec = 4.0;
        /// <summary>Dolgu seyreltme penceresi; rampayı izleyebilecek kadar kısa.</summary>
        public const double FillWindowSec = 1.0;
        /// <summary>
        /// Dolguda cümle başı sayılmak için gereken sessizlik. Seyreltme cümle başlarını bütçe dışı korur; dolguda sık "cümle başı"
        /// geçişte yoğunluk sıçraması yapar, bu yüzden eşik melodi modundan (0.5 sn) uzun.
        /// </summary>
        public const double FillPhraseGapSec = 2.0;
        /// <summary>Vurgulu hece: velocity bu yüzdeliğin üstünde (ya da cümle başı).</summary>
        public const double AccentPercentile = 0.7;

        public static VocalResult Generate(VocalInputs input, MelodyPreset preset)
        {
            var r = new VocalResult();
            r.Map = MelodyGenerator.NewResult(input.BeatTimes, input.VocalMidi.Count, out var tempo);
            if (tempo == null) return r;

            // 1) Vokal hattı
            var syllables = BuildVocalLine(input, r);
            r.Syllables = syllables.Count;

            // 2) Bölgeler: vokal bölümleri + hecelerin kapladığı süre, < 4 sn boşluklar birleşik.
            var spans = new List<Span>(input.Sections);
            foreach (var s in syllables) spans.Add(new Span(s.Time, s.End));
            r.VocalRegions = VocalGate.MergeClose(spans, FillMinSilenceSec);
            r.FillRegions = FillRegions(r.VocalRegions, input.SongDurationSec);

            // 3) Vokal seyreltme (8 sn pencere, cümle başları korunur).
            var vocalThin = MelodyThinner.Thin(syllables, preset.TargetNps, preset.MinGapSec);

            // 4) Dolgu: her aralıkta ayrı, komşu vokal yoğunluğundan rampalı hedef.
            var fillThin = new List<MelodyNote>();
            foreach (var region in r.FillRegions)
            {
                var inRegion = new List<MelodyNote>();
                foreach (var m in input.InstrumentLine)
                {
                    if (m.Time < region.Start || m.Time > region.End) continue;
                    // Hold sınırı aşmasın: süre aralığın sonunda kesilir.
                    inRegion.Add(new MelodyNote
                    {
                        Time = m.Time, Duration = Math.Min(m.Duration, region.End - m.Time), Pitch = m.Pitch, Velocity = m.Velocity,
                        ChordPitch = m.ChordPitch, ChordVelocity = m.ChordVelocity,
                    });
                }
                if (inRegion.Count == 0) continue;
                double before = Density(vocalThin, region.Start - NeighbourDensitySec, region.Start);
                double after = Density(vocalThin, region.End, region.End + NeighbourDensitySec);
                bool hasBefore = HasRegionEndingAt(r.VocalRegions, region.Start - BoundaryGapSec);
                bool hasAfter = HasRegionStartingAt(r.VocalRegions, region.End + BoundaryGapSec);
                var target = RampTarget(region, preset.TargetNps, hasBefore ? before : (double?)null, hasAfter ? after : (double?)null);
                fillThin.AddRange(MelodyThinner.Thin(inRegion, target, preset.MinGapSec, FillWindowSec, FillPhraseGapSec));
            }

            // 5) Birleştir, haritaya çevir. Kaynak = referans.
            var instrument = new HashSet<MelodyNote>(fillThin);
            var line = new List<MelodyNote>(vocalThin);
            line.AddRange(fillThin);
            line.Sort((a, b) => a.Time.CompareTo(b.Time));

            var source = MelodyGenerator.BuildFromLine(line, tempo, preset, input.OffsetSec, r.Map,
                (planned, src, p) => AssignChords(planned, src, p, instrument));

            var sourceByTime = new Dictionary<double, NoteSource>();
            for (int i = 0; i < source.Count; i++)
                sourceByTime[source[i].Time + input.OffsetSec] = instrument.Contains(source[i]) ? NoteSource.Instrument : NoteSource.Vocal;
            foreach (var n in r.Map.Notes)
            {
                var src = sourceByTime[n.Time];
                r.Sources.Add(src);
                if (src == NoteSource.Vocal) r.VocalNotes++;
                else r.InstrumentNotes++;
            }

            // 6) Geçiş yoğunlukları (rapor / test).
            var times = new List<double>();
            foreach (var n in r.Map.Notes) times.Add(n.Time - input.OffsetSec);
            foreach (var region in r.FillRegions)
            {
                if (HasRegionEndingAt(r.VocalRegions, region.Start - BoundaryGapSec))
                    r.Transitions.Add((region.Start, DensityOf(times, region.Start - 4, region.Start), DensityOf(times, region.Start, region.Start + 4)));
                if (HasRegionStartingAt(r.VocalRegions, region.End + BoundaryGapSec))
                    r.Transitions.Add((region.End, DensityOf(times, region.End - 4, region.End), DensityOf(times, region.End, region.End + 4)));
            }
            return r;
        }

        public static List<MelodyNote> BuildVocalLine(VocalInputs input, VocalResult stats)
        {
            var gated = VocalLine.Gate(input.VocalMidi, input.Sections);
            stats.GatedOut = input.VocalMidi.Count - gated.Count;
            var mono = VocalLine.Monophonic(gated);
            stats.SimultaneousDropped = gated.Count - mono.Count;
            var clean = VocalLine.RemoveOutliers(mono);
            stats.OutliersDropped = mono.Count - clean.Count;
            var syllables = SyllableSplitter.Split(clean, input.VocalOnsets ?? Array.Empty<double>(), out int split);
            stats.SplitNotes = split;
            return syllables;
        }

        /// <summary>Vokal bölgelerinin tümleyeni (şarkı içinde), sınırlardan 0.5 sn içeride. Bölgeler arası boşluk zaten ≥ 4 sn.</summary>
        public static List<Span> FillRegions(IReadOnlyList<Span> vocalRegions, double songDurationSec)
        {
            var result = new List<Span>();
            double cursor = 0;
            bool first = true;
            foreach (var v in vocalRegions)
            {
                double start = first ? cursor : cursor + BoundaryGapSec;
                double end = v.Start - BoundaryGapSec;
                if (end - start > 0) result.Add(new Span(start, end));
                cursor = v.End;
                first = false;
            }
            double lastStart = first ? 0 : cursor + BoundaryGapSec;
            if (songDurationSec - lastStart > 0) result.Add(new Span(lastStart, songDurationSec));
            return result;
        }

        /// <summary>Sınırlardan içeri doğru, komşu vokal yoğunluğundan zorluk hedefine doğrusal rampa (yakın sınır belirler).</summary>
        public static Func<double, double> RampTarget(Span region, double target, double? before, double? after)
        {
            return t =>
            {
                double fromStart = t - region.Start, fromEnd = region.End - t;
                if (before.HasValue && (fromStart <= fromEnd || !after.HasValue))
                    return Lerp(before.Value, target, fromStart / RampSec);
                if (after.HasValue)
                    return Lerp(after.Value, target, fromEnd / RampSec);
                return target;
            };
        }

        private static double Lerp(double a, double b, double w) => a + (b - a) * Math.Max(0, Math.Min(1, w));

        private static double Density(IReadOnlyList<MelodyNote> line, double from, double to)
        {
            if (to <= from) return 0;
            int c = 0;
            foreach (var m in line)
                if (m.Time >= from && m.Time < to) c++;
            return c / (to - from);
        }

        private static double DensityOf(List<double> times, double from, double to)
        {
            int c = 0;
            foreach (double t in times)
                if (t >= from && t < to) c++;
            return c / (to - from);
        }

        private static bool HasRegionEndingAt(IReadOnlyList<Span> regions, double t)
        {
            foreach (var r in regions)
                if (Math.Abs(r.End - t) < 1e-9) return true;
            return false;
        }

        private static bool HasRegionStartingAt(IReadOnlyList<Span> regions, double t)
        {
            foreach (var r in regions)
                if (Math.Abs(r.Start - t) < 1e-9) return true;
            return false;
        }

        /// <summary>
        /// Akor (Zor): vokal notasında vurgulu hece (cümle başı ya da güçlü velocity) aynı notanın ikinci şeride eklenmesiyle;
        /// enstrüman dolgusunda §7.1 kuralı (aynı anda güçlü vurulan ikinci nota). Hold'lar hariç, toplam pay sınırlı.
        /// </summary>
        public static void AssignChords(List<PlannedNote> planned, List<MelodyNote> source, MelodyPreset preset, HashSet<MelodyNote> instrument)
        {
            if (planned.Count == 0) return;
            var vels = new List<int>();
            foreach (var m in source)
                if (!instrument.Contains(m)) vels.Add(m.Velocity);
            vels.Sort();
            int strong = vels.Count > 0 ? vels[Math.Min(vels.Count - 1, (int)(vels.Count * AccentPercentile))] : int.MaxValue;

            var mixVels = new List<int>();
            foreach (var m in source)
                if (instrument.Contains(m)) mixVels.Add(m.Velocity);
            mixVels.Sort();
            int mixStrong = mixVels.Count > 0
                ? mixVels[Math.Min(mixVels.Count - 1, (int)(mixVels.Count * MelodyGenerator.ChordStrengthPercentile))]
                : int.MaxValue;

            var candidates = new List<int>();
            var score = new double[planned.Count];
            for (int i = 0; i < planned.Count; i++)
            {
                if (planned[i].HoldBeats > 0) continue;
                var m = source[i];
                if (instrument.Contains(m))
                {
                    if (m.ChordPitch < 0 || m.Velocity < mixStrong || m.ChordVelocity < mixStrong) continue;
                    score[i] = m.Velocity + m.ChordVelocity;
                }
                else
                {
                    if (!m.PhraseStart && m.Velocity < strong) continue;
                    score[i] = 2 * m.Velocity + (m.PhraseStart ? 64 : 0);
                }
                candidates.Add(i);
            }
            candidates.Sort((a, b) => score[a] != score[b] ? score[b].CompareTo(score[a]) : a.CompareTo(b));
            int max = (int)(planned.Count * preset.ChordShare);
            for (int k = 0; k < candidates.Count && k < max; k++) planned[candidates[k]].Chord = true;
        }
    }
}
