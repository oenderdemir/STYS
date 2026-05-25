# Faz 69 — Merkezi Çalışma Tesisi (Working Facility) Kontekst Altyapısı

## Amaç

Muhasebe modülündeki tüm ekranlarda bağımsız ve tekrarlayan tesis filtrelerini ortadan kaldırmak, yerine **tek bir merkezi "Çalışma Tesisi" seçimi** getirmek. Kullanıcı muhasebeye girdiğinde bir kez tesis seçer, seçim `localStorage`'da kalıcı olur, tüm ekranlar bu seçimi paylaşır.

---

## Mimari

```
┌──────────────────────────────────────────────────┐
│  MuhasebeTesisContextService (singleton)         │
│  ┌────────────────────────────────────────────┐  │
│  │  seciliTesis: Signal<MuhasebeTesisModel|null>│  │
│  │  tesisler: Signal<MuhasebeTesisModel[]>      │  │
│  │  tesisSecenekleri: Signal<TesisSecenek[]>   │  │
│  │  initialize() → GET /ui/rezervasyon/tesisler │  │
│  └────────────────────────────────────────────┘  │
│  localStorage → stys_muhasebe_calisma_tesisi     │
└────────────────────┬─────────────────────────────┘
                     │ inject()
         ┌───────────┼───────────┐
         ▼           ▼           ▼
   ┌──────────┐ ┌──────────┐ ┌──────────┐
   │ Secim    │ │ Context  │ │ Her      │
   │ Dialog   │ │ Bar      │ │ muhasebe │
   │ (zorunlu)│ │ (üst bar)│ │ ekranı   │
   └──────────┘ └──────────┘ └──────────┘
```

---

## Oluşturulan Dosyalar

### 1. `MuhasebeTesisContextService`
**Dosya:** [`frontend/src/app/pages/muhasebe/services/muhasebe-tesis-context.service.ts`](../frontend/src/app/pages/muhasebe/services/muhasebe-tesis-context.service.ts)

Merkezi tesis kontekst servisi. `providedIn: 'root'` — tüm muhasebe ekranları tarafından paylaşılır.

**Temel API:**
| Metot / Sinyal | Açıklama |
|---|---|
| `seciliTesis` | `Signal<MuhasebeTesisModel \| null>` — seçili tesis |
| `tesisler` | `Signal<MuhasebeTesisModel[]>` — tüm tesisler |
| `tesisSecenekleri` | `Signal<{label, value}[]>` — p‑select için seçenekler |
| `tesislerLoading` | `Signal<boolean>` |
| `tesislerError` | `Signal<string \| null>` |
| `initialize()` | `Observable<MuhasebeTesisModel[]>` — listeyi yükler, localStorage seçimini doğrular |
| `selectTesis(t)` | Tesisi seçer, localStorage'a yazar |
| `clearTesis()` | Seçimi temizler |
| `requireSeciliTesis()` | Seçili tesisi döndürür (yoksa throw) |
| `requireSeciliTesisId()` | Seçili tesis ID'sini döndürür (yoksa throw) |

### 2. `MuhasebeTesisSecimDialogComponent`
**Dosya:** [`frontend/src/app/pages/muhasebe/components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component.ts`](../frontend/src/app/pages/muhasebe/components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component.ts)

Zorunlu tesis seçim dialog'u. `seciliTesis` sinyali `null` olduğunda otomatik açılır, kapatılamaz. Kullanıcı bir tesis seçip "Tesisi Seç ve Devam Et" butonuna tıklayana kadar dialog açık kalır.

**Özel davranış:** Tek tesis varsa otomatik seçilir.

### 3. `MuhasebeTesisContextBarComponent`
**Dosya:** [`frontend/src/app/pages/muhasebe/components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component.ts`](../frontend/src/app/pages/muhasebe/components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component.ts)

Her muhasebe sayfasının üst kısmında gösterilen bağlam çubuğu.

- Seçili tesis varsa: `🏢 Tesis Adı` etiketi + "Değiştir" butonu
- Seçili tesis yoksa: `⚠ Tesisi seçilmedi` uyarı etiketi + "Tesisi Seç" butonu

