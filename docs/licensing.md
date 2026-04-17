# TOD Lisanslama Altyapisi - Kurulum ve Calistirma Rehberi

## Genel Akis

```
1. Generator ile anahtar + lisans dosyasi uret  (tek komut)
2. Public key'i uygulamaya gom
3. appsettings.json'a Licensing blogu ekle
4. Program.cs'e lisanslama satirlarini ekle
5. Uygulamayi calistir, arayuzden lisans dosyasini yukle
```

---

## Adim 1: Lisans Dosyasi Uretimi

Generator araci key yoksa otomatik olusturur, bilgileri sorar, lisans dosyasini uretir.

```bash
cd tools/Tod.LicenseGenerator
dotnet run -- generate
```

Ilk calistirmada ECDSA P-256 anahtar cifti otomatik uretilir:
- `license-private.key` — **Git'e commitlEMEYIN!**
- `license-public.key` — Uygulamaya gomulecek.

Ardindan asagidaki bilgiler sorulur:

| Soru | Varsayilan | Aciklama |
|------|------------|----------|
| Urun kodu | STYS | Urun tanimi |
| Musteri kodu | — | Lisansin ait oldugu musteri |
| Musteri adi | — | Goruntuleme amacli |
| Ortam adi | Production | Lisansin gecerli oldugu ortam |
| Instance ID | instance-01 | Deployment instance kimligi |
| Gecerlilik (gun) | 365 | Kac gun gecerli olacak |
| Aktif moduller | (bos=tumunu ac) | Virgullu liste |
| Deployment marker | (bos) | K8s/Docker ortami icin ek baglama |

Sonucta `license-stys-musteri001.json` gibi bir dosya uretilir.

> **Onemli:** Bu komutu **lisansin calismasi gereken sunucuda/makinede** calistirin.
> Fingerprint, o makinenin adini ve OS bilgisini icerir.

### Diger Komutlar

```bash
dotnet run -- fingerprint       # Bu makinenin fingerprint bilgilerini gosterir
dotnet run -- show-public-key   # Public key parcalarini gosterir
```

## Adim 2: Public Key'i Uygulamaya Gomme (Sadece Bir Kez)

Ilk key uretiminden sonra ekrana yazdirilan public key parcalarini kopyalayin.
Daha sonra gormek icin: `dotnet run -- show-public-key`

Asagidaki dosyayi acin:

```
platform/TOD.Platform.Licensing/EcdsaLicenseSignatureVerifier.cs
```

`PublicKeyParts` dizisini guncellemeniz gerekir. Ornek:

```csharp
private static readonly string[] PublicKeyParts =
[
    "MFkwEwYHKoZIzj0CAQYIKoZIzj",
    "0DAQcDQgAE1234567890abcdef",
    "ghijklmnopqrstuvwxyz123456"
];
```

> Bu islem sadece ilk kurulumda veya key degistiginde yapilir.

## Adim 3: appsettings.json Konfigurasyonu

Backend projesinin `appsettings.json` dosyasina ekleyin:

```json
{
  "Licensing": {
    "LicenseFilePath": "Data/license.json",
    "EnvironmentName": "Production",
    "InstanceId": "instance-01",
    "CustomerCode": "MUSTERI001",
    "DeploymentMarker": "",
    "CacheDurationSeconds": 300,
    "TimeGuardStatePath": "Data/.license-state",
    "ExcludedPaths": ["/health", "/auth", "/ui/license", "/ui/menuitem"]
  }
}
```

| Alan | Aciklama |
|------|----------|
| `LicenseFilePath` | Lisans dosyasinin kaydedilecegi/okunacagi yol |
| `EnvironmentName` | Lisans uretimindeki ortam adi ile ayni olmali |
| `InstanceId` | Lisans uretimindeki instance ID ile ayni olmali |
| `CustomerCode` | Lisans uretimindeki musteri kodu ile ayni olmali |
| `DeploymentMarker` | Lisans uretimindeki deployment marker ile ayni olmali |
| `CacheDurationSeconds` | Lisansin bellekte kac saniye cache'lenecegi (varsayilan 300) |
| `TimeGuardStatePath` | Saat geri alma korumasinin state dosyasi |
| `ExcludedPaths` | Lisans kontrolunden muaf tutulacak path prefix'leri |

