using AutoMapper;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.MuhasebeDonemleri.Services;
using STYS.Muhasebe.MuhasebeFisleri.Entities;
using STYS.Muhasebe.MuhasebeHesapPlanlari.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Dtos;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using STYS.Tesisler.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.TahsilatOdemeBelgeleri.Services;

/// <summary>
/// TahsilatOdemeBelgesi'nden muhasebe fisi ureten orkestrator. SatisBelgesiMuhasebeFisService ile
/// ayni desen: cross-aggregate islem oldugu icin BaseRdbmsService'ten turemez, DbContext uzerinden
/// calisir. Kaynak modulden bagimsizdir (rezervasyon, kasa tahsilati, cari tahsilat ekrani vb.
/// herhangi bir TahsilatOdemeBelgesi icin cagrilabilir) ve HER ZAMAN ayri/manuel tetiklenir.
///
/// Muhasebe mantigi:
///   Borc  : secilen KasaBankaHesap'in muhasebe hesabi (Kasa/Banka/POS)
///   Alacak: Tesis.RezervasyonTahsilatAlacakHesapTipi konfigurasyonuna gore
///           - Cari       => CariKart.MuhasebeHesapPlaniId (varsayilan)
///           - AlinanAvans=> MuhasebeAnaHesapKodlari.AlinanSiparisAvanslari uzerinden coz
/// </summary>
public class TahsilatOdemeBelgesiMuhasebeFisService : ITahsilatOdemeBelgesiMuhasebeFisService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly IMuhasebeDonemService _muhasebeDonemService;

    public TahsilatOdemeBelgesiMuhasebeFisService(
        StysAppDbContext dbContext,
        IMapper mapper,
        IMuhasebeDonemService muhasebeDonemService)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _muhasebeDonemService = muhasebeDonemService;
    }

    public async Task<TahsilatOdemeBelgesiDto> FisOlusturAsync(int tahsilatOdemeBelgesiId, CancellationToken cancellationToken = default)
    {
        var belgeOnOkuma = await _dbContext.TahsilatOdemeBelgeleri
            .Include(x => x.CariKart)
            .Include(x => x.KasaBankaHesap)
            .FirstOrDefaultAsync(x => x.Id == tahsilatOdemeBelgesiId && !x.IsDeleted, cancellationToken);

        if (belgeOnOkuma is null)
            throw new BaseException("Tahsilat/odeme belgesi bulunamadi.", 404);

        if (belgeOnOkuma.Durum != TahsilatOdemeBelgeDurumlari.Aktif)
            throw new BaseException($"Belge 'Aktif' durumda degil. Mevcut durum: {belgeOnOkuma.Durum}", 400);

        await EnsureFisOlusturulabilirAsync(belgeOnOkuma, cancellationToken);

        if (belgeOnOkuma.Tutar <= 0)
            throw new BaseException("Belge tutari sifirdan buyuk olmalidir.", 400);

        if (!belgeOnOkuma.KasaBankaHesapId.HasValue || belgeOnOkuma.KasaBankaHesap is null)
            throw new BaseException("Fis uretimi icin belgede bir kasa/banka/POS hesabi tanimli olmalidir.", 400);

        if (belgeOnOkuma.CariKart is null)
            throw new BaseException("Belgede tanimli cari kart bulunamadi.", 404);

        // CariKart.MuhasebeHesapPlaniId sadece alacak hesabi "Cari" modundaysa zorunludur;
        // "AlinanAvans" modunda alacak hesabi MuhasebeAnaHesapKodlari.AlinanSiparisAvanslari
        // uzerinden cozulur ve cari kartin kendi hesap plani baglantisina ihtiyac duyulmaz
        // (bkz. ResolveAlacakHesabiAsync).

        if (!belgeOnOkuma.KasaBankaHesap.MuhasebeHesapPlaniId.HasValue)
            throw new BaseException("Belgedeki kasa/banka/POS hesabinin muhasebe hesap plani baglantisi yok.", 400);

        var tesisId = belgeOnOkuma.KasaBankaHesap.TesisId ?? belgeOnOkuma.CariKart.TesisId
            ?? throw new BaseException("Fis uretimi icin tesis belirlenemedi.", 400);

        var aktifDonemDto = await _muhasebeDonemService.GetAktifDonemAsync(tesisId, belgeOnOkuma.BelgeTarihi, cancellationToken);
        if (aktifDonemDto is null)
            throw new BaseException("Belge tarihi icin acik muhasebe donemi bulunamadi.", 400);

        const int maxRetry = 3;
        for (var attempt = 0; attempt < maxRetry; attempt++)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var belge = await _dbContext.TahsilatOdemeBelgeleri
                    .Include(x => x.CariKart)
                    .Include(x => x.KasaBankaHesap)
                    .FirstOrDefaultAsync(x => x.Id == tahsilatOdemeBelgesiId && !x.IsDeleted, cancellationToken);

                if (belge is null)
                    throw new BaseException("Tahsilat/odeme belgesi bulunamadi.", 404);

                await EnsureFisOlusturulabilirAsync(belge, cancellationToken);

                // MuhasebeFisService.IptalEtAsync ters kayit fisine de ayni KaynakModul/KaynakId'yi
                // kopyalar (bkz. o metodun 5. adimi) — ters kaydin kendisi Durum=TersKayit tasir,
                // Iptal degil. Bu yuzden yalnizca Taslak/Onayli (fiilen "acik/engelleyici" olan)
                // durumlar burada engelleyici sayilir; Iptal VE TersKayit ikisi de "serbest" demektir.
                var mevcutFis = await _dbContext.MuhasebeFisler
                    .Where(f => !f.IsDeleted
                                && f.KaynakModul == MuhasebeKaynakModulleri.TahsilatOdemeBelgesi
                                && f.KaynakId == tahsilatOdemeBelgesiId
                                && (f.Durum == MuhasebeFisDurumlari.Taslak || f.Durum == MuhasebeFisDurumlari.Onayli))
                    .Select(f => new { f.Id, f.FisNo })
                    .FirstOrDefaultAsync(cancellationToken);

                if (mevcutFis is not null)
                    throw new BaseException($"Bu belge icin zaten bir muhasebe fisi olusturulmus. Mevcut fis: {mevcutFis.FisNo}", 409);

                var borcHesap = await GetHesapPlaniByIdAsync(belge.KasaBankaHesap!.MuhasebeHesapPlaniId!.Value, cancellationToken);
                var alacakHesap = await ResolveAlacakHesabiAsync(tesisId, belge.CariKart!, cancellationToken);

                var satirlar = new List<MuhasebeFisSatir>
                {
                    new()
                    {
                        MuhasebeHesapPlaniId = borcHesap.Id,
                        SiraNo = 1,
                        Borc = belge.Tutar,
                        Alacak = 0m,
                        ParaBirimi = belge.ParaBirimi,
                        Kur = 1,
                        KasaBankaHesapId = belge.KasaBankaHesapId,
                        Aciklama = $"Tahsilat - {belge.BelgeNo}"
                    },
                    new()
                    {
                        MuhasebeHesapPlaniId = alacakHesap.Id,
                        SiraNo = 2,
                        Borc = 0m,
                        Alacak = belge.Tutar,
                        ParaBirimi = belge.ParaBirimi,
                        Kur = 1,
                        CariKartId = belge.CariKartId,
                        Aciklama = $"Tahsilat - {belge.BelgeNo}"
                    }
                };

                var fisNo = await GenerateFisNoAsync(tesisId, aktifDonemDto.MaliYil, cancellationToken);

                var fis = new MuhasebeFis
                {
                    TesisId = tesisId,
                    MaliYil = aktifDonemDto.MaliYil,
                    Donem = aktifDonemDto.DonemNo,
                    FisNo = fisNo,
                    FisTarihi = belge.BelgeTarihi,
                    FisTipi = MuhasebeFisTipleri.Tahsil,
                    KaynakModul = MuhasebeKaynakModulleri.TahsilatOdemeBelgesi,
                    KaynakId = belge.Id,
                    Durum = MuhasebeFisDurumlari.Taslak,
                    Aciklama = $"Tahsilat belgesi muhasebe fisi - {belge.BelgeNo}",
                    ToplamBorc = belge.Tutar,
                    ToplamAlacak = belge.Tutar,
                    Satirlar = satirlar
                };

                await _dbContext.MuhasebeFisler.AddAsync(fis, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);

                belge.MuhasebeFisId = fis.Id;
                belge.MuhasebeFisOlusturmaTarihi = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                return _mapper.Map<TahsilatOdemeBelgesiDto>(belge);
            }
            catch (DbUpdateException ex) when (IsUniqueConflict(ex) && attempt < maxRetry - 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                continue;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        throw new BaseException("Fis numarasi uretilemedi. Lutfen tekrar deneyiniz.", 500);
    }

    /// <summary>
    /// MuhasebeFisId'nin doluluguna degil, baglantili fisin GUNCEL durumuna gore karar verir —
    /// SatisBelgesiService.ThrowIfMuhasebeFisiIslemiEngellerAsync ile ayni "durum bazli, MuhasebeFisId
    /// hic sifirlanmaz" deseni (bkz. TahsilatOdemeBelgesiDto.MuhasebeFisDurumu). Fis iptal edilse bile
    /// (MuhasebeFisService.IptalEtAsync kaynak modulden bagimsiz calisir, TahsilatOdemeBelgesi'ye hic
    /// dokunmaz) MuhasebeFisId hep son baglanti fisi gosterir; "serbest mi" sorusu her zaman burada,
    /// canli fis durumu okunarak cevaplanir.
    /// </summary>
    private async Task EnsureFisOlusturulabilirAsync(TahsilatOdemeBelgesi belge, CancellationToken cancellationToken)
    {
        if (!belge.MuhasebeFisId.HasValue)
            return; // Fis yok -> serbest

        var fis = await _dbContext.MuhasebeFisler
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == belge.MuhasebeFisId.Value, cancellationToken);

        if (fis is null || fis.IsDeleted)
            throw new BaseException(
                "Belgeye bagli muhasebe fisi bulunamadi veya silinmis. Sistem yoneticinize basvurun.", 400);

        switch (fis.Durum)
        {
            case MuhasebeFisDurumlari.Iptal:
                return; // Ters kayit olusturulmus, muhasebe etkisi sifirlanmis -> serbest

            case MuhasebeFisDurumlari.Taslak:
            case MuhasebeFisDurumlari.Onayli:
                throw new BaseException(
                    $"Bu belge icin daha once muhasebe fisi olusturulmus (Fis No: {fis.FisNo}, Durum: {fis.Durum}).", 409);

            default:
                throw new BaseException(
                    $"Belgeye bagli muhasebe fisi beklenmeyen bir durumda ({fis.Durum}). Sistem yoneticinize basvurun.", 400);
        }
    }

    private async Task<MuhasebeHesapPlani> ResolveAlacakHesabiAsync(int tesisId, STYS.Muhasebe.CariKartlar.Entities.CariKart cariKart, CancellationToken cancellationToken)
    {
        var tesis = await _dbContext.Tesisler.AsNoTracking().FirstOrDefaultAsync(x => x.Id == tesisId, cancellationToken);
        var alacakHesapTipi = tesis?.RezervasyonTahsilatAlacakHesapTipi ?? RezervasyonTahsilatAlacakHesapTipleri.Cari;

        if (alacakHesapTipi == RezervasyonTahsilatAlacakHesapTipleri.AlinanAvans)
        {
            return await GetHesapPlaniByAnaKodAsync(MuhasebeAnaHesapKodlari.AlinanSiparisAvanslari, tesisId, cancellationToken);
        }

        if (!cariKart.MuhasebeHesapPlaniId.HasValue)
        {
            throw new BaseException("Belgedeki cari kartin muhasebe hesap plani baglantisi yok.", 400);
        }

        return await GetHesapPlaniByIdAsync(cariKart.MuhasebeHesapPlaniId.Value, cancellationToken);
    }

    private async Task<MuhasebeHesapPlani> GetHesapPlaniByIdAsync(int id, CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted && x.AktifMi && x.HareketGorebilirMi && x.DetayHesapMi, cancellationToken);

        return hesap ?? throw new BaseException("Hesap plani kaydi bulunamadi veya aktif/hareket gorebilir/detay hesap degil.", 400);
    }

    private async Task<MuhasebeHesapPlani> GetHesapPlaniByAnaKodAsync(string anaKod, int tesisId, CancellationToken cancellationToken)
    {
        var hesap = await _dbContext.MuhasebeHesapPlanlari
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.AktifMi
                        && x.HareketGorebilirMi
                        && x.DetayHesapMi
                        && (x.TesisId == tesisId || x.TesisId == null)
                        && (x.TamKod == anaKod || x.Kod == anaKod || x.AnaHesapKodu == anaKod || x.TamKod.StartsWith(anaKod + ".")))
            .OrderByDescending(x => x.TesisId == tesisId)
            .ThenBy(x => x.TamKod)
            .FirstOrDefaultAsync(cancellationToken);

        return hesap ?? throw new BaseException(
            $"{anaKod} hesabi bulunamadi. Lutfen hesap planinda bu koda sahip aktif ve hareket gorebilir bir detay hesap tanimlayin.", 400);
    }

    private async Task<string> GenerateFisNoAsync(int tesisId, int maliYil, CancellationToken cancellationToken)
    {
        var prefix = $"{maliYil}-THS-";

        var mevcutFisNolar = await _dbContext.MuhasebeFisler
            .Where(x => x.TesisId == tesisId && x.MaliYil == maliYil && !x.IsDeleted && x.FisNo.StartsWith(prefix))
            .Select(x => x.FisNo)
            .ToListAsync(cancellationToken);

        var maxSira = 0;
        foreach (var fisNo in mevcutFisNolar)
        {
            var siraStr = fisNo[prefix.Length..];
            if (int.TryParse(siraStr, out var sira) && sira > maxSira)
                maxSira = sira;
        }

        return $"{prefix}{(maxSira + 1):D6}";
    }

    private static bool IsUniqueConflict(DbUpdateException ex)
    {
        return ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2601 || sqlEx.Number == 2627);
    }
}
