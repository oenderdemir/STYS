# Muhasebe Regresyon Testleri - Faz B-2

## Test Edilen Senaryolar
- Cari kart oluşturma ve açılış bakiyesi cari hareketi akışı.
- Cari kart banka hesapları ve yetkili kişiler detay/edit akışı.
- Satış/fatura belgesi satır parametreleri ve toplamlar.
- Belgeden muhasebe fişi oluşturma, duplicate engeli ve fiş onayı.
- Muhasebe fişi onay sonrası düzenleme kısıtı ve rapor görünürlüğü.
- Belge iptali, ters kayıt, stok/cari hareket iptali ve buton görünürlüğü.
- Cari hareket kapama koruması ve açılış bakiyesi düzeltme akışı.
- Yevmiye, muavin, mizan, cari bakiye ve export kontrolleri.
- Tesis / yetki scope ve hesaplar ekranı tesis filtresi.

## Bulunan Hatalar
- Kritik regresyon hatası bulunmadı.

## Yapılan Düzeltmeler
- Düzeltme yapılmadı.

## Eksik Kalan İş Kuralları
- ÖTV / ÖİV / Konaklama Vergisi satır bilgileri belgede korunuyor; muhasebe fişi toplam etkisi ayrı iş kuralı olarak ele alınmalıysa sonraki faz gerekir.

## Backend
- Kod değişikliği yapılmadı.

## Frontend
- Kod değişikliği yapılmadı.

## Migration
- Gerekmedi.

## Build
- `dotnet build backend/STYS.csproj` başarılı.
- `npm run build` başarılı.
- Mevcut uyarılar var, hata yok.

## Test
- Kod incelemesi ve build doğrulaması yapıldı.
- Manuel runtime test yapılmadı.

## Commit
- Bu fazda kod değişikliği yapılmadı; bu yüzden commit oluşturulmadı.
