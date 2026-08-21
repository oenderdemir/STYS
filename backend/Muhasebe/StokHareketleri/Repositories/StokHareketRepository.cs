using AutoMapper;
using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.Depolar.Entities;
using STYS.Muhasebe.StokHareketleri.Dtos;
using STYS.Muhasebe.StokHareketleri.Entities;
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
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m
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
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m
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

    public async Task<decimal> GetBakiyeMiktariAsync(int depoId, int tasinirKartId, CancellationToken cancellationToken = default)
    {
        var rows = await BuildBaseQuery([depoId])
            .Where(x => x.TasinirKartId == tasinirKartId)
            .Select(x => new
            {
                Giris = StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m,
                Cikis = StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu) ? x.Miktar : 0m
            })
            .ToListAsync(cancellationToken);

        return rows.Sum(x => x.Giris) - rows.Sum(x => x.Cikis);
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
                Miktar = x.Miktar,
                BirimFiyat = x.BirimFiyat,
                Tutar = x.Tutar,
                DepoKod = x.Depo != null ? x.Depo.Kod : string.Empty,
                DepoAd = x.Depo != null ? x.Depo.Ad : string.Empty,
                StokKodu = x.TasinirKart != null ? x.TasinirKart.StokKodu : string.Empty,
                TasinirKartAd = x.TasinirKart != null ? x.TasinirKart.Ad : string.Empty,
                Birim = x.TasinirKart != null ? x.TasinirKart.Birim : string.Empty
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
            .Where(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu))
            .Sum(x => x.Miktar);
        var cikisMiktari = hareketler
            .Where(x => StokHareketTipleri.IsCikisEtkisi(x.HareketTipi, x.TransferYonu))
            .Sum(x => x.Miktar);
        var girisHareketleri = hareketler
            .Where(x => StokHareketTipleri.IsGirisEtkisi(x.HareketTipi, x.TransferYonu))
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
                    Miktar = x.Miktar,
                    Birim = x.Birim,
                    BirimFiyat = x.BirimFiyat,
                    ToplamTutar = x.Tutar,
                    HareketSayisi = 1
                })
                .OrderBy(x => x.HareketTarihi)
                .ToList(),
            DepoMalzemeKayitTipleri.FiyatFarkliMalzemeleriAyriKayittaTut => girisHareketleri
                .GroupBy(x => x.BirimFiyat)
                .Select(g => new StokDetaySatirDto
                {
                    HareketTarihi = null,
                    Miktar = g.Sum(x => x.Miktar),
                    Birim = g.First().Birim,
                    BirimFiyat = g.Key,
                    ToplamTutar = g.Sum(x => x.Tutar),
                    HareketSayisi = g.Count()
                })
                .OrderBy(x => x.BirimFiyat)
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
                Miktar = toplamMiktar,
                Birim = girisHareketleri[0].Birim,
                BirimFiyat = ortalamaFiyat,
                ToplamTutar = toplamTutar,
                HareketSayisi = girisHareketleri.Count
            }
        ];
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

    private sealed class StokDetayKaynakSatiri
    {
        public DateTime HareketTarihi { get; set; }
        public string HareketTipi { get; set; } = string.Empty;
        public string? TransferYonu { get; set; }
        public decimal Miktar { get; set; }
        public decimal BirimFiyat { get; set; }
        public decimal Tutar { get; set; }
        public string DepoKod { get; set; } = string.Empty;
        public string DepoAd { get; set; } = string.Empty;
        public string StokKodu { get; set; } = string.Empty;
        public string TasinirKartAd { get; set; } = string.Empty;
        public string Birim { get; set; } = string.Empty;
    }
}