---

## Tamamlanan Entegrasyonlar

### ✅ Satış Belgeleri
- `satis-belgeleri.component.ts`: `MuhasebeTesisContextService` inject edildi, `ngOnInit`'te `initialize()` çağrılıyor, `openCreateDialog()`'da tesisId bağlamdan alınıyor, `loadBelgeler()`'de filtreye tesisId ekleniyor.
- `satis-belgeleri.component.html`: `<app-muhasebe-tesis-secim-dialog />` ve `<app-muhasebe-tesis-context-bar />` eklendi.

### ✅ Muhasebe Fişleri Listesi
- `muhasebe-fisler.component.ts`: Context servis entegre edildi, `loadTesisler()` kaldırıldı, `loadFromTesisContext()` eklendi.
- `muhasebe-fisler.component.html`: Tesis dropdown grid'den kaldırıldı, context bar ve seçim dialog'u eklendi. Düzenleme dialog'undaki tesis dropdown context'e bağlandı.

### ✅ Faz 69A — Rapor Ekranları Entegrasyonu
- `yevmiye-defteri`: Tesis dropdown kaldırıldı. `filter.tesisId` artık `MuhasebeTesisContextService` üzerinden set ediliyor ve rapor / Excel çağrılarında seçili tesis zorunlu kullanılıyor.
- `muavin-defter`: Tesis dropdown kaldırıldı. Hesap kodu, dönem ve tarih filtreleri korunurken `filter.tesisId` merkezi context'ten alınıyor.
- `hizli-mizan`: Tesis dropdown kaldırıldı. Mizan ve karşılaştırma çağrıları seçili tesis ile çalışıyor; tesis değişince sonuçlar temizleniyor.
- `dashboard`: Dashboard artık açılışta seçili çalışma tesisi ile yükleniyor. Tesis değişince dashboard verisi yeni bağlamla yeniden yükleniyor.
- `kdv-hareket-raporu`: Tesis dropdown kaldırıldı. Filtre değişiklikleri ve manuel sorgu seçili tesis ile çalışıyor.
- `kdv-ozet-raporu`: Tesis dropdown kaldırıldı. Sorgu ve Excel dışa aktarımı seçili tesis üzerinden yapılıyor.
- `kdv-beyanname-hazirlik-kontrol`: Tesis dropdown kaldırıldı. Kontrol çağrıları seçili tesis ile yapılıyor.
- `donem-kapanis-kontrol`: Tesis dropdown kaldırıldı. Kapanış ön kontrolü seçili çalışma tesisi ile çalışıyor; `maliYil` / `donemNo` query paramları korunuyor.

## Faz 69A — Rapor Ekranları Entegrasyonu

Bu faz ile muhasebe rapor ekranları için **merkezi çalışma tesisi tek tesis kaynağı** haline getirildi.

### Entegre edilen rapor ekranları
- Yevmiye Defteri
- Muavin Defter
- Hızlı Mizan
- Muhasebe Dashboard
- KDV Hareket Raporu
- KDV Özet Raporu
- KDV Beyanname Hazırlık Kontrol
- Dönem Kapanış Kontrol

### Kaldırılan tesis filtreleri
- `filter.tesisId` için bağımsız `p-select` alanları kaldırıldı.
- Ekranlardaki `tesisSecenekleri`, `loadTesisler()` ve benzeri lokal tesis yükleme akışları kaldırıldı.
- Kullanıcı tesis değiştirmek istediğinde artık yalnızca üstteki `MuhasebeTesisContextBarComponent` üzerindeki **Değiştir** aksiyonunu kullanır.

