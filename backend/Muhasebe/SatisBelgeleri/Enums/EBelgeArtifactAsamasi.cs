namespace STYS.Muhasebe.SatisBelgeleri.Enums;

public enum EBelgeArtifactAsamasi
{
    Unsigned = 1,

    /// <summary>XAdES-BES ile imzalanmış, immutable, gönderime hazır UBL XML (bkz. Faz 2B.7).</summary>
    SignedReady = 2
}
