using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using STYS.Tesisler.Entities;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Kamp.Entities;

public class KampDonemiTesis : BaseEntity<int>
{
    public int KampDonemiId { get; set; }

    public int TesisId { get; set; }

    public bool AktifMi { get; set; } = true;

    public bool BasvuruyaAcikMi { get; set; } = true;

    public int ToplamKontenjan { get; set; }

    [MaxLength(512)]
    public string? Aciklama { get; set; }

    [MaxLength(2048)]
    public string? KonaklamaTarifeKodlariJson { get; set; }

    [NotMapped]
    public List<string> KonaklamaTarifeKodlari
    {
        get => string.IsNullOrEmpty(KonaklamaTarifeKodlariJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(KonaklamaTarifeKodlariJson) ?? [];
        set => KonaklamaTarifeKodlariJson = value.Count == 0
            ? null
            : JsonSerializer.Serialize(value);
    }

    public KampDonemi? KampDonemi { get; set; }

    public Tesis? Tesis { get; set; }
}
