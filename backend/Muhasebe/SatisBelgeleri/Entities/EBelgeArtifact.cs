using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.Persistence.Rdbms.Entities;

namespace STYS.Muhasebe.SatisBelgeleri.Entities;

/// <summary>
/// Renderer'ın ürettiği bir belge çıktısının IMMUTABLE kaydı (bkz. görev md.4 - "oluşturulduktan
/// sonra içerik/hash değiştirilemez, düzeltme gerekirse yeni artifact"). İlk tür/aşama yalnız
/// UblXml/Unsigned'dır; ileride PDF/imzalı UBL gibi başka tür ve aşamalar aynı tabloya EKLENİR
/// (bkz. StysAppDbContext, benzersizlik KurumId+EBelgeKaydiId+ArtifactTipi+ArtifactAsamasi
/// üzerindendir - farklı tip/aşama serbestçe bir arada var olabilir).
/// </summary>
public class EBelgeArtifact : BaseEntity<long>, ITenantEntity
{
    public int KurumId { get; set; }

    public int EBelgeKaydiId { get; set; }
    public EBelgeKaydi EBelgeKaydi { get; set; } = null!;

    public EBelgeArtifactTipi ArtifactTipi { get; set; }

    public EBelgeArtifactAsamasi ArtifactAsamasi { get; set; }

    /// <summary>Bu artefaktı üreten GİB kural seti kimliği (bkz. GibKuralSeti.KuralSetiKimligi).</summary>
    public string RuleSetId { get; set; } = string.Empty;

    /// <summary>Kaynak canonical snapshot'ın şema sürümü (bkz. EBelgeSnapshot.SnapshotSchemaVersion) - int olarak saklanır (yalnız "2" destekleniyor, ileride sayısal karşılaştırma için).</summary>
    public int SnapshotSchemaVersion { get; set; }

    /// <summary>Bu artefaktın üretildiği ANDAKI canonical snapshot'ın SHA-256'sı (bkz. EBelgeSnapshot.CanonicalSha256) - hash zincirinin ilk halkası.</summary>
    public string KaynakSnapshotSha256 { get; set; } = string.Empty;

    /// <summary>Icerik alanının SHA-256'sı - renderer'ın EXACT çıktı byte'ları üzerinden hesaplanır, yeniden serialize EDİLMEZ.</summary>
    public string ArtifactSha256 { get; set; } = string.Empty;

    public byte[] Icerik { get; set; } = [];

    public string MimeType { get; set; } = string.Empty;

    /// <summary>Deterministik, resmi fatura numarasından türetilmiş dosya adı - kullanıcı girdisinden DOĞRUDAN path haline getirilmez.</summary>
    public string DosyaAdi { get; set; } = string.Empty;

    public DateTime OlusturulmaZamaniUtc { get; set; }
}