> **Dikkat:** `ExcludedPaths` icerisinde su path'ler olmalidir:
> - `/auth` — Login/token endpoint'leri. Yoksa giris yapilamaz.
> - `/ui/license` — Lisans upload/status endpoint'leri. Yoksa ilk lisans yuklenemez.
> - `/ui/menuitem` — Menu API. Yoksa sidebar yuklenemez.
> - `/health` — Health check endpoint'leri.

## Adim 4: Program.cs Entegrasyonu

`Program.cs` dosyasina asagidaki satirlari ekleyin:

```csharp
using TOD.Platform.Licensing.AspNetCore;

// --- builder.Services bolumu ---

// Lisanslama servislerini DI'a ekle
builder.Services.AddTodLicensing(builder.Configuration);

// Controller'lara modul filter'ini ekle (mevcut AddControllers varsa icine ekleyin)
builder.Services.AddControllers(options =>
{
    options.AddTodLicenseModuleFilter();
});

// --- app bolumu ---

// Startup'ta lisansi kontrol et (dosya yoksa uyari verir, crash etmez)
await app.ValidateLicenseOnStartupAsync();

// Her HTTP isteginde lisans kontrolu
app.UseTodLicenseGuard();
```

> `ValidateLicenseOnStartupAsync()` varsayilan olarak lisans yoksa crash ETMEZ,
> sadece uyari loglar. Boylece uygulama baslar ve arayuzden lisans yuklenebilir.
> Zorunlu mod icin: `await app.ValidateLicenseOnStartupAsync(throwOnFailure: true);`

## Adim 5: Arayuzden Lisans Yukleme

1. Uygulamayi baslatIn.
2. Arayuzde **Lisans Yonetimi** sayfasina gidin.
3. Adim 1'de uretilen `.json` dosyasini secin.
4. Dosya yuklenir, imza dogrulanir ve aktif hale gelir.
5. Ekranda lisans durumu, kalan gun ve aktif moduller goruntulenir.

Lisans yuklendikten sonra tum API istekleri calismaya baslar.

## Adim 6: Modul Bazli Lisanslama (Opsiyonel)

Controller veya action'lara `RequiresLicensedModule` attribute'u ekleyin:

```csharp
using TOD.Platform.Licensing.AspNetCore;

[RequiresLicensedModule("Muhasebe")]
[ApiController]
[Route("api/muhasebe")]
public class MuhasebeController : ControllerBase
{
    // Bu controller sadece "Muhasebe" modulu lisansliysa erisime acik olur
}
```

Lisansta `enabledModules` alani bos birakilirsa tum moduller aktif sayilir.

Service layer'da programmatic kontrol:

```csharp
if (!await _licenseService.IsModuleLicensedAsync("Fiyatlandirma"))
    throw new LicenseException("Fiyatlandirma modulu lisansli degil.");
```

---

## Kontrol Listesi

- [ ] Generator ile lisans dosyasi uretildi (`dotnet run -- generate`)
- [ ] `license-private.key` guvenli saklanip `.gitignore`'a eklendi
- [ ] Public key parcalari `EcdsaLicenseSignatureVerifier.cs`'e yazildi
- [ ] `STYS.csproj`'a `Licensing.AspNetCore` referansi eklendi (zaten eklendi)
- [ ] `appsettings.json`'a `Licensing` blogu eklendi
- [ ] `Program.cs`'e `AddTodLicensing` + `ValidateLicenseOnStartupAsync` + `UseTodLicenseGuard` eklendi
- [ ] Uygulama baslatildi
- [ ] Arayuzden lisans dosyasi yuklendi ve dogrulandi

## Onemli Notlar

- `license-private.key` dosyasini **asla** git'e commitlEMEYIN.
- Lisans uretimindeki ortam bilgileri ile `appsettings.json`'daki degerler **birebir ayni** olmalidir.
- Fingerprint, makine adi ve OS bilgisini icerir. **Major OS guncellemesi** sonrasi yeni lisans gerekebilir.
- Container ortamlarinda `DeploymentMarker` alani ile sabit bir baglama noktasi saglayin.
- Persistent volume olmayan container'larda her restart'ta lisans yeniden yuklenmelidir.
  Bu durumda lisans dosyasini Docker volume ile mount edin.
