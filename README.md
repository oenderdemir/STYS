# STYS

Bu repository, STYS backend (`backend`), frontend (`frontend`), platform modulleri (`platform`) ve test projelerini (`tests`) icerir.

## Diger Dokumanlar

- Kurumsal tanitim brosuru: [docs/tanitim-brosuru.md](docs/tanitim-brosuru.md)

## Proje Yapisi

- `backend`: ASP.NET Core + EF Core domain ve API katmani
- `frontend`: Angular UI
- `platform`: Ortak platform kutuphaneleri (identity, persistence, aspnetcore)
- `tests`: Otomasyon testleri

## Rezervasyon Senaryo Akisi (PlantUML)

```plantuml
@startuml
title Rezervasyon Senaryosu Bulma Akisi\n(GetKonaklamaSenaryolariAsync)

skinparam Shadowing false
skinparam ActivityBorderColor #4b5563
skinparam ActivityBackgroundColor #f8fafc
skinparam ArrowColor #334155
skinparam NoteBackgroundColor #fff7ed
skinparam NoteBorderColor #fb923c

start

:ValidateScenarioRequest(request);
note right
Zorunlu alanlar kontrol edilir:
- TesisId
- MisafirTipiId
- KonaklamaTipiId
- KisiSayisi
- Baslangic < Bitis
end note

:EnsureCanAccessTesisAsync(request.TesisId);
note right
Kullanici bu tesise erisebiliyor mu?
Yetki/scope kontrolu.
end note

partition "Sezon Kurali Kontrolu (EnsureSeasonRuleComplianceAsync)" {
  :Kurallari getir\n(TesisId + AktifMi + Tarih cakismasi);
  if (Kural var mi?) then (Hayir)
    :Kontrolu gec;
  else (Evet)
    if (StopSaleMi=true olan var mi?) then (Evet)
      :400 Hata\n"Stop-sale aktif";
      stop
    else (Hayir)
      :Gerekli minimum geceyi bul\n(Max(MinimumGece));
      :Tesis giris/cikis saatlerini al;
      :Gece sayisini hesapla\n(EnumerateChargeWindows);
      if (GeceSayisi < MinimumGece?) then (Evet)
        :400 Hata\n"Minimum gece saglanmadi";
        stop
      else (Hayir)
        :Kontrolu gec;
      endif
    endif
  endif
}

:scenarios = [];

:GetRoomAvailabilitiesAsync(full interval);
note right
Musait oda havuzu:
- Oda/Bina/OdaTipi aktif
- Tesis ve opsiyonel OdaTipi filtresi
- Aktif OdaKullanimBlok kaydi olanlar dislanir
- Mevcut doluluk dusulur
- Paylasimli odada kalan kapasite dikkate alinir
end note

:BuildSingleSegmentVariants(...);
:Tek segmentli alternatifleri ekle;

if (Konaklama suresi > 6 saat?) then (Evet)
  :BuildTwoSegmentScenarioAsync(...);
  note right
Iki segmentli alternatif sadece
anlamliysa uretilir.
Ayni oda dagilimiysa eklenmez.
  end note
  if (Segmentli senaryo olustu mu?) then (Evet)
    :Listeye ekle;
  endif
endif

:Tekrarli senaryolari temizle\n(GroupBy CreateScenarioKey);

while (Her senaryo)
  :CalculateScenarioPriceAsync(...);
  note right
Fiyat hesaplama:
- Baz ucret (gunluk pencere + oda atamasi)
- Secili indirimler
- Nihai ucret
  end note
  :Senaryoya fiyat alanlarini yaz;
endwhile

:En ucuzdan pahaliya sirala;
:Ilk 5 senaryoyu al;
:Kod ata (SENARYO-1..N);

:return sortedByPrice;
stop

@enduml
```

## Oda Degisimi ve Ucret Kurali (PlantUML)

