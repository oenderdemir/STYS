# PAVO Payment Receipts (StartPayment + GetPaymentResult)

PAVO ödeme işlemlerinden dönen müşteri, işyeri ve hata slip görsellerinin hem
`StartPayment` hem de `GetPaymentResult` üzerinden alınması, güvenli şekilde merkezi
STYS'e kaydedilmesi ve POS Yönetimi ekranından görüntülenmesi.

## Akış

```
StartPayment ──────────────► receipt images dönerse ─► persist
     │
     └─ response kayboldu / receipt dönmedi
                 │
                 ▼
        GetPaymentResult(SaleReference) ─► receipt images tekrar istenir ─► persist
```

Her iki endpoint'in response'ları **aynı merkezi receipt persistence pipeline**'ından geçer.

## Sorumluluklar

- **StartPayment**: birincil ödeme + slip alımı. Receipt istek seçenekleri
  (`ReceiptImage`, `CustomerReceiptImageEnabled`, `MerchantReceiptImageEnabled`) backend'de
  `BuildStartCommand` içinde açıkça kurulur; mevcut wire contract/reference parity korunur.
- **GetPaymentResult**: `SaleReference` üzerinden sonucu ve slip'i geri alır (recovery).
  Wire request'i `PaymentResult.SaleReference` + `AdditionalInfo` + `TransactionHandle`
  taşır; `AdditionalInfo` receipt image istek seçeneklerini içerir.
- **Provider sorgulama penceresi**: 48 saat. Bu, provider constraint'i olarak dokümante
  edilir; mevcut reconciliation semantics'i değiştirilmez. 48 saati aşmış bir sorgu için
  ayrı bir operational error (`PAVO_PAYMENT_RESULT_QUERY_WINDOW_EXPIRED`) üretilmez — bu
  receipt geliştirmesini büyütmemek adına yalnızca limit dokümante edilir.

## GetPaymentResult wire contract

```json
{
  "PaymentResult": {
    "SaleReference": "...",
    "AdditionalInfo": {
      "receiptImage": true,
      "customerReceiptImageEnabled": true,
      "merchantReceiptImageEnabled": true,
      "receiptWidth": "58mm",
      "headUnmaskLength": 0,
      "tailUnmaskLength": 4,
      "printData": { "...": false }
    }
  },
  "TransactionHandle": { "SerialNumber": "...", "Fingerprint": "...", "TransactionSequence": 1, "TransactionDate": "..." }
}
```

## Receipt tipleri

| PAVO alanı | Enum | Değer |
|---|---|---|
| `customerReceiptImage` | `Customer` | 1 |
| `merchantReceiptImage` | `Merchant` | 2 |
| `errorReceiptImage` | `Error` | 3 |

## Storage (güvenli / public değil)

- Dosyalar `wwwroot` **dışında**, configurable `PosReceiptStorage:RootPath` altında tutulur:
  `<root>/<KurumId>/<PosOdemeIslemiId>/customer.png|merchant.png|error.png`.
- Yol segmentleri yalnızca trusted server-side ID/enum'dan üretilir; PAVO/frontend'den dosya
  adı alınmaz (path traversal imkânsız).
- `PosReceiptStorage:MaxImageBytes` (varsayılan 5 MB) üzerindeki görseller reddedilir.
- Yazma atomiktir: önce `random.tmp`, sonra flush + atomic move.
- Slipler `Content-Type: image/png` ile, authenticated endpoint üzerinden stream edilir.

## Görsel doğrulama / bütünlük

- Base64: plain (`iVBORw0KGgo...`) ve defensive `data:image/png;base64,...` desteklenir.
- Decode edilen payload PNG signature (`89 50 4E 47 0D 0A 1A 0A`) ile doğrulanır.
- Her receipt için SHA-256 (uppercase hex) hesaplanır; dedup ve idempotency için kullanılır.

## Idempotency / dedup

- Unique invariant: `(PosOdemeIslemiId, Tip)` — her ödeme için tip başına tek mantıksal kayıt.
- Aynı SHA tekrar gelirse **no-op**; farklı SHA gelirse kontrollü in-place replacement
  (kayıt sayısı yine 1 kalır, eski dosya temizlenir).

## Sanitization (merkezi Base64 yok)

Raw PAVO result payload'ı merkezde sanitize edilir:

```
raw ResultPayload → deserialize → receipt extract → validate + store → image fields null → serialize
```

- `AgentCommand.ResultPayload` ve `PosOdemeIslemi.SonSaglayiciYaniti` **hiçbir zaman** raw
  Base64 receipt içermez; `customerReceiptImage` / `merchantReceiptImage` / `errorReceiptImage`
  alanları null'lanır.
- Receipt persistence hatası ödeme business state'ini değiştirmez (best-effort).

## Güvenlik

- **Raw PAN yok**: `cardNo` wire'da parse edilse bile domain/backend/frontend/log'a taşınmaz.
- **Public erişim yok**: dosyalar static file middleware ile servis edilmez; tahmin edilebilir
  URL ile erişilemez.
- **Tenant izolasyonu**: receipt erişimi sırasında `payment.KurumId` doğrulanır; SuperAdmin
  mevcut convention'a göre çalışır.
- **Fiziksel path dışarı sızmaz**: `StoragePath` frontend'e dönmez; content endpoint yalnızca
  stream döner.

## API

- `GET /ui/pos/payments/{paymentId}/receipts` → slip metadata (`PosOdemeSlipDto[]`).
- `GET /ui/pos/payments/{paymentId}/receipts/{receiptId}/content` → PNG stream.
- Permission: `StructurePermissions.PosYonetimi.View`.

## Entegrasyon bileşenleri

- Agent wire: `PavoWireGetPaymentResultRequest` + `PavoWireGetPaymentResultAdditionalInfo`.
- Agent domain: `PavoReceiptRequestOptions`, `PavoPaymentOperationData.ErrorReceiptImage`.
- Backend entity: `PosOdemeSlip` (schema `entegrasyon`, tablo `PosOdemeSlipleri`).
- Backend storage: `IPosReceiptStorage` / `PosReceiptStorage`.
- Backend persistence: `IPosReceiptPersistenceService` / `PosReceiptPersistenceService`.
- Backend retrieval: `IPosReceiptService` / `PosReceiptService`.
- Sanitization: `AgentCommandService.ApplyPavoPaymentResult` + `SanitizeReceiptImages`.
