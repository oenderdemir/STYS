using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Common.Constants;
using STYS.Muhasebe.Common.Services;
using STYS.Muhasebe.KasaBankaHesaplari.Entities;
using STYS.Muhasebe.MuhasebeFisleri.Services;
using STYS.Muhasebe.PosTahsilatValorleri.Entities;
using STYS.Muhasebe.TahsilatOdemeBelgeleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

public class PosTahsilatValorSnapshotService : IPosTahsilatValorSnapshotService
{
    private readonly StysAppDbContext _dbContext;
    private readonly IValorTarihHesaplamaService _valorTarihHesaplamaService;
    private readonly IMuhasebeFisService _muhasebeFisService;

    public PosTahsilatValorSnapshotService(
        StysAppDbContext dbContext,
        IValorTarihHesaplamaService valorTarihHesaplamaService,
        IMuhasebeFisService muhasebeFisService)
    {
        _dbContext = dbContext;
        _valorTarihHesaplamaService = valorTarihHesaplamaService;
        _muhasebeFisService = muhasebeFisService;
    }

    public async Task<PosTahsilatValor?> OlusturSnapshotAsync(TahsilatOdemeBelgesi belge, CancellationToken cancellationToken = default)
    {
        if (belge.OdemeYontemi != OdemeYontemleri.KrediKarti || !belge.KasaBankaHesapId.HasValue)
        {
            return null;
        }

        var krediKartiHesap = await _dbContext.KasaBankaHesaplari
            .FirstOrDefaultAsync(x => x.Id == belge.KasaBankaHesapId.Value && !x.IsDeleted, cancellationToken);

        if (krediKartiHesap is null || krediKartiHesap.Tip != KasaBankaHesapTipleri.KrediKarti)
        {
            return null;
        }

        var tesisId = krediKartiHesap.TesisId
            ?? throw new BaseException("Kredi kartı/POS hesabının tesisi belirlenemedi, valör kaydı oluşturulamadı.", 400);

        var odemeTarihi = DateOnly.FromDateTime(belge.BelgeTarihi);
        var beklenenValorTarihi = _valorTarihHesaplamaService.HesaplaValorTarihi(odemeTarihi, krediKartiHesap.ValorGunSayisi, krediKartiHesap.ValorGunTuru);

        var brut = belge.Tutar;
        decimal komisyon;
        decimal net;
        string durum;

        if (krediKartiHesap.KomisyonOrani.HasValue)
        {
            komisyon = ParaTutarYuvarlamaHelper.Yuvarla(brut * krediKartiHesap.KomisyonOrani.Value / 100m);
            net = brut - komisyon;
            durum = PosTahsilatValorDurumlari.ValorBekliyor;
        }
        else
        {
            komisyon = 0m;
            net = brut;
            durum = krediKartiHesap.ValorGunundeOtomatikHesabaAktarMi
                ? PosTahsilatValorDurumlari.MutabakatBekliyor
                : PosTahsilatValorDurumlari.ValorBekliyor;
        }

        var valor = new PosTahsilatValor
        {
            TesisId = tesisId,
            TahsilatOdemeBelgesiId = belge.Id,
            KrediKartiHesapId = krediKartiHesap.Id,
            BagliBankaHesapId = krediKartiHesap.BagliBankaHesapId,
            KomisyonGiderHesapPlaniId = krediKartiHesap.KomisyonGiderHesapPlaniId,
            OdemeTarihi = belge.BelgeTarihi,
            ValorGunSayisi = krediKartiHesap.ValorGunSayisi,
            ValorGunTuru = krediKartiHesap.ValorGunTuru,
            BeklenenValorTarihi = beklenenValorTarihi,
            OtomatikAktarimMi = krediKartiHesap.ValorGunundeOtomatikHesabaAktarMi,
            KomisyonOraniSnapshot = krediKartiHesap.KomisyonOrani,
            BrutTutar = brut,
            KomisyonTutari = komisyon,
            NetTutar = net,
            ParaBirimi = belge.ParaBirimi,
            Durum = durum
        };

        await _dbContext.PosTahsilatValorleri.AddAsync(valor, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return valor;
    }

    public async Task IptalEtAsync(int tahsilatOdemeBelgesiId, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PosTahsilatValorleri
            .FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[PosTahsilatValorleri] WITH (UPDLOCK, ROWLOCK)
WHERE [TahsilatOdemeBelgesiId] = {tahsilatOdemeBelgesiId} AND [IsDeleted] = 0")
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            return;
        }

        switch (entity.Durum)
        {
            case PosTahsilatValorDurumlari.ValorBekliyor:
            case PosTahsilatValorDurumlari.MutabakatBekliyor:
            case PosTahsilatValorDurumlari.Hata:
                entity.Durum = PosTahsilatValorDurumlari.Iptal;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;

            case PosTahsilatValorDurumlari.Aktariliyor:
                throw new BaseException("Valör aktarım işlemi sürüyor, tahsilat şu an iptal edilemez.", 409);

            case PosTahsilatValorDurumlari.TersKayitOlusturuluyor:
                throw new BaseException("Valör düzeltme/ters kayıt işlemi sürüyor, tahsilat şu an iptal edilemez.", 409);

            case PosTahsilatValorDurumlari.Aktarildi:
                if (!entity.MuhasebeFisId.HasValue)
                {
                    throw new BaseException("Aktarılmış valör kaydının muhasebe fişi bulunamadı; veri tutarsızlığı.", 500);
                }

                var sonuc = await _muhasebeFisService.PosValorTransferFisiniIptalEtAsync(
                    entity.MuhasebeFisId.Value, entity.Id, entity.TesisId,
                    "Tahsilat iptali nedeniyle valör transferi ters kaydı", cancellationToken);

                entity.TersKayitMuhasebeFisId = sonuc.TersKayitFisId;
                entity.Durum = PosTahsilatValorDurumlari.Iptal;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;

            case PosTahsilatValorDurumlari.AktarimFisiIptalEdildi:
                // Ters kaydi zaten var - ikinci ters kayit URETILMEZ, idempotent gecis.
                entity.Durum = PosTahsilatValorDurumlari.Iptal;
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;

            case PosTahsilatValorDurumlari.Iptal:
                // Zaten iptal - idempotent no-op.
                return;

            default:
                throw new BaseException($"Valör kaydı beklenmeyen bir durumda ({entity.Durum}).", 500);
        }
    }
}
