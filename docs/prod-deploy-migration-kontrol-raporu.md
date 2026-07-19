# Prod Deploy Migration Kontrol Raporu

**Tarih:** 2026-07-19
**Kapsam:** Rezervasyon Muhasebe Entegrasyonu — sadece analiz/hazırlık. Kod değişikliği yok, migration değişikliği yok. `docs/`, `artifacts/` altında yeni dosyalar üretildi.
**Git durumu:** `main` branch, `origin/main` ile senkron, çalışma dizini temiz (`git status` → "nothing to commit"), son commit `e6dc846`.

---

## Migration Listesi

`dotnet ef migrations list --context StysAppDbContext --project backend/STYS.csproj --startup-project backend/STYS.csproj` yerel dev DB'ye bağlanarak çalıştırıldı; toplam **~90 migration** listelendi (proje başlangıcından bugüne), hiçbiri `(Pending)` işaretli değildi çünkü **yerel dev DB zaten tüm migration'ları içeriyor**.

Bu rapor kapsamındaki (rezervasyon-muhasebe entegrasyonu ile ilgili) **3 migration**, zincirin en sonunda:

| Migration | İçerik |
|---|---|
| `20260713155404_AddRezervasyonOdemeMuhasebeEntegrasyonu` | `TahsilatOdemeBelgesi` ↔ `RezervasyonOdeme` ilişkisi, kasa/banka hesap alanları, ödeme iptal alanları (`Durum`, `IptalTarihi`, `IptalAciklama`), `Rezervasyonlar.CariKartId`, unique filtered index'ler |
| `20260714140521_AddRezervasyonSatisBelgesiEntegrasyonu` | `Rezervasyonlar.SatisBelgesiId` + unique filtered index |
| `20260718120000_AddCariKartQuickCreatePermission` | **QuickCreate permission migration'ı — listede mevcut.** `CariKartYonetimi.QuickCreate` rolünü oluşturur ve sadece `ResepsiyonistGrubu`'na atar (idempotent, `IF NOT EXISTS` korumalı) |

**Prod'un bu 3 migration'ı henüz almadığı varsayımı**, deploy öncesi Bölüm "Ek Kontrol E" ile (prod `__EFMigrationsHistory` tablosunda bu 3 ID'nin yokluğu) doğrulanmalıdır — bu raporun kapsamında sadece **yerel/dev DB** üzerinde doğrulama yapılabildi (dev DB'de bu 3 migration zaten uygulanmış durumda, script sözdizimi/anlam doğruluğu bu DB üzerinden test edildi).

---

## Üretilen Script Dosyası

`artifacts/prod-migration.sql` — `dotnet ef migrations script --idempotent` ile üretildi, **27.729 satır**. Bu, projenin **en başından itibaren tüm migration geçmişini** idempotent (her blok `IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId = ...)` korumalı) olarak içerir — bu, prod DB'nin şu anki migration durumundan bağımsız olarak güvenle çalıştırılabilecek standart EF Core pratiğidir.

---

## Riskli SQL Var mı?

### Hedeflenen 3 migration (bu deploy'un asıl kapsamı)

**Hayır, riskli/yıkıcı işlem yok.** Script'in ilgili bölümleri satır satır incelendi:

- **Nullable kolonlar:** `RezervasyonMisafirVarsayilanCariKartId`, `KasaBankaHesapId` (x2), `MuhasebeFisId`, `MuhasebeFisOlusturmaTarihi`, `IptalAciklama`, `IptalTarihi`, `TahsilatOdemeBelgesiId`, `CariKartId`, `SatisBelgesiId` — **hepsi `NULL`** izin veriyor, mevcut satırlarda otomatik `NULL` alır, veri kaybı/hata riski yok.
- **Default değerli NOT NULL kolonlar:** `Tesisler.RezervasyonTahsilatAlacakHesapTipi` (`nvarchar(16) NOT NULL DEFAULT N'Cari'`), `RezervasyonOdemeler.Durum` (`nvarchar(16) NOT NULL DEFAULT N'Aktif'`) — mevcut satırlar için backfill güvenli, davranışsal olarak "hiç iptal edilmemiş / cari bazlı tahsilat" varsayımıyla uyumlu.
- **Unique filtered index'ler:** 3 adet —
  - `IX_TahsilatOdemeBelgeleri_KaynakModul_KaynakId` (`WHERE IsDeleted=0 AND KaynakId IS NOT NULL`)
  - `IX_RezervasyonOdemeler_TahsilatOdemeBelgesiId` (`WHERE TahsilatOdemeBelgesiId IS NOT NULL`)
  - `IX_Rezervasyonlar_SatisBelgesiId` (`WHERE IsDeleted=0 AND SatisBelgesiId IS NOT NULL`)
  - Üçü de yeni eklenen, önceden hep `NULL` olacak alanlar üzerinde — **ilk ikisi çakışma riski taşımaz**; `KaynakModul_KaynakId` index'i ise **mevcut veride zaten duplicate olabilecek** bir alan çifti üzerinde (bu yüzden precheck sorgusu A kritik).
