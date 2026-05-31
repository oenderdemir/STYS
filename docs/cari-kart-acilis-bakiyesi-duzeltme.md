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
- Her düzeltme işlemi yeni bir hareket üretir; eski düzeltme hareketleri güncellenmez.

## Etki
- Eski açılış hareketi korunur.
- Geçmiş hareket izi bozulmaz.
- Cari bakiye aktif hareketler üzerinden doğru hesaplanır.

## API ve UI
- `POST /api/ui/muhasebe/cari-kartlar/{id}/acilis-bakiyesi-duzelt` endpointi eklendi.
- Cari kartlar ekranında "Açılış Bakiyesi Düzelt" dialogu eklendi.
- İşlem fark kadar yeni cari hareket oluşturur.
