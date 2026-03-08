# STYS

Bu repository, STYS backend (`backend`), frontend (`frontend`), platform modulleri (`platform`) ve test projelerini (`tests`) icerir.

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