```plantuml
@startuml
title Oda Degisimi Akisi\n(GetOdaDegisimSecenekleriAsync + KaydetOdaDegisimiAsync)

skinparam Shadowing false
skinparam ActivityBorderColor #4b5563
skinparam ActivityBackgroundColor #f8fafc
skinparam ArrowColor #334155
skinparam NoteBackgroundColor #ecfeff
skinparam NoteBorderColor #06b6d4

start

:Rezervasyonu scope ile bul;
if (Durum Taslak/Onayli mi?) then (Hayir)
  :400 Hata\n"Bu rezervasyon durumu icin oda degisimi yapilamaz";
  stop
endif

partition "Secenekleri Getir" {
  :Segment + Oda atamalarini oku;
  :Aktif blokla cakisan atamalari bul;
  if (Problemli atama yok) then (Evet)
    :Bos liste don;
    stop
  endif

  while (Her problemli atama)
    :Ayni tesis icin aday odalari topla;
    note right
    Filtreler:
    - Oda/Bina/OdaTipi aktif
    - Kapasite >= AyrilanKisiSayisi
    - Aktif OdaKullanimBlok cakismasi yok
    - Diger rezervasyon dolulugu + ayni segment diger atamalar
    - Kalan kapasite yeterli
    end note
    :Aday oda listesini DTO'ya yaz;
  endwhile

  :Secenek DTO'sunu don;
}

partition "Degisikligi Kaydet" {
  :Istenen yeni oda secimlerini al;
  if (Ayni segmentte ayni oda iki kez secildi mi?) then (Evet)
    :400 Hata;
    stop
  endif

  :Son secimlere gore blok/doluluk/kapasite kontrolu;
  if (Uygun degil) then (Evet)
    :400 Hata;
    stop
  endif

  :RezervasyonSegmentOdaAtama kayitlarini guncelle;
  :Snapshot alanlarini guncelle\n(OdaNo/Bina/OdaTipi/Paylasim/Kapasite);
  :Degisen segmentlerin konaklayan atamalarini temizle;

  if (Rezervasyonda MisafirTipiId ve KonaklamaTipiId var mi?) then (Evet)
    :Yeni oda dagilimiyla fiyati yeniden hesapla;
    if (Yeni nihai ucret < Mevcut toplam ucret?) then (Evet)
      :ToplamUcret'i yeni degere indir;
      :ToplamBazUcret'i min(mevcut, yeni baz) yap;
      :Indirim kirilimini yeniden yaz;
    else (Hayir)
      :Mevcut fiyati koru\n(artis yansitilmaz);
    endif
  endif

  :SaveChanges;
  :Kayit sonucunu don;
}

stop
@enduml
```

## Arizali Oda Sureci (PlantUML)

```plantuml
@startuml
title Arizali Oda Sureci\n(OdaKullanimBlok + Rezervasyon etkisi)

skinparam Shadowing false
skinparam ActivityBorderColor #4b5563
skinparam ActivityBackgroundColor #f8fafc
skinparam ArrowColor #334155
skinparam NoteBackgroundColor #fef2f2
skinparam NoteBorderColor #ef4444

start

partition "Bakim/Ariza Kaydi" {
  :Kullanici Oda Kullanim Blok ekranindan\nAriza kaydi girer;
  :Validate\n(Oda, Tesis scope, Baslangic<Bitis, AktifMi);
  :Kaydi olustur\nBlokTipi=Ariza, AktifMi=true;
}

partition "Rezervasyona Etki" {
  :Rezervasyon listesi yenilenir;
  :GetReservationsRequiringRoomReassignmentAsync;
  if (Rezervasyon segmenti\nblokla cakisiyor mu?) then (Evet)
    :OdaDegisimiGerekli=true;
  else (Hayir)
    :Normal akis;
  endif
}

partition "Check-in Koruma" {
  :Check-in denemesi;
  :EnsureNoActiveRoomBlockForReservationAsync;
  if (Aktif blok var mi?) then (Evet)
    :400 Hata\n"Check-in icin oda degisimi gereklidir";
    note right
    Bu adim, arizali odanin\nfiziksel kullanimini engeller.
    end note
  else (Hayir)
    :Check-in devam eder;
  endif
}

partition "Operasyonel Cozum" {
  :Oda degisim secenekleri acilir;
  :Uygun alternatif oda secilir;
  :KaydetOdaDegisimiAsync;
  :Segment oda snapshotlari guncellenir;
  :Degisen segmentte konaklayan atamalari temizlenir\n(plan yeniden girilir);
  :Gerekirse fiyat guncellenir\n(sadece asagi yonlu);
}

partition "Kapanis" {
  :Ariza kaydi pasife alinabilir\nveya bitis tarihi gecer;
  :Sonraki rezervasyon aramalarinda oda tekrar musait olabilir;
}

stop
@enduml
```
