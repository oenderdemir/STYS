# STYS

Bu repository, STYS backend (`backend`), frontend (`frontend`), platform modulleri (`platform`) ve test projelerini (`tests`) icerir.

## Diger Dokumanlar

- Kurumsal tanitim brosuru: [docs/tanitim-brosuru.md](docs/tanitim-brosuru.md)
- Kurumsal sunum deck'i: [docs/kurumsal-sunum-deck.md](docs/kurumsal-sunum-deck.md)
- Demo toplantisi ozellik akisi: [docs/demo-toplantisi-ozellik-akisi.md](docs/demo-toplantisi-ozellik-akisi.md)

## Docker Ile Test Ortami

Tam Docker test ortami icin:

1. `.env.example` dosyasini `.env` olarak kopyalayin.
2. Gerekli gizli degerleri guncelleyin:
   - veritabani alanlari:
     - `STYS_DB_HOST`
     - `STYS_DB_PORT`
     - `STYS_DB_NAME`
     - `STYS_DB_USER`
     - `STYS_DB_PASSWORD`
   - `STYS_JWT_KEY`
   - gerekirse JWT sureleri:
     - `STYS_JWT_ACCESS_MINUTES`
     - `STYS_JWT_REFRESH_DAYS`
     - `STYS_JWT_RETENTION_DAYS`
   - gerekirse rate limit ayarlari:
     - `STYS_RATE_LIMIT_PERMIT_LIMIT`
     - `STYS_RATE_LIMIT_WINDOW_SECONDS`
     - `STYS_RATE_LIMIT_QUEUE_LIMIT`
   - gerekirse log ayarlari:
     - `STYS_LOG_FILE_PATH`
     - `STYS_LOG_RETAINED_FILE_COUNT_LIMIT`
     - `STYS_LOG_LEVEL_DEFAULT`
3. Uygulamalari ayaga kaldirin:

```powershell
docker compose up -d --build
```

Varsayilan erisimler:

- Frontend: `http://localhost:8080`
- Swagger: `http://localhost:8080/swagger/`
- MSSQL: `localhost,14333`

Notlar:

- Frontend container'i `nginx` ile servis edilir.
- Browser tarafindaki `/api/*` istekleri otomatik olarak backend container'ina proxy edilir.
- `/swagger/*` ve `/health/*` istekleri de frontend container'i uzerinden backend'e proxy edilir.
- Backend host portu disa acilmaz; API ve Swagger erisimi sadece frontend reverse proxy uzerinden saglanir.
- `/swagger/` basic auth ile korunur. Kimlik bilgileri `.env` icindeki `STYS_SWAGGER_AUTH_USERNAME` ve `STYS_SWAGGER_AUTH_PASSWORD` alanlarindan gelir.
- Backend container'i acilis sirasinda `TodIdentityDbContext` ve `StysAppDbContext` migration'larini uygular.
- JWT ve benzeri runtime ayarlari `docker-compose.yml` icindeki environment alanindan, `.env` dosyasi ile override edilebilir.
- Database, JWT, rate limiting ve log ayarlari `.env` dosyasinda ayri section'lar halinde yonetilir.
- Test ortaminda Swagger'i kapatmak istersen `.env` icinde `STYS_ENABLE_SWAGGER=false` yapabilirsin.
- Servisler explicit network'lerde calisir:
  - `stys-edge`: disa acilan frontend katmani
  - `stys-internal`: frontend, backend ve mssql ic haberlesmesi
- Windows ortaminda SQL Server data host bind mount yerine Docker named volume icinde tutulur: `stys_mssql_data`.
- Backend loglari hostta `./Data/logs/backend` klasorune yazilir.
- Nginx access/error loglari hostta `./Data/logs/frontend` klasorune yazilir.

## Docker Image Push

ACR veya baska bir OCI registry'ye backend ve frontend image'larini push etmek icin:

```powershell
.\scripts\push-images.ps1 -RegistryName todregistry
```

Bu komut:

- `az acr login --name todregistry`
- `docker compose build backend frontend`
- `docker compose push backend frontend`

akisini calistirir.

Varsayilan image isimleri:

- backend: `todregistry.azurecr.io/stys/backend:<tag>`
- frontend: `todregistry.azurecr.io/stys/frontend:<tag>`

Varsayilan tag:

- calisma zamani damgasi (`yyyyMMddHHmmss`)

Istersen tag'i elle verebilirsin:

```powershell
.\scripts\push-images.ps1 -RegistryName todregistry -Tag v1.0.0
```

Registry server'i dogrudan vermek istersen:

```powershell
.\scripts\push-images.ps1 -RegistryServer todregistry.azurecr.io -Tag test-20260401 -SkipLogin
```

Compose image referanslari `.env` ile de override edilebilir:

- `STYS_BACKEND_IMAGE`
- `STYS_FRONTEND_IMAGE`
- `STYS_IMAGE_TAG`

## Remote Deploy

Test veya hedef sunucuda, `mssql` container'ina dokunmadan sadece `backend` ve `frontend` image'larini registry'den cekip guncellemek icin:
Ilk kurulumda `mssql` yoksa script onu bir kez ayaga kaldirir. `mssql` zaten calisiyorsa dokunmaz.

```powershell
.\scripts\deploy-remote.ps1
```

Bu komut sunlari yapar:

- `mssql` yoksa veya calismiyorsa `docker compose up -d mssql`
- `docker compose pull backend frontend`
- `docker compose up -d --no-deps backend frontend`

Eger deploy server'da docker login yoksa:

```powershell
.\scripts\deploy-remote.ps1 `
  -WithLogin `
  -RegistryServer todregistry.azurecr.io `
  -Username todregistry `
  -Password "<registry-password>"
```

Bu akista:

- `mssql` varsa korunur
- `mssql` sadece yoksa veya durmussa ayağa kaldirilir
- sadece uygulama katmani guncellenir

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