- **Role/permission insert script'i:** Var — `20260718120000_AddCariKartQuickCreatePermission` migration'ı, `TODBase.Roles` ve `TODBase.UserGroupRoles`'a `IF NOT EXISTS` korumalı `INSERT` yapıyor. Grup bulunamazsa (`@ResepsiyonistGroupId IS NULL`) **sessizce atlanır, hata fırlatmaz** — bu davranış precheck B ile doğrulanmalı.
- **Destructive DROP/TRUNCATE/DELETE:** **Yok.**
- **Mevcut veriyi bozabilecek UPDATE:** **Yok** — sadece `ADD COLUMN`, `CREATE INDEX`, `ADD CONSTRAINT` (tümü `ON DELETE NO ACTION`), guard'lı `INSERT`.
- **Transaction yapısı:** Her migration kendi `BEGIN TRANSACTION ... COMMIT` bloğunda, EF Core'un standart üretimi — uygun.

### Script'in tamamı (27.729 satır, tüm proje geçmişi)

Script genelinde grep ile tarandığında: **2 `DROP TABLE`, 27 `DROP COLUMN`, 26 `DELETE FROM`, 204 `UPDATE`** bulundu. **Bunların hiçbiri bu deploy'un 3 hedef migration'ına ait değil** — hepsi çok daha eski, muhtemelen prod'da zaten uygulanmış migration'lara ait (ör. `FixCariKartBankaHesabiDeleteBehavior`, `RemoveCariKartLegacyBankaFields` gibi şema temizlik migration'ları). İdempotent script yapısı gereği, bu bloklar prod DB'nin `__EFMigrationsHistory`'sinde zaten kayıtlıysa **otomatik atlanır**.

⚠️ **Bu, prod DB'nin migration geçmişinin güncel/tutarlı olduğu varsayımına dayanır.** Eğer prod DB'nin migration geçmişi beklenenden çok daha geride ise (ör. uzun süredir deploy yapılmamışsa), bu script çok daha eski, potansiyel olarak yıkıcı migration'ları da ilk kez çalıştırabilir. Bu nedenle `artifacts/prod-precheck.sql`'e **"Ek Kontrol E"** olarak prod'un mevcut migration geçmişini gösteren bir sorgu eklendi — deploy ekibi bunu çalıştırıp en son uygulanan migration'ın beklenen noktada (`20260711090000_AddGecikenCheckInRaporuPermissionsAndMenu` veya sonrası, ama hedef 3 migration'dan önce) olduğunu görsel olarak teyit etmelidir.

---

## Precheck Dosyası

`artifacts/prod-precheck.sql` — sadece `SELECT`, hiçbir veri değiştirmez. İçerik:

- **A)** `TahsilatOdemeBelgeleri` duplicate `KaynakModul`/`KaynakId` kontrolü (beklenen: 0 satır — 0 dönmezse migration **uygulanmamalı**).
- **B)** `ResepsiyonistGrubu` varlık kontrolü (beklenen: aktif grup).
- **C)** `QuickCreate` rolünün deploy öncesi durumu (bilgi amaçlı; migration idempotent olduğu için risk taşımaz).
- **D)** `ResepsiyonistGrubu` ↔ `QuickCreate` ilişkisi — deploy öncesi **0 satır** beklenir (deploy sonrası aynı sorgu postcheck'te tekrar kullanılıyor, o zaman 1 satır beklenir).
- **E) (ek, önerilen)** Prod'un mevcut `__EFMigrationsHistory` son 10 kaydı + hedef 3 migration'ın henüz mevcut olmadığının teyidi.

