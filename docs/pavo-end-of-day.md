# PAVO Central End of Day (Gün Sonu) + Slip Yönetimi

Merkezi STYS POS Yönetimi ekranından bir PAVO POS cihazı için gün sonu (PerformEOD)
başlatılması, sonucun merkezde saklanması ve gün sonu sliplerinin güvenli şekilde
yönetilmesi.

## Akış

```
STYS Backend → PosGunSonuIslemi=Pending → AgentCommand/PavoPerformEOD → STYS Agent
  → local PAVO → POST /PerformEOD → response validation → PosGunSonuIslemi + PosGunSonuSlipi
  → POS Yönetimi → Gün Sonu Geçmişi → Slipler → Slip Görüntüle
```

## PerformEOD request

```json
{
  "PerformEOD": {
    "AdditionalInfo": {
      "print": false,
      "receiptImage": true,
      "useSummary": true,
      "receiptWidth": "58mm",
      "printData": { "receiptJsonEnabled": true, "receiptTextEnabled": true, "receiptTextWidth": "40" }
    }
  },
  "TransactionHandle": { "SerialNumber": "...", "TransactionDate": "...", "TransactionSequence": 1, "Fingerprint": "..." }
}
```

## Başarı / hata / belirsizlik

- **Successful**: HTTP 2xx + `HasAbondon == false` + `HasError == false` + response
  `TransactionHandle.SerialNumber` request serial ile eşleşir.
- **Failed**: response alındı ama `HasError`/`HasAbondon` ya da device serial mismatch.
- **Unknown**: cihazın request'i almış olabileceği ambiguous transport hataları (response
  timeout, body read failure). `CONNECTION_REFUSED` / `NETWORK_UNREACHABLE` / `CONNECT_TIMEOUT`
  cihaza hiç ulaşmadığı için `Failed` sayılır.

## Retry policy

- PerformEOD **state-changing**'dir; otomatik retry yapılmaz (`MaxRetryCount=0`).
- Unknown sonucundan sonra otomatik yeni PerformEOD üretilmez.
- Aynı command duplicate delivery gelirse agent `FileAgentCommandExecutionStore` fiziksel
  işlemin ikinci kez çalışmasını engeller.

## Sequence

- Outgoing sequence tek source of truth: `IPavoCommandSequenceReservationService`.
- `ReserveAsync` kullanılır (pairing'in `ReserveForPairingAsync`'i değil).
- HTTP response gerçekten alındıysa `AdvanceAsync`; pre-response connection failure'da advance edilmez.
- Response `TransactionSequence` outgoing sequence olarak kullanılmaz.

## Entity / storage

- `PosGunSonuIslemi` (Pending/Successful/Failed/Unknown) — Base64/path/hash TUTMAZ.
- `PosGunSonuSlipi` (`PosGunSonuSlipTipi.EodImage=1`) — filesystem metadata + göreli path.
- Storage: `<root>/<KurumId>/<PosCihaziId>/<PosGunSonuIslemiId>/eod-<SHA256>.png` (wwwroot dışı).
- PNG signature + max 10 MB + SHA-256 + atomic write (temp + move).
- Idempotency: `(PosGunSonuIslemiId, SlipTipi, Sha256)` unique; aynı SHA → no-op; farklı SHA → immutable path + eski dosya DB commit sonrası cleanup.

## Sanitization

- `data.eodImage` (Base64) merkezde persist edilmez.
- `cardNo` (case-insensitive) recursive olarak `eodData`/`eodJson`/`eodText` içinden kaldırılır.
- `AgentCommand.ResultPayload` sanitize edilir.

## Slip erişim API'leri

- `POST /ui/pos/cihazlar/{id}/eod` — gün sonu başlat.
- `GET  /ui/pos/cihazlar/{id}/eod` — geçmiş (SlipSayisi ile).
- `GET  /ui/pos/eod/{eodId}` — detay (+slipler).
- `GET  /ui/pos/eod/{eodId}/receipts` — slip listesi.
- `GET  /ui/pos/eod/{eodId}/receipts/{receiptId}/content` — PNG stream.

Permission: `PosYonetimi.View` / `PosYonetimi.Manage`. Kurum + Tesis scope doğrulanır;
`StoragePath` hiçbir DTO'ya çıkmaz.

## Gün sonu başlatma guard'ları

- device/tenant/tesis/agent/PAVO pairing validation
- aynı cihazda aktif PavoPerformEOD yok (sp_getapplock `pavo-eod:<cihazId>`)
- kesinleşmemiş ödeme (Pending/SentToAgent/Processing/Unknown) yok
- aktif AgentApplyUpgrade yok

## Komut süresi dolması

PavoPerformEOD command expire olursa `PosGunSonuIslemi` `Pending`'te sonsuz kalmaz; `Unknown`
olarak sonuçlandırılır (otomatik yeni EOD üretilmez).
