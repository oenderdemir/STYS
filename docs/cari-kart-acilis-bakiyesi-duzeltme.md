# Cari Kart Açılış Bakiyesi Düzeltme

## Amaç
- Kullanılmamış açılış bakiyesi cari kart üzerinden güncellenebilir.
- Kullanılmış açılış bakiyesi doğrudan değiştirilemez.
- Kullanılmış açılış bakiyesi için fark kadar düzeltme cari hareketi oluşturulur.

## Kural
- Açılış hareketi aktif ve kullanılmamışsa kart update ile senkron edilir.
- Açılış hareketi kullanılmışsa kart update engellenir.
- Düzeltme akışı `CariKartAcilisBakiyesiDuzeltAsync` ile çalışır.

## Düzeltme Hareketi
- KaynakModul: `CariKartAcilisDuzeltme`
- BelgeTuru: `AcilisDuzeltme`
- Fark > 0 ise borç hareketi oluşturulur.
- Fark < 0 ise alacak hareketi oluşturulur.
- Fark = 0 ise yeni hareket oluşturulmaz.

## Etki
- Eski açılış hareketi korunur.
- Geçmiş hareket izi bozulmaz.
- Cari bakiye aktif hareketler üzerinden doğru hesaplanır.
