# Tevkifat Muhasebesi Ön Analizi

## Amaç

Satış ve alış faturalarında tevkifatlı işlemlerin muhasebe etkisini belirlemek ve sonraki uygulama fazları için net teknik karar çıkarmak.

## Mevcut durum

- `SatisBelgesiSatiri` üzerinde `TevkifatPay`, `TevkifatPayda`, `TevkifatTutari` alanları var.
- `SatisBelgesiDto` ve `Create/Update` request modelleri tevkifat verisini taşıyor.
- UI tevkifat oranını seçtiriyor ve satır bazında tevkifat/net KDV/özet hesaplıyor.
- `SatisBelgesiMuhasebeFisService` tevkifatlı belgeleri şu anda açık hata ile reddediyor.

## Satış tevkifatı

- Önerilen akış: 120 alıcılar borç, 600 satış geliri alacak, 391 hesaplanan KDV alacak, tevkifat için ayrıca uygun karşı hesap borç/alacak satırı.
- Mevcut hesap planında tevkifat için özel sabit/hesap eşleşmesi görünmüyor.
- `136` / tevkifat alacağı veya benzeri bir hesap için mevcut merkezi sabit tespit edilmedi.
- Bu yüzden satış tevkifatı, mevcut satış stratejisine küçük eklemeden ziyade ayrı muhasebe stratejisi gerektirir.

## Alış tevkifatı

- Önerilen akış: 153/740/770 borç, 191 borç, 320 alacak, tevkifat sorumluluğu için 360 veya uygun karşı hesap.
- `360 Ödenecek Vergi ve Fonlar` için mevcut merkezi sabit/mapping tespit edilmedi.
- Alış tevkifatı da mevcut alış stratejisine küçük eklemeden çok ayrı faz gerektirir.

## Hesap planı ihtiyacı

- Mevcut sistemde net bulunan hesaplar:
  - 120 Alıcılar
  - 320 Satıcılar
  - 191 İndirilecek KDV
  - 391 Hesaplanan KDV
- Tevkifat için gerekli özel hesap/mapping tespit edilmedi.
- Sonraki fazda tevkifat hesap eşleme altyapısı gerekir.

## Model/DTO yeterliliği

- `TevkifatPay` / `TevkifatPayda` satır bazında yeterli.
- `TevkifatTutari` satır bazında hesaplanabiliyor.
- `ToplamTevkifatTutari` ve `ToplamNetKdv` belge seviyesinde DTO’da mevcut.
- Eksik olan alan, muhasebe fişi üretimi için tevkifat karşı hesap eşlemesidir.

## Strateji önerisi

- Tevkifatlı satış ve alış işlemleri mevcut satış/alış stratejilerine küçük if ekleriyle gömülmemeli.
- Ayrı tevkifat stratejileri daha temiz olur:
  - `SatisTevkifatliFaturaMuhasebeFisStratejisi`
  - `AlisTevkifatliFaturaMuhasebeFisStratejisi`
- Önce tevkifat hesap eşleme altyapısı kurulmalı, sonra fiş üretimi açılmalı.

## UI etkisi

- UI’da tevkifat oranı seçiliyor.
- Tevkifat tutarı ve net KDV doğru gösteriliyor.
- Genel toplam net KDV’ye göre hesaplanıyor.
- Ancak backend muhasebe fişi tarafı tevkifatı henüz desteklemiyor.

## Riskler

- 191/391 yönünün yanlış kullanılması.
- Tevkifat karşı hesabının yanlış sınıflandırılması.
- Satıcı/alıcı net tutarının yanlış yazılması.
- İade ve tevkifat kombinasyonlarının karmaşıklaşması.
- KDV beyannamesi ve tevkifat beyannamesi uyumsuzluğu.

## Önerilen sonraki fazlar

- Faz 75B: Tevkifat hesap eşleme altyapısı
- Faz 75C: Satış tevkifatı muhasebe fişi
- Faz 75D: Alış tevkifatı muhasebe fişi
- Faz 75E: Tevkifat raporları ve KDV beyanname etkisi
