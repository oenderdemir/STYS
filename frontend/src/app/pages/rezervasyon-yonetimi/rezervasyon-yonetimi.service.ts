import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import {
    KonaklamaSenaryoAramaRequestDto,
    KonaklamaSenaryoDto,
    RezervasyonDashboardDto,
    RezervasyonDegisiklikGecmisiDto,
    RezervasyonDetayDto,
    RezervasyonEkHizmetKaydetRequestDto,
    RezervasyonEkHizmetSecenekleriDto,
    RezervasyonIndirimKuraliSecenekDto,
    RezervasyonKaydetRequestDto,
    RezervasyonCheckInKontrolDto,
    RezervasyonKayitSonucDto,
    RezervasyonKonaklamaHakkiDurumGuncelleRequestDto,
    RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto,
    RezervasyonKonaklamaTipiDto,
    RezervasyonKonaklayanPlanDto,
    RezervasyonKonaklayanPlanKaydetRequestDto,
    RezervasyonListeDto,
    RezervasyonMisafirTipiDto,
    RezervasyonOdemeKaydetRequestDto,
    RezervasyonOdemeOzetDto,
    RezervasyonOdaDegisimKaydetRequestDto,
    RezervasyonOdaDegisimSecenekDto,
    RezervasyonOdaTipiDto,
    RezervasyonTesisDto,
    SenaryoFiyatHesaplaRequestDto,
    SenaryoFiyatHesaplamaSonucuDto,
    UygunOdaAramaRequestDto,
    UygunOdaDto
} from './rezervasyon-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class RezervasyonYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTesisler(): Observable<RezervasyonTesisDto[]> {
        return this.http.get<ApiResponse<RezervasyonTesisDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    getOdaTipleriByTesis(tesisId: number): Observable<RezervasyonOdaTipiDto[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<RezervasyonOdaTipiDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/oda-tipleri`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda tipi listesi alinamadi.');
            })
        );
    }

    getMisafirTipleri(tesisId: number): Observable<RezervasyonMisafirTipiDto[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<RezervasyonMisafirTipiDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/misafir-tipleri`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Misafir tipleri alinamadi.');
            })
        );
    }

    getKonaklamaTipleri(tesisId: number): Observable<RezervasyonKonaklamaTipiDto[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<RezervasyonKonaklamaTipiDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/konaklama-tipleri`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama tipleri alinamadi.');
            })
        );
    }

    getRezervasyonKayitlari(tesisId: number | null): Observable<RezervasyonListeDto[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<RezervasyonListeDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon kayitlari alinamadi.');
            })
        );
    }

    getGunlukDashboard(
        tesisId: number,
        tarih?: string,
        kpiBaslangicTarihi?: string,
        kpiBitisTarihi?: string
    ): Observable<RezervasyonDashboardDto> {
        let params = new HttpParams().set('tesisId', tesisId);
        if (tarih && tarih.trim().length > 0) {
            params = params.set('tarih', tarih);
        }
        if (kpiBaslangicTarihi && kpiBaslangicTarihi.trim().length > 0) {
            params = params.set('kpiBaslangicTarihi', kpiBaslangicTarihi);
        }
        if (kpiBitisTarihi && kpiBitisTarihi.trim().length > 0) {
            params = params.set('kpiBitisTarihi', kpiBitisTarihi);
        }

        return this.http.get<ApiResponse<RezervasyonDashboardDto>>(`${this.apiBaseUrl}/ui/rezervasyon/dashboard`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon dashboard verisi alinamadi.');
            })
        );
    }

    exportOdemeRaporuExcel(tesisIds: number[], baslangicTarihi: string, bitisTarihi: string): Observable<Blob> {
        let params = new HttpParams()
            .set('baslangicTarihi', baslangicTarihi)
            .set('bitisTarihi', bitisTarihi);

        for (const tesisId of tesisIds) {
            params = params.append('tesisIds', tesisId);
        }

        return this.http.get(`${this.apiBaseUrl}/ui/rezervasyon/odeme-raporu/excel`, {
            params,
            responseType: 'blob'
        });
    }

    exportOdemeRaporuPdf(tesisIds: number[], baslangicTarihi: string, bitisTarihi: string): Observable<Blob> {
        let params = new HttpParams()
            .set('baslangicTarihi', baslangicTarihi)
            .set('bitisTarihi', bitisTarihi);

        for (const tesisId of tesisIds) {
            params = params.append('tesisIds', tesisId);
        }

        return this.http.get(`${this.apiBaseUrl}/ui/rezervasyon/odeme-raporu/pdf`, {
            params,
            responseType: 'blob'
        });
    }

    getRezervasyonDetay(rezervasyonId: number): Observable<RezervasyonDetayDto> {
        return this.http.get<ApiResponse<RezervasyonDetayDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/detay`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon detayi alinamadi.');
            })
        );
    }

    getDegisiklikGecmisi(rezervasyonId: number): Observable<RezervasyonDegisiklikGecmisiDto[]> {
        return this.http.get<ApiResponse<RezervasyonDegisiklikGecmisiDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/degisiklik-gecmisi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon degisiklik gecmisi alinamadi.');
            })
        );
    }

    getKonaklayanPlani(rezervasyonId: number): Observable<RezervasyonKonaklayanPlanDto> {
        return this.http.get<ApiResponse<RezervasyonKonaklayanPlanDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/konaklayan-plani`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklayan plani alinamadi.');
            })
        );
    }

    saveKonaklayanPlani(rezervasyonId: number, request: RezervasyonKonaklayanPlanKaydetRequestDto): Observable<RezervasyonKonaklayanPlanDto> {
        return this.http.put<ApiResponse<RezervasyonKonaklayanPlanDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/konaklayan-plani`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklayan plani kaydedilemedi.');
            })
        );
    }

    getOdaDegisimSecenekleri(rezervasyonId: number): Observable<RezervasyonOdaDegisimSecenekDto> {
        return this.http.get<ApiResponse<RezervasyonOdaDegisimSecenekDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/oda-degisim-secenekleri`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda degisim secenekleri alinamadi.');
            })
        );
    }

    saveOdaDegisimi(rezervasyonId: number, request: RezervasyonOdaDegisimKaydetRequestDto): Observable<RezervasyonKayitSonucDto> {
        return this.http.put<ApiResponse<RezervasyonKayitSonucDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/oda-degisim`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Oda degisimi kaydedilemedi.');
            })
        );
    }

    searchUygunOdalar(request: UygunOdaAramaRequestDto): Observable<UygunOdaDto[]> {
        return this.http.post<ApiResponse<UygunOdaDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/uygun-odalar`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Uygun oda listesi alinamadi.');
            })
        );
    }

    searchKonaklamaSenaryolari(request: KonaklamaSenaryoAramaRequestDto): Observable<KonaklamaSenaryoDto[]> {
        return this.http.post<ApiResponse<KonaklamaSenaryoDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/senaryo-ara`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama senaryolari alinamadi.');
            })
        );
    }

    getIndirimKurallari(
        tesisId: number,
        misafirTipiId: number,
        konaklamaTipiId: number,
        baslangicTarihi: string,
        bitisTarihi: string
    ): Observable<RezervasyonIndirimKuraliSecenekDto[]> {
        const params = new HttpParams()
            .set('tesisId', tesisId)
            .set('misafirTipiId', misafirTipiId)
            .set('konaklamaTipiId', konaklamaTipiId)
            .set('baslangicTarihi', baslangicTarihi)
            .set('bitisTarihi', bitisTarihi);

        return this.http.get<ApiResponse<RezervasyonIndirimKuraliSecenekDto[]>>(`${this.apiBaseUrl}/ui/rezervasyon/indirim-kurallari`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Indirim kurallari alinamadi.');
            })
        );
    }

    hesaplaSenaryoFiyati(request: SenaryoFiyatHesaplaRequestDto): Observable<SenaryoFiyatHesaplamaSonucuDto> {
        return this.http.post<ApiResponse<SenaryoFiyatHesaplamaSonucuDto>>(`${this.apiBaseUrl}/ui/rezervasyon/senaryo-fiyat-hesapla`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Senaryo fiyati hesaplanamadi.');
            })
        );
    }

    createRezervasyon(request: RezervasyonKaydetRequestDto): Observable<RezervasyonKayitSonucDto> {
        return this.http.post<ApiResponse<RezervasyonKayitSonucDto>>(`${this.apiBaseUrl}/ui/rezervasyon`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon kaydedilemedi.');
            })
        );
    }

    tamamlaCheckIn(rezervasyonId: number): Observable<RezervasyonKayitSonucDto> {
        return this.http.post<ApiResponse<RezervasyonKayitSonucDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/check-in`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Check-in tamamlanamadi.');
            })
        );
    }

    getCheckInKontrol(rezervasyonId: number): Observable<RezervasyonCheckInKontrolDto> {
        return this.http.get<ApiResponse<RezervasyonCheckInKontrolDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/check-in-kontrol`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Check-in kontrolu alinamadi.');
            })
        );
    }

    tamamlaCheckOut(rezervasyonId: number): Observable<RezervasyonKayitSonucDto> {
        return this.http.post<ApiResponse<RezervasyonKayitSonucDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/check-out`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Check-out tamamlanamadi.');
            })
        );
    }

    iptalEt(rezervasyonId: number): Observable<RezervasyonKayitSonucDto> {
        return this.http.post<ApiResponse<RezervasyonKayitSonucDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/iptal`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Rezervasyon iptal edilemedi.');
            })
        );
    }

    getOdemeOzeti(rezervasyonId: number): Observable<RezervasyonOdemeOzetDto> {
        return this.http.get<ApiResponse<RezervasyonOdemeOzetDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/odeme-ozeti`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Odeme ozeti alinamadi.');
            })
        );
    }

    getEkHizmetSecenekleri(rezervasyonId: number): Observable<RezervasyonEkHizmetSecenekleriDto> {
        return this.http.get<ApiResponse<RezervasyonEkHizmetSecenekleriDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/ek-hizmet-secenekleri`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet secenekleri alinamadi.');
            })
        );
    }

    kaydetEkHizmet(rezervasyonId: number, request: RezervasyonEkHizmetKaydetRequestDto): Observable<RezervasyonOdemeOzetDto> {
        return this.http.post<ApiResponse<RezervasyonOdemeOzetDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/ek-hizmetler`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet kaydi olusturulamadi.');
            })
        );
    }

    guncelleEkHizmet(rezervasyonId: number, ekHizmetId: number, request: RezervasyonEkHizmetKaydetRequestDto): Observable<RezervasyonOdemeOzetDto> {
        return this.http.put<ApiResponse<RezervasyonOdemeOzetDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/ek-hizmetler/${ekHizmetId}`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet kaydi guncellenemedi.');
            })
        );
    }

    silEkHizmet(rezervasyonId: number, ekHizmetId: number): Observable<RezervasyonOdemeOzetDto> {
        return this.http.delete<ApiResponse<RezervasyonOdemeOzetDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/ek-hizmetler/${ekHizmetId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Ek hizmet kaydi silinemedi.');
            })
        );
    }

    kaydetOdeme(rezervasyonId: number, request: RezervasyonOdemeKaydetRequestDto): Observable<RezervasyonOdemeOzetDto> {
        return this.http.post<ApiResponse<RezervasyonOdemeOzetDto>>(`${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/odemeler`, request).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Odeme kaydedilemedi.');
            })
        );
    }

    guncelleKonaklamaHakkiDurumu(
        rezervasyonId: number,
        hakId: number,
        request: RezervasyonKonaklamaHakkiDurumGuncelleRequestDto
    ): Observable<RezervasyonDetayDto> {
        return this.http.put<ApiResponse<RezervasyonDetayDto>>(
            `${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/konaklama-haklari/${hakId}/durum`,
            request
        ).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama hakki durumu guncellenemedi.');
            })
        );
    }

    kaydetKonaklamaHakkiTuketim(
        rezervasyonId: number,
        hakId: number,
        request: RezervasyonKonaklamaHakkiTuketimKaydiKaydetRequestDto
    ): Observable<RezervasyonDetayDto> {
        return this.http.post<ApiResponse<RezervasyonDetayDto>>(
            `${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/konaklama-haklari/${hakId}/tuketimler`,
            request
        ).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama hakki tuketim kaydi eklenemedi.');
            })
        );
    }

    silKonaklamaHakkiTuketim(
        rezervasyonId: number,
        hakId: number,
        tuketimKaydiId: number
    ): Observable<RezervasyonDetayDto> {
        return this.http.delete<ApiResponse<RezervasyonDetayDto>>(
            `${this.apiBaseUrl}/ui/rezervasyon/kayitlar/${rezervasyonId}/konaklama-haklari/${hakId}/tuketimler/${tuketimKaydiId}`
        ).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Konaklama hakki tuketim kaydi silinemedi.');
            })
        );
    }
}
