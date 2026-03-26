using STYS;

namespace STYS.ErisimTeshis;

public static class ErisimTeshisModulTanimlari
{
    public static readonly IReadOnlyList<ErisimTeshisModulTanimi> Tumu =
    [
        ErisimTeshisModulTanimi.Genel("il-yonetimi", "Il Yonetimi", "/iller", StructurePermissions.IlYonetimi.Menu, StructurePermissions.IlYonetimi.View, StructurePermissions.IlYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("tesis-yonetimi", "Tesis Yonetimi", "/tesisler", StructurePermissions.TesisYonetimi.Menu, StructurePermissions.TesisYonetimi.View, StructurePermissions.TesisYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("bina-yonetimi", "Bina Yonetimi", "/binalar", StructurePermissions.BinaYonetimi.Menu, StructurePermissions.BinaYonetimi.View, StructurePermissions.BinaYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("isletme-alani-yonetimi", "Isletme Alani Yonetimi", "/isletme-alanlari", StructurePermissions.IsletmeAlaniYonetimi.Menu, StructurePermissions.IsletmeAlaniYonetimi.View, StructurePermissions.IsletmeAlaniYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-tipi-yonetimi", "Oda Tipi Yonetimi", "/oda-tipleri", StructurePermissions.OdaTipiYonetimi.Menu, StructurePermissions.OdaTipiYonetimi.View, StructurePermissions.OdaTipiYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-yonetimi", "Oda Yonetimi", "/odalar", StructurePermissions.OdaYonetimi.Menu, StructurePermissions.OdaYonetimi.View, StructurePermissions.OdaYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-ozellik-yonetimi", "Oda Ozellik Yonetimi", "/oda-ozellikler", StructurePermissions.OdaOzellikYonetimi.Menu, StructurePermissions.OdaOzellikYonetimi.View, StructurePermissions.OdaOzellikYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-fiyat-yonetimi", "Oda Fiyat Yonetimi", "/oda-fiyatlari", StructurePermissions.OdaFiyatYonetimi.Menu, StructurePermissions.OdaFiyatYonetimi.View, StructurePermissions.OdaFiyatYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("ek-hizmet-yonetimi", "Ek Hizmet Yonetimi", "/ek-hizmetler", StructurePermissions.EkHizmetYonetimi.Menu, StructurePermissions.EkHizmetYonetimi.View, StructurePermissions.EkHizmetYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("konaklama-tipi-yonetimi", "Konaklama Tipi Yonetimi", "/konaklama-tipleri", StructurePermissions.KonaklamaTipiYonetimi.Menu, StructurePermissions.KonaklamaTipiYonetimi.View, StructurePermissions.KonaklamaTipiYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("misafir-tipi-yonetimi", "Misafir Tipi Yonetimi", "/misafir-tipleri", StructurePermissions.MisafirTipiYonetimi.Menu, StructurePermissions.MisafirTipiYonetimi.View, StructurePermissions.MisafirTipiYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("sezon-yonetimi", "Sezon Yonetimi", "/sezon-kurallari", StructurePermissions.SezonYonetimi.Menu, StructurePermissions.SezonYonetimi.View, StructurePermissions.SezonYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-kullanim-blok-yonetimi", "Oda Bakim / Ariza", "/oda-bakim-ariza", StructurePermissions.OdaKullanimBlokYonetimi.Menu, StructurePermissions.OdaKullanimBlokYonetimi.View, StructurePermissions.OdaKullanimBlokYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-temizlik-yonetimi", "Oda Temizlik Yonetimi", "/oda-temizlik-yonetimi", StructurePermissions.OdaTemizlikYonetimi.Menu, StructurePermissions.OdaTemizlikYonetimi.View, StructurePermissions.OdaTemizlikYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("indirim-kurali-yonetimi", "Indirim Kurali Yonetimi", "/indirim-kurallari", StructurePermissions.IndirimKuraliYonetimi.Menu, StructurePermissions.IndirimKuraliYonetimi.View, StructurePermissions.IndirimKuraliYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("rezervasyon-yonetimi", "Rezervasyon Yonetimi", "/rezervasyon-yonetimi", StructurePermissions.RezervasyonYonetimi.Menu, StructurePermissions.RezervasyonYonetimi.View, StructurePermissions.RezervasyonYonetimi.Manage)
    ];
}
