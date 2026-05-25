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

## Kalan Entegrasyon Listesi

Aşağıdaki ekranlar yukarıdaki pattern ile güncellenmelidir:

| # | Ekran | Dosya | Tesis Kullanımı |
|---|-------|-------|----------------|
| 1 | **Fiş Oluştur** | `fis-olustur/muhasebe-fis-olustur.component.*` | `tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 2 | **Yevmiye Defteri** | `yevmiye-defteri/yevmiye-defteri.component.*` | `filter.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 3 | **Hızlı Mizan** | `hizli-mizan/hizli-mizan.component.*` | `filter.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 4 | **Muavin Defter** | `muavin-defter/muavin-defter.component.*` | `filter.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 5 | **Dashboard** | `dashboard/muhasebe-dashboard.component.*` | `filter.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 6 | **KDV Hareket Raporu** | `kdv-hareket-raporu/kdv-hareket-raporu.component.*` | `filter.tesisId`, `tesisSecenekleri` |
| 7 | **KDV Özet Raporu** | `kdv-ozet-raporu/kdv-ozet-raporu.component.*` | `filter.tesisId`, `tesisSecenekleri` |
| 8 | **KDV Beyanname Kontrol** | `kdv-beyanname-hazirlik-kontrol/...` | `filter.tesisId`, `tesisSecenekleri` |
| 9 | **Dönemler** | `donemler/muhasebe-donemler.component.*` | `filter.tesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 10 | **Dönem Kapanış Kontrol** | `donem-kapanis-kontrol/...` | `filter.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 11 | **Cari Kartlar** | `cari-kartlar/cari-kartlar.*` | `selectedTesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 12 | **Hesaplar** | `hesaplar/hesaplar.*` | `selectedTesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 13 | **Kasa/Banka Hesapları** | `kasa-banka-hesaplari/kasa-banka-hesaplari.*` | `selectedTesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 14 | **Depolar** | `depolar/depolar.*` | `selectedTesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 15 | **Taşınır Kartları** | `tasinir-kartlari/tasinir-kartlari.*` | `selectedTesisId`, `model.tesisId`, `tesisSecenekleri`, `loadTesisler()` |
| 16 | **Taşınır Fiş Taslağı** | `tasinir-fis-taslagi/...` | `filter.tesisId`, `tesisSecenekleri` |

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
- **Durum:** Kısmi — core altyapı tamamlandı, Satış Belgeleri ve Fişler entegre edildi, diğer ekranlar entegrasyon rehberine göre yapılacak.
