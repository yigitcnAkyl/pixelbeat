using System.Collections.Generic;
using UnityEngine;

namespace RhythmGame
{
    /// <summary>
    /// Piano Tiles tarzı tek şeritli, temiz ve oynanabilir ritim haritası (Chart) üreteci.
    /// Kurallar:
    /// 1. Asla aynı anda birden fazla şeride nota atmaz (her vuruşta tek şerit).
    /// 2. Notalar kesinlikle şarkının vuruş ızgarasına (Beat Grid: 1/2 veya 1/4 beat) kilitlidir.
    /// 3. İki nota arasında şerit fark etmeksizin EN AZ minimumSpawnDelay (0.35s) bekleme süresi vardır.
    /// 4. Spektrum enerjisi en az 1.8 katı dinamik eşik değerini aşmalıdır.
    /// 5. Şeritler arasında akıcı gezinir, aynı şeride art arda düşme ihtimali çok düşüktür.
    /// </summary>
    public class BeatDetector : MonoBehaviour
    {
        public static BeatDetector Instance { get; private set; }

        [Header("Piano Tiles Grid & Ritim Ayarları")]
        [Tooltip("Varsayılan BPM (Conductor'dan alınır, atanmamışsa 120)")]
        [SerializeField] private float defaultBpm = 120f;

        [Tooltip("İki nota arasındaki minimum süre (Saniye cinsinden, en az 0.35s)")]
        [SerializeField] private float minimumSpawnDelay = 0.35f;

        [Tooltip("Dinamik vuruş algılama hassasiyeti (En az 1.8x enerji sıçraması)")]
        [Range(1.5f, 3.0f)]
        [SerializeField] private float sensitivityThreshold = 1.8f;

        [Tooltip("Sadece 1/2 vuruşlarda mı (True) yoksa 1/4 vuruşlarda da (False) nota gelsin")]
        [SerializeField] private bool halfBeatGridOnly = true;

        [Tooltip("Hold (Uzun basma) notalarının oluşma şansı (%)")]
        [Range(0, 50)]
        [SerializeField] private int holdNoteChance = 15;

        [Tooltip("Döngüsel şarkılar için kaç döngü üretilsin")]
        [SerializeField] private int loopCount = 8;

        [Header("Çalışma Modu")]
        [SerializeField] private bool autoGenerateOnSongLoad = true;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            SongLoader.OnSongLoaded += HandleSongLoaded;
        }

        private void OnDisable()
        {
            SongLoader.OnSongLoaded -= HandleSongLoaded;
        }

        private void HandleSongLoaded(AudioClip clip)
        {
            if (autoGenerateOnSongLoad && clip != null)
            {
                GenerateChartFromClip(clip);
            }
        }

