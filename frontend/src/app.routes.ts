import { Routes } from '@angular/router';
import { authChildGuard, authGuard, guestGuard } from './app/pages/auth';
import { AppLayout } from './app/layout/component/app.layout';
import { BinaYonetimi } from './app/pages/bina-yonetimi/bina-yonetimi';
import { Dashboard } from './app/pages/dashboard/dashboard';
import { Documentation } from './app/pages/documentation/documentation';
import { ErisimTeshis } from './app/pages/erisim-teshis/erisim-teshis';
import { EkHizmetAtamaYonetimi } from './app/pages/ek-hizmet-yonetimi/ek-hizmet-atama-yonetimi';
import { EkHizmetTanimYonetimi } from './app/pages/ek-hizmet-yonetimi/ek-hizmet-tanim-yonetimi';
import { EkHizmetYonetimi } from './app/pages/ek-hizmet-yonetimi/ek-hizmet-yonetimi';
import { IndirimKuraliYonetimi } from './app/pages/indirim-kurali-yonetimi/indirim-kurali-yonetimi';
import { IlYonetimi } from './app/pages/il-yonetimi/il-yonetimi';
import { IsletmeAlaniSinifiYonetimi } from './app/pages/isletme-alani-sinifi-yonetimi/isletme-alani-sinifi-yonetimi';
import { KampDonemiAtamaYonetimi } from './app/pages/kamp-yonetimi/kamp-donemi-atama-yonetimi';
import { KampDonemiTanimYonetimi } from './app/pages/kamp-yonetimi/kamp-donemi-tanim-yonetimi';
import { KampTarifeleriYonetimiComponent } from './app/pages/kamp-yonetimi/kamp-tarifeleri-yonetimi';
import { KampBenimBasvurularimPage } from './app/pages/kamp-yonetimi/kamp-benim-basvurularim';
import { KampBasvuruPage } from './app/pages/kamp-yonetimi/kamp-basvuru';
import { KampIadeYonetimiPage } from './app/pages/kamp-yonetimi/kamp-iade-yonetimi';
import { KampPuanKuraliYonetimiPage } from './app/pages/kamp-yonetimi/kamp-puan-kurali-yonetimi';
import { KampRezervasyonlariPage } from './app/pages/kamp-yonetimi/kamp-rezervasyonlari';
import { KampTahsisYonetimiPage } from './app/pages/kamp-yonetimi/kamp-tahsis-yonetimi';
import { KampProgramiTanimYonetimi } from './app/pages/kamp-yonetimi/kamp-programi-tanim-yonetimi';
import { KonaklamaTipiTanimYonetimi } from './app/pages/konaklama-tipi-yonetimi/konaklama-tipi-tanim-yonetimi';
import { KonaklamaTipiYonetimi } from './app/pages/konaklama-tipi-yonetimi/konaklama-tipi-yonetimi';
import { KullaniciGrupYonetimi } from './app/pages/kullanici-grup-yonetimi/kullanici-grup-yonetimi';
import { KullaniciYonetimi } from './app/pages/kullanici-yonetimi/kullanici-yonetimi';
import { Landing } from './app/pages/landing/landing';
import { MenuYonetimi } from './app/pages/menu-yonetimi/menu-yonetimi';
import { MisafirTipiTanimYonetimi } from './app/pages/misafir-tipi-yonetimi/misafir-tipi-tanim-yonetimi';
import { MisafirTipiYonetimi } from './app/pages/misafir-tipi-yonetimi/misafir-tipi-yonetimi';
import { Notfound } from './app/pages/notfound/notfound';
import { OdaFiyatYonetimi } from './app/pages/oda-fiyat-yonetimi/oda-fiyat-yonetimi';
import { OdaKullanimBlokYonetimi } from './app/pages/oda-kullanim-blok-yonetimi/oda-kullanim-blok-yonetimi';
import { OdaTemizlikYonetimi } from './app/pages/oda-temizlik-yonetimi/oda-temizlik-yonetimi';
import { OdaTipiYonetimi } from './app/pages/oda-tipi-yonetimi/oda-tipi-yonetimi';
import { OdaSinifiYonetimi } from './app/pages/oda-sinifi-yonetimi/oda-sinifi-yonetimi';
import { OdaOzellikYonetimi } from './app/pages/oda-ozellik-yonetimi/oda-ozellik-yonetimi';
import { OdaYonetimi } from './app/pages/oda-yonetimi/oda-yonetimi';
import { RolYonetimi } from './app/pages/rol-yonetimi/rol-yonetimi';
import { RezervasyonYonetimi } from './app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi';
import { RezervasyonDashboard } from './app/pages/rezervasyon-dashboard/rezervasyon-dashboard';
import { OdaRezervasyonTakvimi } from './app/pages/oda-rezervasyon-takvimi/oda-rezervasyon-takvimi';
import { OdaDolulukAylikComponent } from './app/pages/raporlar/oda-doluluk-aylik/oda-doluluk-aylik';
import { KonaklamaKisiSayisiRaporComponent } from './app/pages/raporlar/konaklama-kisi-sayisi/konaklama-kisi-sayisi-rapor';
import { OdemeDurumuRaporComponent } from './app/pages/raporlar/odeme-durumu/odeme-durumu-rapor';
import { GunlukGirisCikisRaporComponent } from './app/pages/raporlar/gunluk-giris-cikis/gunluk-giris-cikis-rapor';
import { OdaMusaitlikRaporComponent } from './app/pages/raporlar/oda-musaitlik/oda-musaitlik-rapor';
import { OdaTipiDolulukRaporComponent } from './app/pages/raporlar/oda-tipi-doluluk/oda-tipi-doluluk-rapor';
import { OrtalamaKonaklamaSuresiRaporComponent } from './app/pages/raporlar/ortalama-konaklama-suresi/ortalama-konaklama-suresi-rapor';
import { RezervasyonDurumDagilimiRaporComponent } from './app/pages/raporlar/rezervasyon-durum-dagilimi/rezervasyon-durum-dagilimi-rapor';
import { GecikenCheckInRaporComponent } from './app/pages/raporlar/geciken-check-in/geciken-check-in-rapor';
import { RestoranYonetimi } from './app/pages/restoran-yonetimi/restoran-yonetimi';
import { RestoranMasaYonetimi } from './app/pages/restoran-yonetimi/restoran-masa-yonetimi';
import { RestoranMenuYonetimi } from './app/pages/restoran-yonetimi/restoran-menu-yonetimi';
import { RestoranKategoriHavuzuYonetimi } from './app/pages/restoran-yonetimi/restoran-kategori-havuzu-yonetimi';
import { RestoranSiparisYonetimi } from './app/pages/restoran-yonetimi/restoran-siparis-yonetimi';
import { GarsonServisPage } from './app/pages/restoran-yonetimi/garson-servis';
import { MusteriMenuPage } from './app/pages/musteri-menu/musteri-menu';
import { CariKartlarPage } from './app/pages/muhasebe/cari-kartlar/cari-kartlar';
import { CariHareketlerPage } from './app/pages/muhasebe/cari-hareketler/cari-hareketler';
import { KasaHareketleriPage } from './app/pages/muhasebe/kasa-hareketleri/kasa-hareketleri';
import { BankaHareketleriPage } from './app/pages/muhasebe/banka-hareketleri/banka-hareketleri';
import { TahsilatOdemeBelgeleriPage } from './app/pages/muhasebe/tahsilat-odeme-belgeleri/tahsilat-odeme-belgeleri';
import { TasinirKodlariPage } from './app/pages/muhasebe/tasinir-kodlari/tasinir-kodlari';
import { TasinirKartlariPage } from './app/pages/muhasebe/tasinir-kartlari/tasinir-kartlari';
import { DepolarPage } from './app/pages/muhasebe/depolar/depolar';
import { StokHareketleriPage } from './app/pages/muhasebe/stok-hareketleri/stok-hareketleri';
import { MuhasebeHesapPlaniPage } from './app/pages/muhasebe/muhasebe-hesap-plani/muhasebe-hesap-plani';
import { KasaBankaHesaplariPage } from './app/pages/muhasebe/kasa-banka-hesaplari/kasa-banka-hesaplari';
import { HesaplarPage } from './app/pages/muhasebe/hesaplar/hesaplar';
import { PaketTurleriPage } from './app/pages/muhasebe/paket-turleri/paket-turleri';
import { HizliMizanComponent } from './app/pages/muhasebe/hizli-mizan/hizli-mizan.component';
import { MuavinDefterComponent } from './app/pages/muhasebe/muavin-defter/muavin-defter.component';
import { TasinirFisTaslagiPageComponent } from './app/pages/muhasebe/tasinir-fis-taslagi/tasinir-fis-taslagi-page.component';
import { YevmiyeDefteriComponent } from './app/pages/muhasebe/yevmiye-defteri/yevmiye-defteri.component';
import { MuhasebeDashboardComponent } from './app/pages/muhasebe/dashboard/muhasebe-dashboard.component';
import { MuhasebeFislerComponent } from './app/pages/muhasebe/fisler/muhasebe-fisler.component';
import { MuhasebeFisOlusturComponent } from './app/pages/muhasebe/fis-olustur/muhasebe-fis-olustur.component';
import { MuhasebeDonemlerComponent } from './app/pages/muhasebe/donemler/muhasebe-donemler.component';
import { DonemKapanisKontrolComponent } from './app/pages/muhasebe/donem-kapanis-kontrol/donem-kapanis-kontrol.component';
import { KdvIstisnaTanimlariComponent } from './app/pages/muhasebe/kdv-istisna-tanimlari/kdv-istisna-tanimlari.component';
import { KdvHareketRaporuComponent } from './app/pages/muhasebe/kdv-hareket-raporu/kdv-hareket-raporu.component';
import { KdvOzetRaporuComponent } from './app/pages/muhasebe/kdv-ozet-raporu/kdv-ozet-raporu.component';
import { KdvBeyannameHazirlikKontrolComponent } from './app/pages/muhasebe/kdv-beyanname-hazirlik-kontrol/kdv-beyanname-hazirlik-kontrol.component';
import { SatisBelgeleriComponent } from './app/pages/muhasebe/satis-belgeleri/satis-belgeleri.component';
import { TevkifatHesapEslemeleriPage } from './app/pages/muhasebe/tevkifat-hesap-eslemeleri/tevkifat-hesap-eslemeleri.component';
import { LisansYonetimi } from './app/pages/lisans-yonetimi/lisans-yonetimi';
import { SezonYonetimi } from './app/pages/sezon-yonetimi/sezon-yonetimi';
import { TesisYonetimi } from './app/pages/tesis-yonetimi/tesis-yonetimi';
import { KurumYonetimi } from './app/pages/kurum-yonetimi/kurum-yonetimi';
import { UlkeYonetimi } from './app/pages/ulke-yonetimi/ulke-yonetimi';
import { KbsBildirimMerkezi } from './app/pages/kbs-bildirim-merkezi/kbs-bildirim-merkezi';

