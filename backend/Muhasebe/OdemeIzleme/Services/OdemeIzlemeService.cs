using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.CariHareketler.Entities;
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
            query = query.Where(b => b.KasaBankaHesapId == filter.KasaBankaHesapId.Value);
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
            var eslesenBelgeIdler = _dbContext.PosTahsilatValorleri.AsNoTracking()
                .Where(v => !v.IsDeleted && v.Durum == filter.ValorDurumu)
                .Select(v => v.TahsilatOdemeBelgesiId);
            query = query.Where(b => eslesenBelgeIdler.Contains(b.Id));
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
                KasaBankaHesapAdi = b.KasaBankaHesap != null ? b.KasaBankaHesap.Ad : null,
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
            : await _dbContext.PosTahsilatValorleri.AsNoTracking()
                .Where(v => !v.IsDeleted && krediKartiIdler.Contains(v.TahsilatOdemeBelgesiId))
                .Select(v => v.TahsilatOdemeBelgesiId)
                .Distinct()
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
            var hesap = await _dbContext.KasaBankaHesaplari.AsNoTracking()
                .Where(x => x.Id == belge.KasaBankaHesapId.Value)
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
        }

        if (belge.MuhasebeFisId.HasValue)
        {
            var fis = await _dbContext.MuhasebeFisler.AsNoTracking()
                .Where(f => f.Id == belge.MuhasebeFisId.Value)
                .Select(f => new { f.FisNo, f.FisTarihi, f.Durum })
                .FirstOrDefaultAsync(cancellationToken);
            if (fis is not null)
            {
                dto.MuhasebeFisNo = fis.FisNo;
                dto.MuhasebeFisTarihi = fis.FisTarihi;
                dto.MuhasebeFisDurumu = fis.Durum;
            }
        }

        var posValor = await _dbContext.PosTahsilatValorleri.AsNoTracking()
            .Where(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belge.Id)
            .Select(v => new { v.Id, v.Durum, v.BeklenenValorTarihi, v.NetTutar })
            .FirstOrDefaultAsync(cancellationToken);
        if (posValor is not null)
        {
            dto.PosTahsilatValorId = posValor.Id;
            dto.PosValorDurumu = posValor.Durum;
            dto.PosBeklenenValorTarihi = posValor.BeklenenValorTarihi;
            dto.PosNetTutar = posValor.NetTutar;
        }

        if (belge.KapatilacakCariHareketId.HasValue)
        {
            dto.KapatildiMi = await _dbContext.CariHareketler.AsNoTracking()
                .AnyAsync(h => !h.IsDeleted && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == belge.Id, cancellationToken);
        }

        var rezervasyonOdeme = await _dbContext.RezervasyonOdemeler.AsNoTracking()
            .Where(r => r.TahsilatOdemeBelgesiId == belge.Id)
            .Select(r => new { r.RezervasyonId, ReferansNo = r.Rezervasyon != null ? r.Rezervasyon.ReferansNo : null })
            .FirstOrDefaultAsync(cancellationToken);
        if (rezervasyonOdeme is not null)
        {
            dto.RezervasyonId = rezervasyonOdeme.RezervasyonId;
            dto.RezervasyonReferansNo = rezervasyonOdeme.ReferansNo;
        }

        dto.BakiyeyeDahilMi = belge.Durum == TahsilatOdemeBelgeDurumlari.Aktif;
        dto.BakiyeyeDahilDegilGerekcesi = dto.BakiyeyeDahilMi ? null : "Ödeme iptal edilmiş, bakiye hesaplarına dahil edilmez.";

        dto.Uyarilar = await BuildUyarilarAsync(belge.Id, belge.BelgeNo, belge.BelgeTarihi, belge.Tutar, belge.Durum, belge.OdemeYontemi,
            belge.ParaBirimi, belge.KasaBankaHesapId, belge.CariKartId, belge.MuhasebeFisId, belge.KapatilacakCariHareketId, tesisId, cancellationToken);

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

        var hareketQuery = _dbContext.CariHareketler.AsNoTracking().Where(h => !h.IsDeleted && h.CariKartId == cari.Id);
        if (filter.TarihBaslangic.HasValue)
        {
            hareketQuery = hareketQuery.Where(h => h.HareketTarihi >= filter.TarihBaslangic.Value.ToDateTime(TimeOnly.MinValue));
        }
        if (filter.TarihBitis.HasValue)
        {
            hareketQuery = hareketQuery.Where(h => h.HareketTarihi < filter.TarihBitis.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        }

        var hareketler = await hareketQuery
            .OrderBy(h => h.HareketTarihi).ThenBy(h => h.Id)
            .Select(h => new { h.Id, h.HareketTarihi, h.BelgeTuru, h.BelgeNo, h.Aciklama, h.BorcTutari, h.AlacakTutari, h.KalanTutar, h.Durum, h.KaynakModul, h.KaynakId, h.KapandiMi })
            .ToListAsync(cancellationToken);

        var acilisNet = cari.AcilisBakiyeYonu == "Alacak" ? -(cari.AcilisBakiyeTutari ?? 0m) : (cari.AcilisBakiyeTutari ?? 0m);
        var kumulatif = acilisNet;

        var dto = new CariHareketDokumDto
        {
            CariKartId = cari.Id,
            CariUnvan = cari.UnvanAdSoyad,
            AcilisBakiyeTutari = cari.AcilisBakiyeTutari ?? 0m,
            AcilisBakiyeYonu = cari.AcilisBakiyeYonu
        };

        foreach (var h in hareketler)
        {
            var hesaplamaDisi = h.Durum != CariHareketDurumlari.Aktif;
            if (!hesaplamaDisi)
            {
                kumulatif += h.BorcTutari - h.AlacakTutari;
                dto.ToplamBorc += h.BorcTutari;
                dto.ToplamAlacak += h.AlacakTutari;
            }
            else
            {
                dto.ToplamIptalEdilmisTutar += h.BorcTutari + h.AlacakTutari;
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
                KumulatifBakiye = kumulatif,
                HesaplamaDisiMi = hesaplamaDisi
            });
        }

        dto.KalanBakiye = kumulatif;

        // Bu cariye ait, henuz bankaya aktarilmamis (bekleyen) POS tahsilatlarinin toplami - bakiyeye
        // NEDEN dahil olmadigini/oldugunu ayri gostermek icin (otomatik olarak KalanBakiye'ye
        // EKLENMEZ, yalnizca bilgilendirme amaclidir).
        dto.AktarilmayiBekleyenPosTutari = await (
            from v in _dbContext.PosTahsilatValorleri.AsNoTracking()
            join b in _dbContext.TahsilatOdemeBelgeleri.AsNoTracking() on v.TahsilatOdemeBelgesiId equals b.Id
            where !v.IsDeleted && b.CariKartId == cari.Id
                && (v.Durum == PosTahsilatValorDurumlari.ValorBekliyor || v.Durum == PosTahsilatValorDurumlari.MutabakatBekliyor || v.Durum == PosTahsilatValorDurumlari.Hata)
            select v.NetTutar)
            .SumAsync(cancellationToken);

        return dto;
    }

    public async Task<List<BeyanEdilenOdemeEslesmeDto>> KarsilastirAsync(BeyanEdilenOdemeKarsilastirmaFilterDto filter, CancellationToken cancellationToken = default)
    {
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

        var sonuc = new List<BeyanEdilenOdemeEslesmeDto>();
        foreach (var a in adaylar)
        {
            string guven;
            string gerekce;

            var belgeNoEslesiyor = !string.IsNullOrWhiteSpace(filter.BelgeNoTahmini)
                && a.BelgeNo.Contains(filter.BelgeNoTahmini, StringComparison.OrdinalIgnoreCase);
            var yontemEslesiyor = string.IsNullOrWhiteSpace(filter.OdemeYontemi) || a.OdemeYontemi == filter.OdemeYontemi;
            var hesapEslesiyor = !filter.KasaBankaHesapId.HasValue || a.KasaBankaHesapId == filter.KasaBankaHesapId.Value;

            if (belgeNoEslesiyor)
            {
                guven = OdemeGuvenSeviyeleri.Kesin;
                gerekce = "Belge no, tarih, tutar ve para birimi birebir eşleşiyor.";
            }
            else if (yontemEslesiyor && hesapEslesiyor && (filter.OdemeYontemi != null || filter.KasaBankaHesapId != null))
            {
                guven = OdemeGuvenSeviyeleri.YuksekOlasilik;
                gerekce = "Tarih, tutar, para birimi ve ödeme yöntemi/hesabı eşleşiyor.";
            }
            else
            {
                guven = OdemeGuvenSeviyeleri.IncelenmesiGereken;
                gerekce = "Yalnızca tarih ve tutar eşleşiyor - ödeme yöntemi/hesap bilgisi doğrulanmadı, kesin eşleşme değildir.";
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
                Gerekce = gerekce
            });
        }

        return sonuc.OrderByDescending(x => x.GuvenSeviyesi == OdemeGuvenSeviyeleri.Kesin ? 2 : x.GuvenSeviyesi == OdemeGuvenSeviyeleri.YuksekOlasilik ? 1 : 0).ToList();
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
            var posVarMi = await _dbContext.PosTahsilatValorleri.AsNoTracking().AnyAsync(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belgeId, cancellationToken);
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
                .Where(h => !h.IsDeleted && h.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi && h.KaynakId == belgeId)
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
            .Where(v => !v.IsDeleted && v.TahsilatOdemeBelgesiId == belgeId)
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
