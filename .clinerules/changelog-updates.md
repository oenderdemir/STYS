## Brief overview
  Her turda / her islem sonrasi `changes.md` dosyasi guncellenmelidir. Bu kural STYS projesine ozgudur.

## Changelog kurallari
  - Her degisiklik turu sonunda (backend/frontend/build/herhangi bir islem), yapilan tum degisiklikler `changes.md` dosyasina append edilmelidir.
  - Her tur icin su bilgiler mutlaka kaydedilmelidir:
    - Tur basligi ve kisa aciklama (`## Tur X - ...`)
    - Yeni olusturulan dosyalar listesi (varsa)
    - Degistirilen dosyalar listesi (varsa)
    - Build sonuclari (Backend: BASARILI/BASARISIZ, Frontend: BASARILI/BASARISIZ)
  - Degisiklik yoksa bile build sonuclari ile birlikte not dusulmelidir.
  - Format: Mevcut `changes.md` dosyasindaki yapi ve stil aynen korunmalidir (### alt basliklar, kod bloklari, tablolar).
  - Her tur `---` ayraci ile birbirinden ayrilmalidir.
  - Degisen dosyalar listesinde `changes.md` kendisi de dahil edilmelidir (bu dosya da her turda guncellenir).