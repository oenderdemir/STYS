using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using STYS.Muhasebe.Kdv.Enums;
using STYS.Muhasebe.SatisBelgeleri.Enums;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// Üretilen artefaktın hangi aşamada olduğunu type-safe biçimde işaretler. Unsigned çıktı hiçbir
/// zaman dış entegratöre gönderilebilir NİHAİ artefakt sayılmaz (bkz. görev md.10). SignedReady
/// bu fazda hiç üretilmez - gelecekteki imzalama fazı ekleyecektir.
/// </summary>
public enum EBelgeUblArtifactStage
{
    Unsigned = 1,
    SignedReady = 2,
}

/// <summary>
/// Faz 2B.5 renderer sonucu. XmlBytes tam olarak Sha256'nın hesaplandığı byte dizisidir -
/// hash hesaplandıktan SONRA XML yeniden serialize edilmez (bkz. görev md.3). ArtifactStage
/// her zaman Unsigned'dır (bu fazda imzalama yoktur) - sonucu tüketen kod bunu ASLA nihai,
/// gönderilebilir bir artefakt olarak ele almamalıdır.
/// </summary>
public sealed record EBelgeUblRenderSonucu(
    ImmutableArray<byte> UnsignedUblUtf8,
    string UnsignedUblSha256,
    string KullanilanProfileId,
    string KullanilanInvoiceTypeCode,
    string KuralSetiKimligi,
    string RendererSurumu,
    EBelgeUblArtifactStage ArtifactStage);

/// <summary>
/// Deterministik, imzasız UBL-TR fatura XML renderer'ı. Girdisi YALNIZ
/// <see cref="EBelgeCanonicalSnapshotV2"/>'dir; DB/saat/rastgelelik erişimi YOKTUR. Akış: (1)
/// snapshot sürüm/kapsam/otoriter alan/mali toplam doğrulaması (saf, senkron), (2) deterministik
/// XML üretimi + tek seferlik SHA-256, (3) yerel XSD doğrulaması (bilinen tek imzasız-XML bulgusu
/// dışında sıfır hata), (4) sidecar üzerinden GERÇEK Schematron doğrulaması (ağ I/O - bu yüzden
/// sözleşme async'tir), (5) yalnız hepsi geçerse sonuç döner (bkz. docs/e-belge-ubl-pdf-eposta-renderer-hazirlik-raporu.md, Faz 2B.5).
/// </summary>
public interface IEBelgeUblRenderer
{
    Task<EBelgeUblRenderSonucu> RenderAsync(EBelgeCanonicalSnapshotV2 snapshot, CancellationToken cancellationToken);
}

public sealed class EBelgeUblRenderer : IEBelgeUblRenderer
{
    public const string RendererSurumu = "2B.5.0";

    private const string NsInvoice = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
    private const string NsCac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    private const string NsCbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
    private const string NsExt = "urn:oasis:names:specification:ubl:schema:xsd:CommonExtensionComponents-2";

    private const string ParaBirimi = "TRY";
    private const string BeklenenProfileId = "EARSIVFATURA";
    private const string BeklenenInvoiceTypeCode = "SATIS";
    private const string BeklenenBirimKodu = "C62";
    private const string KdvTaxTypeCode = "0015";

    private readonly GibKuralSeti _kuralSeti;
    private readonly IEBelgeUblXsdValidator _xsdValidator;
    private readonly IEBelgeSchematronValidator _schematronValidator;

    public EBelgeUblRenderer(
        GibKuralSeti kuralSeti,
        IEBelgeUblXsdValidator xsdValidator,
        IEBelgeSchematronValidator schematronValidator)
    {
        _kuralSeti = kuralSeti;
        _xsdValidator = xsdValidator;
        _schematronValidator = schematronValidator;
    }

