package stys.ebelge.schematron;

/**
 * Resmî GİB UBL-TR_Main_Schematron.xml, kök seviyede {@code <let name="type" value="efatura"/>}
 * bildirir. ISO Schematron skeleton'ın (iso_svrl_for_xslt1.xsl) resmî derleme davranışı, kök
 * seviyedeki {@code <sch:let>} bildirimlerini üretilen XSLT'de {@code <xsl:param>} olarak
 * derler (GENUINELY doğrulandı - bkz. sidecar SONUC kanıtları) - bu, GİB'in KENDİ resmî derleme
 * hattının desteklediği, standart bir XSLT stylesheet parametresidir; metin tabanlı bir
 * yama/hack DEĞİLDİR. Her {@link DocumentProfile}, bu $type parametresine hangi değerin runtime'da
 * bağlanacağını belirler - GİB kaynak dosyası bu eşlemeyle DEĞİŞTİRİLMEZ.
 */
enum DocumentProfile {
    EARSIV("earchive"),
    EFATURA("efatura");

    final String schematronTypeValue;

    DocumentProfile(String schematronTypeValue) {
        this.schematronTypeValue = schematronTypeValue;
    }

    static DocumentProfile fromRuleSetId(String ruleSetId, String baseRuleSetId) {
        if (ruleSetId == null) {
            return null;
        }

        if (ruleSetId.equals(baseRuleSetId + "/EARSIV")) {
            return EARSIV;
        }

        if (ruleSetId.equals(baseRuleSetId + "/EFATURA")) {
            return EFATURA;
        }

        return null;
    }
}
