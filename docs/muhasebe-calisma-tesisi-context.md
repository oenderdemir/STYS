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
- Örnek kalanlar: `banka-hareketleri`, `cari-hareketler`, `kasa-hareketleri`, `stok-hareketleri`, `tahsilat-odeme-belgeleri`.

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

## Faz 69B Sonrası Durum

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
- Bu faz dışında kalan muhasebe operasyon / tanım ekranları ileriki fazlara bırakıldı.
- Örnekler: `banka-hareketleri`, `cari-hareketler`, `kasa-hareketleri`, `stok-hareketleri`, `tahsilat-odeme-belgeleri`.

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
- **Güncelleme:** Faz 69B CRUD / tanım ekranları entegrasyonu eklendi.
- **Durum:** Kısmi — core altyapı, Satış Belgeleri, Fişler, rapor ekranları ve Faz 69B CRUD / tanım ekranları entegre edildi. Kalan diğer muhasebe operasyon ekranları sonraki fazlara bırakıldı.
