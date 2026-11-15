# PixelBeat — Oyun Tasarım Belgesi (GDD)

> Bu belge oyunun tek doğruluk kaynağıdır. Bir kararı değiştirirsek önce burayı güncelle.

## 1. Özet
- **Tür:** 4 şeritli dikey ritim oyunu (osu!mania / DJMAX tarzı).
- **Platform:** PC (Windows), klavye.
- **Görsel:** Retro arcade / neon pixel art. Koyu arka plan, parlak neon renkler, hafif CRT hissi.
- **Çekirdek döngü:** Notalar yukarıdan aşağı akar → vuruş çizgisine geldiği anda doğru tuşa bas / basılı tut → değerlendirme + combo + skor.

## 2. Kontroller
| Şerit | 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| Tuş | S | D | K | L |

- Yeni Input System kullanılır. Tuş atamaları ileride değiştirilebilir olmalı (şimdilik sabit).
- Pause: Esc (prototipte basit duraklatma yeterli).

## 3. Notalar
- **Tap nota:** Vuruş çizgisine geldiğinde bas.
- **Hold nota:** Başında bas, kuyruğun sonunda bırak.
  - Basış zamanı normal pencerelerle değerlendirilir.
  - **Bırakış zamanı da değerlendirilir** (pencereler 1.5 kat toleranslı).
  - Kuyruk bitmeden çok erken bırakılırsa (Good penceresinin dışında) hold kırılır → Miss.
  - Kuyruk bittikten sonra Good penceresi geçene kadar bırakılmazsa → bırakış Good sayılır (oyuncuyu cezalandırma).
- Notalar yukarıdan gelir, vuruş çizgisi ekranın alt kısmındadır.

## 4. Değerlendirme (Judgement)
| Sonuç | Basış penceresi | Bırakış penceresi (hold sonu) |
|---|---|---|
| Perfect | ±40 ms | ±60 ms |
| Great | ±80 ms | ±120 ms |
| Good | ±120 ms | ±180 ms |
| Miss | dışı / hiç basılmadı | dışı / erken kırıldı |

- Good penceresinden önce yapılan basış (çok erken) notayı tüketmez, yok sayılır.
- Pencere değerleri tek bir ayar dosyasında (ScriptableObject) tutulur, kodun içine gömülmez.

## 5. Skor, Combo, Can
- **Skor:** Maksimum 1.000.000. Her değerlendirme (tap = 1, hold = basış + bırakış = 2) eşit pay alır.
  Perfect %100, Great %70, Good %40, Miss %0.
- **Combo:** Miss dışındaki her sonuç +1, Miss sıfırlar. Max combo kaydedilir.
- **Can:** 0–100, 100 ile başlar. Miss −8, Good +0, Great +1, Perfect +2 (en fazla 100).
  Can 0 olursa şarkı başarısız olur (müzik durur, "FAILED" gösterilir).
- **Sonuç:** Şarkı bitince skor, max combo, her sonuçtan kaç tane ve harf derecesi (S/A/B/C/D) gösterilir.

## 6. Zamanlama (teknik — kritik)
- Tüm oyun zamanı `AudioSettings.dspTime` ile ölçülür. `Time.time` kullanılmaz.
- Müzik `AudioSource.PlayScheduled` ile, küçük bir gecikmeyle (ör. 1 sn) planlanarak başlatılır.
- **Conductor** sınıfı tek zaman kaynağıdır: BPM, şarkı offset'i, şarkı pozisyonu (sn ve beat), beat başına saniye.
- Kullanıcı **ses offset'i** (ms) ayarı: ses/ekran/klavye gecikmesini telafi eder.
- **Nota hızı** (scroll speed) ayarı: notaların ekranda kaç saniye önce görüneceği.
- Notaların ekrandaki konumu her karede şarkı pozisyonundan hesaplanır (hareketi biriktirerek değil).

## 7. Beatmap
- **Format:** Düzenlenebilir JSON dosyası (`Assets/Data/Beatmaps/<şarkı>.json`).
  - Üst bilgi: şarkı adı, sanatçı, ses dosyası, BPM, offset (sn), zorluk.
  - Notalar: `lane` (0–3), `beat` (başlangıç, beat cinsinden), `lengthBeats` (0 = tap, >0 = hold).
- Beat cinsinden tutulur, böylece BPM ızgarasına oturur ve elle düzeltmek kolaydır.
- **Otomatik üretici (Editor aracı):** Ses dosyası + BPM (+ offset) verilir →
  - Onset (vuruş başlangıcı) tespiti yapılır, sonuçlar beat ızgarasına (1/1, 1/2, 1/4) oturtulur.
  - Uzun süren / sürdürülen enerjiden hold notalar tahmin edilir.
  - Yoğunluk (zorluk) parametresi: kaç nota üretileceği.
  - Şerit dağıtımı: aynı şeride çok art arda yığılma, oynanamaz akorlar (aynı anda 3+ tuş) ve hold sırasında aynı şeride nota olmamalı.
  - Çıktı yukarıdaki JSON formatına yazılır; sonra elle düzenlenebilir.

## 8. Görsel / Teknik
- **Çözünürlük:** 320×180 referans, Pixel Perfect Camera ile tam sayı büyütme (1080p = 6x, 1440p = 8x, 4K = 12x).
- **Sprite ayarları:** Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- **Yerleşim (öneri):** Ortada 4 şerit (her biri ~20 px genişliğinde), altta vuruş çizgisi + tuş göstergeleri (basınca yanar), sağda skor/combo, üstte can barı.
- **Geri bildirim:** Vuruşta şerit parlaması, değerlendirme yazısı (PERFECT / GREAT / GOOD / MISS), combo sayacı.
- Prototipte placeholder (basit renkli kare) sprite'lar yeterli. Gerçek pixel art sonra çizilecek.

## 9. Ses
- Telifsiz müzik kullanılacak. BPM bilgisi şarkının kaynağından alınır.
- Ses dosyaları: `Assets/Audio/Music/`. Müzik için Load Type = Decompress On Load veya Compressed In Memory (gecikme testine göre).
- Vuruş sesi (hitsound) opsiyonel, ayarlardan kapatılabilir.

## 10. Prototip kapsamı ve yol haritası
Prototip: **1 şarkı, sadece oynanış.** Menü, şarkı seçimi, kayıt yok.

1. **Temel:** Pixel Perfect kamera (320×180), Conductor, her beat'te yanıp sönen test karesi (senkron doğrulama).
2. **Tap notalar:** 4 şerit, elle yazılmış küçük test JSON'u, S D K L girişi, değerlendirme.
3. **Hold notalar:** Basış + bırakış değerlendirmesi, kırılma.
4. **Skor / combo / can:** Başarısızlık ve şarkı sonu sonuç yazısı.
5. **Otomatik beatmap üretici:** Editor aracı.
6. **Neon görünüm:** Placeholder pixel art, efektler, geri bildirim.

Sonrası (prototip dışı): ana menü, şarkı seçimi, ayarlar ekranı, offset kalibrasyon ekranı, birden fazla zorluk, skor kaydı.
