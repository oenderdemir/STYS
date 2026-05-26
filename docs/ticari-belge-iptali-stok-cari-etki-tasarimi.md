# Ticari Belge İptali Stok / Cari Etki Tasarımı

## Amaç
- Satış, alış ve iade belgeleri iptal edildiğinde stok, cari ve muhasebe ilişkisinin nasıl yönetileceğini netleştirmek.

## Mevcut Durum
- Muhasebe fişi iptali ters kayıt üretiyor.
- Kapalı/kısmi kapalı cari hareket varsa fiş iptali engelleniyor.
- Ticari belge iptali için ayrı backend akışı yok.

## Ticari Belge İptali Tanımı
- Ticari belge iptali, belgenin stok ve cari etkisini geri alan işlemdir.
- Muhasebe fişi iptalinden ayrı ele alınmalıdır.

## İptal Sırası
1. Belge bulunur ve iptal edilebilirliği kontrol edilir.
2. Kapama varsa önce tahsilat/ödeme geri alınır.
3. Muhasebe fişi varsa ters kayıtla kapatılır.
4. Stok ve cari hareketler iptal edilir ya da terslenir.
5. Belge durumu `Iptal` yapılır.

## Stok Etkisi
- İlk tercih: stok hareketi `Durum = Iptal`.
- Ters stok hareketi daha sonra ayrı fazda değerlendirilebilir.
- Downstream stok bağı varsa iptal engeli gerekebilir.

## Cari Etkisi
- Kapatılmamış cari hareket `Durum = Iptal` yapılabilir.
- Kapalı/kısmi kapalı cari hareket varsa önce kapama geri alınmalıdır.
- Kapama sonrası cari hareket iptali güvenli kabul edilir.

## Muhasebe Fişi Etkisi
- Fiş varsa `MuhasebeFisService.IptalEtAsync` çağrılmalıdır.
- Ters kayıt başarısızsa belge iptali tamamlanmamalıdır.
- `MuhasebeFisId` korunmalıdır.

## Belge Durumu
- `SatisBelgesiDurumu.Iptal` kullanılabilir.
- İptal edilmiş belge yeniden fişlenmemeli ve güncellenmemelidir.

## Engelleme Kuralları
- İptal edilmiş belge tekrar iptal edilemez.
- Kapalı/kısmi kapalı cari hareket varken iptal edilmez.
- Fiş iptali başarısızsa ticari iptal yapılmaz.
- Downstream stok bağı varsa iptal engellenir.

## Transaction Yaklaşımı
- Belge iptali tek transaction içinde yürütülmelidir.
- Muhasebe, stok ve cari güncellemeleri birlikte commit edilmelidir.

## Entity / Migration İhtiyacı
- Mevcut `Iptal` durumları yeterli.
- Yeni alan zorunlu değil.
- İptal tarihi/kullanıcısı istenirse sonraki fazda eklenebilir.

## Önerilen Karar
- Ticari belge iptali, stok + cari + muhasebe etkisini ayrı ama tek işlem akışında yönetsin.
- Muhasebe fişi iptali tek başına ticari belge iptali sayılmasın.

## Sonraki Fazlar
- Faz 88C-B — Ticari Belge İptali Backend Uygulaması
- Faz 88C-C — Ticari Belge İptali UI Aksiyonu
- Faz 88C-D — İptal Edilmiş Belge Regresyon Testleri
- Faz 89 — Stok Bakiye ve Negatif Stok Kontrolü

## Faz 88C-B Notu
- Backend ticari belge iptal akışı eklendi.
- Muhasebe fişi ters kayıtla iptal edilir.
- Stok ve cari hareketler `Durum = Iptal` yapılır.
- Kapalı/kısmi kapalı cari hareket varsa iptal engellenir.

## Faz 88C-C Notu
- Satış Belgeleri ekranına iptal aksiyonu eklendi.
- Confirm dialog ile kullanıcı onayı alınıyor.
- Backend iptal endpointi kullanılıyor.
- UI değişikliği dışında migration yapılmadı.
