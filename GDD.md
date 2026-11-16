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
  - Üst bilgi: şarkı adı, sanatçı, ses dosyası (bilgi), BPM, offset (sn), zorluk.
  - **Tempo:** isteğe bağlı `beatTimes` = her tam beat'in ses dosyasındaki zamanı (sn, kesin artan) → **değişken tempo**.
    Doluysa beat → zaman komşu iki beat arasında doğrusal hesaplanır (listenin önü/sonu ilk/son aralıkla devam eder); `bpm` ortalama tempodur (bilgi), `offset` kullanılmaz.
    Boşsa sabit tempo: zaman = `offset` + beat × 60/`bpm` (elle yazılan haritalar).
  - Notalar: `lane` (0–3), `beat` (başlangıç, beat cinsinden, kesirli olabilir: 12.5 = 1/2, 12.25 = 1/4), `lengthBeats` (0 = tap, >0 = hold).
- Beat cinsinden tutulur, böylece tempo ızgarasına oturur ve elle düzeltmek kolaydır.
- Çalınacak beatmap ve şarkı (AudioClip) `SongLoader`'da Inspector'dan seçilir. Şarkı yoksa beatmap'in beat'lerinde metronom tıkı çalar; "beat tıkı ekle" seçeneği şarkının üstüne tık ekler (beat takibini kulakla kontrol için).
- **Otomatik üretici (Editor aracı, *PixelBeat > Beatmap Üretici*):** Ses dosyası + zorluk (1–10) + isteğe bağlı BPM ipucu →
  - Sabit BPM varsayılmaz: spektral akıdan onset gücü → otokorelasyonla tempo (ipucu yarım/iki kat karışıklığını önler) → ~8 sn pencerelerde yerel tempo → dinamik programlamayla beat takibi. Sonuç `beatTimes`'a yazılır.
  - Onset tespiti yapılır, sonuçlar beat ızgarasına (1/1, 1/2, 1/4) oturtulur. Kolay: 1/1 + güçlü 1/2; orta: 1/2; zor (8+): 1/4.
  - Uzun süren / sürdürülen enerjiden hold notalar tahmin edilir (baskın bandın enerjisi tepe değerin yarısının üstünde kaldığı ve araya yeni güçlü onset girmediği süre; 1–4 beat).
  - Yoğunluk (zorluk) parametresi: beat başına ~0.4 (1) … 2.2 (10) nota; akor ve hold oranı zorlukla artar.
  - Şerit dağıtımı: aynı şeride çok art arda yığılma (en fazla 2), oynanamaz akorlar (aynı anda 3+ tuş) ve hold sırasında (bitişi dahil) aynı şeride nota olmamalı.
  - **Mobil, iki başparmak:** şerit 0–1 sol el, 2–3 sağ el; aynı anda en fazla 2 parmak; akor el başına bir nota; hold sürerken akor ve ikinci hold yok, diğer notalar serbest elde; aynı el art arda en az 150 ms.
  - Kısıtlar içinde şerit ses perdesine göre seçilir (bas → sol, tiz → sağ). Üretim belirleyicidir (aynı girdi → aynı harita).
  - Çıktı yukarıdaki JSON formatına yazılır, oynanabilirlik denetiminden geçer; sonra elle düzenlenebilir.

### 7.1 Melodi tabanlı üretim (MIDI) — tercih edilen yöntem
Beat tabanlı üretim notaları ritme oturtur ama melodik şarkılarda "duyduğun sese" denk gelmez. Bu yüzden asıl yöntem **MIDI'den melodi** çıkarmaktır (Piano Tiles hissi).
- **Girdi:** Şarkının ses dosyası + MIDI. MIDI ya hazır bulunur ya da şarkıdan **Basic Pitch** (Spotify, açık kaynak) ile otomatik çıkarılır. Basic Pitch çıktısı kayıtla zaten hizalıdır.
  Hazır (elle yazılmış) MIDI kullanılırsa kayda **hizalama** gerekir (ör. onset/chroma ile DTW); bu sonraki iş.
- **Melodi çıkarma:** Çok sesli MIDI'den melodi hattı seçilir: ~60 ms içindeki aynı anda başlayan notalardan en tizi (skyline), çok kısa/zayıf "hayalet" notalar ve süslemeler atılır, alt perde sınırı uygulanır.
- **Zamanlama:** Notalar MIDI'deki gerçek zamanlarında kalır (beat ızgarasına zorla oturtulmaz). Global offset ayarı olur.
- **Yoğunluk/zorluk:** Hedef nota/sn (ör. Kolay ~1.5, Normal ~2.5, Zor ~3.5). Fazla notalar önem sırasına göre elenir (vuruş gücü, süre, melodik vurgu); notalar arası minimum boşluk korunur.
- **Şerit = perde konturu:** Melodi yükselince sağa, alçalınca sola kayar; tekrarlanan perde aynı/komşu şeride düşer (aynı şeride çok hızlı art arda gelmesin); büyük atlamalar uzak şeritlere. Eşleme yerel perde aralığına göre (kayan pencere) yapılır ki 4 şeridin hepsi kullanılsın.
- **Hold:** Yeterince uzun tutulan melodi notaları (ör. ≥ 0.4 sn) hold olur, uzunluk üst sınırlı.
- **Akor:** Yüksek zorlukta, aynı anda güçlü vurulan birden fazla nota varsa 2'li akor.
- GDD §7'deki şerit kuralları (yığılma yok, 3+ akor yok, hold sürerken o şeride nota yok, iki parmakla oynanabilir) aynen geçerli.
- Beat tabanlı üretici "ritim modu" olarak kalır; vuruş çizgisi parlaması için beat zamanları yine şarkıdan çıkarılır.
- **Sonraki iş — Kayıt modu:** Şarkı çalarken kullanıcı melodiye dokunur, dokunuşlar notaya dönüşür; otomatik haritadaki hatalar böyle düzeltilir.