export const appRoutes: Routes = [
    { path: 'auth', canActivate: [guestGuard], loadChildren: () => import('./app/pages/auth/auth.routes') },
    { path: 'landing', component: Landing },
    { path: 'notfound', component: Notfound },
    {
        path: 'kamp-basvurusu',
        component: AppLayout,
        children: [
            { path: '', component: KampBasvuruPage, data: { breadcrumb: ['Ana Menu', 'Kamp Basvurusu'] } }
        ]
    },
    { path: 'musteri-menu/:restoranId', component: MusteriMenuPage, data: { breadcrumb: ['Musteri Menusu'] } },
    {
        path: '',
        component: AppLayout,
        canActivate: [authGuard],
        canActivateChild: [authChildGuard],
        children: [
            { path: '', component: RezervasyonDashboard, data: { } },
            { path: 'kullanicilar', component: KullaniciYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Kullanicilar'] } },
            { path: 'kullanici-gruplar', component: KullaniciGrupYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Kullanici Gruplari'] } },
            { path: 'yetkiler', component: RolYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Yetkiler'] } },
            { path: 'menuler', component: MenuYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Menuler'] } },
            { path: 'ulkeler', component: UlkeYonetimi, data: { breadcrumb: ['Sistem', 'Ulke Yonetimi'] } },
            { path: 'iller', component: IlYonetimi, data: { breadcrumb: ['Sistem', 'Il Yonetimi'] } },
            { path: 'kurum-yonetimi', component: KurumYonetimi, data: { breadcrumb: ['Sistem', 'Kurum Yonetimi'] } },
            { path: 'tesisler', component: TesisYonetimi, data: { breadcrumb: ['Isletme', 'Tesis Yonetimi'] } },
            { path: 'binalar', component: BinaYonetimi, data: { breadcrumb: ['Tesis', 'Bina Yonetimi'] } },
            { path: 'isletme-alanlari', component: IsletmeAlaniSinifiYonetimi, data: { breadcrumb: ['Isletme', 'Isletme Alanlari'] } },
            { path: 'isletme-alani-siniflari', component: IsletmeAlaniSinifiYonetimi, data: { breadcrumb: ['Isletme', 'Isletme Alanlari'] } },
            { path: 'oda-siniflari', component: OdaSinifiYonetimi, data: { breadcrumb: ['Isletme', 'Oda Siniflari'] } },
            { path: 'oda-tipleri', component: OdaTipiYonetimi, data: { breadcrumb: ['Isletme', 'Oda Tipleri'] } },
            { path: 'oda-ozellikler', component: OdaOzellikYonetimi, data: { breadcrumb: ['Isletme', 'Oda Ozellikleri'] } },
            { path: 'odalar', component: OdaYonetimi, data: { breadcrumb: ['Tesis', 'Odalar'] } },
            { path: 'oda-fiyatlari', component: OdaFiyatYonetimi, data: { breadcrumb: ['Tesis', 'Oda Fiyatlari'] } },
            { path: 'ek-hizmet-tanimlari', component: EkHizmetTanimYonetimi, data: { breadcrumb: ['Tesis', 'Ek Hizmetler', 'Global Tanimlari'] } },
            { path: 'ek-hizmet-atamalari', component: EkHizmetAtamaYonetimi, data: { breadcrumb: ['Tesis', 'Ek Hizmetler', 'Tesis Atamalari'] } },
            { path: 'ek-hizmet-tarifeleri', component: EkHizmetYonetimi, data: { breadcrumb: ['Tesis', 'Ek Hizmetler', 'Tarifeler'] } },
            { path: 'ek-hizmetler', redirectTo: 'ek-hizmet-tarifeleri', pathMatch: 'full' },
            { path: 'erisim-teshis', component: ErisimTeshis, data: { breadcrumb: ['Sistem', 'Erisim Teshis'] } },
            { path: 'oda-bakim-ariza', component: OdaKullanimBlokYonetimi, data: { breadcrumb: ['Ana Menu', 'Oda Bakim/Ariza'] } },
            { path: 'oda-temizlik-yonetimi', component: OdaTemizlikYonetimi, data: { breadcrumb: ['Ana Menu', 'Oda Temizlik'] } },
            { path: 'indirim-kurallari', component: IndirimKuraliYonetimi, data: { breadcrumb: ['Isletme', 'Indirim Kurallari'] } },
            { path: 'konaklama-tipi-tanimlari', component: KonaklamaTipiTanimYonetimi, data: { breadcrumb: ['Isletme', 'Konaklama Tipleri', 'Global Tanimlari'] } },
            { path: 'konaklama-tipi-atamalari', component: KonaklamaTipiYonetimi, data: { breadcrumb: ['Isletme', 'Konaklama Tipleri', 'Tesis Atamalari'] } },
            { path: 'konaklama-tipleri', redirectTo: 'konaklama-tipi-atamalari', pathMatch: 'full' },
            { path: 'misafir-tipi-tanimlari', component: MisafirTipiTanimYonetimi, data: { breadcrumb: ['Isletme', 'Misafir Tipleri', 'Global Tanimlari'] } },
            { path: 'misafir-tipi-atamalari', component: MisafirTipiYonetimi, data: { breadcrumb: ['Isletme', 'Misafir Tipleri', 'Tesis Atamalari'] } },
            { path: 'misafir-tipleri', redirectTo: 'misafir-tipi-atamalari', pathMatch: 'full' },
            { path: 'kamp-programlari', component: KampProgramiTanimYonetimi, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Programlar'] } },
            { path: 'kamp-donemleri', component: KampDonemiTanimYonetimi, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Donemler'] } },
            { path: 'kamp-donemi-atamalari', component: KampDonemiAtamaYonetimi, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Tesis Atamalari'] } },
            { path: 'kamp-tahsisleri', component: KampTahsisYonetimiPage, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Tahsisler'] } },
            { path: 'kamp-rezervasyonlari', component: KampRezervasyonlariPage, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Rezervasyonlar'] } },
            { path: 'kamp-iade-yonetimi', component: KampIadeYonetimiPage, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Iade Yonetimi'] } },
            { path: 'kamp-puan-kurallari', component: KampPuanKuraliYonetimiPage, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Puan Kurallari'] } },
            { path: 'kamp-tarifeleri', component: KampTarifeleriYonetimiComponent, data: { breadcrumb: ['Isletme', 'Kamp Yonetimi', 'Tarifeler'] } },
            { path: 'kamp-basvurularim', component: KampBenimBasvurularimPage, data: { breadcrumb: ['Ana Menu', 'Kamp Basvurularim'] } },
            { path: 'sezon-kurallari', component: SezonYonetimi, data: { breadcrumb: ['Isletme', 'Sezon Kurallari'] } },
            { path: 'rezervasyon-yonetimi', component: RezervasyonYonetimi, data: { breadcrumb: ['Ana Menu', 'Rezervasyon Yonetimi'] } },
            { path: 'kbs-bildirim-merkezi', component: KbsBildirimMerkezi, data: { breadcrumb: ['Ana Menu', 'KBS Bildirim Merkezi'] } },
            { path: 'rezervasyon-dashboard', component: RezervasyonDashboard, data: { breadcrumb: ['Ana Menu', 'Rezervasyon Dashboard'] } },
            { path: 'oda-rezervasyon-takvimi', component: OdaRezervasyonTakvimi, data: { breadcrumb: ['Rezervasyon Yönetimi', 'Oda Rezervasyon Takvimi'] } },
            { path: 'raporlar/oda-doluluk-aylik', component: OdaDolulukAylikComponent, data: { breadcrumb: ['Raporlar', 'Aylık Oda Planı'] } },
            { path: 'raporlar/konaklama-kisi-sayisi', component: KonaklamaKisiSayisiRaporComponent, data: { breadcrumb: ['Raporlar', 'Aylık Konaklayan Kişi Sayısı'] } },
            { path: 'raporlar/odeme-durumu', component: OdemeDurumuRaporComponent, data: { breadcrumb: ['Raporlar', 'Ödeme Durumu / Borçlu Rezervasyonlar'] } },
            { path: 'raporlar/gunluk-giris-cikis', component: GunlukGirisCikisRaporComponent, data: { breadcrumb: ['Raporlar', 'Günlük Giriş-Çıkış Listesi'] } },
            { path: 'raporlar/oda-musaitlik', component: OdaMusaitlikRaporComponent, data: { breadcrumb: ['Raporlar', 'Boş Oda / Müsaitlik Raporu'] } },
            { path: 'raporlar/oda-tipi-doluluk', component: OdaTipiDolulukRaporComponent, data: { breadcrumb: ['Raporlar', 'Oda Tipi Bazlı Doluluk Raporu'] } },
            { path: 'raporlar/ortalama-konaklama-suresi', component: OrtalamaKonaklamaSuresiRaporComponent, data: { breadcrumb: ['Raporlar', 'Ortalama Konaklama Süresi Raporu'] } },
            { path: 'raporlar/rezervasyon-durum-dagilimi', component: RezervasyonDurumDagilimiRaporComponent, data: { breadcrumb: ['Raporlar', 'Rezervasyon Durum Dağılımı'] } },
            { path: 'raporlar/geciken-check-in', component: GecikenCheckInRaporComponent, data: { breadcrumb: ['Raporlar', 'Geciken Check-in Raporu'] } },
            { path: 'restoran-yonetimi', component: RestoranYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Restoran Yonetimi'] } },
            { path: 'restoran-kategori-havuzu', component: RestoranKategoriHavuzuYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Kategori Havuzu'] } },
            { path: 'restoran-masa-yonetimi', component: RestoranMasaYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Masa Yonetimi'] } },
            { path: 'restoran-menu-yonetimi', component: RestoranMenuYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Menu Yonetimi'] } },
            { path: 'restoran-siparis-yonetimi', component: RestoranSiparisYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Siparis Yonetimi'] } },
            { path: 'garson-servis', component: GarsonServisPage, data: { breadcrumb: ['Isletme', 'Restoran', 'Garson Servis'] } },
            { path: 'muhasebe/dashboard', component: MuhasebeDashboardComponent, data: { breadcrumb: ['Finans Yönetimi', 'Muhasebe Özet'] } },
            { path: 'muhasebe/cari-kartlar', component: CariKartlarPage, data: { breadcrumb: ['Cari Yönetim', 'Cari Kartlar'] } },
            { path: 'muhasebe/cari-hareketler', component: CariHareketlerPage, data: { breadcrumb: ['Cari Yönetim', 'Cari Hareketler'] } },
            { path: 'muhasebe/kasa-hareketleri', component: KasaHareketleriPage, data: { breadcrumb: ['Finans Yönetimi', 'Kasa Hareketleri'] } },
            { path: 'muhasebe/banka-hareketleri', component: BankaHareketleriPage, data: { breadcrumb: ['Finans Yönetimi', 'Banka Hareketleri'] } },
            { path: 'muhasebe/kasa-banka-hesaplari', component: KasaBankaHesaplariPage, data: { breadcrumb: ['Finans Yönetimi', 'Finansal Hesaplar'] } },
            { path: 'muhasebe/hesaplar', component: HesaplarPage, data: { breadcrumb: ['Cari Yönetim', 'Hesaplar'] } },
            { path: 'muhasebe/tahsilat-odeme-belgeleri', component: TahsilatOdemeBelgeleriPage, data: { breadcrumb: ['Cari Yönetim', 'Tahsilat/Odeme Belgeleri'] } },
            { path: 'muhasebe/tasinir-kodlari', component: TasinirKodlariPage, data: { breadcrumb: ['Taşınır / Demirbaş Yönetimi', 'Taşınır Kodları'] } },
            { path: 'muhasebe/tasinir-kartlari', component: TasinirKartlariPage, data: { breadcrumb: ['Taşınır / Demirbaş Yönetimi', 'Taşınır Kartları'] } },
            { path: 'muhasebe/depolar', component: DepolarPage, data: { breadcrumb: ['Stok & Depo Yönetimi', 'Depolar'] } },
            { path: 'muhasebe/stok-hareketleri', component: StokHareketleriPage, data: { breadcrumb: ['Stok & Depo Yönetimi', 'Stok Hareketleri'] } },
            { path: 'muhasebe/hesap-plani', component: MuhasebeHesapPlaniPage, data: { breadcrumb: ['Muhasebe Yönetimi', 'Hesap Plani'] } },
            { path: 'muhasebe/paket-turleri', component: PaketTurleriPage, data: { breadcrumb: ['Stok & Depo Yönetimi', 'Paket Turleri'] } },
            { path: 'muhasebe/hizli-mizan', component: HizliMizanComponent, data: { breadcrumb: ['Finans Yönetimi', 'Hızlı Mizan'] } },
            { path: 'muhasebe/tasinir-fis-taslagi', component: TasinirFisTaslagiPageComponent, data: { breadcrumb: ['Taşınır / Demirbaş Yönetimi', 'Taşınır Fiş Taslağı'] } },
            { path: 'muhasebe/fisler', component: MuhasebeFislerComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Muhasebe Fisleri'] } },
            { path: 'muhasebe/fisler/yeni', component: MuhasebeFisOlusturComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Muhasebe Fisleri', 'Yeni Fis'] } },
            { path: 'muhasebe/donemler', component: MuhasebeDonemlerComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Muhasebe Donemleri'] } },
            { path: 'muhasebe/donem-kapanis-kontrol', component: DonemKapanisKontrolComponent, data: { breadcrumb: ['Muhasebe', 'Dönem Kapanış Ön Kontrol'] } },
            { path: 'muhasebe/kdv-istisna-tanimlari', component: KdvIstisnaTanimlariComponent, data: { breadcrumb: ['Vergi & KDV İşlemleri', 'KDV İstisna Tanımları'] } },
            { path: 'muhasebe/tevkifat-hesap-eslemeleri', component: TevkifatHesapEslemeleriPage, data: { breadcrumb: ['Muhasebe', 'Tevkifat Hesap Eşlemeleri'] } },
            { path: 'muhasebe/kdv-hareket-raporu', component: KdvHareketRaporuComponent, data: { breadcrumb: ['Vergi & KDV İşlemleri', 'KDV Hareket Raporu'] } },
            { path: 'muhasebe/kdv-ozet-raporu', component: KdvOzetRaporuComponent, data: { breadcrumb: ['Vergi & KDV İşlemleri', 'KDV Özet Raporu'] } },
            { path: 'muhasebe/kdv-beyanname-hazirlik-kontrol', component: KdvBeyannameHazirlikKontrolComponent, data: { breadcrumb: ['Muhasebe', 'KDV Beyanname Hazırlık Kontrolü'] } },
            { path: 'muhasebe/satis-belgeleri', component: SatisBelgeleriComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Satış Belgeleri'], belgeModu: 'satis' } },
            { path: 'muhasebe/alis-belgeleri', component: SatisBelgeleriComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Alış Belgeleri'], belgeModu: 'alis' } },
            { path: 'muhasebe/yevmiye-defteri', component: YevmiyeDefteriComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Yevmiye Defteri'] } },
            { path: 'muhasebe/muavin-defter', component: MuavinDefterComponent, data: { breadcrumb: ['Muhasebe Yönetimi', 'Muavin Defter'] } },
            { path: 'lisans-yonetimi', component: LisansYonetimi, data: { breadcrumb: ['Sistem', 'Lisans Yonetimi'] } },
            { path: 'uikit', loadChildren: () => import('./app/pages/uikit/uikit.routes'), data: { breadcrumb: ['Ana Menu', 'UI Kit'] } },
            { path: 'documentation', component: Documentation, data: { breadcrumb: ['Sistem', 'Dokumantasyon'] } },
            { path: 'pages', loadChildren: () => import('./app/pages/pages.routes'), data: { breadcrumb: ['Ana Menu', 'Sayfalar'] } }
        ]
    },
    { path: '**', redirectTo: '/notfound' }
];
