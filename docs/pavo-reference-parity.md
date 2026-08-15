# PAVO 509 — Reference Parity Report

**Reference source of truth:** `pavo.rar` → `Pavo509.Client` (verified working against a real PAVO 509
device: pairing + payment).

Files used as the contract authority:

- `Pavo509.Client/PavoApiClient.cs` — runtime semantics (sequence, success criteria, validation)
- `Pavo509.Client/Program.cs` — HTTP client setup, pairing retry loop
- `Pavo509.Client/appsettings.json`, `Models/PavoOptions.cs` — defaults
- `Models/TransactionHandle.cs`, `PairingRequest.cs`, `StartPaymentRequest.cs`,
  `PerformEodRequest.cs`, `DeviceCommandRequest.cs`, `PavoResponse.cs` — wire contract

**STYS wire layer:** `agent/STYS.Agent.Modules.Pavo/PavoWireDtos.cs` mirrors the reference models
1:1. STYS domain concepts live in `STYS.Agent.Contracts/Dtos/PavoDtos.cs` and are joined to the wire
layer by an explicit mapper in `PavoRestClient`. No STYS-specific field ever reaches the wire.

---

## Parity matrix — shared / reference operations

| Contract item | Reference | STYS | Difference |
|---|---|---|---|
| Connection | `http://{ip}:4567`, timeout 180s, `Accept: application/json` | same; HTTPS remains an opt-in STYS extension driven solely by `UseHttps` | NONE |
| Pairing endpoint | `POST /Pairing` | `POST /Pairing` | NONE |
| Pairing request | `{ "TransactionHandle": {...} }`, nothing else | identical; `PosCihaziId`/`IpAddress`/`HttpPort`/`HttpsPort`/`UseHttps`/`CurrentFingerprint` stay internal and never serialize | NONE |
| `TransactionHandle.SerialNumber` | `string`, from config | `string`, from the local device record | NONE |
| `TransactionHandle.Fingerprint` | `string`, client/application identity from config (`Pavo509DotNetClient`) | `string`, stable configured client identity (`Pavo:Fingerprint`, default `STYS.Agent`, override `STYS_PAVO_FINGERPRINT`) | NONE |
| `TransactionHandle.TransactionSequence` | `int`, starts at 1 | wire `int` (checked conversion from the domain's `long`), starts at 1 | NONE |
| `TransactionHandle.TransactionDate` | `string`, `yyyy-MM-dd'T'HH:mm:ss.ffffff`, InvariantCulture | wire `string`, same format/culture (formatted at the mapping boundary) | NONE |
| Pairing response model | `PavoResponse` — 7 properties only | `PavoWireResponse` — same 7, same nullability | NONE |
| `ErrorCode` | `int?` | `int?` | NONE |
| `Errors` | `List<string>?` (nullable) | `List<string>?` (nullable — absent vs. empty preserved) | NONE |
| Response `TransactionHandle` | `TransactionHandle?` (nullable) | nullable; treated as remote/device metadata only | NONE |
| Response TransactionHandle semantics | never written back into request state | never written back; outgoing fingerprint and sequence are owned by the client | NONE |
| Sequence increment semantics | `_transactionSequence++` after **any** HTTP response | advance when an HTTP response is received, independent of backend command completion | NONE |
| Pair retry after HTTP response (business error or non-2xx) | next attempt uses sequence + 1 | same | NONE |
| Pair retry after network failure / timeout | next attempt reuses the same sequence | same | NONE |
| StartPayment request | `{ "TransactionHandle": {...}, "Payment": {...} }` | identical shape, property set, casing and nesting | NONE |
| Payment validation | `amount <= 0` reject; `installment == 1` or `< 0` reject; blank `saleReference` reject | same three rules, rejected outright (no silent correction) | NONE |
| Payment defaults | see `PavoOptions` | mirrored in `PavoPaymentDefaults` / DTO defaults | NONE |
| `Amount` | major currency unit, decimal | major currency unit, decimal (never converted to minor units) | NONE |
| `SelectedSlots` / `SelectedTerminals` | configured list; `null` when empty | same, `null` when empty | NONE |
| Receipt header | `AdditionalInfo.Header = ReceiptHeader` | `Header = ReceiptHeader` (no longer the payment description) | NONE |
| Receipt footer | `AdditionalInfo.Footer = ReceiptFooter` | same | NONE |
| Receipt list | exactly 3 rows: reference / amount / date | exactly 3 rows, same names, order and formatting | NONE |
| Payment response model | `PaymentResponseData`, 28 fields | same field set and JSON names on the wire model | NONE |
| Payment response types | `transactionNo`/`batchNo` `long?`, `resultStatus` `int?`, `resultDate` `string?` | identical types | NONE |
| Payment success criteria | commonSuccess **and** `Data != null` **and** `Data.IsSuccessful` | `PavoResponseHelpers.IsPaymentSuccessful` enforces all three | NONE |
| Empty / malformed / null-parsed body | not success | hard failure (`EMPTY_RESPONSE` / `INVALID_RESPONSE`) | NONE |
| HTTP non-2xx | not success | transport failure at the operation level; the parsed device envelope is preserved unchanged | NONE |
| PerformEOD | `POST /PerformEOD`, nested `PerformEOD.AdditionalInfo` | implemented, wire shape identical | NONE |
| RebootDevice | `POST /RebootDevice`, handle only | implemented, wire shape identical | NONE |
| EnterPinMode | `POST /EnterPinMode`, handle only | implemented, wire shape identical | NONE |
| ExitPinMode | `POST /ExitPinMode`, handle only | implemented, wire shape identical | NONE |
| EOD / device-command response fields | `gunSonu`, `eodData`, `eodJson`, `eodText`, `eodImage`, `reboot`, `enterPinModeMessage`, `exitPinModeMessage` | all present with reference types (`JsonElement?` for the EOD payloads) | NONE |
| JSON serializer | `WriteIndented = true`, `PropertyNameCaseInsensitive = true` | same; every wire property carries an explicit `[JsonPropertyName]` | NONE |

**Shared/reference difference count: NONE.**

---

## Deliberate STYS-side behaviour that is not a reference difference

These are STYS product requirements layered *around* the reference contract. None of them changes a
byte on the wire or a success/sequence decision for a reference operation.

| Item | Why it exists | Wire impact |
|---|---|---|
| `cardNo` dropped at the wire→domain boundary | Sensitive card data must not be logged, persisted, or forwarded to backend/frontend. It **is** parsed on the wire model for exact contract parity. | None — request unaffected; response parsing unaffected. |
| Sequence persisted to disk | Agent restarts must not replay sequence numbers. The reference client is memory-only. | None — the peek/advance algorithm is identical; only the storage medium differs. |
| HTTPS support (`UseHttps`) | Some STYS deployments front the device with TLS. | None in reference mode: `UseHttps = false` yields `http://host:4567`. |
| Legacy `PosCihazi.TargetFingerprint` / `PairingId` / `PairingCode` columns | Pre-existing STYS schema. No longer written from a pairing response, since the reference has no such fields. | None. |

## STYS-only extensions (no reference counterpart)

`Ping`, `GetDeviceInfo`, `GetPaymentResult` do not exist in `Pavo509.Client`. They are retained
because STYS features depend on them, and they are explicitly marked as extensions in code. Rules
enforced for them:

- They reuse the reference response envelope but add no speculative fields to it.
- They never alter pairing semantics.
- They never overwrite the client fingerprint from a response.
- They never write a response sequence into request state.
- They treat HTTP transport success separately from device envelope success, so a non-2xx response does not mutate the parsed device body.
- Before a device has ever been paired, `GetDeviceInfo` borrows the current sequence **without**
  advancing it, so a pre-pair diagnostic can never push the initial Pairing off sequence 1.

---

## Reference debug mode

To compare STYS against the reference client byte-for-byte on a real device:

```
STYS_PAVO_FINGERPRINT=Pavo509DotNetClient
```

with the device configured as HTTP on port 4567 and the same serial number. Expected first request:

```
POST http://<PAVO-IP>:4567/Pairing

{
  "TransactionHandle": {
    "SerialNumber": "<device serial>",
    "Fingerprint": "Pavo509DotNetClient",
    "TransactionSequence": 1,
    "TransactionDate": "yyyy-MM-ddTHH:mm:ss.ffffff"
  }
}
```

---

## Golden contract tests

`tests/STYS.Tests/Agent/PavoReferenceGoldenContractTests.cs` locks the contract with fixtures derived
from the reference project. JSON is compared structurally (property names, nesting, primitive kinds,
values, null presence); ordering and whitespace are ignored, but a type mismatch such as
`"transactionNo": 123` vs `"transactionNo": "123"` fails.

Coverage: Pairing request/response, StartPayment request/success response, business failure with a
clean envelope, empty body, malformed JSON, response-vs-request sequence independence, retry sequence
after an answered failure, retry sequence after connection error and after timeout, payment wire
types, `cardNo` containment, PerformEOD request/response, and RebootDevice / EnterPinMode /
ExitPinMode requests — plus the five payment validation rules.

---

## Real device verification

**NOT EXECUTED AGAINST REAL PAVO DEVICE** — no PAVO 509 hardware was reachable from this environment.
Steps 1–12 of the manual verification plan (reference pairing → STYS pairing → request/response JSON
comparison → payment → EOD → device commands) remain outstanding and must be run on site before
acceptance sign-off.
