# Kullanici Grubu Default Sayfa

- Kullanici grubu `DefaultRoute` alanı nullable'dır.
- Boş/null ise sistem mevcut varsayılan sayfasını kullanır.
- Doluysa `"/"` ile başlayan bir route path olmalıdır.
- Bir kullanıcıda birden fazla default sayfa varsa grup adı alfabetik sıralanır, sonra `Id` ile deterministik seçim yapılır.
- Seçilen route kullanıcının erişemediği bir sayfaya denk gelirse backend `null` döner ve frontend mevcut varsayılan yönlendirmeye düşer.
- Login ve `auth/me` cevapları default route bilgisini taşır.
