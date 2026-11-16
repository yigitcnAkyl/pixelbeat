using System;
using System.Collections.Generic;

namespace PixelBeat.EditorTools.BeatmapGen.Melody
{
    /// <summary>MIDI'den okunan tek nota (zamanlar saniye, dosyanın başından).</summary>
    public sealed class MidiNote
    {
        public double Start;
        public double End;
        public int Pitch;
        public int Velocity;
        public int Channel;
        public int Track;
        public double Duration => End - Start;
    }

    public sealed class MidiFile
    {
        public int Format;
        public int TrackCount;
        public int Division;
        /// <summary>Başlangıca, sonra perdeye göre sıralı.</summary>
        public List<MidiNote> Notes = new List<MidiNote>();
        /// <summary>(saniye, BPM) tempo değişimleri; ilk eleman her zaman 0 sn.</summary>
        public List<KeyValuePair<double, double>> Tempos = new List<KeyValuePair<double, double>>();
    }

    /// <summary>
    /// Basit Standard MIDI File (format 0/1) okuyucu. Unity'ye bağımlı değil.
    /// Tempo değişimleri (tüm izlerden), SMPTE bölme, running status, velocity 0 = note-off desteklenir.
    /// Aynı kanal + perdede üst üste binen notalar FIFO eşlenir; kapanmayan nota iz sonunda kapanır.
    /// </summary>
    public static class MidiReader
    {
        public const int DrumChannel = 9;
        private const int DefaultTempoUs = 500000; // 120 BPM

        public static MidiFile Read(byte[] data, bool includeDrums = false)
        {
            if (data == null || data.Length < 14 || !Tag(data, 0, "MThd"))
                throw new FormatException("MIDI değil (MThd başlığı yok).");

            int headerLen = (int)BE32(data, 4);
            var file = new MidiFile
            {
                Format = BE16(data, 8),
                TrackCount = BE16(data, 10),
                Division = BE16(data, 12),
            };
            if (file.Format > 1) throw new FormatException($"MIDI format {file.Format} desteklenmiyor (0 ya da 1 olmalı).");

            // İzleri topla (bilinmeyen chunk'lar atlanır).
            var tracks = new List<ArraySegment<byte>>();
            int p = 8 + headerLen;
            while (p + 8 <= data.Length)
            {
                long len = BE32(data, p + 4);
                if (p + 8 + len > data.Length) throw new FormatException("MIDI kesik (iz uzunluğu dosyayı aşıyor).");
                if (Tag(data, p, "MTrk")) tracks.Add(new ArraySegment<byte>(data, p + 8, (int)len));
                p += 8 + (int)len;
            }

            // 1. geçiş: tempo olayları (tick, µs/beat), 2. geçiş: notalar (tick).
            var tempoTicks = new List<KeyValuePair<long, int>>();
            var raw = new List<RawNote>();
            for (int ti = 0; ti < tracks.Count; ti++) ParseTrack(tracks[ti], ti, tempoTicks, raw, includeDrums);

            var clock = new TickClock(file.Division, tempoTicks);
            foreach (var kv in clock.TempoChangesSec) file.Tempos.Add(kv);

            foreach (var r in raw)
            {
                file.Notes.Add(new MidiNote
                {
                    Start = clock.ToSeconds(r.StartTick),
                    End = clock.ToSeconds(r.EndTick),
                    Pitch = r.Pitch,
                    Velocity = r.Velocity,
                    Channel = r.Channel,
                    Track = r.Track,
                });
            }
            file.Notes.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Pitch.CompareTo(b.Pitch));
            return file;
        }

        private struct RawNote
        {
            public long StartTick, EndTick;
            public int Pitch, Velocity, Channel, Track;
        }

