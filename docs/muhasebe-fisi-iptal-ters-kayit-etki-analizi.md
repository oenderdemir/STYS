# Muhasebe Fişi İptal / Ters Kayıt Etki Analizi

## Amaç
- Muhasebe fişi iptali ve ters kaydının satış, alış, iade, stok, cari ve tahsilat/ödeme akışlarına etkisini netleştirmek.

## Mevcut Durum
- `MuhasebeFis` üzerinde `Durum`, `TersKayitFisId`, `IptalEdilenFisId`, `KaynakModul`, `KaynakId` alanları var.
- `MuhasebeFisService` iptal işleminde ters kayıt fişi oluşturuyor ve orijinal fişi `Iptal` durumuna alıyor.
- `SatisBelgesi` üzerinde `MuhasebeFisId` ve `MuhasebeFisOlusturmaTarihi` var.
- `StokHareket` ve `CariHareket` üzerinde `Durum`, `KaynakModul`, `KaynakId` alanları var.

## Fiş İptali Etkisi
- Fiş iptali muhasebe etkisini ters kayıtla sıfırlıyor.
- Kaynak fiş `Iptal`, ters fiş `TersKayit` durumda tutuluyor.
- `MuhasebeFisId` belge üzerinde korunmalı; otomatik temizlenmemeli.
- Aynı belge için tekrar fiş oluşturma bu aşamada otomatik açılmamalı.

## Ters Kayıt Etkisi
- Ters kayıt, orijinal fişin borç/alacak tersidir.
- Kaynak belge ve ticari hareketler otomatik terslenmemeli.
- Ters kayıt ile ticari belge iptali birbirinden ayrılmalı.

## Stok Hareketi Etkisi
- Stok hareketi için mevcut `Durum = Iptal` alanı yeterli.
- Fiş iptali stok hareketini otomatik silmemeli.
- Gerekirse stok hareketi `Iptal` yapılmalı; ters stok hareketi ayrı fazda değerlendirilmeli.

## Cari Hareket Etkisi
- Cari hareketler için `Durum = Iptal`, `KapananTutar`, `KalanTutar`, `KapandiMi` alanları mevcut.
- Kapama yapılmış cari hareket varsa önce kapama geri alınmalı.
- Fiş iptali, kapalı cari hareketi doğrudan bozmamalı.

## Tahsilat / Ödeme Kapama İlişkisi
- Kapama yapılmış belge üzerinde fiş iptali önce tahsilat/ödeme kapama geri alımını gerektirebilir.
- Kapama geri alınmadan fiş iptali yapılması engellenmeli.

## Engelleme Kuralları
- Onaylı fişlerde iptal/ters kayıt dışı mutasyon yapılmamalı.
- Ters kayıt fişi ikinci kez terslenmemeli veya iptal edilmemeli.
- Kapalı cari hareket varken fiş iptali yapılmamalı.
- Stok hareketine bağlı downstream işlem varsa iptal engellenmeli.

## Entity / Migration İhtiyacı
- Mevcut alanlar karar için yeterli.
- Yeni entity alanı veya migration gerekmiyor.
- İleride stok/cari için ters ilişki takibi istenirse ek alan gerekebilir.

## Önerilen Karar
- `MuhasebeFişi iptali` sadece muhasebe fişini ters kayıtla kapatsın.
- `Ticari belge iptali` stok ve cari etkileri yönetsin.
- `Ters kayıt` muhasebe içi geri alma mekanizması olsun.
- Aynı belge için yeniden fişleme ayrı ve açık bir iş akışıyla yapılmalı.

## Sonraki Fazlar
- Faz 88B — Muhasebe Fişi İptal / Ters Kayıt Backend Güvenlik Kuralları
- Faz 88C — Ticari Belge İptali Stok / Cari Etki Tasarımı
- Faz 88D — Muhasebe Fişi Ters Kayıt UI İyileştirmesi
- Faz 88E — Fiş İptali ve Belge Yeniden Fiş Oluşturma Akışı

## Faz 88B — Backend Güvenlik Kuralları
- İptal edilmiş ve ters kayıt fişlerinde tekrar işlem engeli eklendi.
- Kapalı veya kısmi kapalı cari hareket bulunan kaynak satış belgelerinde fiş iptali engellenir.
- Stok/cari hareket iptali ticari belge iptal fazına bırakıldı.
- Migration yapılmadı.

## Faz 88C — Ticari Belge İptali Etki Ayrımı

- Ticari belge iptali, muhasebe fişi iptalinden ayrıştırıldı.
- Stok ve cari iptali ayrı faza bırakıldı.
- Bu fazda kod ve migration yapılmadı.
