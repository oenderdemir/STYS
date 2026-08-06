namespace STYS.Muhasebe.SatisBelgeleri.Enums;

/// <summary>
/// Faz 2B.10 - bir kurumun e-belge sürecini STYS ile HANGİ yöntemle yürüttüğünü type-safe olarak
/// ifade eder. Bu enum, birbirinden bağımsız/çelişebilen boolean config alanları (ör.
/// "UblSTYSTarafindanUretilsin") YERİNE, TEK bir merkezi kaynaktan (bkz.
/// <see cref="EBelgeYontemYetenekleri"/>) tutarlı bir yetenek planı türetmeyi sağlar.
/// </summary>
public enum EBelgeEntegrasyonYontemi
{
    /// <summary>
    /// Kurum için AÇIK bir karar VERİLMEMİŞTİR. Global e-belge süreci aktif olduktan SONRA bu
    /// durum SESSİZCE <see cref="Kullanilmayacak"/> olarak YORUMLANMAZ - fail-closed bir
    /// karar nedeni (<c>PolitikaYapilandirilmadi</c>) üretir.
    /// </summary>
    Yapilandirilmadi = 0,

    /// <summary>Kurum AÇIKÇA STYS e-belge zincirini KULLANMAYACAKTIR. Bu, bir HATA DEĞİLDİR.</summary>
    Kullanilmayacak = 1,

    /// <summary>Mali belge BAŞKA bir kurum sistemi tarafından oluşturulacaktır - STYS yerel e-belge pipeline'ı OLUŞTURMAZ.</summary>
    HariciMuhasebeSistemi = 2,

    /// <summary>STYS canonical snapshot + XSD/Schematron doğrulanmış unsigned UBL + immutable unsigned artifact ÜRETİR - yerel imza ÜRETMEZ, GİB'e otomatik GÖNDERMEZ.</summary>
    GibPortal = 3,

    /// <summary>Gelecekte özel entegratör adapter'ıyla kullanılacaktır - gerçek adapter YOKKEN production'da aktif hale GETİRİLEMEZ.</summary>
    OzelEntegrator = 4,

    /// <summary>Gelecekte mali mühür/HSM + GİB gönderim adapter'ıyla kullanılacaktır - gerçek adapter YOKKEN production'da aktif hale GETİRİLEMEZ.</summary>
    DogrudanGib = 5
}