### Yeni davranış
- Her rapor ekranının üstüne zorunlu seçim dialog'u ve context bar eklendi.
- Sayfa açılışında seçili tesis yoksa `MuhasebeTesisSecimDialogComponent` zorunlu açılır.
- Servis çağrıları öncesinde `requireSeciliTesisId()` kullanılarak `tesisId: null` gönderimi engellenir.
- Tesis değiştiğinde mevcut rapor sonuçları temizlenir.
- Dashboard ekranı tesis değişiminde veriyi otomatik yeniden yükler; diğer rapor ekranlarında kullanıcı mevcut akışı (`Ara`, `Sorgula`, `Kontrol Et`, `Excel`) ile devam eder.

### Güvenlik notu
- Backend access scope güvenliği aynen korunur.
- Frontend tesis context sadece kullanıcı deneyimi içindir; backend yetki kontrolünün yerine geçmez.

---

## Faz 69B — CRUD / Tanım Ekranları Entegrasyonu

Bu fazda merkezi çalışma tesisi altyapısı raporlardan CRUD / tanım / operasyon ekranlarına yayıldı. Bu batch'te global kabul edilen ekran tespit edilmedi; hedeflenen tüm ekranlar tesis bazlı çalışıyor.

### Entegre edilen ekranlar
- Fiş Oluştur
- Dönemler
- Cari Kartlar
- Hesaplar
- Kasa/Banka Hesapları
- Depolar
- Taşınır Kartları
- Taşınır Fiş Taslağı

### Kaldırılan tesis filtreleri
- Ekranlardaki bağımsız tesis dropdown / filter alanları kaldırıldı.
- Tesis seçimi artık yalnızca `MuhasebeTesisContextBarComponent` üzerinden değiştiriliyor.
- Create/Edit formlarındaki tesis alanları readonly bilgiye dönüştürüldü veya tamamen kaldırıldı.

### TesisId davranışı
- Listeleme ve create/update payload'ları seçili çalışma tesisinden besleniyor.
- `tesisId: null` gönderimi backend çağrılarında engelleniyor.
- Açık dialog / form varsa tesis değişiminde güvenli şekilde kapatılıyor veya temizleniyor.

### Tesis değişince davranış
- Liste ekranlarında veri yeniden yükleniyor.
- Açık form/diyalog varsa kapatılıyor.
- Taşınır fiş taslağı modal dialog'u tesis değişiminde kapanıyor.

### Güvenlik notu
- Backend access scope güvenliği aynen korunur.
- Frontend tesis context sadece kullanıcı deneyimi içindir; backend yetki kontrolünün yerine geçmez.

### Kalan ekranlar
- Bu faz kapsamında ele alınmayan diğer muhasebe operasyon/tanım ekranları sonraki fazlara bırakıldı.
- Bu listede artık Faz 69C kapsamındaki ekranlar yer almıyor.

---

## Faz 69C — Operasyon Ekranları Entegrasyonu

Bu faz ile merkezi çalışma tesisi altyapısı kalan muhasebe operasyon ekranlarına yayıldı.

### Entegre edilen operasyon ekranları
- Banka Hareketleri
- Cari Hareketler
- Kasa Hareketleri
- Stok Hareketleri
- Tahsilat / Ödeme Belgeleri

### Kaldırılan tesis filtreleri
- Operasyon ekranlarındaki bağımsız tesis dropdown / filter alanları kaldırıldı.
- Kullanıcı tesis değiştirmek istediğinde artık yalnızca üstteki `MuhasebeTesisContextBarComponent` üzerindeki **Değiştir** aksiyonunu kullanır.

### Create/Edit ve liste davranışı
- Create/Edit formlarında tesis alanı varsa readonly bilgiye çevrildi veya kaldırıldı.
- Kayıt oluşturma / güncelleme / listeleme akışları seçili çalışma tesisi ile uyumlu hale getirildi.
- Depo, cari, kasa/banka ve taşınır seçenek listeleri seçili tesis bağlamına göre daraltıldı.
- Açık form veya dinamik dialog varsa tesis değişiminde güvenli şekilde kapatılıyor ya da temizleniyor.

### Güvenlik notu
- Backend access scope güvenliği aynen korunur.
- Frontend tesis context sadece kullanıcı deneyimi içindir; backend yetki kontrolünün yerine geçmez.