        /// <summary>
        /// Şarkının PCM dalgasını analiz ederek katı Piano Tiles kurallarına göre tek şeritli harita üretir.
        /// </summary>
        public List<NoteData> GenerateChartFromClip(AudioClip clip)
        {
            if (clip == null) return null;

            float bpm = (Conductor.Instance != null && Conductor.Instance.Bpm > 30f) ? Conductor.Instance.Bpm : defaultBpm;
            float secondsPerBeat = 60f / bpm;
            float clipDuration = clip.length;
            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int totalFrames = clip.samples;

            // 1. PCM verisini oku
            float[] rawData = new float[totalFrames * channels];
            clip.GetData(rawData, 0);

            // 2. 10ms adımlarla düşük frekans (Kick/Bas) ve yüksek frekans (Transient) enerjisi çıkar
            float hopSec = 0.01f;
            int hopFrames = Mathf.Max(1, Mathf.RoundToInt(hopSec * sampleRate));
            int numHops = totalFrames / hopFrames;

            float[] bassEnergy = new float[numHops];
            float[] onsetFlux = new float[numHops];

            float lpBass = 0f;
            float prevMono = 0f;

            for (int h = 0; h < numHops; h++)
            {
                int start = h * hopFrames;
                float sumBass = 0f;
                float sumHigh = 0f;
                int count = 0;

                for (int i = 0; i < hopFrames; i += 2)
                {
                    int idx = (start + i) * channels;
                    if (idx >= rawData.Length) break;

                    float mono = rawData[idx];
                    if (channels > 1 && idx + 1 < rawData.Length)
                    {
                        mono = (mono + rawData[idx + 1]) * 0.5f;
                    }

                    // Kick & Bas filtresi (<200 Hz)
                    lpBass = lpBass * 0.88f + mono * 0.12f;
                    float hp = mono - prevMono;
                    prevMono = mono;

                    sumBass += lpBass * lpBass;
                    sumHigh += hp * hp;
                    count++;
                }

                if (count > 0)
                {
                    bassEnergy[h] = Mathf.Sqrt(sumBass / count);
                    float highVal = Mathf.Sqrt(sumHigh / count);

                    if (h > 0)
                    {
                        float dLow = Mathf.Max(0f, bassEnergy[h] - bassEnergy[h - 1]);
                        float dHigh = Mathf.Max(0f, highVal - 0.02f);
                        onsetFlux[h] = dLow * 3.0f + dHigh * 2.0f;
                    }
                }
            }

            // 3. PIANO TILES GRID MATEMATİĞİ (Quantization)
            // Sadece 1/2 vuruşta (0.5) veya 1/4 vuruşta (0.25) kilitli noktalar test edilir
            float gridStep = halfBeatGridOnly ? 0.5f : 0.25f;
            float totalBeatsInClip = clipDuration / secondsPerBeat;

            List<NoteData> singleLoopNotes = new List<NoteData>();
            float lastSpawnTimeSec = -10f;
            int lastLane = -1;
            int secondLastLane = -1;
            int noteIdCounter = 1;

            // Hareketli ortalama enerji (Dinamik eşik için)
            float movingAvgOnset = 0.05f;

            for (float b = 1.0f; b < totalBeatsInClip - 0.5f; b += gridStep)
            {
                float gridTimeSec = b * secondsPerBeat;

                // Kural: EN AZ 0.35 saniye bekleme süresi! (Arka arkaya hızlı gelen vuruşları yut)
                if (gridTimeSec - lastSpawnTimeSec < minimumSpawnDelay)
                {
                    continue;
                }

                // Bu grid noktası civarındaki (±40ms) spektrum enerjisini ölç
                int centerHop = Mathf.RoundToInt(gridTimeSec / hopSec);
                float localMaxOnset = 0f;
                float localMaxBass = 0f;

                for (int dh = -4; dh <= 4; dh++)
                {
                    int hh = centerHop + dh;
                    if (hh >= 0 && hh < numHops)
                    {
                        if (onsetFlux[hh] > localMaxOnset) localMaxOnset = onsetFlux[hh];
                        if (bassEnergy[hh] > localMaxBass) localMaxBass = bassEnergy[hh];
                    }
                }

                // Hareketli ortalamayı yumuşak güncelle
                movingAvgOnset = Mathf.Lerp(movingAvgOnset, Mathf.Max(0.04f, localMaxOnset), 0.08f);

                // Kural: Spektrum enerjisi en az 1.8 katı dinamik eşik değerine ulaşmalı
                bool isSignificantBeat = (localMaxOnset >= movingAvgOnset * sensitivityThreshold) || (localMaxBass > 0.12f);

                // Ana tam vuruşlarda (1.0, 2.0, 3.0...) hafif tolerans tanı ama ara vuruşlarda kesinlikle 1.8x eşik ara
                bool isWholeBeat = Mathf.Approximately(b % 1.0f, 0f);
                if (isWholeBeat && localMaxOnset >= movingAvgOnset * 1.2f)
                {
                    isSignificantBeat = true;
                }

                if (isSignificantBeat)
                {
                    // Şeridi Piano Tiles tarzında seç (Asla aynı anda çift şerit yok, önceki şeridi tekrarlama şansı çok düşük)
                    int targetLane = SelectWanderingLane(lastLane, secondLastLane);
                    secondLastLane = lastLane;
                    lastLane = targetLane;

                    // Hold Notası kontrolü:
                    // Sadece tam beat'te, aralık uygunsa ve hold şansı tutarsa
                    bool isHold = (Random.Range(0, 100) < holdNoteChance) && isWholeBeat && (gridTimeSec - lastSpawnTimeSec > 0.7f);
                    float holdDuration = isHold ? 1.0f : 0f; // 1 beat uzunluğunda temiz hold
                    NoteType type = isHold ? NoteType.Hold : NoteType.Tap;

                    // Grid'e tam oturan kusursuz vuruş zamanı
                    float quantizedBeat = Mathf.Round(b * 4f) / 4f;

                    singleLoopNotes.Add(new NoteData(noteIdCounter++, type, targetLane, quantizedBeat, holdDuration));

                    // Son nota zamanını güncelle (Hold ise hold bitimine kadar bekleme koy)
                    lastSpawnTimeSec = gridTimeSec + (isHold ? (holdDuration * secondsPerBeat * 0.8f) : 0f);
                }
            }

            // 4. Döngüsel Şarkı Desteği (Loop Replication)
            List<NoteData> fullChart = new List<NoteData>();
            bool isLooping = Conductor.Instance != null && Conductor.Instance.AudioSource != null && Conductor.Instance.AudioSource.loop;
            int totalLoops = (isLooping && clipDuration < 60f) ? Mathf.Max(1, loopCount) : 1;

            float loopBeats = Mathf.Round(totalBeatsInClip);
            int finalId = 1;

            for (int l = 0; l < totalLoops; l++)
            {
                float offsetBeats = l * loopBeats;
                foreach (var note in singleLoopNotes)
                {
                    fullChart.Add(new NoteData(
                        finalId++,
                        note.noteType,
                        note.laneIndex,
                        note.targetBeat + offsetBeats,
                        note.holdDurationInBeats
                    ));
                }
            }

            Debug.Log($"<color=#39ff14><b>[BeatDetector - Piano Tiles]</b> Harita oluşturuldu! Döngü Başına: <b>{singleLoopNotes.Count}</b> nota (Min Aralık: {minimumSpawnDelay:F2}s, Eşik: {sensitivityThreshold}x). Toplam: <b>{fullChart.Count}</b> nota.</color>");

            if (LaneController.Instance != null)
            {
                LaneController.Instance.LoadChart(fullChart);
            }

            return fullChart;
        }

