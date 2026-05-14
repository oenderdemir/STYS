---
description: Her tur sonunda yapılan değişikliklere uygun git commit mesajı öner
---

Her işlem turunun sonunda yapılan değişikliklere uygun bir git commit mesajı üret.

Kurallar:
- Commit mesajı yapılan gerçek değişikliklere dayanmalı.
- Commit mesajı kısa, net ve anlamlı olmalı.
- Conventional Commits formatını kullan.
- Gereksiz açıklama, uzun paragraf veya kod bloğu ekleme.
- Eğer değişiklik yoksa commit mesajı üretme; bunun yerine “Commit mesajı gerektiren değişiklik yok.” yaz.
- Commit mesajını Türkçe üret.
- Scope biliniyorsa ekle, bilinmiyorsa scope kullanma.

Kullanılacak format:

```text
<type>(<scope>): <kısa açıklama