### Kalan ekranlar
- Bu faz sonunda hedeflenen operasyon ekranları tamamlandı.
- Muhasebe modülünde kalan diğer ekranlar varsa bunlar bu Faz 69 serisinin kapsamı dışında değerlendirilir.

## Faz 69C-A — Servis Filtre Parametreleri Düzeltmesi

Bu alt fazda Faz 69C ile bağlanan operasyon ekranlarının listeleme ve özet servis çağrılarında eksik kalan `tesisId` parametreleri tamamlandı.

### Düzeltilen ekranlar
- Banka Hareketleri
- Kasa Hareketleri
- Stok Hareketleri
- Cari Hareketler
- Tahsilat / Ödeme Belgeleri

### Yapılan düzeltmeler
- `BankaHareketleriService.getPaged(...)` ve `KasaHareketleriService.getPaged(...)` çağrıları seçili `tesisId` ile çalışacak hale getirildi.
- `StokHareketleriService` listeleme ile birlikte stok bakiye ve stok kart özet çağrılarında da `tesisId` destekleyecek şekilde güncellendi.
- `CariHareketlerService` listeleme çağrılarında `tesisId` query paramı eklendi.
- `TahsilatOdemeBelgeleriService` listeleme ve günlük özet çağrıları seçili tesis ile çalışacak hale getirildi.

### Backend durumu
- Servis filtrelerinin bir kısmı backend controller tarafında da eksik olduğu için ilgili endpoint'ler küçük `tesisId` filter desteği ile güncellendi.
- Migration gerekmedi.
- Backend access scope güvenliği korunmaya devam ediyor.

### Kalan durum
- Bu alt faz sonunda hedeflenen operasyon ekranlarında listeleme ve özet akışları seçili çalışma tesisi ile uyumlu hale getirildi.
- Seçenek listesi ve context bar davranışı Faz 69C'deki haliyle korunuyor.

## Faz 69D — Regresyon Taraması ve Temizlik

Bu fazda `frontend/src/app/pages/muhasebe` ağacı tarandı ve kalan eski tesis pattern'leri temizlendi veya doğrulandı.

### Taranan anahtar kelimeler
- `tesisSecenekleri`
- `loadTesisler`
- `selectedTesisId`
- `selectedTesis`
- `filter.tesisId`
- `model.tesisId`
- `tesisId:`
- `tesisId =`
- `Tesis`
- `Tesis Seç`
- `Tesis seçiniz`
- `p-select`
- `MuhasebeTesisContextService`
- `MuhasebeTesisSecimDialogComponent`
- `MuhasebeTesisContextBarComponent`

### Taranan ekranlar
- Satış Belgeleri
- Muhasebe Fişleri
- Fiş Oluştur
- Yevmiye Defteri
- Muavin Defter
- Hızlı Mizan
- Dashboard
- KDV Hareket Raporu
- KDV Özet Raporu
- KDV Beyanname Hazırlık Kontrol
- Dönem Kapanış Kontrol
- Dönemler
- Cari Kartlar
- Cari Hareketler
- Hesaplar
- Kasa/Banka Hesapları
- Kasa Hareketleri
- Banka Hareketleri
- Depolar
- Stok Hareketleri
- Taşınır Kartları
- Taşınır Fiş Taslağı
- Tahsilat/Ödeme Belgeleri
- Paket Türleri
- KDV İstisna Tanımları
- Taşınır Kodları

### Temizlenen eski pattern'ler
- Kullanılmayan `getTesisler()` helper metotları kaldırıldı.
- `MuhasebeDönemler` ekranının listeleme çağrısı seçili tesisle server-side çalışacak hale getirildi; tüm kayıtları çekip client-side tesis filtresi uygulayan son net liste örneği kapatıldı.
- Eski tesis dropdown pattern'leri için yeni bir UI alanı eklenmedi.