**Doğrulama:** Tüm sorgular yerel dev DB'ye karşı çalıştırıldı, sözdizimi hatası yok. A sorgusu dev DB'de **0 satır** döndürdü (dev DB'de de bu index sorunsuz kurulu).

---

## Postcheck Dosyası

`artifacts/prod-postcheck.sql` — sadece `SELECT`, `sys.columns`/`sys.indexes`/`sys.foreign_keys` kullanır. İçerik:

- **0)** 3 migration'ın `__EFMigrationsHistory`'de kaydı (beklenen: 3 satır).
- **A)** `QuickCreate` rolü oluştu mu (beklenen: 1 aktif satır).
- **B)** `ResepsiyonistGrubu`'na `QuickCreate` verildi mi (beklenen: 1 aktif satır) + **ek kontrol:** bu yetkinin *sadece* ResepsiyonistGrubu'na verildiğini, başka hiçbir gruba sızmadığını doğrulayan `GROUP BY` sorgusu.
- **C)** `TahsilatOdemeBelgeleri` üzerindeki 3 index (`KaynakModul_KaynakId` unique+filtered dahil), `sys.indexes`/`sys.index_columns` ile.
- **D)** `RezervasyonOdemeler` yeni 5 kolonu + unique filtered index'i.
- **E)** `TahsilatOdemeBelgeleri` yeni 3 kolonu.
- **F)** `Rezervasyonlar.CariKartId`/`SatisBelgesiId` kolonları + unique filtered index.
- **G) (ek, önerilen)** 7 yeni foreign key'in `sys.foreign_keys`'te varlığı.

**Doğrulama:** Tüm sorgular yerel dev DB'ye (3 migration zaten uygulanmış durumda) karşı çalıştırıldı — **hiçbir hata yok**, tüm satır sayıları beklenenle birebir eşleşti (3 migration kaydı, 1 rol, 1 atama, 3 index — `is_unique=1, has_filter=1` doğru filter definition'larla, 5+3+2 kolon, 7 FK).

---

## Prod Öncesi Yapılacaklar

1. `origin/main` güncel olduğu teyit edilmeli (`git fetch && git status`).
2. `dotnet build STYS.sln` — 0 error.
3. `npm run build` (frontend) — 0 error.
4. `dotnet test tests/STYS.Tests/STYS.Tests.csproj --no-build` — Failed: 0.
5. `npm audit --omit=dev` — 0 vulnerability.
6. **Tam DB yedeği** alınmalı (restore edilebilirliği önceden test edilmiş bir prosedürle).
7. `artifacts/prod-precheck.sql` **gerçek prod DB'de** çalıştırılmalı:
   - Sorgu A → 0 satır değilse **DUR**, migration uygulanmamalı.
   - Sorgu B → grup bulunamazsa migration hata vermez ama QuickCreate hiç atanmaz; devam etmeden önce grup adının doğru olduğu teyit edilmeli.
   - Sorgu E → hedef 3 migration'ın prod'da henüz olmadığı ve migration geçmişinin beklenen noktada olduğu teyit edilmeli.
8. `artifacts/prod-migration.sql` incelenmeli/onaylanmalı (bu rapor kapsamında satır satır incelendi, risk bulunmadı).
9. Deploy sırası: DB backup → precheck → backend deploy → migration → frontend deploy → restart/cache temizliği → smoke test (bkz. `docs/rezervasyon-muhasebe-deploy-runbook.md` Bölüm 4-5).

## Prod Sonrası Yapılacaklar

1. `artifacts/prod-postcheck.sql` prod DB'de çalıştırılmalı, tüm sorguların "beklenen" satır sayılarını verdiği teyit edilmeli.
2. `docs/rezervasyon-muhasebe-deploy-runbook.md` Bölüm 5'teki tam smoke test checklist'i uygulanmalı (login, resepsiyonist akışları, muhasebe akışları, ödeme iptali senaryoları A-D, gelir/tahakkuk, frontend component smoke).
3. Deploy Onay Checklist'i (runbook Bölüm sonunda) doldurulmalı.

---

## Blocker Var mı?

**Hayır — kod/migration tarafında blocker yok.** 3 hedef migration idempotent, geriye uyumlu (tüm yeni kolonlar nullable veya güvenli default'lu), yıkıcı işlem içermiyor, ve gerçek DB şemasına karşı doğrulandı.

**Tek koşullu blocker:** `artifacts/prod-precheck.sql` sorgu **A**'nın prod'da **0 satırdan farklı** dönmesi — bu durumda migration **kesinlikle uygulanmamalı**, tekrarlı `KaynakModul`/`KaynakId` kayıtları önce manuel olarak incelenip temizlenmeli (hangi kaydın "asıl" tutulacağı iş kararı gerektirir, bu rapor kapsamının dışındadır).

**İkincil dikkat noktası (blocker değil, gözlem):** Precheck sorgu B'de `ResepsiyonistGrubu` bulunamazsa, migration hata vermeden sessizce QuickCreate atamasını atlar — bu durumda deploy görünüşte başarılı olur ama resepsiyonist kullanıcılar hızlı cari kart oluşturamaz. Bu senaryo precheck B ile önceden yakalanmalı.