## 8. Görsel / Teknik
- **Çözünürlük (dikey):** Genişlik **180 px sabit**, yükseklik telefonun en-boy oranına göre uzar (16:9'da 320, uzun telefonlarda ~360–400). Pixel Perfect Camera ile tam sayı büyütme (1080 px genişlikli telefonda 6x). Önemli öğeler (şeritler, vuruş çizgisi) güvenli alanda kalmalı; çentik/kamera deliği için Screen.safeArea dikkate alınır.
- **Kare hızı:** Application.targetFrameRate = 60 (destekleyen telefonlarda 120 seçeneği sonra).
- **Sprite ayarları:** Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- **Yerleşim (öneri):** 4 şerit ekran genişliğini neredeyse doldurur (her biri ~40 px), vuruş çizgisi alttan ~%20 yukarıda, altında dokunma göstergeleri (basınca yanar). Üstte can barı ve skor, ortada combo/değerlendirme yazısı.
- **Geri bildirim:** Vuruşta şerit parlaması, değerlendirme yazısı (PERFECT / GREAT / GOOD / MISS), combo sayacı.
  - Vuruşta şeritte vuruş çizgisinden yukarı additive dither parlama + 1–2 px parçacıklar. Perfect: beyaz parlama, daha uzun (~0.18 sn), 10 parçacık (beyaz + şerit rengi); Great 6, Good 3 (şerit rengi); Miss yok. Hold bırakışında yarısı.
- **Neon paleti (16 renk)** — tüm placeholder renkler buradan seçilir (kodda `PaletteColor` / `PaletteRef`, serbest renk girilemez):

  | # | Ad | Hex | Kullanım |
  |---|---|---|---|
  | 0 | Void | `#0B0B16` | en koyu arka plan |
  | 1 | Night | `#141428` | şerit paneli |
  | 2 | Slate | `#22223A` | buton, can barı zemini |
  | 3 | Steel | `#343448` | ayraç, gösterge çerçevesi |
  | 4 | Ash | `#505068` | kaçan nota |
  | 5 | Haze | `#8A8AB0` | soluk yazı, vuruş çizgisi |
  | 6 | White | `#FFFFFF` | parlama, skor |
  | 7 | Cyan | `#33FFFF` | şerit 0/3, Perfect |
  | 8 | Teal | `#1C8C9C` | cyan gölge / hold gövdesi |
  | 9 | Magenta | `#FF40C0` | şerit 1/2, combo |
  | 10 | Plum | `#8E2470` | magenta gölge, ufuk |
  | 11 | Violet | `#7A4DFF` | arka plan ızgarası |
  | 12 | Lime | `#66FF66` | Great |
  | 13 | Gold | `#FFD040` | Good, S derecesi, güneş |
  | 14 | Orange | `#FF8A3D` | güneş |
  | 15 | Red | `#FF4060` | Miss, FAILED, düşük can |
- **Placeholder pixel art:** Editor'de kodla, sadece paletten üretilir (*PixelBeat > Bake Neon Sprites* → `Assets/Art/Neon/`, `Assets/Data/NeonSkin.asset`): nota başı, hold gövdesi/kuyruğu, nota halesi, dokunma göstergesi (boşta/basılı), vuruş çizgisi (normal/parlak), şerit paneli, şerit parlaması, parçacıklar, arka plan. 9-slice / döşeme ile boyutlanır (ölçekleme yok). Çok tonlu sprite'lar renk başına ayrı üretilir. PNG'ler elle çizilenlerle değiştirilebilir.
- **Neon parlama:** Bloom kullanılmaz (pixel art'ı bulanıklaştırır, telefonda pahalı). Notaların ve parlamaların arkasında önceden çizilmiş dither'lı hale sprite'ları additive (`PixelBeat/SpriteAdditive`) çizilir.
- **Arka plan:** Koyu gökyüzü + şeritli neon güneş + şehir silüeti (sprite, pivot = ufuk) ve ufuktan akan perspektifli neon ızgara çizgileri. Şerit paneli yarı saydam (arka plan altından görünür). AI ile çizilmiş görsel `NeonBackground > Background Sprite` alanına konarak değiştirilebilir.
- **CRT:** Ekran çözünürlüğünde hafif scanline (her oyun pikseli satırının alt ~1/3'ü) + hafif vinyet. Aç/kapat: kalibrasyon ekranındaki "CRT" butonu (PlayerPrefs'e kaydedilir), varsayılan açık.
- **AI görsel içe aktarma:** `Assets/Art/Incoming/` → *PixelBeat > AI Görsel İçe Aktar*: seçilen piksel boyutuna nearest-neighbor küçültme, palete indirgeme (OKLab'de en yakın renk, isteğe bağlı 2x2 Bayer dither), Point / sıkıştırmasız / PPU 16 sprite olarak `Assets/Art/Imported/`.

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
6. **Otomatik beatmap üretici:** Editor aracı. ✅
   - Test şarkısı: `lumiere.mp3` (yerel, gitignore'da) → `lumiere.json` (zorluk 6, ipucu 161 BPM).
7. **Neon görünüm:** Placeholder pixel art, efektler, geri bildirim. ✅ (telefonda build/FPS testi bekliyor (Unity Hub tamamen yeniden başlatılınca yapılacak))

Sonrası (prototip dışı): ana menü, şarkı seçimi, ayarlar ekranı, birden fazla zorluk, skor kaydı, Google Play yayını.
