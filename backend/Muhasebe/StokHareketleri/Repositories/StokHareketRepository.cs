using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
using STYS.Muhasebe.StokLotlari.Dtos;
using TOD.Platform.Persistence.Rdbms.Repositories;

namespace STYS.Muhasebe.StokHareketleri.Repositories;

public class StokHareketRepository : BaseRdbmsRepository<StokHareket, int>, IStokHareketRepository
{
    private readonly StysAppDbContext _dbContext;

    public StokHareketRepository(StysAppDbContext dbContext, IMapper mapper)
        : base(dbContext, mapper)
    {
        _dbContext = dbContext;
    }

    public async Task<List<StokBakiyeDto>> GetDepoStokBakiyeleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery(depoIds)
            .Select(x => new
            {
                x.DepoId,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.DepoId, x.DepoKod, x.DepoAd, x.TasinirKartId, x.StokKodu, x.TasinirKartAd, x.Birim })
            .Select(g => new StokBakiyeDto
            {
                DepoId = g.Key.DepoId,
                DepoKod = g.Key.DepoKod,
                DepoAd = g.Key.DepoAd,
                TasinirKartId = g.Key.TasinirKartId,
                StokKodu = g.Key.StokKodu,
                TasinirKartAd = g.Key.TasinirKartAd,
                Birim = g.Key.Birim,
                GirisMiktari = g.Sum(x => x.Giris),
                CikisMiktari = g.Sum(x => x.Cikis),
                BakiyeMiktari = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis)
            })
            .OrderBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ToList();
    }

    public async Task<List<StokKartOzetDto>> GetStokKartOzetleriAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery(depoIds)
            .Select(x => new
            {
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                Ad = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.TasinirKartId, x.StokKodu, x.Ad, x.Birim })
            .Select(g => new StokKartOzetDto
            {
                TasinirKartId = g.Key.TasinirKartId,
                StokKodu = g.Key.StokKodu,
                Ad = g.Key.Ad,
                Birim = g.Key.Birim,
                GirisMiktari = g.Sum(x => x.Giris),
                CikisMiktari = g.Sum(x => x.Cikis),
                BakiyeMiktari = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis)
            })
            .OrderBy(x => x.StokKodu)
            .ToList();
    }

    public async Task<List<StokDegerlemeDto>> GetStokDegerlemeAsync(IEnumerable<int>? depoIds, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery(depoIds)
            .Select(x => new
            {
                x.DepoId,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                x.TasinirKartId,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                HareketTipi = x.HareketTipi,
                TransferYonu = x.TransferYonu,
                SayimFarkiYonu = x.SayimFarkiYonu,
                x.Miktar,
                x.BirimFiyat,
                x.MaliyetTutari
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.DepoId, x.DepoKod, x.DepoAd, x.TasinirKartId, x.StokKodu, x.TasinirKartAd, x.Birim })
            .Select(g =>
            {
                var bakiyeMiktari = g.Sum(x => GetMovementQuantityEffect(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar));
                var toplamStokDegeri = g.Sum(x => GetMovementCostEffect(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu, x.Miktar, x.MaliyetTutari, x.BirimFiyat));
                var ortalamaMaliyet = bakiyeMiktari == 0
                    ? 0m
                    : Math.Round(toplamStokDegeri / bakiyeMiktari, 6, MidpointRounding.AwayFromZero);
                var maliyetEksikMi = g.Any(x =>
                    StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu)
                    && x.MaliyetTutari == null);

                return new StokDegerlemeDto
                {
                    DepoId = g.Key.DepoId,
                    DepoKod = g.Key.DepoKod,
                    DepoAd = g.Key.DepoAd,
                    TasinirKartId = g.Key.TasinirKartId,
                    StokKodu = g.Key.StokKodu,
                    TasinirKartAd = g.Key.TasinirKartAd,
                    Birim = g.Key.Birim,
                    BakiyeMiktari = bakiyeMiktari,
                    OrtalamaMaliyet = ortalamaMaliyet,
                    ToplamStokDegeri = toplamStokDegeri,
                    MaliyetEksikMi = maliyetEksikMi
                };
            })
            .Where(x => x.BakiyeMiktari > 0)
            .OrderBy(x => x.DepoKod)
            .ThenBy(x => x.StokKodu)
            .ToList();
    }

    public async Task<decimal> GetBakiyeMiktariAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId)
            .Select(x => new
            {
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows.Sum(x => x.Giris) - rows.Sum(x => x.Cikis);
    }

    public async Task<decimal> GetLotBakiyeMiktariAsync(int depoId, int tasinirKartId, int stokLotId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId && x.StokLotId == stokLotId)
            .Select(x => new
            {
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows.Sum(x => x.Giris) - rows.Sum(x => x.Cikis);
    }

    public async Task<List<StokLotBakiyeDto>> GetLotBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId && x.StokLotId.HasValue && x.StokLot != null)
            .Select(x => new
            {
                x.StokLotId,
                LotNo = x.StokLot != null ? x.StokLot.LotNo : string.Empty,
                SonKullanmaTarihi = x.StokLot != null ? x.StokLot.SonKullanmaTarihi : null,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.StokLotId, x.LotNo, x.SonKullanmaTarihi })
            .Select(g => new StokLotBakiyeDto
            {
                StokLotId = g.Key.StokLotId!.Value,
                LotNo = g.Key.LotNo,
                SonKullanmaTarihi = g.Key.SonKullanmaTarihi,
                GirisMiktari = g.Sum(x => x.Giris),
                CikisMiktari = g.Sum(x => x.Cikis),
                BakiyeMiktari = g.Sum(x => x.Giris) - g.Sum(x => x.Cikis)
            })
            .OrderBy(x => x.LotNo)
            .ThenBy(x => x.SonKullanmaTarihi)
            .ToList();
    }

    public async Task<List<StokSeriBakiyeDto>> GetSeriBakiyeleriAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId && x.StokSeriId.HasValue && x.StokSeri != null)
            .Select(x => new
            {
                x.StokSeriId,
                SeriNo = x.StokSeri != null ? x.StokSeri.SeriNo : string.Empty,
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.StokSeriId, x.SeriNo })
            .Where(g => g.Sum(x => x.Giris) - g.Sum(x => x.Cikis) > 0)
            .Select(g => new StokSeriBakiyeDto
            {
                StokSeriId = g.Key.StokSeriId!.Value,
                SeriNo = g.Key.SeriNo
            })
            .OrderBy(x => x.SeriNo)
            .ToList();
    }

    public async Task<StokDetayDto> GetStokDetayAsync(int depoId, int tasinirKartId, DepoMalzemeKayitTipleri malzemeKayitTipi, CancellationToken cancellationToken = default)
    {
        var hareketler = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId)
            .Select(x => new StokDetayKaynakSatiri
            {
                HareketTarihi = x.HareketTarihi,
                HareketTipi = x.HareketTipi,
                TransferYonu = x.TransferYonu,
                SayimFarkiYonu = x.SayimFarkiYonu,
                Miktar = x.Miktar,
                BirimFiyat = x.BirimFiyat,
                Tutar = x.Tutar,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty,
                TakipTipi = x.TasinirKart != null ? x.TasinirKart.TakipTipi : null,
                StokLotId = x.StokLotId,
                StokSeriId = x.StokSeriId,
                LotNo = x.StokLot != null ? x.StokLot.LotNo : null,
                SeriNo = x.StokSeri != null ? x.StokSeri.SeriNo : null,
                SonKullanmaTarihi = x.StokLot != null ? x.StokLot.SonKullanmaTarihi : null
            })
            .OrderBy(x => x.HareketTarihi)
            .ToListAsync(cancellationToken);

        if (hareketler.Count == 0)
        {
            return new StokDetayDto
            {
                DepoId = depoId,
                TasinirKartId = tasinirKartId,
                MalzemeKayitTipi = malzemeKayitTipi.ToString(),
                Aciklama = ResolveAciklama(malzemeKayitTipi)
            };
        }

        var girisMiktari = hareketler
            .Where(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
            .Sum(x => x.Miktar);
        var cikisMiktari = hareketler
            .Where(x => StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
            .Sum(x => x.Miktar);
        var girisHareketleri = hareketler
            .Where(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu, x.SayimFarkiYonu))
            .ToList();

        return new StokDetayDto
        {
            DepoId = depoId,
            DepoKod = hareketler[0].DepoKod,
            DepoAd = hareketler[0].DepoAd,
            MalzemeKayitTipi = malzemeKayitTipi.ToString(),
            TasinirKartId = tasinirKartId,
            StokKodu = hareketler[0].StokKodu,
            TasinirKartAd = hareketler[0].TasinirKartAd,
            Birim = hareketler[0].Birim,
            GirisMiktari = girisMiktari,
            CikisMiktari = cikisMiktari,
            BakiyeMiktari = girisMiktari - cikisMiktari,
            Aciklama = ResolveAciklama(malzemeKayitTipi),
            Satirlar = BuildDetaySatirlari(girisHareketleri, malzemeKayitTipi)
        };
    }

    private IQueryable<StokHareket> BuildBaseQuery(IEnumerable<int>? depoIds)
    {
        var query = _dbContext.StokHareketleri
            .AsNoTracking()
            .Include(x => x.Depo)
            .Include(x => x.TasinirKart)
            .Include(x => x.StokLot)
            .Include(x => x.StokSeri)
            .Where(x => x.Durum == StokHareketDurumlari.Aktif);

        if (depoIds is not null)
        {
            var depoIdList = depoIds as int[] ?? depoIds.ToArray();
            if (depoIdList.Length > 0)
            {
                query = query.Where(x => depoIdList.Contains(x.DepoId));
            }
        }

        return query;
    }

    private static List<StokDetaySatirDto> BuildDetaySatirlari(
        List<StokDetayKaynakSatiri> girisHareketleri,
        DepoMalzemeKayitTipleri malzemeKayitTipi)
    {
        return malzemeKayitTipi switch
        {
            DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut => girisHareketleri
                .Select(x => new StokDetaySatirDto
                {
                    HareketTarihi = x.HareketTarihi,
                    StokLotId = x.StokLotId,
                    StokSeriId = x.StokSeriId,
                    Miktar = x.Miktar,
                    Birim = x.Birim,
                    BirimFiyat = x.BirimFiyat,
                    ToplamTutar = x.Tutar,
                    HareketSayisi = 1,
                    LotNo = x.LotNo,
                    SeriNo = x.SeriNo,
                    SonKullanmaTarihi = x.SonKullanmaTarihi
                })
                .OrderBy(x => x.HareketTarihi)
                .ToList(),
            DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut => girisHareketleri
                .GroupBy(x => BuildFiyatBazliDetayGrupKey(x))
                .Select(g => new StokDetaySatirDto
                {
                    HareketTarihi = null,
                    StokLotId = g.Key.StokLotId,
                    StokSeriId = g.Key.StokSeriId,
                    Miktar = g.Sum(x => x.Miktar),
                    Birim = g.First().Birim,
                    BirimFiyat = g.Key.BirimFiyat,
                    ToplamTutar = g.Sum(x => x.Tutar),
                    HareketSayisi = g.Count(),
                    LotNo = g.Select(x => x.LotNo).Distinct().Count() == 1 ? g.Select(x => x.LotNo).FirstOrDefault() : null,
                    SeriNo = g.Select(x => x.SeriNo).Distinct().Count() == 1 ? g.Select(x => x.SeriNo).FirstOrDefault() : null,
                    SonKullanmaTarihi = g.Select(x => x.SonKullanmaTarihi).Distinct().Count() == 1 ? g.Select(x => x.SonKullanmaTarihi).FirstOrDefault() : null
                })
                .OrderBy(x => x.LotNo)
                .ThenBy(x => x.SeriNo)
                .ThenBy(x => x.BirimFiyat)
                .ToList(),
            DepoMalzemeKayitTipleri.MalzemeleriAyniKayittaTut => BuildTekKayitDetayi(girisHareketleri),
            _ => []
        };
    }

    private static List<StokDetaySatirDto> BuildTekKayitDetayi(List<StokDetayKaynakSatiri> girisHareketleri)
    {
        if (girisHareketleri.Count == 0)
        {
            return [];
        }

        if (string.Equals(girisHareketleri[0].TakipTipi, "Lot", StringComparison.Ordinal))
        {
            return girisHareketleri
                .GroupBy(x => x.StokLotId)
                .Select(BuildLotOzetSatiri)
                .OrderBy(x => x.LotNo)
                .ThenBy(x => x.BirimFiyat)
                .ToList();
        }

        if (string.Equals(girisHareketleri[0].TakipTipi, "Seri", StringComparison.Ordinal))
        {
            return girisHareketleri
                .GroupBy(x => x.StokSeriId)
                .Select(BuildSeriOzetSatiri)
                .OrderBy(x => x.SeriNo)
                .ThenBy(x => x.BirimFiyat)
                .ToList();
        }

        var toplamMiktar = girisHareketleri.Sum(x => (decimal)x.Miktar);
        var toplamTutar = girisHareketleri.Sum(x => (decimal)x.Tutar);
        var ortalamaFiyat = toplamMiktar == 0
            ? 0
            : Math.Round(toplamTutar / toplamMiktar, 2, MidpointRounding.AwayFromZero);

        return
        [
            new StokDetaySatirDto
            {
                HareketTarihi = null,
                StokLotId = null,
                StokSeriId = null,
                Miktar = toplamMiktar,
                Birim = girisHareketleri[0].Birim,
                BirimFiyat = ortalamaFiyat,
                ToplamTutar = toplamTutar,
                HareketSayisi = girisHareketleri.Count,
                LotNo = girisHareketleri.Select(x => x.LotNo).Distinct().Count() == 1 ? girisHareketleri.Select(x => x.LotNo).FirstOrDefault() : null,
                SeriNo = girisHareketleri.Select(x => x.SeriNo).Distinct().Count() == 1 ? girisHareketleri.Select(x => x.SeriNo).FirstOrDefault() : null,
                SonKullanmaTarihi = girisHareketleri.Select(x => x.SonKullanmaTarihi).Distinct().Count() == 1 ? girisHareketleri.Select(x => x.SonKullanmaTarihi).FirstOrDefault() : null
            }
        ];
    }

    private static StokDetaySatirDto BuildLotOzetSatiri(IGrouping<int?, StokDetayKaynakSatiri> grup)
    {
        var toplamMiktar = grup.Sum(x => (decimal)x.Miktar);
        var toplamTutar = grup.Sum(x => (decimal)x.Tutar);
        var ortalamaFiyat = toplamMiktar == 0
            ? 0
            : Math.Round(toplamTutar / toplamMiktar, 2, MidpointRounding.AwayFromZero);

        return new StokDetaySatirDto
        {
            HareketTarihi = null,
            StokLotId = grup.Key,
            StokSeriId = null,
            Miktar = toplamMiktar,
            Birim = grup.First().Birim,
            BirimFiyat = ortalamaFiyat,
            ToplamTutar = toplamTutar,
            HareketSayisi = grup.Count(),
            LotNo = grup.Select(x => x.LotNo).Distinct().Count() == 1 ? grup.Select(x => x.LotNo).FirstOrDefault() : null,
            SeriNo = null,
            SonKullanmaTarihi = grup.Select(x => x.SonKullanmaTarihi).Distinct().Count() == 1 ? grup.Select(x => x.SonKullanmaTarihi).FirstOrDefault() : null
        };
    }

    private static StokDetaySatirDto BuildSeriOzetSatiri(IGrouping<int?, StokDetayKaynakSatiri> grup)
    {
        var first = grup.First();
        return new StokDetaySatirDto
        {
            HareketTarihi = null,
            StokLotId = null,
            StokSeriId = grup.Key,
            Miktar = grup.Sum(x => x.Miktar),
            Birim = first.Birim,
            BirimFiyat = first.BirimFiyat,
            ToplamTutar = grup.Sum(x => x.Tutar),
            HareketSayisi = grup.Count(),
            SeriNo = first.SeriNo
        };
    }

    private static FiyatBazliDetayGrupKey BuildFiyatBazliDetayGrupKey(StokDetayKaynakSatiri satir)
    {
        return satir.TakipTipi switch
        {
            "Lot" => new FiyatBazliDetayGrupKey(satir.StokLotId, null, satir.BirimFiyat),
            "Seri" => new FiyatBazliDetayGrupKey(null, satir.StokSeriId, satir.BirimFiyat),
            _ => new FiyatBazliDetayGrupKey(null, null, satir.BirimFiyat)
        };
    }

    private static string ResolveAciklama(DepoMalzemeKayitTipleri malzemeKayitTipi)
    {
        return malzemeKayitTipi switch
        {
            DepoMalzemeKayitTipleri.MalzemeleriAyriKayittaTut => "Her giriş ayrı stok detayı olarak gösterilir. Çıkışlar belirli giriş partilerine dağıtılmaz.",
            DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut => "Aynı birim fiyatlı girişler birlikte gösterilir. Çıkışlar fiyat katmanlarına dağıtılmaz.",
            DepoMalzemeKayitTipleri.MalzemeleriAyniKayittaTut => "Tüm girişler tek stok satırında ağırlıklı ortalama fiyat ile özetlenir.",
            _ => string.Empty
        };
    }

    private static decimal GetMovementQuantityEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar)
    {
        if (StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return miktar;
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return -miktar;
        }

        return 0m;
    }

    private static decimal GetMovementCostEffect(string? hareketTipi, string? transferYonu, string? sayimFarkiYonu, decimal miktar, decimal? maliyetTutari, decimal birimFiyat)
    {
        var tutar = maliyetTutari ?? Math.Round(miktar * birimFiyat, 2, MidpointRounding.AwayFromZero);
        if (StokHareketTipleri.IsGirisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return tutar;
        }

        if (StokHareketTipleri.IsCikisEtkisi(hareketTipi, transferYonu, sayimFarkiYonu))
        {
            return -(maliyetTutari ?? 0m);
        }

        return 0m;
    }

    private sealed class StokDetayKaynakSatiri
    {
        public DateTime HareketTarihi { get; set; }
        public string HareketTipi { get; set; } = string.Empty;
        public string? TransferYonu { get; set; }
        public string? SayimFarkiYonu { get; set; }
        public decimal Miktar { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal Tutar { get; set; }
        public string DepoKod { get; set; } = string.Empty;
        public string DepoAd { get; set; } = string.Empty;
        public string StokKodu { get; set; } = string.Empty;
        public string TasinirKartAd { get; set; } = string.Empty;
        public string Birim { get; set; } = string.Empty;
        public string? TakipTipi { get; set; }
        public int? StokLotId { get; set; }
        public int? StokSeriId { get; set; }
        public string? LotNo { get; set; }
        public string? SeriNo { get; set; }
        public DateTime? SonKullanmaTarihi { get; set; }
    }

    private sealed record FiyatBazliDetayGrupKey(int? StokLotId, int? StokSeriId, decimal BirimFiyat);
}
