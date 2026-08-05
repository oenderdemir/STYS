namespace STYS.Muhasebe.SatisBelgeleri.Enums;

public enum EBelgeOutboxIsTuru
{
    ArtefaktOlustur = 1,

    /// <summary>Unsigned UBL artifact'ını XAdES-BES ile imzalar (bkz. Faz 2B.7).</summary>
    UblImzala = 2
}
