using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
using STYS.Muhasebe.CariKartlar.Entities;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.OdemeIzleme.Dtos;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.Persistence.Rdbms.Paging;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.OdemeIzleme.Services;

public class OdemeIzlemeService : IOdemeIzlemeService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMuhasebeTesisScopeService _tesisScopeService;

    public OdemeIzlemeService(StysAppDbContext dbContext, IMuhasebeTesisScopeService tesisScopeService)
    {
        _dbContext = dbContext;
        _tesisScopeService = tesisScopeService;
    }

    public async Task<PagedResult<OdemeAramaSatiriDto>> AraAsync(PagedRequest request, OdemeAramaFilterDto filter, CancellationToken cancellationToken = default)
    {
        var (pageNumber, pageSize) = request.Normalize();
        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);

        if (tesisIds.Count == 0)
        {
            return new PagedResult<OdemeAramaSatiriDto>([], pageNumber, pageSize, 0);
        }

        // TahsilatOdemeBelgesi'nin kendi TesisId'si YOK - tesis kapsami CariKart.TesisId uzerinden
        // uygulanir (bkz. arastirma bulgulari). Bu, yetkisiz bir tesisin verisinin sorguya HIC
        // girmemesini saglayan tek dogru kapsam yoludur.
        var query = _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value));

        if (!string.IsNullOrWhiteSpace(filter.BelgeNo))
        {
            query = query.Where(b => b.BelgeNo.Contains(filter.BelgeNo));
        }
        if (filter.CariKartId.HasValue)
        {
            query = query.Where(b => b.CariKartId == filter.CariKartId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.CariAramaMetni))
        {
            query = query.Where(b => b.CariKart != null
                && (b.CariKart.UnvanAdSoyad.Contains(filter.CariAramaMetni) || b.CariKart.CariKodu.Contains(filter.CariAramaMetni)));
        }
        if (filter.TarihBaslangic.HasValue)
        {
            query = query.Where(b => b.BelgeTarihi >= filter.TarihBaslangic.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (filter.TarihBitis.HasValue)
        {
            query = query.Where(b => b.BelgeTarihi < filter.TarihBitis.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }
        if (filter.TutarMin.HasValue)
        {
            query = query.Where(b => b.Tutar >= filter.TutarMin.Value);
        }
        if (filter.TutarMax.HasValue)
        {
            query = query.Where(b => b.Tutar <= filter.TutarMax.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.ParaBirimi))
        {
            query = query.Where(b => b.ParaBirimi == filter.ParaBirimi);
        }
        if (!string.IsNullOrWhiteSpace(filter.OdemeYontemi))
        {
            query = query.Where(b => b.OdemeYontemi == filter.OdemeYontemi);
        }
        if (!string.IsNullOrWhiteSpace(filter.BelgeTipi))
        {
            query = query.Where(b => b.BelgeTipi == filter.BelgeTipi);
        }
        if (filter.KasaBankaHesapId.HasValue)
        {
            query = query.Where(b => b.KasaBankaHesapId == filter.KasaBankaHesapId.Value
                && b.KasaBankaHesap != null && !b.KasaBankaHesap.IsDeleted
                && b.KasaBankaHesap.TesisId.HasValue && b.CariKart != null && b.KasaBankaHesap.TesisId == b.CariKart.TesisId);
        }
        if (!string.IsNullOrWhiteSpace(filter.Durum))
        {
            query = query.Where(b => b.Durum == filter.Durum);
        }
        if (filter.SadeceFissizOlanlar == true)
        {
            query = query.Where(b => b.MuhasebeFisId == null);
        }
        if (!string.IsNullOrWhiteSpace(filter.ValorDurumu))
        {
            // POS valor kaydi yalnizca odemenin KENDI tesisiyle (CariKart.TesisId) ayni tesisteyse
            // eslesir - baska tesise ait bir kayit (kullanici o tesise de yetkili olsa dahi) bu
            // odemeyi asla eslestirmemeli.
            query = query.Where(b => _dbContext.PosTahsilatValorleri.AsNoTracking()
                .Any(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == b.Id && v.Durum == filter.ValorDurumu
                    && b.CariKart != null && b.CariKart.TesisId == v.TesisId));
        }

        // ── Ek filtreler (hepsi GERCEKTEN sorguya uygulanir) ──

        if (!string.IsNullOrWhiteSpace(filter.MuhasebeFisNo))
        {
            // Fis yalnizca FisNo eslesirse DEGIL, AYRICA odemenin KENDI tesisiyle (CariKart.TesisId)
            // ayni tesise aitse eslesir - baska tesise ait bir fis (kullanici o tesise de yetkili
            // olsa dahi) bu odemeyi asla eslestirmemeli.
            var fisIdler = _dbContext.MuhasebeFisler.AsNoTracking()
                .Where(f => !f.IsDeleted && f.FisNo.Contains(filter.MuhasebeFisNo))
                .Select(f => new { FisId = (int?)f.Id, f.TesisId });
            query = query.Where(b => fisIdler.Any(f => f.FisId == b.MuhasebeFisId
                && b.CariKart != null && f.TesisId == b.CariKart.TesisId));
        }

        if (filter.MaliYil.HasValue || filter.Donem.HasValue)
        {
            var donemFisIdler = _dbContext.MuhasebeFisler.AsNoTracking()
                .Where(f => !f.IsDeleted
                    && (!filter.MaliYil.HasValue || f.MaliYil == filter.MaliYil.Value)
                    && (!filter.Donem.HasValue || f.Donem == filter.Donem.Value))
                .Select(f => (int?)f.Id);
            query = query.Where(b => donemFisIdler.Contains(b.MuhasebeFisId));
        }

        if (!string.IsNullOrWhiteSpace(filter.RezervasyonReferansNo))
        {
            var rezervasyonBelgeIdler = _dbContext.RezervasyonOdemeler.AsNoTracking()
                .Where(r => r.TahsilatOdemeBelgesiId.HasValue
                    && r.Rezervasyon != null && r.Rezervasyon.ReferansNo.Contains(filter.RezervasyonReferansNo))
                .Select(r => r.TahsilatOdemeBelgesiId!.Value);
            query = query.Where(b => rezervasyonBelgeIdler.Contains(b.Id));
        }

        if (!string.IsNullOrWhiteSpace(filter.Iban))
        {
            // Hesap yalnizca IBAN'i eslesirse DEGIL, AYRICA odemenin KENDI tesisiyle (CariKart.TesisId)
            // ayni tesise aitse eslesir - baska tesise ait bir hesap (kullanici o tesise de yetkili
            // olsa dahi) bu odemeyi asla eslestirmemeli.
            query = query.Where(b => b.KasaBankaHesap != null && !b.KasaBankaHesap.IsDeleted
                && b.KasaBankaHesap.Iban != null && b.KasaBankaHesap.Iban.Contains(filter.Iban)
                && b.KasaBankaHesap.TesisId.HasValue && b.CariKart != null && b.KasaBankaHesap.TesisId == b.CariKart.TesisId);
        }

        if (filter.OlusturulmaBaslangic.HasValue)
        {
            var ust = filter.OlusturulmaBaslangic.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(b => b.CreatedAt >= ust);
        }
        if (filter.OlusturulmaBitis.HasValue)
        {
            var ust = filter.OlusturulmaBitis.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(b => b.CreatedAt < ust);
        }

        if (!string.IsNullOrWhiteSpace(filter.OlusturanKullanici))
        {
            query = query.Where(b => b.CreatedBy != null && b.CreatedBy.Contains(filter.OlusturanKullanici));
        }

        if (filter.SadeceIptalEdilmisOlanlar.HasValue)
        {
            query = filter.SadeceIptalEdilmisOlanlar.Value
                ? query.Where(b => b.Durum == TahsilatOdemeBelgeDurumlari.Iptal)
                : query.Where(b => b.Durum == TahsilatOdemeBelgeDurumlari.Aktif);
        }

        var toplam = await query.CountAsync(cancellationToken);

        var satirlar = await query
            .OrderByDescending(b => b.BelgeTarihi).ThenByDescending(b => b.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new OdemeAramaSatiriDto
            {
                Id = b.Id,
                BelgeNo = b.BelgeNo,
                BelgeTarihi = b.BelgeTarihi,
                BelgeTipi = b.BelgeTipi,
                Durum = b.Durum,
                Tutar = b.Tutar,
                ParaBirimi = b.ParaBirimi,
                OdemeYontemi = b.OdemeYontemi,
                CariKartId = b.CariKartId,
                CariKodu = b.CariKart != null ? b.CariKart.CariKodu : string.Empty,
                CariUnvan = b.CariKart != null ? b.CariKart.UnvanAdSoyad : string.Empty,
                KasaBankaHesapAdi = b.KasaBankaHesap != null && !b.KasaBankaHesap.IsDeleted
                    && b.KasaBankaHesap.TesisId.HasValue && b.CariKart != null && b.KasaBankaHesap.TesisId == b.CariKart.TesisId
                    ? b.KasaBankaHesap.Ad
                    : null,
                MuhasebeFisId = b.MuhasebeFisId
            })
            .ToListAsync(cancellationToken);

        // Sayfadaki her satir icin HAFIF uyari sayaci (tam liste yalnizca detayda) - iki ek, ID-bazli
        // (dar kapsamli) sorgu ile, N+1 OLMADAN.
        var sayfaIdler = satirlar.Select(x => x.Id).ToList();
        var fissizAktifNakitIdler = sayfaIdler.Count == 0
            ? []
            : await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                .Where(b => sayfaIdler.Contains(b.Id) && b.Durum == TahsilatOdemeBelgeDurumlari.Aktif
                    && b.MuhasebeFisId == null && OdemeYontemleri.NakitHareketiGerektirenler.Contains(b.OdemeYontemi))
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);

        var krediKartiIdler = sayfaIdler.Count == 0
            ? []
            : await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                .Where(b => sayfaIdler.Contains(b.Id) && b.Durum == TahsilatOdemeBelgeDurumlari.Aktif && b.OdemeYontemi == OdemeYontemleri.KrediKarti)
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);
        var posVarOlanIdler = krediKartiIdler.Count == 0
            ? []
            : await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                .Where(b => krediKartiIdler.Contains(b.Id) && _dbContext.PosTahsilatValorleri.AsNoTracking()
                    .Any(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == b.Id && b.CariKart != null && b.CariKart.TesisId == v.TesisId))
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);
        var posEksikIdler = krediKartiIdler.Except(posVarOlanIdler).ToHashSet();

        foreach (var s in satirlar)
        {
            var sayac = 0;
            if (fissizAktifNakitIdler.Contains(s.Id)) sayac++;
            if (posEksikIdler.Contains(s.Id)) sayac++;
            s.UyariSayisi = sayac;
        }

        return new PagedResult<OdemeAramaSatiriDto>(satirlar, pageNumber, pageSize, toplam);
    }

    public async Task<OdemeDetayDto> GetDetayAsync(int id, CancellationToken cancellationToken = default)
    {
        var belge = await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Id == id)
            .Select(b => new
            {
                b.Id, b.BelgeNo, b.BelgeTarihi, b.BelgeTipi, b.Durum, b.Tutar, b.ParaBirimi, b.OdemeYontemi, b.Aciklama,
                b.CariKartId, b.KasaBankaHesapId, b.MuhasebeFisId, b.KapatilacakCariHareketId,
                b.CreatedBy, b.CreatedAt, b.UpdatedBy, b.UpdatedAt,
                CariKodu = b.CariKart != null ? b.CariKart.CariKodu : null,
                CariUnvan = b.CariKart != null ? b.CariKart.UnvanAdSoyad : null,
                CariTesisId = b.CariKart != null ? b.CariKart.TesisId : null
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Ödeme kaydı bulunamadı.", 404);

        var tesisId = belge.CariTesisId ?? throw new BaseException("Ödemenin tesisi belirlenemedi.", 400);
        await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId, cancellationToken);

        var tesisAdi = await _dbContext.Tesisler.AsNoTracking().Where(t => t.Id == tesisId).Select(t => t.Ad).FirstOrDefaultAsync(cancellationToken);

        var dto = new OdemeDetayDto
        {
            Id = belge.Id,
            BelgeNo = belge.BelgeNo,
            BelgeTarihi = belge.BelgeTarihi,
            BelgeTipi = belge.BelgeTipi,
            Durum = belge.Durum,
            Tutar = belge.Tutar,
            ParaBirimi = belge.ParaBirimi,
            OdemeYontemi = belge.OdemeYontemi,
            Aciklama = belge.Aciklama,
            CariKartId = belge.CariKartId,
            CariKodu = belge.CariKodu ?? string.Empty,
            CariUnvan = belge.CariUnvan ?? string.Empty,
            TesisId = tesisId,
            TesisAdi = tesisAdi,
            KasaBankaHesapId = belge.KasaBankaHesapId,
            MuhasebeFisId = belge.MuhasebeFisId,
            KapatilacakCariHareketId = belge.KapatilacakCariHareketId,
            OlusturanKullanici = belge.CreatedBy,
            OlusturmaTarihi = belge.CreatedAt,
            DegistirenKullanici = belge.UpdatedBy,
            DegisiklikTarihi = belge.UpdatedAt
        };

        if (belge.KasaBankaHesapId.HasValue)
        {
            // YETKI KAPSAMI SORGUNUN KENDISINDE: hesap YALNIZCA (a) kullanicinin yetkili oldugu
            // tesislerden biriyse VE (b) odemenin KENDI tesisiyle (CariKart.TesisId) AYNI tesise
            // aitse yuklenir. Kullanici birden fazla tesise yetkili olabilir - "yetkili herhangi bir
            // tesise ait" YETERLI DEGILDIR, hesap MUTLAKA bu odemenin tesisiyle eslesmelidir. Hesabin
            // TesisId'si null ise de GECERLI SAYILMAZ (x.TesisId == tesisId, null icin hep false).
            // Baska tesise ait bir hesabin adi/banka adi/IBAN'i (maskeli dahi olsa)/muhasebe hesap
            // kodu DTO'ya HIC girmez - "once oku sonra temizle" yaklasimi kullanilmaz.
            var yetkiliTesisIds = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);

            var hesap = await _dbContext.KasaBankaHesaplari.AsNoTracking()
                .Where(x => x.Id == belge.KasaBankaHesapId.Value
                    && x.TesisId.HasValue && yetkiliTesisIds.Contains(x.TesisId.Value)
                    && x.TesisId == tesisId)
                .Select(x => new { x.Ad, x.Tip, x.BankaAdi, x.Iban, MuhasebeHesapKodu = x.MuhasebeHesapPlani != null ? x.MuhasebeHesapPlani.TamKod : null })
                .FirstOrDefaultAsync(cancellationToken);

            if (hesap is not null)
            {
                dto.KasaBankaHesapAdi = hesap.Ad;
                dto.KasaBankaHesapTipi = hesap.Tip;
                dto.BankaAdi = hesap.BankaAdi;
                dto.IbanMaskeli = MaskeleIban(hesap.Iban);
                dto.MuhasebeHesapKodu = hesap.MuhasebeHesapKodu;
            }
            else
            {
                dto.BagliHesapErisimKisitliMi = true;

                // Neden kodu SECIMI GUVENLIDIR - hesap adi/IBAN/kod gibi hicbir ayrinti okumaz,
                // yalnizca ID ve TesisId'nin kullanicinin ZATEN yetkili oldugu bir tesise ait olup
                // olmadigini kontrol eder. Kullanici o tesisi zaten gorebildigi icin bu bilgi ekstra
                // bir sey ifsa etmez: yetkili bir tesisteyse ama odemenin tesisinden FARKLIYSA
                // TESIS_UYUSMAZLIGI donulur; hesap hic yoksa veya kullanicinin YETKILI OLMADIGI bir
                // tesisteyse (hangisi oldugu ifsa edilmeden) genel yetki kapsami disi kodu donulur.
                var yetkiliTesisteVarMi = await _dbContext.KasaBankaHesaplari.AsNoTracking()
                    .AnyAsync(x => x.Id == belge.KasaBankaHesapId.Value
                        && x.TesisId.HasValue && yetkiliTesisIds.Contains(x.TesisId.Value), cancellationToken);

                var nedenKodu = yetkiliTesisteVarMi
                    ? OdemeErisimKisitiNedenKodlari.TesisUyusmazligi
                    : OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaHesapBaglantisi;

                dto.ErisimKisitiNedenKodlari.Add(nedenKodu);
                dto.Uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = nedenKodu,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                    Aciklama = "Bağlı kasa/banka hesabı mevcut yetki kapsamınızla veya ödemenin tesisiyle uyuşmuyor; hesap ayrıntıları gösterilmiyor."
                });
            }
        }

        // Fisi ID'ye KOR KOR guvenmeden dogrula: IgnoreQueryFilters ile arar ki "bulunamadi" ile
        // "soft-delete edilmis" ayirt edilebilsin. Ayrica fis SATIRLARINDAN, odemenin kasa/banka
        // hesabinin gercekten etkilenip etkilenmedigi kontrol edilir.
        DogrulanmisFis? dogrulanmisFis = null;
        if (belge.MuhasebeFisId.HasValue)
        {
            // YETKI KAPSAMI SORGUNUN KENDISINDE: fis YALNIZCA (a) kullanicinin yetkili oldugu
            // tesislerden biriyse VE (b) odemenin KENDI tesisiyle (CariKart.TesisId) AYNI tesise
            // aitse yuklenir. Kullanici birden fazla tesise yetkili olabilir - "yetkili herhangi bir
            // tesise ait" YETERLI DEGILDIR, fis MUTLAKA bu odemenin tesisiyle eslesmelidir.
            // IgnoreQueryFilters KASITLI (soft-delete'i "bulunamadi"dan ayirmak icin) ANCAK tesis
            // kisiti KALDIRILMAZ - baska tesisin fis no/tarihi/durumu asla donmez.
            var yetkiliTesisIdsFis = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);

            var fis = await _dbContext.MuhasebeFisler.IgnoreQueryFilters().AsNoTracking()
                .Where(f => f.Id == belge.MuhasebeFisId.Value && yetkiliTesisIdsFis.Contains(f.TesisId)
                    && f.TesisId == tesisId)
                .Select(f => new { f.Id, f.FisNo, f.FisTarihi, f.Durum, f.IsDeleted, f.TesisId, f.MaliYil, f.Donem })
                .FirstOrDefaultAsync(cancellationToken);

            if (fis is null)
            {
                // Fis ya yok, ya yetki kapsami disinda, ya da odemenin tesisinden FARKLI bir tesise
                // ait - hangisi oldugu ifsa edilmez, ayrinti donmez. Bakiye degerlendirmesinde
                // Bulundu=false oldugu icin bu fis ASLA gecerli kabul edilmez (MuhasebeFisDogrulama
                // degistirilmedi).
                dogrulanmisFis = new DogrulanmisFis(belge.MuhasebeFisId.Value, Bulundu: false, SoftDeleteEdilmis: false,
                    Durum: null, TesisId: null, MaliYil: null, Donem: null, FisTarihi: null, BeklenenKasaBankaHesabiEtkilenmisMi: null);

                dto.BagliFisErisimKisitliMi = true;

                // Neden kodu SECIMI GUVENLIDIR - fis no/tarih/durum gibi hicbir ayrinti okumaz,
                // yalnizca ID ve TesisId'nin kullanicinin ZATEN yetkili oldugu bir tesise ait olup
                // olmadigini kontrol eder. Kullanici o tesisi zaten gorebildigi icin bu bilgi ekstra
                // bir sey ifsa etmez: yetkili bir tesisteyse ama odemenin tesisinden FARKLIYSA
                // TESIS_UYUSMAZLIGI donulur; fis hic yoksa veya kullanicinin YETKILI OLMADIGI bir
                // tesisteyse (hangisi oldugu ifsa edilmeden) genel yetki kapsami disi kodu donulur.
                var yetkiliTesisteVarMi = await _dbContext.MuhasebeFisler.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(f => f.Id == belge.MuhasebeFisId.Value && yetkiliTesisIdsFis.Contains(f.TesisId), cancellationToken);

                var nedenKodu = yetkiliTesisteVarMi
                    ? OdemeErisimKisitiNedenKodlari.TesisUyusmazligi
                    : OdemeErisimKisitiNedenKodlari.YetkiKapsamiDisindaFisBaglantisi;

                dto.ErisimKisitiNedenKodlari.Add(nedenKodu);
                dto.Uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = nedenKodu,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                    Aciklama = "Bağlı muhasebe fişi bulunamadı veya mevcut yetki kapsamınızla/ödemenin tesisiyle uyuşmuyor; fiş ayrıntıları gösterilmiyor."
                });
            }
            else
            {
                dto.MuhasebeFisNo = fis.FisNo;
                dto.MuhasebeFisTarihi = fis.FisTarihi;
                dto.MuhasebeFisDurumu = fis.Durum;

                bool? hesapEtkilenmis = null;
                if (belge.KasaBankaHesapId.HasValue)
                {
                    hesapEtkilenmis = await _dbContext.MuhasebeFisSatirlari.IgnoreQueryFilters().AsNoTracking()
                        .AnyAsync(s => !s.IsDeleted && s.MuhasebeFisId == fis.Id && s.KasaBankaHesapId == belge.KasaBankaHesapId.Value, cancellationToken);
                }

                dogrulanmisFis = new DogrulanmisFis(fis.Id, Bulundu: true, SoftDeleteEdilmis: fis.IsDeleted,
                    Durum: fis.Durum, TesisId: fis.TesisId, MaliYil: fis.MaliYil, Donem: fis.Donem,
                    FisTarihi: fis.FisTarihi, BeklenenKasaBankaHesabiEtkilenmisMi: hesapEtkilenmis);
            }
        }

        var posValor = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belge.Id && v.TesisId == tesisId)
            .Select(v => new { v.Id, v.Durum, v.BeklenenValorTarihi, v.NetTutar })
            .FirstOrDefaultAsync(cancellationToken);
        if (posValor is not null)
        {
            dto.PosTahsilatValorId = posValor.Id;
            dto.PosValorDurumu = posValor.Durum;
            dto.PosBeklenenValorTarihi = posValor.BeklenenValorTarihi;
            dto.PosNetTutar = posValor.NetTutar;
        }

        // Bu odemenin URETTIGI cari hareket (kapama kaydi) - bakiyeye gercekten etki edip
        // etmediginin BIRINCIL kanitidir.
        var kapamaHareketi = await _dbContext.CariHareketler.AsNoTracking()
            .Where(h => !h.IsDeleted && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == belge.Id
                && h.CariKartId == belge.CariKartId)
            .Select(h => new { h.Id, h.Durum, h.BorcTutari, h.AlacakTutari, h.ParaBirimi, h.IliskiliCariHareketId })
            .FirstOrDefaultAsync(cancellationToken);

        dto.KapatildiMi = belge.KapatilacakCariHareketId.HasValue && kapamaHareketi is not null;

        var rezervasyonOdeme = await _dbContext.RezervasyonOdemeler.AsNoTracking()
            .Where(r => r.TahsilatOdemeBelgesiId == belge.Id)
            .Select(r => new { r.RezervasyonId, ReferansNo = r.Rezervasyon != null ? r.Rezervasyon.ReferansNo : null })
            .FirstOrDefaultAsync(cancellationToken);
        if (rezervasyonOdeme is not null)
        {
            dto.RezervasyonId = rezervasyonOdeme.RezervasyonId;
            dto.RezervasyonReferansNo = rezervasyonOdeme.ReferansNo;
        }

        // ── Bakiyeye gercek mali etki (yalnizca belgenin Durum'una BAKILMAZ) ──
        var nedenKodlari = new List<string>();
        var nedenAciklamalari = new List<string>();

        if (belge.Durum != TahsilatOdemeBelgeDurumlari.Aktif)
        {
            nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.OdemeIptalEdilmis);
            nedenAciklamalari.Add("Ödeme iptal edilmiş; hiçbir bakiye hesabına dahil edilmez.");
        }
        else
        {
            if (kapamaHareketi is null)
            {
                nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.CariHareketiYok);
                nedenAciklamalari.Add(belge.KapatilacakCariHareketId.HasValue
                    ? "Ödeme bir borcu kapatmak üzere işaretlenmiş ancak karşılık gelen cari hareket oluşmamış; cari bakiyeyi etkilemiyor."
                    : "Ödemeye ait bir cari hareket bulunamadı; cari bakiyeyi etkilemiyor (ör. avans olarak kaydedilmiş olabilir).");
            }
            else if (kapamaHareketi.Durum != CariHareketDurumlari.Aktif)
            {
                nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.CariHareketiIptalEdilmis);
                nedenAciklamalari.Add("Ödemenin oluşturduğu cari hareket iptal edilmiş; cari bakiyeyi etkilemiyor.");
            }

            // Nakit hareketi doguran odeme yontemlerinde muhasebe fisi beklenir.
            var fisZorunlu = OdemeYontemleri.NakitHareketiGerektirenler.Contains(belge.OdemeYontemi);
            if (fisZorunlu && !belge.MuhasebeFisId.HasValue)
            {
                nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.ZorunluMuhasebeFisiYok);
                nedenAciklamalari.Add("Ödeme nakit/banka/POS hareketi doğurduğu hâlde bir muhasebe fişi üretilmemiş; muhasebe (kasa/banka) bakiyesini etkilemiyor.");
            }
            else if (belge.MuhasebeFisId.HasValue)
            {
                // Odeme belgesinin TesisId+BelgeTarihi'ne karsilik gelen GERCEK muhasebe donemi -
                // bagli fisin MaliYil/DonemNo/FisTarihi'nin bu donemle uyup uymadigini dogrulamak
                // icin kullanilir. Kapali/acik ayrimi KASITLI olarak burada uygulanmaz (bu kontrol
                // "hangi doneme ait olmasi gerekiyordu" sorusuna cevap arar, "donem su an acik mi"
                // sorusuna degil) - yalnizca tarih araligina gore eslesen donem aranir.
                var beklenenDonemKaydi = await _dbContext.MuhasebeDonemler.AsNoTracking()
                    .Where(d => !d.IsDeleted && d.TesisId == tesisId
                        && d.BaslangicTarihi <= belge.BelgeTarihi && d.BitisTarihi >= belge.BelgeTarihi)
                    .Select(d => new { d.MaliYil, d.DonemNo, d.BaslangicTarihi, d.BitisTarihi })
                    .FirstOrDefaultAsync(cancellationToken);

                if (beklenenDonemKaydi is null)
                {
                    // Beklenen donem BULUNAMADI - bu, fisin donem uyumunun (mali yil/donem/tarih
                    // araligi) GUVENILIR bicimde dogrulanamadigi anlamina gelir. "Donem bulunamadi"
                    // sessizce "gecerli" SAYILMAZ - ayri, acik bir neden koduyla bildirilir.
                    nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.MuhasebeDonemiBulunamadi);
                    nedenAciklamalari.Add(
                        "Ödeme tarihine karşılık gelen bir muhasebe dönemi tanımı bulunamadı; bağlı fişin mali yıl/dönem/tarih uyumu doğrulanamadı.");
                }

                // ID'nin dolu olmasi TEK BASINA yeterli DEGILDIR - fis gercekten var mi, silinmis mi,
                // mali etki olusturan durumda mi, dogru tesise mi ait, dogru kasa/banka hesabini mi
                // etkiliyor, dogru mali yil/donem ve donem tarih araligina mi denk geliyor: hepsi
                // dogrulanir.
                var fisSonucu = MuhasebeFisDogrulama.Degerlendir(
                    dogrulanmisFis,
                    beklenenTesisId: tesisId,
                    donemBaslangic: beklenenDonemKaydi?.BaslangicTarihi,
                    donemBitis: beklenenDonemKaydi?.BitisTarihi,
                    kasaBankaHesabiKontrolEdilsinMi: belge.KasaBankaHesapId.HasValue,
                    beklenenMaliYil: beklenenDonemKaydi?.MaliYil,
                    beklenenDonem: beklenenDonemKaydi?.DonemNo);

                if (!fisSonucu.GecerliMi)
                {
                    nedenKodlari.AddRange(fisSonucu.NedenKodlari);
                    nedenAciklamalari.AddRange(fisSonucu.Aciklamalar);
                }
            }

            // Kredi karti tahsilatinda POS valor zinciri gereklidir.
            if (belge.OdemeYontemi == OdemeYontemleri.KrediKarti)
            {
                if (posValor is null)
                {
                    nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.PosValorKaydiYok);
                    nedenAciklamalari.Add("Kredi kartı tahsilatı olduğu hâlde POS valör takip kaydı yok; bankaya aktarım izlenemiyor.");
                }
                else if (posValor.Durum != PosTahsilatValorDurumlari.Aktarildi)
                {
                    nedenKodlari.Add(BakiyeyeDahilEdilmemeNedenKodlari.PosValorHenuzAktarilmamis);
                    nedenAciklamalari.Add($"POS valör kaydı '{posValor.Durum}' durumunda; tutar henüz banka hesabına aktarılmamış.");
                }
            }
        }

        var cariEtkisiVar = belge.Durum == TahsilatOdemeBelgeDurumlari.Aktif
            && kapamaHareketi is not null && kapamaHareketi.Durum == CariHareketDurumlari.Aktif;

        dto.BakiyeyeDahilMi = cariEtkisiVar;
        dto.BakiyeyeDahilEdilmeDurumu = belge.Durum != TahsilatOdemeBelgeDurumlari.Aktif
            ? BakiyeyeDahilEdilmeDurumlari.DahilDegil
            : nedenKodlari.Count == 0
                ? BakiyeyeDahilEdilmeDurumlari.TamamenDahil
                : cariEtkisiVar
                    ? BakiyeyeDahilEdilmeDurumlari.KismenDahil
                    : BakiyeyeDahilEdilmeDurumlari.DahilDegil;
        dto.BakiyeyeDahilEdilmemeNedenKodlari = nedenKodlari;
        dto.BakiyeyeDahilEdilmemeAciklamalari = nedenAciklamalari;

        if (kapamaHareketi is not null)
        {
            dto.EtkiledigiTutar = kapamaHareketi.BorcTutari - kapamaHareketi.AlacakTutari;
            dto.EtkiledigiParaBirimi = string.IsNullOrWhiteSpace(kapamaHareketi.ParaBirimi) ? null : kapamaHareketi.ParaBirimi;
            dto.EtkiledigiCariVeyaBorc = kapamaHareketi.IliskiliCariHareketId.HasValue
                ? $"{dto.CariUnvan} - kapatılan cari hareket #{kapamaHareketi.IliskiliCariHareketId}"
                : dto.CariUnvan;
        }

        // ONEMLI (madde 9): dto.Uyarilar bu noktada zaten yetki/erisim uyarilari (BagliHesapErisimKisitliMi/
        // BagliFisErisimKisitliMi) TASIYOR OLABILIR - dogrudan ATAMA yaparsak bu guvenlik uyarilari
        // SESSIZCE KAYBOLUR. Bunun yerine BIRLESTIRILIR, ayni UyariTipi icin MUKERRER eklenmez,
        // ve sira KARARLIDIR (once mevcut/guvenlik uyarilari, sonra BuildUyarilarAsync sonuclari).
        var ekUyarilar = await BuildUyarilarAsync(belge.Id, belge.BelgeNo, belge.BelgeTarihi, belge.Tutar, belge.Durum, belge.OdemeYontemi,
            belge.ParaBirimi, belge.KasaBankaHesapId, belge.CariKartId, belge.MuhasebeFisId, belge.KapatilacakCariHareketId, tesisId, cancellationToken);

        var gorulenUyariTipleri = dto.Uyarilar.Select(u => u.UyariTipi).ToHashSet();
        foreach (var uyari in ekUyarilar)
        {
            if (gorulenUyariTipleri.Add(uyari.UyariTipi))
            {
                dto.Uyarilar.Add(uyari);
            }
        }

        return dto;
    }

    public async Task<CariHareketDokumDto> GetCariHareketDokumuAsync(CariHareketDokumFilterDto filter, CancellationToken cancellationToken = default)
    {
        var cari = await _dbContext.CariKartlar.AsNoTracking()
            .Where(c => !c.IsDeleted && c.Id == filter.CariKartId)
            .Select(c => new { c.Id, c.UnvanAdSoyad, c.TesisId, c.AcilisBakiyeTutari, c.AcilisBakiyeYonu })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException("Cari kart bulunamadı.", 404);

        await _tesisScopeService.EnsureCanAccessTesisAsync(cari.TesisId ?? throw new BaseException("Carinin tesisi belirlenemedi.", 400), cancellationToken);

        var baslangicUst = filter.TarihBaslangic?.ToDateTime(TimeOnly.MinValue);
        var bitisUst = filter.TarihBitis?.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var hareketQuery = _dbContext.CariHareketler.AsNoTracking().Where(h => !h.IsDeleted && h.CariKartId == cari.Id);
        if (baslangicUst.HasValue)
        {
            hareketQuery = hareketQuery.Where(h => h.HareketTarihi >= baslangicUst.Value);
        }
        if (bitisUst.HasValue)
        {
            hareketQuery = hareketQuery.Where(h => h.HareketTarihi < bitisUst.Value);
        }

        var hareketler = await hareketQuery
            .OrderBy(h => h.HareketTarihi).ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.HareketTarihi, h.BelgeTuru, h.BelgeNo, h.Aciklama, h.BorcTutari, h.AlacakTutari, h.KalanTutar, h.Durum, h.KaynakModul, h.KaynakId, h.KapandiMi, h.ParaBirimi })
            .ToListAsync(cancellationToken);

        // DEVREDEN BAKIYE: tarih araligi baslangicindan ONCEKI aktif hareketlerin net etkisi.
        // Bu hareketler donem icinde gosterilmez ama bakiyeye GERCEKTEN dahildir - yok sayilirsa
        // acilis bakiyesinden baslamak yanlis bir kalan bakiye uretirdi.
        var devredenler = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (baslangicUst.HasValue)
        {
            var oncekiler = await _dbContext.CariHareketler.AsNoTracking()
                .Where(h => !h.IsDeleted && h.CariKartId == cari.Id
                    && h.HareketTarihi < baslangicUst.Value
                    && h.Durum == CariHareketDurumlari.Aktif)
                .GroupBy(h => h.ParaBirimi)
                .Select(g => new { ParaBirimi = g.Key, Net = g.Sum(x => x.BorcTutari - x.AlacakTutari) })
                .ToListAsync(cancellationToken);

            foreach (var o in oncekiler)
            {
                devredenler[NormalizeParaBirimi(o.ParaBirimi)] = o.Net;
            }
        }

        var dto = new CariHareketDokumDto
        {
            CariKartId = cari.Id,
            CariUnvan = cari.UnvanAdSoyad,
            AcilisBakiyeTutari = cari.AcilisBakiyeTutari ?? 0m,
            AcilisBakiyeYonu = cari.AcilisBakiyeYonu,
            TarihBaslangic = filter.TarihBaslangic,
            TarihBitis = filter.TarihBitis
        };

        // Para birimi bazinda AYRI kumulatif bakiye - farkli para birimleri ASLA tek toplamda
        // birlestirilmez (kur donusum altyapisi yok).
        var acilisNet = cari.AcilisBakiyeYonu == CariKartAcilisBakiyeYonleri.Alacak
            ? -(cari.AcilisBakiyeTutari ?? 0m)
            : (cari.AcilisBakiyeTutari ?? 0m);

        var ozetler = new Dictionary<string, CariBakiyeParaBirimiOzetiDto>(StringComparer.OrdinalIgnoreCase);
        var kumulatifler = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);

        CariBakiyeParaBirimiOzetiDto OzetAl(string paraBirimi)
        {
            if (!ozetler.TryGetValue(paraBirimi, out var o))
            {
                // Acilis bakiyesinin para birimi CariKart'ta ayrica TUTULMUYOR ve kurum/tesis
                // seviyesinde guvenilir bir "baz para birimi" ayari da YOK (bkz. arastirma - Tesis/
                // Kurum/CariKart entity'lerinde boyle bir alan bulunmuyor). Bu yuzden TRY VARSAYILMAZ:
                // acilis bakiyesi yalnizca "Bilinmiyor" grubuna yazilir, normal/TRY toplamlarina
                // KATILMAZ (madde 8).
                var acilis = string.Equals(paraBirimi, BilinmeyenParaBirimi, StringComparison.OrdinalIgnoreCase) ? acilisNet : 0m;
                var devreden = acilis + devredenler.GetValueOrDefault(paraBirimi);

                o = new CariBakiyeParaBirimiOzetiDto { ParaBirimi = paraBirimi, DevredenBakiye = devreden };
                ozetler[paraBirimi] = o;
                kumulatifler[paraBirimi] = devreden;
            }
            return o;
        }

        // Donem oncesi hareketi olan ama donem ICINDE hic hareketi olmayan para birimleri de
        // ozette gorunmeli (devreden bakiyeleri sifir degil).
        foreach (var pbKey in devredenler.Keys)
        {
            OzetAl(pbKey);
        }

        // Acilis bakiyesi varsa ve carinin hic hareketi/devreden kaydi yoksa bile "Bilinmiyor"
        // grubu OLUSTURULMALIDIR - aksi halde acilis bakiyesi hicbir ozette gorunmeden KAYBOLUR.
        if (acilisNet != 0m)
        {
            OzetAl(BilinmeyenParaBirimi);
            dto.Uyarilar.Add(
                "Cari kartın açılış bakiyesi için güvenilir bir para birimi kaydı yok (kart üzerinde ayrı bir para " +
                "birimi alanı, kurum/tesis bazında güvenilir bir baz para birimi ayarı da bulunmuyor); TRY varsayılmadı, " +
                "'Bilinmiyor' grubunda ayrı gösterildi ve normal/TRY toplamlarına dahil edilmedi.");
        }

        foreach (var h in hareketler)
        {
            // Bos para birimi TRY VARSAYILMAZ - "Bilinmiyor" olarak ayri izlenir.
            var pb = NormalizeParaBirimi(h.ParaBirimi);
            var ozet = OzetAl(pb);
            var hesaplamaDisi = h.Durum != CariHareketDurumlari.Aktif;

            if (!hesaplamaDisi)
            {
                kumulatifler[pb] += h.BorcTutari - h.AlacakTutari;
                ozet.ToplamBorc += h.BorcTutari;
                ozet.ToplamAlacak += h.AlacakTutari;
            }
            else
            {
                ozet.IptalEdilmisTutar += h.BorcTutari + h.AlacakTutari;
            }

            dto.Hareketler.Add(new CariHareketDokumSatiriDto
            {
                Id = h.Id,
                HareketTarihi = h.HareketTarihi,
                BelgeTuru = h.BelgeTuru,
                BelgeNo = h.BelgeNo,
                Aciklama = h.Aciklama,
                BorcTutari = h.BorcTutari,
                AlacakTutari = h.AlacakTutari,
                KalanTutar = h.KalanTutar,
                Durum = h.Durum,
                KaynakModul = h.KaynakModul,
                KaynakId = h.KaynakId,
                KapandiMi = h.KapandiMi,
                ParaBirimi = pb,
                KumulatifBakiye = kumulatifler[pb],
                HesaplamaDisiMi = hesaplamaDisi
            });
        }

        // Bu cariye ait POS tutarlari - DURUM BAZINDA AYRI (normal bekleyen, mutabakat ve hata
        // BIRLESTIRILMEZ) ve para birimi bazinda. Bunlar cari bakiyesine OTOMATIK EKLENMEZ; yalnizca
        // farkin nereden gelebilecegini aciklamak icin gosterilir.
        //
        // TARIH KAPSAMI: POS kayitlari, cari hareketlerle AYNI tarih araligini kullanmalidir. Bir
        // POS valorunun cari bakiyesini etkileyen olayi, kaynak TAHSILAT belgesinin tarihidir
        // (TahsilatOdemeBelgesi.BelgeTarihi) - cari hareket de bu belgeden dogar. Bu nedenle
        // filtreleme belge tarihine gore yapilir; OdemeTarihi/BeklenenValorTarihi/AktarimTarihi
        // birbirinin yerine KULLANILMAZ.
        var posQuery =
            from v in _dbContext.PosTahsilatValorleri.AsNoTracking()
            join b in _dbContext.TahsilatOdemeBelgeleri.AsNoTracking() on v.TahsilatOdemeBelgesiId equals b.Id
            where !v.IsDeleted && !b.IsDeleted && b.CariKartId == cari.Id
            select new { v.Durum, v.ParaBirimi, v.NetTutar, BelgeTarihi = (DateTime?)b.BelgeTarihi };

        if (baslangicUst.HasValue)
        {
            posQuery = posQuery.Where(x => x.BelgeTarihi >= baslangicUst.Value);
        }
        if (bitisUst.HasValue)
        {
            posQuery = posQuery.Where(x => x.BelgeTarihi < bitisUst.Value);
        }

        var posTutarlari = await posQuery
            .GroupBy(x => new { x.Durum, x.ParaBirimi })
            .Select(g => new { g.Key.Durum, g.Key.ParaBirimi, Toplam = g.Sum(x => x.NetTutar) })
            .ToListAsync(cancellationToken);

        foreach (var p in posTutarlari)
        {
            var pb = NormalizeParaBirimi(p.ParaBirimi);
            var ozet = OzetAl(pb);
            switch (p.Durum)
            {
                case PosTahsilatValorDurumlari.ValorBekliyor:
                    ozet.NormalAktarilmayiBekleyenPos += p.Toplam;
                    break;
                case PosTahsilatValorDurumlari.MutabakatBekliyor:
                    ozet.MutabakatBekleyenPos += p.Toplam;
                    break;
                case PosTahsilatValorDurumlari.Hata:
                    ozet.HataliPos += p.Toplam;
                    break;
                case PosTahsilatValorDurumlari.Aktariliyor:
                case PosTahsilatValorDurumlari.TersKayitOlusturuluyor:
                    ozet.AktarimSurecindekiPos += p.Toplam;
                    break;
                // Aktarildi / Iptal / AktarimFisiIptalEdildi: bakiye aciklamasina ayri bir kalem
                // olarak GIRMEZ (aktarilmis tutar zaten muhasebe tarafinda, iptal edilmis tutar ise
                // hicbir yerde sayilmaz).
            }
        }

        // Tarih araligi verildiginde, kaynak belge tarihi BELIRSIZ olan POS kayitlari doneme
        // SESSIZCE katilmaz; ayrica uyari olarak raporlanir.
        if (baslangicUst.HasValue || bitisUst.HasValue)
        {
            var tarihsizPos = await (
                from v in _dbContext.PosTahsilatValorleri.AsNoTracking()
                join b in _dbContext.TahsilatOdemeBelgeleri.AsNoTracking() on v.TahsilatOdemeBelgesiId equals b.Id
                where !v.IsDeleted && !b.IsDeleted && b.CariKartId == cari.Id && b.BelgeTarihi == default
                group v by v.ParaBirimi into g
                select new { ParaBirimi = g.Key, Toplam = g.Sum(x => x.NetTutar), Adet = g.Count() })
                .ToListAsync(cancellationToken);

            foreach (var t in tarihsizPos)
            {
                var pb = NormalizeParaBirimi(t.ParaBirimi);
                OzetAl(pb).DonemeKatilmayanBelirsizTarihliPos += t.Toplam;
                dto.Uyarilar.Add(
                    $"{t.Adet} adet POS kaydının kaynak belge tarihi tanımsız ({pb} {t.Toplam:N2}); tarih aralığına güvenilir biçimde dahil edilemediği için dönem toplamlarına katılmadı.");
            }
        }

        foreach (var (pb, ozet) in ozetler)
        {
            ozet.AciklananKalanBakiye = kumulatifler[pb];
        }

        if (ozetler.ContainsKey(BilinmeyenParaBirimi))
        {
            dto.Uyarilar.Add("Para birimi tanımsız kayıtlar bulundu; bunlar TRY varsayılmadı, ayrı 'Bilinmiyor' grubunda gösterildi.");
        }

        dto.ParaBirimiOzetleri = [.. ozetler.Values.OrderBy(x => x.ParaBirimi)];
        return dto;
    }

    /// <summary>Para birimi tanimsiz kayitlarin etiketi - TRY VARSAYILMAZ.</summary>
    private const string BilinmeyenParaBirimi = "Bilinmiyor";

    /// <summary>Bos para birimi TRY VARSAYILMAZ - ayri "Bilinmiyor" etiketiyle izlenir.</summary>
    private static string NormalizeParaBirimi(string? paraBirimi) =>
        string.IsNullOrWhiteSpace(paraBirimi) ? BilinmeyenParaBirimi : paraBirimi.Trim().ToUpperInvariant();

    public async Task<List<BeyanEdilenOdemeEslesmeDto>> KarsilastirAsync(BeyanEdilenOdemeKarsilastirmaFilterDto filter, CancellationToken cancellationToken = default)
    {
        // Cok kisa bir referansla genis ve hassas arama yapilmasini engelle - kisa metin hem
        // anlamsiz derecede genis sonuc uretir hem de baska carilerin odemelerinin taranmasina
        // yol acar.
        if (!string.IsNullOrWhiteSpace(filter.BelgeNoTahmini)
            && (NormalizeReferans(filter.BelgeNoTahmini)?.Length ?? 0) < BeyanEdilenOdemeKarsilastirmaFilterDto.MinimumReferansUzunlugu)
        {
            throw new BaseException(
                $"Belge/dekont numarası araması için en az {BeyanEdilenOdemeKarsilastirmaFilterDto.MinimumReferansUzunlugu} karakter girilmelidir.", 400);
        }

        if (filter.Tutar <= 0m)
        {
            throw new BaseException("Karşılaştırma için geçerli bir tutar girilmelidir.", 400);
        }

        var tesisIds = await ResolveTesisIdsAsync(filter.TesisId, cancellationToken);
        if (tesisIds.Count == 0)
        {
            return [];
        }

        var altSinir = filter.Tarih.AddDays(-Math.Abs(filter.TarihToleransGun)).ToDateTime(TimeOnly.MinValue);
        var ustSinir = filter.Tarih.AddDays(Math.Abs(filter.TarihToleransGun) + 1).ToDateTime(TimeOnly.MinValue);

        var adaylar = await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.CariKart != null && b.CariKart.TesisId.HasValue && tesisIds.Contains(b.CariKart.TesisId.Value)
                && b.BelgeTarihi >= altSinir && b.BelgeTarihi < ustSinir
                && b.Tutar == filter.Tutar
                && b.ParaBirimi == filter.ParaBirimi
                && (!filter.CariKartId.HasValue || b.CariKartId == filter.CariKartId.Value))
            .Select(b => new
            {
                b.Id, b.BelgeNo, b.BelgeTarihi, b.Tutar, b.ParaBirimi, b.OdemeYontemi, b.KasaBankaHesapId,
                CariUnvan = b.CariKart != null ? b.CariKart.UnvanAdSoyad : string.Empty
            })
            .ToListAsync(cancellationToken);

        var beyanNormalize = NormalizeReferans(filter.BelgeNoTahmini);

        // TEKILLIK DOGRULAMASI: normalize edilmis referans yetki kapsaminda BIRDEN FAZLA kayda
        // karsilik geliyorsa hicbiri "Kesin" sayilamaz (hangisinin dogru oldugu bilinemez).
        var referansTekilMi = true;
        if (beyanNormalize is not null)
        {
            var ayniReferansliAdaylar = adaylar
                .Count(a => string.Equals(NormalizeReferans(a.BelgeNo), beyanNormalize, StringComparison.Ordinal));

            if (ayniReferansliAdaylar <= 1)
            {
                // Aday listesi tarih/tutar ile daralmis oldugundan, tekilligi TUM yetki kapsaminda
                // (tarih/tutar filtresi olmadan) ayrica dogrula - ayni belge no baska tarih/tutarla
                // da bulunuyorsa referans benzersiz DEGILDIR.
                var kapsamdakiToplam = await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                    .Where(b => !b.IsDeleted && b.CariKart != null && b.CariKart.TesisId.HasValue
                        && tesisIds.Contains(b.CariKart.TesisId.Value))
                    .Select(b => b.BelgeNo)
                    .Where(no => no != null)
                    .ToListAsync(cancellationToken);

                ayniReferansliAdaylar = kapsamdakiToplam
                    .Count(no => string.Equals(NormalizeReferans(no), beyanNormalize, StringComparison.Ordinal));
            }

            referansTekilMi = ayniReferansliAdaylar <= 1;
        }

        var sonuc = new List<BeyanEdilenOdemeEslesmeDto>();
        foreach (var a in adaylar)
        {
            var eslesen = new List<string> { "Tutar", "Para birimi" };
            var uyusmayan = new List<string>();

            var tarihFarki = Math.Abs((a.BelgeTarihi.Date - filter.Tarih.ToDateTime(TimeOnly.MinValue)).Days);
            var tarihBirebir = tarihFarki == 0;
            eslesen.Add(tarihBirebir ? "Tarih (birebir)" : $"Tarih (±{tarihFarki} gün tolerans)");

            // KESIN eslesme YALNIZCA benzersiz referansin BIREBIR (normalize edilmis tam esitlik)
            // eslesmesiyle uretilir. Contains/kismi metin KESIN eslesme URETMEZ - kisa bir metin
            // cok sayida belgeyi yanlislikla "kesin" hale getirirdi.
            var referansBirebirEslesti = beyanNormalize is not null
                && string.Equals(NormalizeReferans(a.BelgeNo), beyanNormalize, StringComparison.Ordinal);

            var yontemVerildi = !string.IsNullOrWhiteSpace(filter.OdemeYontemi);
            var hesapVerildi = filter.KasaBankaHesapId.HasValue;
            var yontemEslesiyor = !yontemVerildi || a.OdemeYontemi == filter.OdemeYontemi;
            var hesapEslesiyor = !hesapVerildi || a.KasaBankaHesapId == filter.KasaBankaHesapId!.Value;

            if (yontemVerildi)
            {
                (yontemEslesiyor ? eslesen : uyusmayan).Add("Ödeme yöntemi");
            }
            if (hesapVerildi)
            {
                (hesapEslesiyor ? eslesen : uyusmayan).Add("Kasa/banka hesabı");
            }
            if (beyanNormalize is not null)
            {
                (referansBirebirEslesti ? eslesen : uyusmayan).Add("Belge/dekont no");
            }
            else
            {
                uyusmayan.Add("Belge/dekont no (beyan edilmedi, doğrulanamadı)");
            }

            // Yontem/hesap acikca verilmis ama UYUSMUYORSA bu aday zaten guclu bir karsit kanit
            // tasir - listeye alinsa bile en dusuk seviyede kalir.
            var celiskiVar = (yontemVerildi && !yontemEslesiyor) || (hesapVerildi && !hesapEslesiyor);

            string guven;
            string gerekce;

            if (referansBirebirEslesti && referansTekilMi && !celiskiVar)
            {
                guven = OdemeGuvenSeviyeleri.Kesin;
                gerekce = tarihBirebir
                    ? "Belge/dekont numarası birebir ve TEKİL olarak eşleşiyor; tutar, para birimi ve tarih de uyuşuyor."
                    : $"Belge/dekont numarası birebir ve TEKİL olarak eşleşiyor; tutar ve para birimi uyuşuyor. Tarih birebir DEĞİL, ±{tarihFarki} gün fark var.";
            }
            else if (referansBirebirEslesti && !referansTekilMi)
            {
                // Ayni normalize referansa birden fazla aday var - hangisinin dogru oldugu
                // bilinemeyeceginden KESIN eslesme URETILMEZ.
                guven = OdemeGuvenSeviyeleri.YuksekOlasilik;
                gerekce = "Belge/dekont numarası eşleşiyor ANCAK aynı numaraya sahip birden fazla kayıt bulunduğu için hangisinin doğru olduğu kesinleştirilemedi.";
                uyusmayan.Add("Referans tekilliği (aynı numaraya sahip birden fazla kayıt var)");
            }
            else if (referansBirebirEslesti && celiskiVar)
            {
                guven = OdemeGuvenSeviyeleri.YuksekOlasilik;
                gerekce = "Belge/dekont numarası eşleşiyor ANCAK beyan edilen ödeme yöntemi/hesabı bu kayıtla çelişiyor; kesin eşleşme sayılmadı.";
            }
            else if (!celiskiVar && (yontemVerildi || hesapVerildi))
            {
                guven = OdemeGuvenSeviyeleri.YuksekOlasilik;
                gerekce = "Tutar, para birimi, tarih aralığı ve ödeme yöntemi/hesabı birlikte uyuşuyor; ancak benzersiz bir referans (belge/dekont no) ile doğrulanmadığı için kesin eşleşme değildir.";
            }
            else
            {
                guven = OdemeGuvenSeviyeleri.IncelenmesiGereken;
                gerekce = celiskiVar
                    ? "Tutar ve tarih uyuşuyor fakat beyan edilen ödeme yöntemi/hesabı bu kayıtla ÇELİŞİYOR - aynı ödeme olmayabilir, incelenmelidir."
                    : "Yalnızca tutar, para birimi ve tarih uyuşuyor. Bu zayıf bir eşleşmedir; aynı ödeme olduğu KANITLANMAMIŞTIR.";
            }

            sonuc.Add(new BeyanEdilenOdemeEslesmeDto
            {
                OdemeId = a.Id,
                BelgeNo = a.BelgeNo,
                BelgeTarihi = a.BelgeTarihi,
                Tutar = a.Tutar,
                ParaBirimi = a.ParaBirimi,
                OdemeYontemi = a.OdemeYontemi,
                CariUnvan = a.CariUnvan,
                GuvenSeviyesi = guven,
                Gerekce = gerekce,
                EslesenAlanlar = eslesen,
                UyusmayanAlanlar = uyusmayan,
                TarihBirebirMi = tarihBirebir,
                TarihFarkiGun = tarihFarki,
                KullanilanNormalizasyon = beyanNormalize is null
                    ? "Referans verilmedi."
                    : "Harf/rakam dışındaki ayırıcı karakterler temizlendi, büyük harfe çevrildi; kısmi metin eşleşmesi KULLANILMADI.",
                ReferansTekilMi = beyanNormalize is null ? null : referansTekilMi
            });
        }

        return sonuc.OrderByDescending(x => x.GuvenSeviyesi == OdemeGuvenSeviyeleri.Kesin ? 2 : x.GuvenSeviyesi == OdemeGuvenSeviyeleri.YuksekOlasilik ? 1 : 0).ToList();
    }

    /// <summary>Referans karsilastirmasi icin guvenli normalizasyon: bosluklar ve ayirici isaretler
    /// atilir, buyuk harfe cevrilir. FARKLI gercek numaralari AYNI degere donusturmemek icin harf ve
    /// rakamlar KORUNUR (yalnizca ayirici karakterler temizlenir).</summary>
    private static string? NormalizeReferans(string? deger)
    {
        if (string.IsNullOrWhiteSpace(deger))
        {
            return null;
        }

        var temiz = new string([.. deger.Where(char.IsLetterOrDigit)]).ToUpperInvariant();
        return temiz.Length == 0 ? null : temiz;
    }

    // ─────────────────────────────────────────────────────────────
    // Yardimcilar
    // ─────────────────────────────────────────────────────────────

    private async Task<List<OdemeUyariDto>> BuildUyarilarAsync(
        int belgeId, string belgeNo, DateTime belgeTarihi, decimal tutar, string durum, string odemeYontemi, string paraBirimi,
        int? kasaBankaHesapId, int cariKartId, int? muhasebeFisId, int? kapatilacakCariHareketId, int tesisId, CancellationToken cancellationToken)
    {
        var uyarilar = new List<OdemeUyariDto>();
        var aktif = durum == TahsilatOdemeBelgeDurumlari.Aktif;

        // 1) Odeme var, fis yok.
        if (aktif && muhasebeFisId is null && OdemeYontemleri.NakitHareketiGerektirenler.Contains(odemeYontemi))
        {
            uyarilar.Add(new OdemeUyariDto
            {
                UyariTipi = OdemeUyariTipleri.OdemeVarFisYok,
                GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                Aciklama = "Ödeme aktif ve nakit hareketi gerektiren bir yöntemle yapılmış ancak bağlı bir muhasebe fişi yok."
            });
        }

        // 2) POS var, valor kaydi yok.
        if (aktif && odemeYontemi == OdemeYontemleri.KrediKarti)
        {
            var posVarMi = await _dbContext.PosTahsilatValorleri.AsNoTracking()
                .AnyAsync(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belgeId && v.TesisId == tesisId, cancellationToken);
            if (!posVarMi)
            {
                uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = OdemeUyariTipleri.PosVarValorYok,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                    Aciklama = "Ödeme yöntemi Kredi Kartı ancak POS valör takip kaydı bulunamadı."
                });
            }
        }

        // 3) Kapatma hedefi var ama kapanmamis / 4) Iptal ama kapama geri alinmamis.
        if (kapatilacakCariHareketId.HasValue)
        {
            var kapamaHareketi = await _dbContext.CariHareketler.AsNoTracking()
                .Where(h => !h.IsDeleted && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == belgeId
                    && h.CariKartId == cariKartId)
                .Select(h => new { h.Id, h.Durum })
                .FirstOrDefaultAsync(cancellationToken);

            if (aktif && kapamaHareketi is null)
            {
                uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = OdemeUyariTipleri.KapatmaHedefiVarAmaKapanmamis,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                    Aciklama = "Ödeme bir borç kapatmak üzere işaretlenmiş ancak karşılık gelen kapama hareketi bulunamadı."
                });
            }
            else if (!aktif && kapamaHareketi is not null && kapamaHareketi.Durum == CariHareketDurumlari.Aktif)
            {
                uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = OdemeUyariTipleri.IptalAmaKapamaGeriAlinmamis,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                    Aciklama = "Ödeme iptal edilmiş ancak oluşturduğu cari hareket kapaması hâlâ aktif görünüyor.",
                    IliskiliBelgeId = kapamaHareketi.Id
                });
            }
        }

        // 5) Mukerrer belge no (ayni tesis kapsaminda).
        var mukerrer = await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
            .Where(b => !b.IsDeleted && b.Id != belgeId && b.BelgeNo == belgeNo
                && b.CariKart != null && b.CariKart.TesisId == tesisId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (mukerrer.HasValue)
        {
            uyarilar.Add(new OdemeUyariDto
            {
                UyariTipi = OdemeUyariTipleri.MukerrerBelgeNo,
                GuvenSeviyesi = OdemeGuvenSeviyeleri.Kesin,
                Aciklama = "Aynı tesiste aynı belge numarasına sahip başka bir ödeme kaydı var.",
                IliskiliBelgeId = mukerrer.Value
            });
        }

        // 6) Ayni tutar + ayni tarih + ayni banka/kasa, farkli cari (yalnizca tutara dayanmaz - banka/tarih de esler).
        if (kasaBankaHesapId.HasValue)
        {
            var gunBaslangic = belgeTarihi.Date;
            var gunBitis = gunBaslangic.AddDays(1);
            var benzer = await _dbContext.TahsilatOdemeBelgeleri.AsNoTracking()
                .Where(b => !b.IsDeleted && b.Id != belgeId && b.CariKartId != cariKartId
                    && b.KasaBankaHesapId == kasaBankaHesapId.Value && b.Tutar == tutar
                    && b.BelgeTarihi >= gunBaslangic && b.BelgeTarihi < gunBitis
                    && b.CariKart != null && b.CariKart.TesisId == tesisId)
                .Select(b => (int?)b.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (benzer.HasValue)
            {
                uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = OdemeUyariTipleri.AyniTutarAyniTarihFarkliCari,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.IncelenmesiGereken,
                    Aciklama = "Aynı gün, aynı tutar ve aynı hesapla başka bir cariye ait ödeme kaydı da var - yanlış cariye işlenmiş olabilir, incelenmesi önerilir.",
                    IliskiliBelgeId = benzer.Value
                });
            }
        }

        // 7) Para birimi tutarsizligi (POS valor kaydiyla).
        var posParaBirimi = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belgeId && v.TesisId == tesisId)
            .Select(v => v.ParaBirimi)
            .FirstOrDefaultAsync(cancellationToken);
        if (posParaBirimi is not null && !string.Equals(posParaBirimi, paraBirimi, StringComparison.OrdinalIgnoreCase))
        {
            uyarilar.Add(new OdemeUyariDto
            {
                UyariTipi = OdemeUyariTipleri.ParaBirimiTutarsizligi,
                GuvenSeviyesi = OdemeGuvenSeviyeleri.YuksekOlasilik,
                Aciklama = $"Ödemenin para birimi ({paraBirimi}) bağlı POS valör kaydının para biriminden ({posParaBirimi}) farklı."
            });
        }

        // 8) Farkli muhasebe donemine dusme.
        if (muhasebeFisId.HasValue)
        {
            var fisDonem = await _dbContext.MuhasebeFisler.AsNoTracking()
                .Where(f => f.Id == muhasebeFisId.Value)
                .Select(f => new { f.MaliYil, f.Donem })
                .FirstOrDefaultAsync(cancellationToken);
            if (fisDonem is not null && (fisDonem.MaliYil != belgeTarihi.Year || fisDonem.Donem != belgeTarihi.Month))
            {
                uyarilar.Add(new OdemeUyariDto
                {
                    UyariTipi = OdemeUyariTipleri.FarkliMuhasebeDonemineDusme,
                    GuvenSeviyesi = OdemeGuvenSeviyeleri.IncelenmesiGereken,
                    Aciklama = $"Ödeme tarihi ({belgeTarihi:yyyy-MM}) ile bağlı muhasebe fişinin dönemi ({fisDonem.MaliYil}-{fisDonem.Donem:00}) farklı."
                });
            }
        }

        return uyarilar;
    }

    private static string? MaskeleIban(string? iban)
    {
        if (string.IsNullOrWhiteSpace(iban) || iban.Length < 6)
        {
            return iban;
        }

        var bas = iban[..4];
        var son = iban[^2..];
        return $"{bas} **** **** **** **{son}";
    }

    private async Task<IReadOnlyList<int>> ResolveTesisIdsAsync(int? tesisId, CancellationToken cancellationToken)
    {
        if (tesisId.HasValue)
        {
            await _tesisScopeService.EnsureCanAccessTesisAsync(tesisId.Value, cancellationToken);
            return [tesisId.Value];
        }

        var effective = await _tesisScopeService.GetEffectiveTesisIdsAsync(cancellationToken);
        return effective;
    }
}
