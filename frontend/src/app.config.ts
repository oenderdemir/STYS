import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, LOCALE_ID, provideZonelessChangeDetection } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import { provideRouter, withEnabledBlockingInitialNavigation, withInMemoryScrolling } from '@angular/router';
import localeTr from '@angular/common/locales/tr';
import Aura from '@primeuix/themes/aura';
import { Translation } from 'primeng/api';
import { providePrimeNG } from 'primeng/config';
import { authTokenInterceptor } from './app/pages/auth';
import { appRoutes } from './app.routes';

registerLocaleData(localeTr, 'tr-TR');

const trLocale: Translation = {
    firstDayOfWeek: 1,
    dayNames: ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'],
    dayNamesShort: ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'],
    dayNamesMin: ['Pz', 'Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct'],
    monthNames: ['Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran', 'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık'],
    monthNamesShort: ['Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz', 'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara'],
    today: 'Bugün',
    clear: 'Temizle',
    dateFormat: 'dd.mm.yy',
    weekHeader: 'Hf'
};

export const appConfig: ApplicationConfig = {
    providers: [
        provideRouter(appRoutes, withInMemoryScrolling({ anchorScrolling: 'enabled', scrollPositionRestoration: 'enabled' }), withEnabledBlockingInitialNavigation()),
        provideHttpClient(withFetch(), withInterceptors([authTokenInterceptor])),
        provideZonelessChangeDetection(),
        { provide: LOCALE_ID, useValue: 'tr-TR' },
        providePrimeNG({
            theme: { preset: Aura, options: { darkModeSelector: '.app-dark' } },
            translation: trLocale
        })
    ]
};