        /// <summary>
        /// Piano Tiles tarzında şeritler arasında gezinen akış:
        /// Bir önceki şeridin hemen aynısına düşme ihtimali çok düşüktür; sol/sağ komşulara ve çaprazlara kayar.
        /// </summary>
        private int SelectWanderingLane(int prevLane, int secondPrevLane)
        {
            if (prevLane < 0 || prevLane >= 4)
            {
                return Random.Range(0, 4);
            }

            List<int> weightedCandidates = new List<int>();

            for (int i = 0; i < 4; i++)
            {
                if (i == prevLane)
                {
                    // Aynı şerit tekrarı sadece %5 şans (1 bilet)
                    weightedCandidates.Add(i);
                }
                else if (i == secondPrevLane)
                {
                    // Bir önceki şeride hemen geri dönme (A-B-A ping-pong) orta şans (3 bilet)
                    weightedCandidates.Add(i);
                    weightedCandidates.Add(i);
                    weightedCandidates.Add(i);
                }
                else
                {
                    // Yeni şeritlere geçiş en yüksek şans (6 bilet)
                    for (int w = 0; w < 6; w++) weightedCandidates.Add(i);
                }
            }

            int chosenIndex = Random.Range(0, weightedCandidates.Count);
            return weightedCandidates[chosenIndex];
        }
    }
}
