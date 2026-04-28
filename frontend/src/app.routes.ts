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
import { LisansYonetimi } from './app/pages/lisans-yonetimi/lisans-yonetimi';
import { SezonYonetimi } from './app/pages/sezon-yonetimi/sezon-yonetimi';
import { TesisYonetimi } from './app/pages/tesis-yonetimi/tesis-yonetimi';
import { UlkeYonetimi } from './app/pages/ulke-yonetimi/ulke-yonetimi';

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
            { path: '', component: RezervasyonDashboard, data: { breadcrumb: ['Ana Menu', 'Dashboard'] } },
            { path: 'kullanicilar', component: KullaniciYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Kullanicilar'] } },
            { path: 'kullanici-gruplar', component: KullaniciGrupYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Kullanici Gruplari'] } },
            { path: 'yetkiler', component: RolYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Yetkiler'] } },
            { path: 'menuler', component: MenuYonetimi, data: { breadcrumb: ['Yetkilendirme', 'Menuler'] } },
            { path: 'ulkeler', component: UlkeYonetimi, data: { breadcrumb: ['Sistem', 'Ulke Yonetimi'] } },
            { path: 'iller', component: IlYonetimi, data: { breadcrumb: ['Sistem', 'Il Yonetimi'] } },
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
            { path: 'rezervasyon-dashboard', component: RezervasyonDashboard, data: { breadcrumb: ['Ana Menu', 'Rezervasyon Dashboard'] } },
            { path: 'restoran-yonetimi', component: RestoranYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Restoran Yonetimi'] } },
            { path: 'restoran-kategori-havuzu', component: RestoranKategoriHavuzuYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Kategori Havuzu'] } },
            { path: 'restoran-masa-yonetimi', component: RestoranMasaYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Masa Yonetimi'] } },
            { path: 'restoran-menu-yonetimi', component: RestoranMenuYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Menu Yonetimi'] } },
            { path: 'restoran-siparis-yonetimi', component: RestoranSiparisYonetimi, data: { breadcrumb: ['Isletme', 'Restoran', 'Siparis Yonetimi'] } },
            { path: 'garson-servis', component: GarsonServisPage, data: { breadcrumb: ['Isletme', 'Restoran', 'Garson Servis'] } },
            { path: 'muhasebe/cari-kartlar', component: CariKartlarPage, data: { breadcrumb: ['Muhasebe', 'Cari Kartlar'] } },
            { path: 'muhasebe/cari-hareketler', component: CariHareketlerPage, data: { breadcrumb: ['Muhasebe', 'Cari Hareketler'] } },
            { path: 'muhasebe/kasa-hareketleri', component: KasaHareketleriPage, data: { breadcrumb: ['Muhasebe', 'Kasa Hareketleri'] } },
            { path: 'muhasebe/banka-hareketleri', component: BankaHareketleriPage, data: { breadcrumb: ['Muhasebe', 'Banka Hareketleri'] } },
            { path: 'muhasebe/kasa-banka-hesaplari', component: KasaBankaHesaplariPage, data: { breadcrumb: ['Muhasebe', 'Finansal Hesaplar'] } },
            { path: 'muhasebe/hesaplar', component: HesaplarPage, data: { breadcrumb: ['Muhasebe', 'Hesaplar'] } },
            { path: 'muhasebe/tahsilat-odeme-belgeleri', component: TahsilatOdemeBelgeleriPage, data: { breadcrumb: ['Muhasebe', 'Tahsilat/Odeme Belgeleri'] } },
            { path: 'muhasebe/tasinir-kodlari', component: TasinirKodlariPage, data: { breadcrumb: ['Muhasebe', 'Tasinir Kodlari'] } },
            { path: 'muhasebe/tasinir-kartlari', component: TasinirKartlariPage, data: { breadcrumb: ['Muhasebe', 'Tasinir Kartlari'] } },
            { path: 'muhasebe/depolar', component: DepolarPage, data: { breadcrumb: ['Muhasebe', 'Depolar'] } },
            { path: 'muhasebe/stok-hareketleri', component: StokHareketleriPage, data: { breadcrumb: ['Muhasebe', 'Stok Hareketleri'] } },
            { path: 'muhasebe/hesap-plani', component: MuhasebeHesapPlaniPage, data: { breadcrumb: ['Muhasebe', 'Hesap Plani'] } },
            { path: 'muhasebe/paket-turleri', component: PaketTurleriPage, data: { breadcrumb: ['Muhasebe', 'Paket Turleri'] } },
            { path: 'lisans-yonetimi', component: LisansYonetimi, data: { breadcrumb: ['Sistem', 'Lisans Yonetimi'] } },
            { path: 'uikit', loadChildren: () => import('./app/pages/uikit/uikit.routes'), data: { breadcrumb: ['Ana Menu', 'UI Kit'] } },
            { path: 'documentation', component: Documentation, data: { breadcrumb: ['Sistem', 'Dokumantasyon'] } },
            { path: 'pages', loadChildren: () => import('./app/pages/pages.routes'), data: { breadcrumb: ['Ana Menu', 'Sayfalar'] } }
        ]
    },
    { path: '**', redirectTo: '/notfound' }
];
