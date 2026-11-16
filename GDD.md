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
- Çalınacak beatmap ve şarkı (AudioClip) `SongLoader`'da Inspector'dan seçilir: **Zorluk** = Özel (tek beatmap: ritim modu / elle yazılmış) ya da Kolay / Normal / Zor; Kolay/Normal/Zor'da **Harita türü** = Melodi (§7.1) ya da Vokal (§7.2), her tür için üç ayrı alan. Şarkı yoksa beatmap'in beat'lerinde metronom tıkı çalar; "beat tıkı ekle" seçeneği şarkının üstüne tık ekler (beat takibini kulakla kontrol için).
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
- **Uygulama (Beatmap Üretici → Mod: Melodi):** ses + `.mid` → `<ad>_easy|normal|hard.json`. Kesinleşen değerler:
  - Melodi hattı, sırayla: alt perde sınırı (varsayılan MIDI 48) ve hayalet temizliği (< 60 ms ya da velocity'si çevresindeki ±3 sn'nin medyanının yarısından az) → skyline (60 ms grupta en tiz, zaman = kendi başlangıcı) → tiz nota çalarken (ilk 0.5 sn) altta başlayan eşlik atılır → yerel register: çevredeki (±3 sn) melodi notalarının %65'liğinin 7 yarım sesten fazla altı bas/eşlik sayılır → süsleme (< 110 ms, 150 ms içinde ±2 yarım ses nota geliyor).
  - **Cümle başları korunur:** önceki melodi notasının bitişinden ≥ 0.5 sn sessizlik sonra gelen nota seyreltmede hep kalır.
  - Seyreltme 8 sn'lik pencerelerde bütçeyle, önem = velocity + süre + melodik vurgu (yerel zirve, ≥ 5 yarım ses atlama).
  - Zorluklar: Kolay 1.5 nota/sn, min boşluk 0.30 sn, hold ≥ 0.5 sn · Normal 2.5, 0.20, ≥ 0.4 · Zor 3.5, 0.15, ≥ 0.4 + 2'li akor (en fazla %10, iki nota da güçlü olmalı). Hedef yoğunluk melodi hattının yoğunluğunu aşamaz.
  - Hold en fazla 2 sn, sonraki notadan en az 0.15 sn önce biter.
  - Şerit: perde, ±4 sn penceredeki yerel aralığa (%10–%90, en az 7 yarım ses) göre 0–3'e eşlenir; yukarı → sağ/yerinde, aşağı → sol/yerinde, tekrar → aynı/komşu, ≥ 5 yarım ses atlama → en az 2 şerit. Kısıtlar ritim moduyla ortak (`LaneAssigner`).
  - Nota zamanı = MIDI zamanı + offset; beat ızgarasına oturtulmaz, şarkının beat zamanlarına göre kesirli beat'e çevrilir.
  - Lumiere (Basic Pitch, 2181 nota → melodi hattı 623): Kolay 323 nota (1.5/sn), Normal 458 (2.1/sn), Zor 594 (2.7/sn, 29 akor). Normal ilk notalar 0.95, 1.31, 2.42 sn.
- **Sonraki iş — Kayıt modu:** Şarkı çalarken kullanıcı melodiye dokunur, dokunuşlar notaya dönüşür; otomatik haritadaki hatalar böyle düzeltilir.

### 7.2 Vokal modu — sözlere göre nota (tercih edilen, vokalli şarkılarda)
Oyuncu ritmi çoğunlukla **sözlerden/şarkıcının sesinden** takip eder (kullanıcı testi: masaya vururken vokali takip etti). Bu yüzden vokalli şarkılarda notalar **söylenen hecelere** konur.
- **Girdiler:** Orijinal şarkı (oyunda çalan), **vokal stem'i** (Moises ile ayrıldı), vokal stem'inden Basic Pitch ile çıkarılmış **vokal MIDI**, isteğe bağlı **enstrüman stem'i** ("other") ve tam mix MIDI'si (§7.1).
  Lumière: `Assets/Audio/Stems/lumiere_vocals.mp3`, `Assets/Audio/Stems/lumiere_other.mp3`, `Assets/Audio/Midi/lumiere_vocals.mid` (532 nota), `Assets/Audio/Midi/lumiere_basicpitch.mid` (tam mix).
- **Vokal kapısı:** Stem ayrımı kusursuz değil; vokal MIDI'sinde şarkıcı susarken de "sızıntı" notalar olur (Lumière'de 532 notanın ~120'si vokal bölümlerinin dışında). Vokal stem'inin ses enerjisinden **vokal aktif bölümleri** çıkarılır, dışındaki vokal notaları atılır.
  Lumière için ölçülen vokal bölümleri yaklaşık **8–122 sn** ve **147–196 sn**.
