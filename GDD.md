# PixelBeat — Oyun Tasarım Belgesi (GDD)

> Bu belge oyunun tek doğruluk kaynağıdır. Bir kararı değiştirirsek önce burayı güncelle.

## 1. Özet
- **Tür:** 4 şeritli dikey ritim oyunu (osu!mania / DJMAX tarzı).
- **Platform:** Android telefon, **dikey (portrait)**, dokunmatik. (PC sürümü yok; klavye sadece Unity Editor'de test için.)
- **Görsel:** Retro arcade / neon pixel art. Koyu arka plan, parlak neon renkler, hafif CRT hissi.
- **Çekirdek döngü:** Notalar yukarıdan aşağı akar → vuruş çizgisine geldiği anda doğru tuşa bas / basılı tut → değerlendirme + combo + skor.

## 2. Kontroller
- **Dokunmatik:** Ekranın alt kısmı 4 eşit dikey dokunma bölgesine ayrılır (her şerit = bir bölge).
  - Bölgeye parmak değdiği an = basış, parmak kalktığı an = bırakış.
  - **Multi-touch** zorunlu (akorlar ve hold sırasında başka şeride basma). Her parmak ayrı takip edilir (touch id).
  - Parmak basılıyken başka şeride kayarsa: o şeridin basışı bırakılmış sayılır (hold kırılabilir), yeni şeride basış sayılmaz.
  - Dokunma bölgesi notanın görsel şeridinden biraz daha geniş/yüksek olabilir (ekranın alt ~%35'i).
- **Editor testi:** Unity Editor'de S D K L tuşları aynı şeritleri tetikler (sadece test için).
- Yeni Input System (EnhancedTouch) kullanılır.
- Pause: ekranın üst köşesinde küçük bir duraklat butonu; uygulama arka plana geçerse otomatik duraklat.

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
- **Can:** 0–100, 100 ile başlar. Miss −6, Good +0, Great +1, Perfect +2 (+ streak bonusu, aşağıda), en fazla 100.
  Can 0 olursa şarkı başarısız olur (müzik durur, "FAILED" gösterilir).
- **Perfect streak:** Art arda gelen Perfect sayısı (combo'dan ayrı). Great, Good veya Miss streak'i sıfırlar.
  Hold'un basış ve bırakışı ayrı ayrı sayılır.
  - Streak'e göre her Perfect'in can kazancı: 0–9 → +2, 10–24 → +3, 25–49 → +4, 50+ → +5.
    Kademe, o Perfect sayıldıktan **sonraki** streak değerine bakar (10. Perfect zaten +3 alır; ekranda kademe rengi değiştiği anda bonus da artar).
  - **Görünüm:** Sağ üst köşede (safe area içinde, skorun altında) **eğik/döndürülmüş** bir yazı.
    Yazı **saat yönünde ~35° döndürülmüş**: soldan sağa okunurken **aşağı iner** (sol üstten sağ alta), harflerin tabanı sol alta bakar.
    Harflerin kendisi de döndürülmüş olmalı (dik harflerin merdiven dizilimi DEĞİL).
    Pixel art bozulmasın diye çalışma anında serbest döndürme yapılmaz: "PERFECT" yazısı ve 0–9 rakamları için
    **önceden döndürülmüş pixel sprite'lar** kullanılır (RotSprite benzeri, yumuşatmasız döndürme ile Editor'de üretilir; ileride elle çizilmiş sprite'larla değiştirilebilir).
    Sayı (streak) aynı açıyla, "PERFECT"in üstünde ona paralel durur. Açı ve konum Inspector'dan ayarlanabilir.
  - Streak 5'ten itibaren görünür, sıfırlanınca kısa bir "kırılma" animasyonuyla (titreme + sönme) kaybolur.
  - Her yeni kademede (10/25/50) kısa bir parlama/büyüme vurgusu olur ve rengi kademeye göre değişir (ör. camgöbeği → yeşil → sarı → pembe), böylece oyuncu can bonusunun arttığını anlar.
- **Sonuç:** Şarkı bitince skor, max combo, her sonuçtan kaç tane ve harf derecesi (S/A/B/C/D) gösterilir.
  - Harf derecesi eşikleri: **S ≥ 950.000**, **A ≥ 900.000**, **B ≥ 800.000**, **C ≥ 700.000**, altı **D**. Başarısız olunca derece verilmez.
  - Miss yoksa "FULL COMBO", hepsi Perfect ise "ALL PERFECT" yazısı.
  - Sonuç ekranı son değerlendirmeden 1 sn sonra açılır. FAILED / sonuç ekranında dokunmak (ya da R) baştan başlatır.

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
- **Çözünürlük (dikey):** Genişlik **180 px sabit**, yükseklik telefonun en-boy oranına göre uzar (16:9'da 320, uzun telefonlarda ~360–400). Pixel Perfect Camera ile tam sayı büyütme (1080 px genişlikli telefonda 6x). Önemli öğeler (şeritler, vuruş çizgisi) güvenli alanda kalmalı; çentik/kamera deliği için Screen.safeArea dikkate alınır.
- **Kare hızı:** Application.targetFrameRate = 60 (destekleyen telefonlarda 120 seçeneği sonra).
- **Sprite ayarları:** Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- **Yerleşim (öneri):** 4 şerit ekran genişliğini neredeyse doldurur (her biri ~40 px), vuruş çizgisi alttan ~%20 yukarıda, altında dokunma göstergeleri (basınca yanar). Üstte can barı ve skor, ortada combo/değerlendirme yazısı.
- **Geri bildirim:** Vuruşta şerit parlaması, değerlendirme yazısı (PERFECT / GREAT / GOOD / MISS), combo sayacı.
- Prototipte placeholder (basit renkli kare) sprite'lar yeterli. Gerçek pixel art sonra çizilecek.

## 9. Ses
- Telifsiz müzik kullanılacak. BPM bilgisi şarkının kaynağından alınır.
- Ses dosyaları: `Assets/Audio/Music/`. Müzik için Load Type = Decompress On Load veya Compressed In Memory (gecikme testine göre).
- **Android ses gecikmesi** PC'den belirgin şekilde yüksek ve telefona göre değişir (onlarca–yüzlerce ms). Project Settings > Audio > DSP Buffer Size = Best latency. Bu yüzden **offset kalibrasyon ekranı prototipe dahil** (bkz. yol haritası). Bluetooth kulaklık gecikmeyi çok artırır; kalibrasyon bunu da telafi etmeli.
- Vuruş sesi (hitsound) opsiyonel, ayarlardan kapatılabilir.

## 10. Prototip kapsamı ve yol haritası
Prototip: **1 şarkı, sadece oynanış.** Menü, şarkı seçimi, kayıt yok.

1. **Temel:** Pixel Perfect kamera, Conductor, her beat'te yanıp sönen test karesi (senkron doğrulama). ✅
2. **Tap notalar:** 4 şerit, elle yazılmış küçük test JSON'u, giriş, değerlendirme. ✅
3. **Mobil'e geçiş:** Dikey yerleşim (180 px genişlik), dokunmatik giriş (multi-touch), Android build ayarları, telefonda test, basit offset kalibrasyon ekranı (tempoya dokunarak gecikme ölçme). ✅
   - Kalibrasyon: 90 BPM metronom (sadece ses), 4 ısınma + 16 ölçülen dokunuş, uçlardan 2'şer atılmış ortalama. Fark penceresi geç tarafa asimetrik (−¼ … +¾ beat).
   - Offset sadece gerçek cihaz build'inde PlayerPrefs'e kaydedilir; Editor / Unity Remote'ta sonuç sadece gösterilir.
   - Üst köşelerde geçici butonlar: CAL (kalibrasyon), R (baştan başlat). Gerçek pause gelene kadar uygulama arka plandan dönünce şarkı baştan başlar.
4. **Hold notalar:** Basış + bırakış değerlendirmesi, kırılma (dokunmatikte parmak kaldırma/kayma dahil). ✅
   - Başı kaçırılan hold: basış + bırakış MISS, tamamı gri akar. Kırılan hold'un kalan gövdesi gri akar.
   - Bırakış sonucu basış yazısının bir satır üstünde ayrı gösterilir. Hold tutulurken şerit parlar.
   - Beatmap yüklenirken aynı şeritte hold sürerken (bitişi dahil) başlayan nota uyarı verir.
5. **Skor / combo / can:** Başarısızlık ve şarkı sonu sonuç yazısı. ✅
6. **Otomatik beatmap üretici:** Editor aracı.
7. **Neon görünüm:** Placeholder pixel art, efektler, geri bildirim.

Sonrası (prototip dışı): ana menü, şarkı seçimi, ayarlar ekranı, birden fazla zorluk, skor kaydı, Google Play yayını.