### Global ekranlar ve istisnalar
- `Paket Türleri`, `KDV İstisna Tanımları` ve `Taşınır Kodları` global tanım ekranlarıdır.
- Bu ekranlarda bağımsız tesis dropdown'u yoktur ve tesis context zorunlu değildir.
- `MuhasebeTesisContextService` bu ekranlarda kullanılmaz; bu bilinçli bir istisnadır.

### Yardımcı / dialog istisnaları
- `MuhasebeTesisSecimDialogComponent` ve `MuhasebeTesisContextBarComponent` helper bileşenlerdir.
- `getTesisler()` benzeri eski helper servis çağrıları kaldırıldı; tesis seçimi artık context servisinden yönetiliyor.

### Servis çağrısı doğrulamaları
- `Banka Hareketleri`, `Kasa Hareketleri`, `Cari Hareketler`, `Stok Hareketleri` ve `Tahsilat/Ödeme Belgeleri` listeleme/özet çağrılarında `tesisId` taşınıyor.
- `Stok Hareketleri` özet uçları depo seçilmeden de seçili tesisle sınırlı çalışıyor.
- `SatisBelgesi`, `MuhasebeFis`, `KDV` ve `DonemKapanis` akışlarındaki filtre DTO'ları seçili tesis ile uyumlu kaldı.

### Backend doğrulamaları
- `BankaHareketleri`, `KasaHareketleri`, `CariHareketler`, `StokHareketleri`, `TahsilatOdemeBelgeleri` controller'ları tesis filtresi alıyor ve uyguluyor.
- `SatisBelgeleri`, `MuhasebeFisleri`, `KDV` ve `DonemKapanis` controller/service akışları yeniden kontrol edildi.
- `MuhasebeDonemleri` controller'ı tesis filtresi almaya başladı; bu sayede dönem listesi artık seçili tesisle uyumlu geliyor.
- Backend access scope güvenliği korunuyor.

### Kalan bilinen sınırlamalar
- Bazı yardımcı lookup servisleri `tesisId` parametresi almıyor ve seçenek listeleri frontend'de seçili tesisle daraltılıyor.
- Bu durum özellikle `Cari Hareketler`, `Tahsilat/Ödeme Belgeleri`, `Banka/Kasa Hareketleri` ve `Stok Hareketleri` içindeki yardımcı seçim listelerinde bilinçli olarak korunuyor.
- Tesis context backend yetkilendirme yerine geçmez.

### Son durum
- Muhasebe modülünde tesis değiştirme merkezi context bar üzerinden yapılır.
- Zorunlu tesis seçimi dialog ile sağlanır.
- Bağımsız tesis dropdown'ları kaldırılmıştır.

---

## Faz 69E — Global Tanım Ekranları ve Yetkili Tesis Doğrulaması

Bu fazda global muhasebe tanım ekranlarında seçili çalışma tesisinin üst bilgi olarak gösterilmesi sağlandı ve `MuhasebeTesisContextService` için kullanılan tesis listesinin yetkili/aktif tesislerden geldiği doğrulandı.

### Global ekranlar
- Paket Türleri
- KDV İstisna Tanımları
- Taşınır Kodları
- Muhasebe Hesap Planı

### Global ekran davranışı
- Bu ekranlarda tesisId filtre veya kayıt parametresi olarak kullanılmaz.
- Bağımsız tesis dropdown'u yoktur.
- Üstte yalnızca `MuhasebeTesisContextBarComponent` gösterilir.
- Çalışma tesisini değiştirme akışı context bar üzerindeki **Değiştir** aksiyonuyla yapılır.
- Bu ekranlar tesis seçilmeden de çalışmaya devam edebilir.

### Tesis listesi ve doğrulama
- `MuhasebeTesisContextService`, mevcut `RezervasyonController.GetTesisler()` endpoint'ini kullanır.
- Bu endpoint `RezervasyonService.GetErisilebilirTesislerAsync()` üzerinden çalışır.
- Liste yalnızca aktif tesisleri döndürür.
- `IUserAccessScopeService` ile scope uygulandığı için scoped kullanıcılar sadece yetkili oldukları tesisleri görür.
- localStorage'daki tesis seçimi, initialize sırasında yüklenen yetkili listeyle doğrulanır.
- `selectTesis(...)` çağrısında listedeki tesisler dışında bir seçim reddedilir.