    public async Task<EBelgeUblRenderSonucu> RenderAsync(EBelgeCanonicalSnapshotV2 snapshot, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        ValidateSnapshotVersion(snapshot);
        ValidateScope(snapshot);
        ValidateAuthoritativeFields(snapshot);
        ValidateMonetaryTotals(snapshot);

        var xmlBytes = BuildXml(snapshot);
        var sha256 = Convert.ToHexString(SHA256.HashData(xmlBytes));
        var immutableXmlBytes = ImmutableArray.Create(xmlBytes);

        // XML/hash, aşağıdaki iki doğrulamadan HERHANGİ biri başarısız olursa dışarı ASLA
        // döndürülmez (bkz. görev md.9 - "başarılı render sonucu dönmemeli").
        _xsdValidator.ValidateUnsignedRendererOutput(immutableXmlBytes);

        var schematronSonucu = await _schematronValidator.ValidateAsync(
            immutableXmlBytes, EBelgeSchematronSidecarOptions.SupportedRuleSetId, cancellationToken);

        if (!schematronSonucu.Valid)
        {
            var ihlalMetinleri = schematronSonucu.Violations
                .Select(v => $"[{v.RuleId}] {v.Location}: {v.Message}")
                .ToList();
            throw new EBelgeUblSchematronValidationFailedException(ihlalMetinleri);
        }

        return new EBelgeUblRenderSonucu(
            immutableXmlBytes,
            sha256,
            snapshot.Belge.ProfileID,
            snapshot.Belge.InvoiceTypeCode,
            _kuralSeti.KuralSetiKimligi,
            RendererSurumu,
            EBelgeUblArtifactStage.Unsigned);
    }

    // ---- md.2: kapsamı bağımsız yeniden doğrula ----

    private static void ValidateSnapshotVersion(EBelgeCanonicalSnapshotV2 snapshot)
    {
        if (!string.Equals(snapshot.Metadata.SnapshotSchemaVersion, EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion, StringComparison.Ordinal))
        {
            throw new EBelgeUblRenderSnapshotVersionUnsupportedException(
                $"Renderer yalnız snapshot şema sürümü '{EBelgeCanonicalSnapshotV2Reader.SupportedSnapshotSchemaVersion}' işler. (Gelen: {snapshot.Metadata.SnapshotSchemaVersion})");
        }
    }

    private static void ValidateScope(EBelgeCanonicalSnapshotV2 snapshot)
    {
        if (snapshot.Belge.BelgeTipi != SatisBelgesiTipi.SatisFaturasi)
        {
            throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız SatisFaturasi işler. (BelgeTipi: {snapshot.Belge.BelgeTipi})");
        }

        if (!string.Equals(snapshot.Belge.ProfileID, BeklenenProfileId, StringComparison.Ordinal))
        {
            throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız ProfileID={BeklenenProfileId} işler. (ProfileID: {snapshot.Belge.ProfileID})");
        }

        if (!string.Equals(snapshot.Belge.InvoiceTypeCode, BeklenenInvoiceTypeCode, StringComparison.Ordinal))
        {
            throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız InvoiceTypeCode={BeklenenInvoiceTypeCode} işler. (InvoiceTypeCode: {snapshot.Belge.InvoiceTypeCode})");
        }

        if (!string.Equals(snapshot.Odeme.ParaBirimi, ParaBirimi, StringComparison.Ordinal))
        {
            throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız {ParaBirimi} para birimini işler. (ParaBirimi: {snapshot.Odeme.ParaBirimi})");
        }

        if (snapshot.Odeme.Kur != 1m)
        {
            throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız kur=1 işler. (Kur: {snapshot.Odeme.Kur})");
        }

        if (snapshot.Iade.IadeEdilenBelgeId.HasValue)
        {
            throw new EBelgeUblRenderScopeUnsupportedException("Renderer iade senaryolarını işlemez.");
        }

        if (snapshot.Satirlar.Count == 0)
        {
            throw new EBelgeUblRenderScopeUnsupportedException("Renderlanacak en az bir satır bulunmalıdır.");
        }

        foreach (var satir in snapshot.Satirlar)
        {
            if (!string.Equals(satir.BirimKodu, BeklenenBirimKodu, StringComparison.Ordinal))
            {
                throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız BirimKodu={BeklenenBirimKodu} işler. (BirimKodu: {satir.BirimKodu})");
            }

            if (satir.KdvUygulamaTipi != KdvUygulamaTipi.Kdvli)
            {
                throw new EBelgeUblRenderScopeUnsupportedException($"Renderer yalnız standart KDV'li satırları işler. (KdvUygulamaTipi: {satir.KdvUygulamaTipi})");
            }

            if (satir.TevkifatPay.HasValue || satir.TevkifatPayda.HasValue || satir.TevkifatTutari != 0m)
            {
                throw new EBelgeUblRenderScopeUnsupportedException("Renderer tevkifatlı satırları işlemez.");
            }

            if (!string.IsNullOrWhiteSpace(satir.KdvIstisnaKodu))
            {
                throw new EBelgeUblRenderScopeUnsupportedException("Renderer KDV istisnalı satırları işlemez.");
            }

            if (satir.OtvTutari != 0m || satir.OivTutari != 0m || satir.KonaklamaVergisiTutari != 0m)
            {
                throw new EBelgeUblRenderScopeUnsupportedException("Renderer ÖTV/ÖİV/konaklama vergisi içeren satırları işlemez.");
            }
        }
    }

