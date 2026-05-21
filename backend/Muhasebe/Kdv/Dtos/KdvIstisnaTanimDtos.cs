using STYS.Muhasebe.Kdv.Enums;
using TOD.Platform.Persistence.Rdbms.Dto;

namespace STYS.Muhasebe.Kdv.Dtos;

public class KdvIstisnaTanimDto : BaseRdbmsDto<int>
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public KdvUygulamaTipi UygulamaTipi { get; set; }
    public bool SatisIslemlerindeKullanilirMi { get; set; }
    public bool AlisIslemlerindeKullanilirMi { get; set; }
    public bool YuklenilenKdvIndirilebilirMi { get; set; }
    public bool IadeHakkiVarMi { get; set; }
    public bool EBelgeKoduZorunluMu { get; set; }
    public bool AktifMi { get; set; }
    public DateTime? GecerlilikBaslangicTarihi { get; set; }
    public DateTime? GecerlilikBitisTarihi { get; set; }
}

public class CreateKdvIstisnaTanimRequest
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public KdvUygulamaTipi UygulamaTipi { get; set; }
    public bool SatisIslemlerindeKullanilirMi { get; set; }
    public bool AlisIslemlerindeKullanilirMi { get; set; }
    public bool YuklenilenKdvIndirilebilirMi { get; set; }
    public bool IadeHakkiVarMi { get; set; }
    public bool EBelgeKoduZorunluMu { get; set; }
    public bool AktifMi { get; set; } = true;
    public DateTime? GecerlilikBaslangicTarihi { get; set; }
    public DateTime? GecerlilikBitisTarihi { get; set; }
}

public class UpdateKdvIstisnaTanimRequest
{
    public string Kod { get; set; } = string.Empty;
    public string Ad { get; set; } = string.Empty;
    public string? Aciklama { get; set; }
    public KdvUygulamaTipi UygulamaTipi { get; set; }
    public bool SatisIslemlerindeKullanilirMi { get; set; }
    public bool AlisIslemlerindeKullanilirMi { get; set; }
    public bool YuklenilenKdvIndirilebilirMi { get; set; }
    public bool IadeHakkiVarMi { get; set; }
    public bool EBelgeKoduZorunluMu { get; set; }
    public bool AktifMi { get; set; }
    public DateTime? GecerlilikBaslangicTarihi { get; set; }
    public DateTime? GecerlilikBitisTarihi { get; set; }
}

public class KdvIstisnaTanimFilterDto
{
    public string? Kod { get; set; }
    public string? Ad { get; set; }
    public KdvUygulamaTipi? UygulamaTipi { get; set; }
    public bool? AktifMi { get; set; }
    public bool? SatisIslemlerindeKullanilirMi { get; set; }
    public bool? AlisIslemlerindeKullanilirMi { get; set; }
}
