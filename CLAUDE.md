# PixelBeat

Pixel art ritim oyunu. Unity 6 (6000.2), 2D URP şablonu.
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
- **Git commit:** Bir görev/adım bitip Unity Console'da derleme hatası olmadığında, sormadan commit at.
  Commit mesajı Türkçe ve açıklayıcı olsun (ör. "Adım 1: Conductor ve metronom eklendi").
  Büyük bir değişikliğe başlamadan önce commit edilmemiş değişiklik varsa önce onları commit et.
  Push yapma (uzak depo yok).
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
- Müzik `AudioSource.PlayScheduled` ile başlatılır.
- Tek bir `Conductor` sınıfı: BPM, offset, şarkı pozisyonu (saniye + beat), beat başına saniye.
  Diğer sistemler zamanı sadece Conductor'dan okur.
- Oyuncu için ses/input gecikmesi kalibrasyon offset'i desteklenmeli.

## Pixel art ayarları
- Sprite import: Filter Mode = Point, Compression = None, tüm sprite'larda aynı Pixels Per Unit.
- Kamerada Pixel Perfect Camera kullanılır.