        private static void ParseTrack(ArraySegment<byte> seg, int trackIndex, List<KeyValuePair<long, int>> tempos,
            List<RawNote> notes, bool includeDrums)
        {
            byte[] d = seg.Array;
            int i = seg.Offset, end = seg.Offset + seg.Count;
            long tick = 0;
            int running = 0;
            // (kanal * 128 + perde) → açık notalar (başlangıç tick, velocity), FIFO
            var open = new Dictionary<int, Queue<KeyValuePair<long, int>>>();

            while (i < end)
            {
                tick += ReadVlq(d, ref i, end);
                if (i >= end) break;
                int status = d[i];

                if (status == 0xFF) // meta
                {
                    if (i + 1 >= end) break;
                    int type = d[i + 1];
                    i += 2;
                    int len = (int)ReadVlq(d, ref i, end);
                    if (i + len > end) throw new FormatException("MIDI kesik (meta olayı).");
                    if (type == 0x51 && len >= 3) tempos.Add(new KeyValuePair<long, int>(tick, (d[i] << 16) | (d[i + 1] << 8) | d[i + 2]));
                    i += len;
                    running = 0;
                    if (type == 0x2F) break; // iz sonu
                    continue;
                }
                if (status == 0xF0 || status == 0xF7) // sysex
                {
                    i++;
                    int len = (int)ReadVlq(d, ref i, end);
                    i += len;
                    running = 0;
                    continue;
                }

                if ((status & 0x80) != 0) { running = status; i++; }
                else if (running == 0) throw new FormatException("MIDI bozuk (running status olmadan veri baytı).");

                int type4 = running & 0xF0;
                int ch = running & 0x0F;
                int dataLen = type4 == 0xC0 || type4 == 0xD0 ? 1 : 2;
                if (i + dataLen > end) throw new FormatException("MIDI kesik (kanal olayı).");
                int a = d[i], b = dataLen > 1 ? d[i + 1] : 0;
                i += dataLen;

                bool isOn = type4 == 0x90 && b > 0;
                bool isOff = type4 == 0x80 || (type4 == 0x90 && b == 0);
                if (!isOn && !isOff) continue;
                if (ch == DrumChannel && !includeDrums) continue;

                int key = ch * 128 + a;
                if (isOn)
                {
                    if (!open.TryGetValue(key, out var q)) open[key] = q = new Queue<KeyValuePair<long, int>>();
                    q.Enqueue(new KeyValuePair<long, int>(tick, b));
                }
                else if (open.TryGetValue(key, out var q) && q.Count > 0)
                {
                    var on = q.Dequeue();
                    notes.Add(new RawNote { StartTick = on.Key, EndTick = tick, Pitch = a, Velocity = on.Value, Channel = ch, Track = trackIndex });
                }
            }

            // Kapanmamış notalar iz sonunda biter.
            foreach (var kv in open)
            foreach (var on in kv.Value)
                notes.Add(new RawNote { StartTick = on.Key, EndTick = Math.Max(on.Key, tick), Pitch = kv.Key % 128, Velocity = on.Value, Channel = kv.Key / 128, Track = trackIndex });
        }

        /// <summary>Tick → saniye (tempo değişimleriyle parça parça doğrusal).</summary>
        private sealed class TickClock
        {
            private readonly long[] ticks;
            private readonly double[] secs;
            private readonly double[] secPerTick;
            private readonly double smpteSecPerTick;

            public readonly List<KeyValuePair<double, double>> TempoChangesSec = new List<KeyValuePair<double, double>>();

            public TickClock(int division, List<KeyValuePair<long, int>> tempoEvents)
            {
                if ((division & 0x8000) != 0)
                {
                    int fps = -(sbyte)(division >> 8);
                    int perFrame = division & 0xFF;
                    if (fps <= 0 || perFrame <= 0) throw new FormatException("MIDI SMPTE bölme geçersiz.");
                    smpteSecPerTick = 1.0 / (fps * perFrame);
                    TempoChangesSec.Add(new KeyValuePair<double, double>(0, 120));
                    return;
                }
                if (division <= 0) throw new FormatException("MIDI bölme (ticks/beat) geçersiz.");

                // Kararlı sıralama: aynı tick'te dosyada sonra gelen tempo geçerli olur.
                var sorted = System.Linq.Enumerable.ToList(System.Linq.Enumerable.OrderBy(tempoEvents, e => e.Key));
                var list = new List<KeyValuePair<long, int>> { new KeyValuePair<long, int>(0, DefaultTempoUs) };
                foreach (var e in sorted)
                {
                    if (e.Value <= 0) continue;
                    if (list[list.Count - 1].Key == e.Key) list[list.Count - 1] = e;
                    else list.Add(e);
                }

                ticks = new long[list.Count];
                secs = new double[list.Count];
                secPerTick = new double[list.Count];
                for (int k = 0; k < list.Count; k++)
                {
                    ticks[k] = list[k].Key;
                    secPerTick[k] = list[k].Value / 1e6 / division;
                    secs[k] = k == 0 ? 0 : secs[k - 1] + (ticks[k] - ticks[k - 1]) * secPerTick[k - 1];
                    TempoChangesSec.Add(new KeyValuePair<double, double>(secs[k], 60e6 / list[k].Value));
                }
            }

            public double ToSeconds(long tick)
            {
                if (ticks == null) return tick * smpteSecPerTick;
                int k = Array.BinarySearch(ticks, tick);
                if (k < 0) k = ~k - 1;
                if (k < 0) k = 0;
                return secs[k] + (tick - ticks[k]) * secPerTick[k];
            }
        }

        private static long ReadVlq(byte[] d, ref int i, int end)
        {
            long v = 0;
            for (int n = 0; n < 4 && i < end; n++)
            {
                int c = d[i++];
                v = (v << 7) | (uint)(c & 0x7F);
                if ((c & 0x80) == 0) return v;
            }
            return v;
        }

        private static bool Tag(byte[] d, int p, string tag) =>
            p + 4 <= d.Length && d[p] == tag[0] && d[p + 1] == tag[1] && d[p + 2] == tag[2] && d[p + 3] == tag[3];

        private static int BE16(byte[] d, int p) => (d[p] << 8) | d[p + 1];
        private static long BE32(byte[] d, int p) => ((long)d[p] << 24) | ((long)d[p + 1] << 16) | ((long)d[p + 2] << 8) | d[p + 3];
    }
}
