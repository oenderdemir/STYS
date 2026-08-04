using System.Collections.Immutable;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>Sidecar'ın döndürdüğü tek bir Schematron ihlali. Mesaj GİB kural setinin kendi metnidir.</summary>
public sealed record EBelgeSchematronViolation(string RuleId, string Location, string Message, string Severity);

/// <summary>Sidecar'dan gelen ham, protokol düzeyinde yanıt - Valid=false GERÇEK bir iş kuralı ihlalidir, hata DEĞİLDİR.</summary>
public sealed record EBelgeSchematronValidationResult(bool Valid, IReadOnlyList<EBelgeSchematronViolation> Violations);

/// <summary>
/// Ayrı bir Java Saxon-HE 13.0 sidecar servisi (bkz. sidecar/schematron-validator) üzerinden
/// resmî GİB UBL-TR Schematron doğrulamasını çalıştırır. Yalnız sabit, whitelist edilmiş
/// rule-set kimliği ve doğrulanacak XML byte içeriğini taşır - stylesheet/path/URL/XPath
/// GEÇİRİLMEZ (bkz. görev md.1). Genuine iş kuralı ihlalleri (Valid=false) fırlatmadan sonuç
/// olarak döner; yalnız ALTYAPI/PROTOKOL sorunları (erişilemez, timeout, geçersiz yanıt) tipli
/// exception fırlatır - bu iki kategori ASLA birleştirilmez (bkz. görev md.8).
/// </summary>
public interface IEBelgeSchematronValidator
{
    Task<EBelgeSchematronValidationResult> ValidateAsync(
        ImmutableArray<byte> xmlBytes,
        string ruleSetId,
        CancellationToken cancellationToken);
}
