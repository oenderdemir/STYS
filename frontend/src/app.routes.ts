import { Routes } from '@angular/router';
import { authChildGuard, authGuard, guestGuard } from './app/pages/auth';
import { AppLayout } from './app/layout/component/app.layout';
import { BinaYonetimi } from './app/pages/bina-yonetimi/bina-yonetimi';
import { Dashboard } from './app/pages/dashboard/dashboard';
import { Documentation } from './app/pages/documentation/documentation';
import { IndirimKuraliYonetimi } from './app/pages/indirim-kurali-yonetimi/indirim-kurali-yonetimi';
import { IlYonetimi } from './app/pages/il-yonetimi/il-yonetimi';
import { IsletmeAlaniSinifiYonetimi } from './app/pages/isletme-alani-sinifi-yonetimi/isletme-alani-sinifi-yonetimi';
import { KonaklamaTipiYonetimi } from './app/pages/konaklama-tipi-yonetimi/konaklama-tipi-yonetimi';
import { KullaniciGrupYonetimi } from './app/pages/kullanici-grup-yonetimi/kullanici-grup-yonetimi';
import { KullaniciYonetimi } from './app/pages/kullanici-yonetimi/kullanici-yonetimi';
import { Landing } from './app/pages/landing/landing';
import { MenuYonetimi } from './app/pages/menu-yonetimi/menu-yonetimi';
import { MisafirTipiYonetimi } from './app/pages/misafir-tipi-yonetimi/misafir-tipi-yonetimi';
import { Notfound } from './app/pages/notfound/notfound';
import { OdaFiyatYonetimi } from './app/pages/oda-fiyat-yonetimi/oda-fiyat-yonetimi';
import { OdaTipiYonetimi } from './app/pages/oda-tipi-yonetimi/oda-tipi-yonetimi';
import { OdaSinifiYonetimi } from './app/pages/oda-sinifi-yonetimi/oda-sinifi-yonetimi';
import { OdaOzellikYonetimi } from './app/pages/oda-ozellik-yonetimi/oda-ozellik-yonetimi';
import { OdaYonetimi } from './app/pages/oda-yonetimi/oda-yonetimi';
import { RolYonetimi } from './app/pages/rol-yonetimi/rol-yonetimi';
import { RezervasyonYonetimi } from './app/pages/rezervasyon-yonetimi/rezervasyon-yonetimi';
import { RezervasyonDashboard } from './app/pages/rezervasyon-dashboard/rezervasyon-dashboard';
import { SezonYonetimi } from './app/pages/sezon-yonetimi/sezon-yonetimi';
import { TesisYonetimi } from './app/pages/tesis-yonetimi/tesis-yonetimi';
import { UlkeYonetimi } from './app/pages/ulke-yonetimi/ulke-yonetimi';

export const appRoutes: Routes = [
    { path: 'auth', canActivate: [guestGuard], loadChildren: () => import('./app/pages/auth/auth.routes') },
    { path: 'landing', component: Landing },
    { path: 'notfound', component: Notfound },
    {
        path: '',
        component: AppLayout,
        canActivate: [authGuard],
        canActivateChild: [authChildGuard],
        children: [
            { path: '', component: Dashboard },
            { path: 'kullanicilar', component: KullaniciYonetimi },
            { path: 'kullanici-gruplar', component: KullaniciGrupYonetimi },
            { path: 'yetkiler', component: RolYonetimi },
            { path: 'menuler', component: MenuYonetimi },
            { path: 'ulkeler', component: UlkeYonetimi },
            { path: 'iller', component: IlYonetimi },
            { path: 'tesisler', component: TesisYonetimi },
            { path: 'binalar', component: BinaYonetimi },
            { path: 'isletme-alanlari', component: IsletmeAlaniSinifiYonetimi },
            { path: 'isletme-alani-siniflari', component: IsletmeAlaniSinifiYonetimi },
            { path: 'oda-siniflari', component: OdaSinifiYonetimi },
            { path: 'oda-tipleri', component: OdaTipiYonetimi },
            { path: 'oda-ozellikler', component: OdaOzellikYonetimi },
            { path: 'odalar', component: OdaYonetimi },
            { path: 'oda-fiyatlari', component: OdaFiyatYonetimi },
            { path: 'indirim-kurallari', component: IndirimKuraliYonetimi },
            { path: 'konaklama-tipleri', component: KonaklamaTipiYonetimi },
            { path: 'misafir-tipleri', component: MisafirTipiYonetimi },
            { path: 'sezon-kurallari', component: SezonYonetimi },
            { path: 'rezervasyon-yonetimi', component: RezervasyonYonetimi },
            { path: 'rezervasyon-dashboard', component: RezervasyonDashboard },
            { path: 'uikit', loadChildren: () => import('./app/pages/uikit/uikit.routes') },
            { path: 'documentation', component: Documentation },
            { path: 'pages', loadChildren: () => import('./app/pages/pages.routes') }
        ]
    },
    { path: '**', redirectTo: '/notfound' }
];
