## Brief overview
  Her tur sonunda, yapilan tum degisiklikleri ozetleyen bir commit mesaji olusturulmalidir. Bu kural STYS projesine ozgudur.

## Commit mesaji kurallari
  - Her tur sonunda (backend/frontend/build/herhangi bir islem), yapilan degisikliklere uygun bir commit mesaji uretilmelidir.
  - Commit mesaji Turkce olmalidir.
  - Format: `type: kisa aciklama` seklinde conventional commits formatinda olmalidir.
    - Ornek: `feat: Kamp yonetimi modulu eklendi`
    - Ornek: `fix: TasinirKod eslestirme index duzeltildi`
  - Kullanilabilecek type'lar: `feat`, `fix`, `refactor`, `chore`, `docs`, `style`, `test`, `perf`, `ci`, `build`
  - Tek bir commit ile tum degisiklikler birlikte commit edilmelidir (her dosya icin ayri commit atilmamalidir).
  - Commit mesaji `changes.md` icerigindeki tur basligi ile uyumlu olmalidir.
  - Olusturulan commit mesaji dogrudan `git commit -m "..."` komutu olarak sunulmali, kullanici onayi beklenmelidir.