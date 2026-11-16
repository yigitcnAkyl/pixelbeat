# PixelBeat

Pixel art ritim oyunu, **Android, dikey, dokunmatik**. Unity 6 (6000.2), 2D URP şablonu.
Geliştirici: Yiğitcan (Türkçe konuş, kısa ve pratik ol).

## Oyun tasarımı
- Oyunun tüm tasarım kararları `GDD.md` dosyasında. Her göreve başlamadan önce GDD.md'nin ilgili bölümünü oku.
- Oyun tasarımıyla ilgili belirsiz bir konu varsa (GDD'de yazmıyorsa) tahmin etme, kullanıcıya sor.
- GDD ile çelişen bir şey yapman gerekirse önce sor; karar değişirse GDD.md'yi de güncelle.
- Bir yol haritası adımını bitirince ne yaptığını ve Unity'de nasıl test edileceğini kısaca yaz.

## Çalışma kuralları
- Büyük ya da geri alınması zor değişikliklerden önce plan sun, onay bekle.
- Unity Editor'e erişim UnityMCP sunucusu üzerinden (sahne, obje, component, console).
- Değişiklikleri küçük adımlarla yap.
- **Git commit + push:** Bir görev/adım bitip Unity Console'da derleme hatası olmadığında, sormadan commit at ve hemen `git push` ile GitHub'a (origin/main) gönder.
  Commit mesajı Türkçe ve açıklayıcı olsun (ör. "Adım 2: Tap notalar eklendi").
  Büyük bir değişikliğe başlamadan önce commit edilmemiş değişiklik varsa önce onları commit + push et.
  Commit mesajlarına "Co-Authored-By", "Generated with Claude" gibi Claude imzası/satırı EKLEME; commit'ler sadece kullanıcının adına olsun.
  Force push (`--force`, `-f`, `--force-with-lease`) ve geçmişi yeniden yazma sadece kullanıcı açıkça isterse.
- Script değiştirdikten sonra Unity Console'da derleme hatası var mı kontrol et.
- `Library/`, `Temp/`, `Logs/`, `UserSettings/` klasörlerine ve `.meta` dosyalarına elle dokunma.
- `.csproj` / `.sln` dosyaları Unity tarafından üretilir, düzenleme.

## Klasör yapısı
- `Assets/Scripts/` — C# kodları (alt klasörler: Core, Gameplay, UI, Audio)
- `Assets/Art/` — sprite'lar, animasyonlar
- `Assets/Audio/` — müzikler ve SFX
- `Assets/Scenes/`
- `Assets/Data/` — ScriptableObject'ler (şarkı/beatmap verisi)

## Kod stili
- Namespace: `PixelBeat` (alt alanlar: `PixelBeat.Core`, `PixelBeat.Gameplay` ...)
- Inspector'a açılacak alanlar: `[SerializeField] private`, public field kullanma.
- Input için yeni Input System paketi.

## Ritim zamanlaması (önemli)
- Zamanlama `AudioSettings.dspTime` ile yapılır, `Time.time` / `Time.deltaTime` ile DEĞİL.
  İstisna: Conductor, dspTime'ın adımları arasını `Time.unscaledTimeAsDouble` ile enterpole eder; sapma `maxDriftMs`'i aşarsa dspTime'a geri senkronlanır.
  Input zamanı event zaman damgasından `Conductor.SongTimeAtRealtime` ile şarkı zamanına çevrilir.
- Müzik `AudioSource.PlayScheduled` ile başlatılır.
- Tek bir `Conductor` sınıfı: BPM, offset, şarkı pozisyonu (saniye + beat), beat başına saniye.
  Diğer sistemler zamanı sadece Conductor'dan okur.
- Oyuncu için ses/input gecikmesi kalibrasyon offset'i desteklenmeli.

## Pixel art ayarları
- Sprite import: Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- Kamerada Pixel Perfect Camera kullanılır.

## Android build notları
- Windows kullanıcı adında Türkçe "ı" var (`C:\Users\Msı`). Android araçları (Gradle, CMake/prefab'ın çalıştırdığı `.bat` dosyaları) ASCII olmayan yollarda bozuluyor.
  Bu yüzden proje `C:\Dev\pixelbeat`'te; kullanıcı klasörü altına (Desktop vb.) taşıma.
- `GRADLE_USER_HOME=C:\Gradle` (kullanıcı ortam değişkeni). Yoksa Gradle önbelleği `C:\Users\Msı\.gradle`'a düşer ve
  `:unityLibrary:configureCMakeDebug[arm64-v8a]` adımı `'-class-path' is not recognized` hatasıyla patlar.
- Ortam değişkeni değişince **Unity Hub'ı sistem tepsisinden de tamamen kapatıp** yeniden aç. Unity, Hub'ın ortamını devralır;
  Hub açık kaldıkça yeni değişkenleri görmez (sadece Unity'yi kapatıp açmak yetmez).
- Geçici klasör: `%TEMP%` kullanıcı ortamında `C:\Users\Msı\AppData\Local\Temp`. Süreçlerde genelde kısa ASCII hali (`C:\Users\MS2078~1\...`) görünüyor,
  şimdiye kadar build'i bu bozmadı. Java "Unable to establish loopback connection" ya da temp yolu hatası verirse
  TEMP/TMP ve Java'nın geçici klasörünü (`-Djava.io.tmpdir`, `-Djdk.net.unixdomain.tmpdir`) ASCII bir klasöre (ör. `C:\Temp\java`) yönlendir.
- Unity'nin adb'si: `C:\Program Files\Unity\Hub\Editor\6000.2.6f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe` (PATH'te adb yok).
- FPS ölçümü için Development build gerekir; `FpsMeter` logcat'e 5 sn'de bir `[FPS] ...` satırı yazar.
- MCP üzerinden build sırasında Editor ana thread'i bloklandığı için UnityMCP bağlantısı kopabilir; build bitince yeniden bağlanmasını bekle.
