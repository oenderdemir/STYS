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
        ErisimTeshisModulTanimi.Genel("ek-hizmet-tanim-yonetimi", "Ek Hizmet Tanimlari", "/ek-hizmet-tanimlari", StructurePermissions.EkHizmetTanimYonetimi.Menu, StructurePermissions.EkHizmetTanimYonetimi.View, StructurePermissions.EkHizmetTanimYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("ek-hizmet-tesis-atama-yonetimi", "Ek Hizmet Atamalari", "/ek-hizmet-atamalari", StructurePermissions.EkHizmetTesisAtamaYonetimi.Menu, StructurePermissions.EkHizmetTesisAtamaYonetimi.View, StructurePermissions.EkHizmetTesisAtamaYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("ek-hizmet-tarife-yonetimi", "Ek Hizmet Tarifeleri", "/ek-hizmet-tarifeleri", StructurePermissions.EkHizmetTarifeYonetimi.Menu, StructurePermissions.EkHizmetTarifeYonetimi.View, StructurePermissions.EkHizmetTarifeYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("konaklama-tipi-tanim-yonetimi", "Konaklama Tipi Tanimlari", "/konaklama-tipi-tanimlari", StructurePermissions.KonaklamaTipiTanimYonetimi.Menu, StructurePermissions.KonaklamaTipiTanimYonetimi.View, StructurePermissions.KonaklamaTipiTanimYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("konaklama-tipi-tesis-atama-yonetimi", "Konaklama Tipi Tesis Atamalari", "/konaklama-tipi-atamalari", StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.Menu, StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.View, StructurePermissions.KonaklamaTipiTesisAtamaYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("misafir-tipi-tanim-yonetimi", "Misafir Tipi Tanimlari", "/misafir-tipi-tanimlari", StructurePermissions.MisafirTipiTanimYonetimi.Menu, StructurePermissions.MisafirTipiTanimYonetimi.View, StructurePermissions.MisafirTipiTanimYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("misafir-tipi-tesis-atama-yonetimi", "Misafir Tipi Tesis Atamalari", "/misafir-tipi-atamalari", StructurePermissions.MisafirTipiTesisAtamaYonetimi.Menu, StructurePermissions.MisafirTipiTesisAtamaYonetimi.View, StructurePermissions.MisafirTipiTesisAtamaYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("kamp-programi-tanim-yonetimi", "Kamp Programlari", "/kamp-programlari", StructurePermissions.KampProgramiTanimYonetimi.Menu, StructurePermissions.KampProgramiTanimYonetimi.View, StructurePermissions.KampProgramiTanimYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("kamp-donemi-tanim-yonetimi", "Kamp Donemleri", "/kamp-donemleri", StructurePermissions.KampDonemiTanimYonetimi.Menu, StructurePermissions.KampDonemiTanimYonetimi.View, StructurePermissions.KampDonemiTanimYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("kamp-donemi-tesis-atama-yonetimi", "Kamp Donemi Tesis Atamalari", "/kamp-donemi-atamalari", StructurePermissions.KampDonemiTesisAtamaYonetimi.Menu, StructurePermissions.KampDonemiTesisAtamaYonetimi.View, StructurePermissions.KampDonemiTesisAtamaYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("kamp-tahsis-yonetimi", "Kamp Tahsisleri", "/kamp-tahsisleri", StructurePermissions.KampTahsisYonetimi.Menu, StructurePermissions.KampTahsisYonetimi.View, StructurePermissions.KampTahsisYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("kamp-rezervasyon-yonetimi", "Kamp Rezervasyonlari", "/kamp-rezervasyonlari", StructurePermissions.KampRezervasyonYonetimi.Menu, StructurePermissions.KampRezervasyonYonetimi.View, StructurePermissions.KampRezervasyonYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("kamp-iade-yonetimi", "Kamp Iade Yonetimi", "/kamp-iade-yonetimi", StructurePermissions.KampIadeYonetimi.Menu, StructurePermissions.KampIadeYonetimi.View, StructurePermissions.KampIadeYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("sezon-yonetimi", "Sezon Yonetimi", "/sezon-kurallari", StructurePermissions.SezonYonetimi.Menu, StructurePermissions.SezonYonetimi.View, StructurePermissions.SezonYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-kullanim-blok-yonetimi", "Oda Bakim / Ariza", "/oda-bakim-ariza", StructurePermissions.OdaKullanimBlokYonetimi.Menu, StructurePermissions.OdaKullanimBlokYonetimi.View, StructurePermissions.OdaKullanimBlokYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("oda-temizlik-yonetimi", "Oda Temizlik Yonetimi", "/oda-temizlik-yonetimi", StructurePermissions.OdaTemizlikYonetimi.Menu, StructurePermissions.OdaTemizlikYonetimi.View, StructurePermissions.OdaTemizlikYonetimi.Manage),
        ErisimTeshisModulTanimi.Genel("indirim-kurali-yonetimi", "Indirim Kurali Yonetimi", "/indirim-kurallari", StructurePermissions.IndirimKuraliYonetimi.Menu, StructurePermissions.IndirimKuraliYonetimi.View, StructurePermissions.IndirimKuraliYonetimi.Manage),
        ErisimTeshisModulTanimi.TesisScoped("rezervasyon-yonetimi", "Rezervasyon Yonetimi", "/rezervasyon-yonetimi", StructurePermissions.RezervasyonYonetimi.Menu, StructurePermissions.RezervasyonYonetimi.View, StructurePermissions.RezervasyonYonetimi.Manage)
    ];
}