    private static void ValidateAuthoritativeFields(EBelgeCanonicalSnapshotV2 snapshot)
    {
        if (string.IsNullOrWhiteSpace(snapshot.Belge.ResmiFaturaNo))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("Resmi fatura numarası bulunmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Belge.EBelgeUuid))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("E-belge UUID bulunmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Kurum.VergiNo) || string.IsNullOrWhiteSpace(snapshot.Kurum.KurumUnvani))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("Düzenleyen kurumun VKN'si ve unvanı bulunmalıdır.");
        }

        if (string.IsNullOrWhiteSpace(snapshot.Kurum.Ilce) || string.IsNullOrWhiteSpace(snapshot.Kurum.Il))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("Düzenleyen kurumun yapısal adresi (ilçe/il) bulunmalıdır.");
        }

        if (snapshot.Alici.KurumsalMi)
        {
            if (string.IsNullOrWhiteSpace(snapshot.Alici.MusteriUnvan) || string.IsNullOrWhiteSpace(snapshot.Alici.MusteriVergiNo))
            {
                throw new EBelgeUblAuthoritativeFieldMissingException("Kurumsal alıcı için unvan ve VKN bulunmalıdır.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(snapshot.Alici.MusteriAd) ||
                string.IsNullOrWhiteSpace(snapshot.Alici.MusteriSoyad) ||
                string.IsNullOrWhiteSpace(snapshot.Alici.MusteriTcKimlikNo))
            {
                throw new EBelgeUblAuthoritativeFieldMissingException("Gerçek kişi alıcı için ayrı ad, ayrı soyad ve TCKN bulunmalıdır.");
            }
        }

        if (string.IsNullOrWhiteSpace(snapshot.Alici.Ilce) || string.IsNullOrWhiteSpace(snapshot.Alici.Il))
        {
            throw new EBelgeUblAuthoritativeFieldMissingException("Alıcının yapısal adresi (ilçe/il) bulunmalıdır.");
        }
    }

    // ---- md.12: XML üretilmeden ÖNCE toplamları merkezî hesaplayıcıyla yeniden doğrula ----

    private static void ValidateMonetaryTotals(EBelgeCanonicalSnapshotV2 snapshot)
    {
        var katkilar = snapshot.Satirlar.Select(s =>
            new SatisBelgesiTutarHesaplayici.SatirTutarKatkisi(s.Matrah, s.KdvTutari, s.SatirToplami));

        var uyusmazliklar = SatisBelgesiTutarHesaplayici.DogrulaBelgeToplamlari(
            katkilar, snapshot.ToplamMatrah, snapshot.ToplamKdv, snapshot.GenelToplam);

        if (uyusmazliklar.Count > 0)
        {
            throw new EBelgeUblMonetaryTotalMismatchException(uyusmazliklar);
        }
    }

    // ---- XML üretimi ----

    private static byte[] BuildXml(EBelgeCanonicalSnapshotV2 snapshot)
    {
        using var memoryStream = new MemoryStream();

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = false,
            NewLineHandling = NewLineHandling.None,
            OmitXmlDeclaration = false,
            CloseOutput = true,
            ConformanceLevel = ConformanceLevel.Document,
        };

        using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement(string.Empty, "Invoice", NsInvoice);
            writer.WriteAttributeString("xmlns", "cac", null, NsCac);
            writer.WriteAttributeString("xmlns", "cbc", null, NsCbc);
            writer.WriteAttributeString("xmlns", "ext", null, NsExt);

            WriteHeader(writer, snapshot);
            WriteParty(writer, "AccountingSupplierParty", BuildSupplierParty(snapshot));
            WriteParty(writer, "AccountingCustomerParty", BuildCustomerParty(snapshot));
            WriteDocumentTaxTotal(writer, snapshot);
            WriteLegalMonetaryTotal(writer, snapshot);

            foreach (var satir in snapshot.Satirlar)
            {
                WriteInvoiceLine(writer, satir);
            }

            writer.WriteEndElement(); // Invoice
            writer.WriteEndDocument();
        }

        return memoryStream.ToArray();
    }

    private static void WriteHeader(XmlWriter w, EBelgeCanonicalSnapshotV2 snapshot)
    {
        WriteCbc(w, "UBLVersionID", "2.1");
        WriteCbc(w, "CustomizationID", "TR1.2");
        WriteCbc(w, "ProfileID", snapshot.Belge.ProfileID);
        WriteCbc(w, "ID", snapshot.Belge.ResmiFaturaNo!);
        WriteCbc(w, "CopyIndicator", "false");
        WriteCbc(w, "UUID", snapshot.Belge.EBelgeUuid);
        WriteCbc(w, "IssueDate", snapshot.Belge.FaturaTarihiTrt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        WriteCbc(w, "IssueTime", snapshot.Belge.FaturaSaatiTrt.ToString("HH:mm:ss", CultureInfo.InvariantCulture));
        WriteCbc(w, "InvoiceTypeCode", snapshot.Belge.InvoiceTypeCode);
        WriteCbc(w, "DocumentCurrencyCode", ParaBirimi);
    }

    private sealed record PartyBilgisi(
        string KimlikSchemeId,
        string KimlikNo,
        string? Unvan,
        string? Ad,
        string? Soyad,
        string? SokakAdi,
        string? BinaNo,
        string? Ilce,
        string? Il,
        string? PostaKodu,
        string? UlkeKodu,
        string? UlkeAdi,
        string? VergiDairesi,
        string Telefon,
        string? Eposta);

    private static PartyBilgisi BuildSupplierParty(EBelgeCanonicalSnapshotV2 snapshot)
    {
        var k = snapshot.Kurum;
        return new PartyBilgisi(
            "VKN", k.VergiNo!, k.KurumUnvani, null, null,
            k.SokakAdi, k.BinaNo, k.Ilce, k.Il, k.PostaKodu, k.UlkeKodu, k.UlkeAdi,
            k.VergiDairesi, k.Telefon, k.Eposta);
    }

    private static PartyBilgisi BuildCustomerParty(EBelgeCanonicalSnapshotV2 snapshot)
    {
        var a = snapshot.Alici;
        if (a.KurumsalMi)
        {
            return new PartyBilgisi(
                "VKN", a.MusteriVergiNo!, a.MusteriUnvan, null, null,
                a.SokakAdi, a.BinaNo, a.Ilce, a.Il, a.PostaKodu, a.UlkeKodu, a.UlkeAdi,
                a.MusteriVergiDairesi, a.MusteriTelefon ?? string.Empty, a.MusteriEposta);
        }

        return new PartyBilgisi(
            "TCKN", a.MusteriTcKimlikNo!, null, a.MusteriAd, a.MusteriSoyad,
            a.SokakAdi, a.BinaNo, a.Ilce, a.Il, a.PostaKodu, a.UlkeKodu, a.UlkeAdi,
            null, a.MusteriTelefon ?? string.Empty, a.MusteriEposta);
    }

    private static void WriteParty(XmlWriter w, string wrapperElementName, PartyBilgisi p)
    {
        w.WriteStartElement("cac", wrapperElementName, NsCac);
        w.WriteStartElement("cac", "Party", NsCac);

        w.WriteStartElement("cac", "PartyIdentification", NsCac);
        w.WriteStartElement("cbc", "ID", NsCbc);
        w.WriteAttributeString("schemeID", p.KimlikSchemeId);
        w.WriteString(p.KimlikNo);
        w.WriteEndElement(); // cbc:ID
        w.WriteEndElement(); // cac:PartyIdentification

        if (p.Unvan is not null)
        {
            w.WriteStartElement("cac", "PartyName", NsCac);
            WriteCbc(w, "Name", p.Unvan);
            w.WriteEndElement();
        }

        var adresVar = !string.IsNullOrWhiteSpace(p.SokakAdi) || !string.IsNullOrWhiteSpace(p.BinaNo) ||
            !string.IsNullOrWhiteSpace(p.Ilce) || !string.IsNullOrWhiteSpace(p.Il) ||
            !string.IsNullOrWhiteSpace(p.PostaKodu) || !string.IsNullOrWhiteSpace(p.UlkeKodu);

        if (adresVar)
        {
            w.WriteStartElement("cac", "PostalAddress", NsCac);
            if (!string.IsNullOrWhiteSpace(p.SokakAdi)) WriteCbc(w, "StreetName", p.SokakAdi!);
            if (!string.IsNullOrWhiteSpace(p.BinaNo)) WriteCbc(w, "BuildingNumber", p.BinaNo!);
            if (!string.IsNullOrWhiteSpace(p.Ilce)) WriteCbc(w, "CitySubdivisionName", p.Ilce!);
            if (!string.IsNullOrWhiteSpace(p.Il)) WriteCbc(w, "CityName", p.Il!);
            if (!string.IsNullOrWhiteSpace(p.PostaKodu)) WriteCbc(w, "PostalZone", p.PostaKodu!);
            if (!string.IsNullOrWhiteSpace(p.UlkeKodu) || !string.IsNullOrWhiteSpace(p.UlkeAdi))
            {
                w.WriteStartElement("cac", "Country", NsCac);
                if (!string.IsNullOrWhiteSpace(p.UlkeKodu)) WriteCbc(w, "IdentificationCode", p.UlkeKodu!);
                if (!string.IsNullOrWhiteSpace(p.UlkeAdi)) WriteCbc(w, "Name", p.UlkeAdi!);
                w.WriteEndElement(); // cac:Country
            }

            w.WriteEndElement(); // cac:PostalAddress
        }

        if (!string.IsNullOrWhiteSpace(p.VergiDairesi))
        {
            w.WriteStartElement("cac", "PartyTaxScheme", NsCac);
            w.WriteStartElement("cac", "TaxScheme", NsCac);
            WriteCbc(w, "Name", p.VergiDairesi);
            w.WriteEndElement(); // cac:TaxScheme
            w.WriteEndElement(); // cac:PartyTaxScheme
        }

        var iletisimVar = !string.IsNullOrWhiteSpace(p.Telefon) || !string.IsNullOrWhiteSpace(p.Eposta);
        if (iletisimVar)
        {
            w.WriteStartElement("cac", "Contact", NsCac);
            if (!string.IsNullOrWhiteSpace(p.Telefon)) WriteCbc(w, "Telephone", p.Telefon);
            if (!string.IsNullOrWhiteSpace(p.Eposta)) WriteCbc(w, "ElectronicMail", p.Eposta);
            w.WriteEndElement(); // cac:Contact
        }

        if (p.Ad is not null && p.Soyad is not null)
        {
            w.WriteStartElement("cac", "Person", NsCac);
            WriteCbc(w, "FirstName", p.Ad);
            WriteCbc(w, "FamilyName", p.Soyad);
            w.WriteEndElement(); // cac:Person
        }

        w.WriteEndElement(); // cac:Party
        w.WriteEndElement(); // wrapper
    }

    private static void WriteDocumentTaxTotal(XmlWriter w, EBelgeCanonicalSnapshotV2 snapshot)
    {
        var gruplar = snapshot.Satirlar
            .GroupBy(s => s.KdvOrani)
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Oran = g.Key,
                Matrah = g.Sum(x => x.Matrah),
                Kdv = g.Sum(x => x.KdvTutari),
            })
            .ToList();

        w.WriteStartElement("cac", "TaxTotal", NsCac);
        WriteTutarCbc(w, "TaxAmount", snapshot.ToplamKdv);

        foreach (var grup in gruplar)
        {
            w.WriteStartElement("cac", "TaxSubtotal", NsCac);
            WriteTutarCbc(w, "TaxableAmount", grup.Matrah);
            WriteTutarCbc(w, "TaxAmount", grup.Kdv);
            WriteCbc(w, "Percent", FormatDecimal(grup.Oran));
            WriteKdvTaxCategory(w, grup.Oran);
            w.WriteEndElement(); // cac:TaxSubtotal
        }

        w.WriteEndElement(); // cac:TaxTotal
    }

    private static void WriteKdvTaxCategory(XmlWriter w, decimal oran)
    {
        w.WriteStartElement("cac", "TaxCategory", NsCac);
        WriteCbc(w, "Percent", FormatDecimal(oran));
        w.WriteStartElement("cac", "TaxScheme", NsCac);
        WriteCbc(w, "Name", "KDV");
        WriteCbc(w, "TaxTypeCode", KdvTaxTypeCode);
        w.WriteEndElement(); // cac:TaxScheme
        w.WriteEndElement(); // cac:TaxCategory
    }

    private static void WriteLegalMonetaryTotal(XmlWriter w, EBelgeCanonicalSnapshotV2 snapshot)
    {
        w.WriteStartElement("cac", "LegalMonetaryTotal", NsCac);
        WriteTutarCbc(w, "LineExtensionAmount", snapshot.ToplamMatrah);
        WriteTutarCbc(w, "TaxExclusiveAmount", snapshot.ToplamMatrah);
        WriteTutarCbc(w, "TaxInclusiveAmount", snapshot.GenelToplam);
        WriteTutarCbc(w, "PayableAmount", snapshot.GenelToplam);
        w.WriteEndElement(); // cac:LegalMonetaryTotal
    }

    private static void WriteInvoiceLine(XmlWriter w, EBelgeCanonicalSatirV2 satir)
    {
        w.WriteStartElement("cac", "InvoiceLine", NsCac);

        WriteCbc(w, "ID", satir.SiraNo.ToString(CultureInfo.InvariantCulture));

        w.WriteStartElement("cbc", "InvoicedQuantity", NsCbc);
        w.WriteAttributeString("unitCode", satir.BirimKodu);
        w.WriteString(FormatDecimal(satir.Miktar));
        w.WriteEndElement(); // cbc:InvoicedQuantity

        WriteTutarCbc(w, "LineExtensionAmount", satir.Matrah);

        if (satir.IndirimTutari != 0m)
        {
            var brutTutar = SatisBelgesiTutarHesaplayici.Yuvarla(satir.Miktar * satir.BirimFiyat);

            w.WriteStartElement("cac", "AllowanceCharge", NsCac);
            WriteCbc(w, "ChargeIndicator", "false");
            WriteCbc(w, "AllowanceChargeReason", "İndirim");
            WriteTutarCbc(w, "Amount", satir.IndirimTutari);
            WriteTutarCbc(w, "BaseAmount", brutTutar);
            w.WriteEndElement(); // cac:AllowanceCharge
        }

        w.WriteStartElement("cac", "TaxTotal", NsCac);
        WriteTutarCbc(w, "TaxAmount", satir.KdvTutari);
        w.WriteStartElement("cac", "TaxSubtotal", NsCac);
        WriteTutarCbc(w, "TaxableAmount", satir.Matrah);
        WriteTutarCbc(w, "TaxAmount", satir.KdvTutari);
        WriteCbc(w, "Percent", FormatDecimal(satir.KdvOrani));
        WriteKdvTaxCategory(w, satir.KdvOrani);
        w.WriteEndElement(); // cac:TaxSubtotal
        w.WriteEndElement(); // cac:TaxTotal

        w.WriteStartElement("cac", "Item", NsCac);
        WriteCbc(w, "Name", satir.Aciklama);
        w.WriteEndElement(); // cac:Item

        w.WriteStartElement("cac", "Price", NsCac);
        WriteTutarCbc(w, "PriceAmount", satir.BirimFiyat);
        w.WriteEndElement(); // cac:Price

        w.WriteEndElement(); // cac:InvoiceLine
    }

    private static void WriteCbc(XmlWriter w, string localName, string value)
    {
        w.WriteElementString("cbc", localName, NsCbc, value);
    }

    private static void WriteTutarCbc(XmlWriter w, string localName, decimal deger)
    {
        w.WriteStartElement("cbc", localName, NsCbc);
        w.WriteAttributeString("currencyID", ParaBirimi);
        w.WriteString(FormatDecimal(deger));
        w.WriteEndElement();
    }

    private static string FormatDecimal(decimal deger) => deger.ToString("0.00", CultureInfo.InvariantCulture);
}