### Güvenlik notu
- Frontend localStorage güvenlik sınırı değildir.
- Backend access scope güvenliği nihai yetki kontrolüdür.
- Yetkisiz bir tesis ID'si manuel olarak yazılsa bile backend tarafı erişimi engeller.

### Kalan notlar
- `Hesaplar` ekranı tesis bazlıdır; bu fazın kapsamı dışındadır.
- Paket Türleri, KDV İstisna Tanımları, Taşınır Kodları ve Muhasebe Hesap Planı global tanımlar olarak bırakılmıştır.

### Son durum
- Muhasebe modülünde tesis context bar tüm ilgili ekranlarda görünür.
- Global tanım ekranlarında tesis bilgisi üst bilgi olarak yer alır.
- Tesis listesi yetkili/aktif kayıtlarla sınırlıdır.

---

## Entegrasyon Rehberi (Kalan Ekranlar İçin)

Aşağıdaki pattern tüm muhasebe ekranları için geçerlidir. Her ekran için 3 değişiklik yapılması yeterlidir:

### TypeScript Değişiklikleri

```typescript
// 1. Import ekle
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';

// 2. Component imports'a ekle
@Component({
    imports: [
        // ... mevcut imports ...
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent,
    ],
})

// 3. Servisi inject et
export class XxxComponent implements OnInit {
    readonly tesisContext = inject(MuhasebeTesisContextService);
    
    // 4. ngOnInit'i güncelle
    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                // Eski loadTesisler() çağrısı yerine:
                const tesis = this.tesisContext.seciliTesis();
                if (tesis) {
                    this.filter.tesisId = tesis.id; // veya this.selectedTesisId
                }
                this.loadData(); // varsa
            },
            error: (err) => this.showError(err)
        });
    }
    
    // 5. Eski loadTesisler() metodunu KALDIR
    // 6. tesisSecenekleri alanını KALDIR (veya context.tesisSecenekleri() kullan)
}
```

### HTML Değişiklikleri

```html
<!-- 1. En üste ekle (p-toast'tan hemen sonra) -->
<app-muhasebe-tesis-secim-dialog />
<app-muhasebe-tesis-context-bar />

<!-- 2. Eski tesis dropdown'unu KALDIR -->
<!-- ÖNCE: -->
<div class="col-span-12 md:col-span-6 lg:col-span-3">
  <label>Tesis</label>
  <p-select [options]="tesisSecenekleri" [(ngModel)]="filter.tesisId" ... />
</div>

<!-- SONRA: tamamen sil (context bar üstlenir) -->

<!-- 3. Dialog/Form içindeki tesis dropdown'larını context'e bağla -->
<p-select
  [options]="tesisContext.tesisSecenekleri()"
  [(ngModel)]="model.tesisId"
  ... />
```

---

## Faz 69B / 69C Sonrası Durum

### Faz 69B ile tamamlanan CRUD / operasyon ekranları

Bu fazda aşağıdaki ekranlar merkezi çalışma tesisi altyapısına bağlandı:

### Tamamlanan rapor ekranları
| # | Ekran | Durum |
|---|-------|-------|
| 1 | **Yevmiye Defteri** | ✅ Tamamlandı (Faz 69A) |
| 2 | **Muavin Defter** | ✅ Tamamlandı (Faz 69A) |
| 3 | **Hızlı Mizan** | ✅ Tamamlandı (Faz 69A) |
| 4 | **Dashboard** | ✅ Tamamlandı (Faz 69A) |
| 5 | **KDV Hareket Raporu** | ✅ Tamamlandı (Faz 69A) |
| 6 | **KDV Özet Raporu** | ✅ Tamamlandı (Faz 69A) |
| 7 | **KDV Beyanname Kontrol** | ✅ Tamamlandı (Faz 69A) |
| 8 | **Dönem Kapanış Kontrol** | ✅ Tamamlandı (Faz 69A) |

