# E-Belge Kurum Süreç Analizi Şablonu

Faz 2B.10 — bu belge, her bir kurum için e-belge (e-fatura/e-arşiv) sürecinin bugün nasıl
yürütüldüğünü kaydetmek için kullanılan bir **veri toplama şablonudur**. Bu belgenin amacı
bilgi toplamaktır — hangi entegrasyon yönteminin (`EBelgeEntegrasyonYontemi`) seçileceğine dair
**hiçbir hukuki/mali karar burada verilmez**; STYS ekibi bu formu doldurduktan sonra kurumla
(ve gerekiyorsa mali müşavir/entegratör ile) birlikte `KurumEBelgePolitikasi` kaydını
(`docs/e-belge-kurum-politikasi-ve-yonlendirme-stratejisi.md`'de tanımlanan API üzerinden) ayrıca
oluşturur.

Her kurum için bu şablonun bir kopyası doldurulur (ör. `docs/kurum-surec-analizleri/<kurum-kodu>.md`).

---

## 1. Kurum Kimliği

| Alan | Değer |
|---|---|
| Kurum adı | |
| Kurum kodu (STYS `Kurum.Kod`) | |
| VKN (STYS'te zaten kayıtlı — burada yalnız çapraz kontrol amaçlı) | |
| Vergi dairesi | |

## 2. Satışı Yapan Mali/Hukuki Birim

| Soru | Yanıt |
|---|---|
| Satışı fiilen hangi hukuki/mali birim (şirket/işletme) yapıyor? | |
| Bu birim, yukarıdaki Kurum kaydıyla aynı tüzel kişilik mi? | |
| Kullanılan VKN, Kurum kaydındaki VKN ile aynı mı? (Farklıysa açıklayın — bu fazda ayrı bir VKN alanı EKLENMEZ, yalnız bilgi amaçlıdır) | |

## 3. Mevcut Belge Türü ve Yöntemi

| Soru | Yanıt |
|---|---|
| Bugün hangi belge türü düzenleniyor? (e-Fatura / e-Arşiv Fatura / kağıt fatura / hiçbiri) | |
| Bugünkü e-Fatura yöntemi nedir? (GİB Portal / özel entegratör / doğrudan entegrasyon / uygulanmıyor) | |
| GİB Portal kullanılıyorsa: kullanıcı adı/portal erişimi kimde? (yalnız sorumlu KİŞİ/BİRİM adı — kimlik bilgisi/parola BURAYA YAZILMAZ) | |
| Özel entegratör kullanılıyorsa: entegratör firma adı ve mevcut entegrasyon türü (API/dosya aktarımı/manuel) | |

## 4. Muhasebe Sistemi

| Soru | Yanıt |
|---|---|
| Kurum bugün hangi muhasebe/ERP sistemini kullanıyor? | |
| Satış belgeleri bugün STYS'te mi yoksa bu harici sistemde mi kesiliyor? | |
| STYS ile bu sistem arasında bugün otomatik bir veri aktarımı var mı? | |

## 5. Belge Düzenleme Sorumluluğu

| Soru | Yanıt |
|---|---|
| Belgeyi bugün fiilen kim düzenliyor/kesiyor? (kurum personeli / mali müşavir / dış hizmet sağlayıcı) | |
| Mali mühür/e-imza sürecinden sorumlu taraf kim? (yalnız SORUMLU TARAF adı — sertifika/PIN bilgisi BURAYA YAZILMAZ) | |

## 6. STYS'ten Beklenen Sorumluluk

| Soru | Yanıt |
|---|---|
| STYS'in bu kurum için üstlenmesi beklenen adımlar neler? (yalnız yerel snapshot/UBL üretimi / imzalama da dahil / hiçbiri, harici sistem sorumlu) | |
| STYS dışına (harici muhasebe sistemine) veri aktarımı gerekiyor mu? | |

> Not: Bu fazda (Faz 2B.10) harici muhasebe sistemine otomatik aktarım **uygulanmamaktadır** —
> bu satır yalnız ihtiyacı KAYDETMEK içindir, ileride ayrı bir faz olarak değerlendirilecektir.

## 7. Hacim Tahmini

| Soru | Yanıt |
|---|---|
| Tahmini günlük belge hacmi | |
| Tahmini aylık belge hacmi | |
| Yoğun dönem/sezon var mı? | |

## 8. Test Ortamı

| Soru | Yanıt |
|---|---|
| Kurumun/entegratörün bir GİB test (özel entegratör test) ortamı erişimi var mı? | |
| STYS'in test/pilot sürecine katılım için ayrılmış bir zaman aralığı var mı? | |

## 9. İletişim

| Alan | Değer |
|---|---|
| Teknik iletişim kişisi (ad, birim) | |
| Mali/muhasebe iletişim kişisi (ad, birim) | |

> Kişisel iletişim bilgileri (e-posta/telefon) bu şablona değil, kurumun mevcut kayıt sistemine
> (STYS kişi/iletişim kayıtları) girilmelidir — bu şablon yalnız SÜREÇ analizi içindir.

## 10. Onaylanan STYS Entegrasyon Yöntemi

| Alan | Değer |
|---|---|
| Yukarıdaki analiz sonucunda kararlaştırılan `EBelgeEntegrasyonYontemi` değeri | |
| Karar tarihi | |
| Onaylayan (kurum tarafı) | |
| Onaylayan (STYS tarafı) | |
| Planlanan aktivasyon tarihi (global 2026-09-15 tarihinden ÖNCE OLAMAZ) | |

> Bu satır doldurulduktan SONRA, `KurumEBelgePolitikasi` kaydı yönetim API'si üzerinden
> (`PUT /ui/kurumlar/{kurumId}/e-belge-politikasi`) oluşturulur/güncellenir — bu şablonun
> doldurulması TEK BAŞINA politikayı aktive ETMEZ.