- **Hece bölme:** Basic Pitch aynı perdede art arda söylenen heceleri tek uzun nota yapabilir. Vokal stem'indeki onset'lere bakılarak uzun notalar hecelere bölünür (bölünen parçalar tap olur; gerçekten uzatılan hece hold kalır).
- **Tek ses:** Vokal tek sesli olduğundan skyline/bas filtresi gerekmez; perde konturu → şerit eşlemesi (§7.1) aynen kullanılır.
- **Kaynak önceliği (karışma yok):** Vokal aktif bölümlerde notalar **sadece vokalden** gelir; o bölümlerde enstrüman ("other"/tam mix) notası **hiç** eklenmez, şarkıcı hece arasında nefes alırken de. Enstrüman notaları **yalnızca** vokalin uzun süre (≥ ~4 sn) sustuğu yerlerde kullanılır. Kısa vokal duraklamaları (nefes, cümle arası < ~4 sn) boş kalır — bunlar müzikal "dinlenme"dir.
  Geçiş sınırlarında iki kaynağın notaları üst üste binmez; sınırın iki yanında en az ~0.5 sn boşluk bırakılır.
- **Enstrümantal aralar:** Vokalin olmadığı bölümler (Lumière'de ~0–8, ~122–147, ~196–222 sn) boş kalmaz; buralara §7.1 melodi modunun ürettiği notalar (tam mix ya da "other" stem'i) yerleştirilir. Geçişlerde ani yoğunluk sıçraması olmamalı.
- **Yoğunluk/zorluk:** §7.1'deki hedefler geçerli; vokal hece hızı yeterli değilse zorluk farkı akor ve min. boşlukla verilir.
- Oyunda çalan ses yine **orijinal şarkı**dır; stem'ler sadece analiz içindir.
- **Uygulama (Beatmap Üretici → Mod: Vokal):** orijinal şarkı + vokal stem'i + vokal MIDI + tam mix MIDI → `<ad>_vocal_easy|normal|hard.json`. Kesinleşen değerler:
  - Kapı: 50 ms RMS zarfı, eşik = zarfın %95'liğinin 30 dB altı (göreli), < 1.5 sn susmalar birleşir, < 1 sn bölümler atılır, kenarlara 0.15 sn pay. Başlangıcı bölüm dışında olan vokal notası atılır.
  - Tek ses: veride vokal MIDI'si tam tek sesli değil (aynı anda başlayan notalar, oktav hataları). 60 ms içinde başlayanlardan **önceki notaya perdece en yakını** kalır (eşitlikte velocity); yerel medyandan (±3 sn) bir oktavdan fazla sapan perde atılır.
  - Hece bölme: ≥ 0.3 sn notalar, vokal stem onset'lerinde bölünür (notanın ilk/son 80 ms'i hariç, parça ≥ 0.12 sn). Son olmayan parçalar tap, son parça hold eşiğini geçerse hold.
  - Vokal bölgesi = vokal bölümleri + hecelerin kapladığı süre, **4 sn'den kısa boşluklar dahil** tek bölge. Enstrüman dolgusu sadece bölgeler dışında, sınırlardan **0.5 sn** içeride (dolgu hold'u da sınırda kesilir).
  - Dolgu: tam mix'in §7.1 melodi hattı, 1 sn pencerelerle seyreltilir; hedef yoğunluk sınırda komşu vokal yoğunluğundan (sınırın dışındaki 4 sn) başlar, 10 sn'de zorluk hedefine çıkar. Dolguda cümle başı eşiği 2 sn (sık cümle başı sıçrama yapmasın).
  - Akor (Zor): vokal bölgesinde **vurgulu hecenin kendisi** ikinci şeride (cümle başı ya da velocity %70'liğin üstü); enstrüman notası kullanılmaz. Dolguda §7.1 kuralı. Toplam pay %10.
  - Lumière: kapı 5.9–122.2 ve 147.3–196.5 sn (6.0 sn'de vokal stem'inde güçlü bir giriş hecesi var, ölçümdeki ~8 sn'den erken), kapı dışı 121 nota atıldı, 41 nota → 382 hece. Kolay 283 nota (vokal 223 / enstrüman 60), Normal 356 (281 / 75), Zor 474 (382 / 92, 43 akor). Geçişlerde komşu 4 sn yoğunluk farkı ≤ 0.7 nota/sn.

## 8. Görsel / Teknik
- **Çözünürlük (dikey):** Genişlik **360 px sabit** (önceden 180; konsept görseldeki detay için 2 katına çıkarıldı), yükseklik telefonun en-boy oranına göre uzar (~640–800). Pixel Perfect Camera ile tam sayı büyütme (1080 px genişlikli telefonda 3x). Önemli öğeler (şeritler, vuruş çizgisi) güvenli alanda kalmalı; çentik/kamera deliği için Screen.safeArea dikkate alınır.
- **Kare hızı:** Application.targetFrameRate = 60 (destekleyen telefonlarda 120 seçeneği sonra).
- **Sprite ayarları:** Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- **Yerleşim (öneri):** 4 şerit ekran genişliğini neredeyse doldurur (her biri ~80 px), **düz** (perspektif yok — okunurluk için), vuruş çizgisi alttan ~%20 yukarıda, altında dokunma düğmeleri. Üstte can barı ve skor, ortada combo/değerlendirme yazısı.
- **Sanat yönü (konsept: `Docs/ui_konsept.png`, Gemini ile üretildi):** Konsept bir stil rehberidir; birebir kopyalanmaz, 360 px pixel art'a uyarlanır.
  - **Şerit renkleri (her şerit ayrı):** 0 = Cyan, 1 = Violet, 2 = Magenta, 3 = Lime. Nota, hold, dokunma düğmesi ve şerit parlaması o şeridin renginde.
  - **Dokunma düğmeleri:** Şerit renginde neon çerçeveli yuvarlak köşeli kare, **ikon yok** (ok/daire gibi ikonlar "kaydır" hissi verir, bizde kaydırma yok). Basınca dolar ve parlar.
  - **Tap nota:** Neon çerçeveli **elmas (◇)**, içi koyu, etrafında hale.
  - **Hold nota:** Elmas baş + şerit renginde parlayan ışın gövde (kenarlarda koyu, ortada açık), kuyrukta küçük elmas. Tutulurken gövde parlaklaşır.
  - **Değerlendirme yazıları:** Kalın, sağa yatık (italik) pixel font, 2–3 tonlu dikey renk geçişi + koyu dış hat + ünlem: `PERFECT!` (cyan→magenta), `GREAT!` (lime→cyan), `GOOD!` (gold), `MISS!` (red→magenta).
  - **Logo:** "PixelBeat" — kalın, yuvarlak hatlı, cyan→magenta geçişli, neon dış hatlı pixel yazı.
  - **Butonlar:** Koyu iç, neon çerçeve (cyan veya magenta), büyük harf yazı, solda küçük ikon olabilir (▶ OYNA, ⚙ AYARLAR).
  - Arka plan: mevcut neon güneş + şehir + perspektifli ızgara kalır (şeritler düz, ızgara sadece arka planda).
  - **Uygulama (360 px):** Şerit 80 px. Tap nota 26 px elmas (1 px koyu kenar, 2 px şerit rengi çerçeve, üst yarıda beyaz iç çizgi, koyu iç, ortada küçük taş) + 40 px additive dither hale. Hold: 12 px ışın (kenar koyu → renk → beyaz merkez, dikey döşenir) + 28 px ışın halesi (tutulurken alfa 0.45 → 0.9) + 14 px kuyruk elması. Dokunma düğmesi: yuvarlak köşeli 9-slice, kare (genişlik ≤ şerit − 12 px), basılıyken dolu + Tiled hale.
  - Değerlendirme yazıları hazır sprite: 5x7 font 3x + 1 px kalınlaştırma, 3 satırda 1 px italik, geçiş PERFECT! Cyan→Violet→Magenta, GREAT! Lime→Cyan, GOOD! Gold→Orange, MISS! Red→Magenta; üst satır beyaz, sağ-alt koyu gölge, Void dış hat, 4 px üst renk hale. Logo: 5x7 glif 3x (+2 px kalın), yuvarlatılmış köşe, Cyan→Violet→Magenta, Void + Magenta dış hat, 5 px hale. Buton çerçevesi (Cyan / Magenta), ▶ ve ⚙ ikonları menü için hazır (HUD butonları bu çerçeveyi kullanır).
  - **Yazılar (HUD, kalibrasyon, sonuç, streak):** 5x7 font `ScreenLayout.UiScale` = 2 ile çizilir (180 px dönemindeki boyutlar). Bu yüzden ekranda iki piksel boyutu bir arada (yazılar 2x, yeni sanat 1x); sonra detaylı 2x font çizilebilir.
  - Kontrol görüntüleri: `Docs/ui_360_oyun.png` (oyun), `Docs/ui_360_sanat.png` (*PixelBeat > Sanat Önizlemesi*: tüm sprite'lar tek tuvalde).
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
  | 7 | Cyan | `#33FFFF` | şerit 0, Perfect |
  | 8 | Teal | `#1C8C9C` | cyan gölge / hold gövdesi |
  | 9 | Magenta | `#FF40C0` | şerit 2, combo |
  | 10 | Plum | `#8E2470` | magenta gölge, ufuk |
  | 11 | Violet | `#7A4DFF` | şerit 1, arka plan ızgarası |
  | 12 | Lime | `#66FF66` | şerit 3, Great |
  | 13 | Gold | `#FFD040` | Good, S derecesi, güneş |
  | 14 | Orange | `#FF8A3D` | güneş |
  | 15 | Red | `#FF4060` | Miss, FAILED, düşük can |
- **Placeholder pixel art:** Editor'de kodla, sadece paletten üretilir (*PixelBeat > Bake Neon Sprites* → `Assets/Art/Neon/`, `Assets/Data/NeonSkin.asset`): elmas nota + hale, hold ışını + hale, kuyruk elması + hale, dokunma düğmesi (boşta/basılı/hale, şerit renginde), değerlendirme yazıları + hale, logo + hale, buton çerçevesi + hale, ▶/⚙ ikonları, vuruş çizgisi (normal/parlak), şerit paneli, şerit parlaması, parçacıklar, 360 px arka plan. 9-slice / döşeme ile boyutlanır (ölçekleme yok). Çok tonlu sprite'lar renk başına ayrı üretilir. PNG'ler elle çizilenlerle değiştirilebilir.
- **Neon parlama:** Bloom kullanılmaz (pixel art'ı bulanıklaştırır, telefonda pahalı). Notaların ve parlamaların arkasında önceden çizilmiş dither'lı hale sprite'ları additive (`PixelBeat/SpriteAdditive`) çizilir.
- **Arka plan:** Koyu gökyüzü + şeritli neon güneş + şehir silüeti (sprite, pivot = ufuk) ve ufuktan akan perspektifli neon ızgara çizgileri. Şerit paneli yarı saydam (arka plan altından görünür). AI ile çizilmiş görsel `NeonBackground > Background Sprite` alanına konarak değiştirilebilir.
- **CRT:** Ekran çözünürlüğünde hafif scanline (her oyun pikseli satırının alt ~1/3'ü) + hafif vinyet. Aç/kapat: kalibrasyon ekranındaki "CRT" butonu (PlayerPrefs'e kaydedilir), varsayılan açık.
- **AI görsel içe aktarma:** `Assets/Art/Incoming/` → *PixelBeat > AI Görsel İçe Aktar*: seçilen piksel boyutuna nearest-neighbor küçültme, palete indirgeme (OKLab'de en yakın renk, isteğe bağlı 2x2 Bayer dither), Point / sıkıştırmasız / PPU tutarlı sprite olarak `Assets/Art/Imported/`.

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
   - Melodi modu (§7.1): `lumiere.mp3` + `lumiere_basicpitch.mid` (yerel) → `lumiere_easy/normal/hard.json`. ✅
   - Vokal modu (§7.2): + `lumiere_vocals.mp3` + `lumiere_vocals.mid` (yerel) → `lumiere_vocal_easy/normal/hard.json`; sahnede Vokal / Normal seçili. ✅
7. **Neon görünüm:** Placeholder pixel art, efektler, geri bildirim. ✅ (telefonda build/FPS testi bekliyor (Unity Hub tamamen yeniden başlatılınca yapılacak))

Sonrası (prototip dışı): ana menü, şarkı seçimi, ayarlar ekranı, birden fazla zorluk, skor kaydı (§11, ✅ UI-2), Google Play yayını.

## 11. Arayüz (UI) — prototip sonrası ilk iş
Ekranlar (hepsi dikey, 360 px genişlik, §8 sanat yönüne uygun, dokunmatik):
- **Ana menü:** Üstte "PixelBeat" logosu, altında büyük **OYNA** ve **AYARLAR** butonları. Arka planda neon arka plan ve hafif animasyon.
- **Şarkı listesi:** Dikey kaydırılabilir liste. Her satırda şarkı adı, sanatçı, süre ve o şarkıdaki **en iyi derece**. Şarkı seçilince: harita türü (**Vokal / Melodi**, sadece mevcut olanlar) ve zorluk (**Kolay / Normal / Zor**) seçimi, her zorluğun yanında en yüksek skor + derece. **BAŞLA** butonu. Şarkılar bir şarkı kataloğundan (ScriptableObject) gelir; yeni şarkı eklemek = kataloğa satır eklemek.
- **Ayarlar:** Nota hızı (kaydırıcı, canlı önizleme), ses offset'i (ms, +/−) ve **KALİBRE ET** (mevcut kalibrasyon ekranı), müzik sesi, efekt sesi, CRT aç/kapa. PlayerPrefs'e kaydedilir.
- **Oyun ekranı:** Mevcut HUD (skor, can, combo, streak) yeni stile uyarlanır. Duraklatma menüsü bu adımda yok; uygulama arka plana giderse şimdilik şarkı baştan başlar.
- **Sonuç ekranı:** Yeni stilde büyük harf derecesi, skor, max combo, Perfect/Great/Good/Miss sayıları, FULL COMBO / ALL PERFECT rozeti, **YENİ REKOR** göstergesi; **TEKRAR** ve **MENÜ** butonları. FAILED ekranı da aynı butonlarla.
- **Skor kaydı:** Şarkı + harita türü + zorluk başına en yüksek skor, derece, max combo, FC/AP bilgisi (PlayerPrefs ya da JSON dosya).
- Ekranlar arası geçiş: kısa neon fade/kaydırma; geri tuşu (Android back) bir önceki ekrana döner.
- **Uygulama (UI-2):**
  - Tek sahne (`Main`); `ScreenRouter` ekran yığınını tutar, menü ekranlarını kodla kurar (SpriteRenderer + `PixelText` / `PixelButton`, 360 px ızgarası). Geçiş: 0.12 sn Void'e karartma + ortadan genişleyen magenta çizgi, sonra açılma. Geri (Escape = Android back): bir önceki ekran; oyundan geri → şarkı detayı (harita boşaltılır, skor kaydedilmez); kalibrasyondan geri → ayarlar; ana menüde → uygulamadan çıkış.
  - Menü dokunuşları `UiPointer` (tek parmak): butonlar **bırakışta** tetiklenir, 8 px'ten fazla kayan dokunuş kaydırmadır. Oyun içi ◀ / R ve kalibrasyon basışta (hızlı tepki).
  - Şarkı kataloğu: `Assets/Data/SongCatalog.asset` (şarkı: kimlik, ad, sanatçı, AudioClip, haritalar = tür + zorluk + JSON). Beatmap Üretici'nin Melodi/Vokal modu "Şarkı kataloğuna ekle" ile haritaları buraya yazar. SongLoader'daki "Özel" beatmap + `autoStartCustom` sadece Editor hızlı testi.
  - Şarkı listesi: satır 320x64 neon çerçeve (ad, sanatçı, süre, en iyi derece), SpriteMask ile kırpılan kaydırma, bırakınca sürtünmeli kayma.
  - Şarkı detayı: VOKAL / MELODI sekmeleri (sadece mevcut olanlar), KOLAY / NORMAL / ZOR satırları (rekor skor + derece, AP'de `+`, harita yoksa YOK), BASLA. Son seçilen tür ve zorluk hatırlanır.
  - Ayarlar: nota hızı 0.40–2.00 sn (0.05 adım, önizleme şeridinde oyundaki piksel/sn hızla akan elmaslar), ses offset'i ±5 ms (−500…+500) + KALIBRE ET, müzik sesi (varsayılan 80), efekt sesi (varsayılan 70; beat tıkı, kalibrasyon metronomu, buton tık sesi), CRT. Hitsound yok.
  - Oyun ekranı: üst köşelerde ◀ (menü) ve R (baştan başlat); CAL kaldırıldı (kalibrasyon ayarlarda).
  - Sonuç: büyük derece harfi (7x font, renk geçişli + hale), FULL COMBO / ALL PERFECT rozeti, yanıp sönen YENI REKOR!, skor (2x), max combo, sayılar, TEKRAR / MENU. FAILED aynı butonlarla. Açıldıktan sonra 0.5 sn dokunuş yok sayılır.
  - Skor kaydı: `persistentDataPath/scores.json` (anahtar `şarkı|tür|zorluk`): en yüksek skor + o skorun derecesi, en iyi max combo, FC/AP kalıcı. Fail edilen oyun kaydedilmez. Bozuk dosya boş kayıtla açılır.
  - Ekran görüntüleri: `Docs/ui_menu_*.png`, `Docs/ui_sonuc.png`, `Docs/ui_failed.png`.