### Faz 69B ile tamamlanan CRUD / operasyon ekranları
| # | Ekran | Dosya | Tesis Kullanımı |
|---|-------|-------|----------------|
| 1 | **Fiş Oluştur** | `fis-olustur/muhasebe-fis-olustur.component.*` | `tesisId` artık context'ten alınıyor |
| 2 | **Dönemler** | `donemler/muhasebe-donemler.component.*` | `filter.tesisId` ve `model.tesisId` context'ten besleniyor |
| 3 | **Cari Kartlar** | `cari-kartlar/cari-kartlar.*` | `model.tesisId` context'ten geliyor |
| 4 | **Hesaplar** | `hesaplar/hesaplar.*` | `model.tesisId` context'ten geliyor |
| 5 | **Kasa/Banka Hesapları** | `kasa-banka-hesaplari/kasa-banka-hesaplari.*` | `model.tesisId` context'ten geliyor |
| 6 | **Depolar** | `depolar/depolar.*` | `model.tesisId` context'ten geliyor |
| 7 | **Taşınır Kartları** | `tasinir-kartlari/tasinir-kartlari.*` | `model.tesisId` context'ten geliyor |
| 8 | **Taşınır Fiş Taslağı** | `tasinir-fis-taslagi/...` | `request.tesisId` context'ten geliyor |

### Kalan muhasebe ekranları
- Faz 69A, 69B ve 69C kapsamındaki hedef ekranlar tamamlandı.
- Bu seri dışında kalan muhasebe ekranları varsa bunlar ileriki fazlara bırakıldı.

---

## localStorage Anahtarı

```
stys_muhasebe_calisma_tesisi → {"id": 1, "ad": "Ana Tesis"}
```

- Sayfa yenilenince korunur.
- Tarayıcı sekmesi kapatılıp açılınca korunur.
- Geçersiz/bozuk veri otomatik temizlenir.
- Silinmiş bir tesis seçili kalmışsa `initialize()` sırasında temizlenir.

---

## Backend Değişiklikleri

**Yok.** Bu faz tamamen frontend tarafındadır. Backend'deki yetki kontrolü (`TesisMuhasebeci` ilişkisi vb.) aynen korunur. Context sadece kullanıcının hangi tesiste çalışmak istediğini belirtir, yetkilendirme backend tarafından yapılmaya devam eder.

---

## Özel Durum: Satış Belgeleri

Satış belgelerinde iki tesisId kaynağı olabilir:
1. **Manuel belgeler:** Context'ten alınan çalışma tesisi kullanılır.
2. **Kaynak tabanlı belgeler** (Rezervasyon, Restoran, vb.): Kaynak modülden gelen `tesisId` korunur. `openEditDialog` sırasında mevcut `tesisId` üzerine yazılmaz.

Bu davranış mevcut implementasyonda `loadBelgeler()` metodunun `tesisContext.seciliTesis()` kontrolü ile sağlanır — eğer `filter.tesisId` zaten doluysa (kaynak tabanlı), context değeri onu ezmez.

---

## Güvenlik Notu

Context servisi **yetkilendirme yapmaz**. Kullanıcının seçtiği tesise erişim yetkisi olup olmadığı her backend çağrısında ayrıca kontrol edilir. Context sadece UI kolaylığı sağlar.

---

## Versiyon

- **Faz:** 69
- **Oluşturma Tarihi:** 2026-05-25
- **Güncelleme:** Faz 69B CRUD / tanım ekranları, Faz 69C operasyon ekranları, Faz 69C-A servis filtre düzeltmeleri, Faz 69D regresyon temizliği ve Faz 69E global tanım/doğrulama adımı eklendi.
- **Durum:** Tamamlandı sayılır — core altyapı, Satış Belgeleri, Fişler, rapor ekranları ve Faz 69B / 69C / 69C-A / 69D / 69E kapsamındaki ekranlar entegre edildi. Kalan ekranlar global tanım ekranları veya bilinçli yardımcı istisnalar olarak not edildi.
