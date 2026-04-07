import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, SortDirection, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { KampBasvuruBaglamDto, KampBasvuruDto, KampBasvuruOnizlemeDto, KampBasvuruRequestDto, KampDonemiDto, KampDonemiTesisAtamaDto, KampDonemiYonetimBaglamDto, KampIadeHesaplamaRequestDto, KampIadeKarariDto, KampKatilimciIptalSonucDto, KampKonaklamaTarifeYonetimDto, KampNoShowIptalSonucDto, KampProgramiDto, KampPuanKuraliYonetimBaglamDto, KampPuanKuraliYonetimKaydetRequestDto, KampRezervasyonBaglamDto, KampRezervasyonIptalRequestDto, KampRezervasyonListeDto, KampRezervasyonUretSonucDto, KampTahsisBaglamDto, KampTahsisKararRequestDto, KampTahsisListeDto, KampTahsisOtomatikKararRequestDto, KampTahsisOtomatikKararSonucDto } from './kamp-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class KampYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getKampProgramlariPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<KampProgramiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<KampProgramiDto>>>(`${this.apiBaseUrl}/ui/kampprogrami/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi listesi alinamadi.');
            })
        );
    }

    getKampProgramlari(): Observable<KampProgramiDto[]> {
        return this.http.get<ApiResponse<KampProgramiDto[]>>(`${this.apiBaseUrl}/ui/kampprogrami`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi listesi alinamadi.');
            })
        );
    }

    getKampProgramiById(id: number): Observable<KampProgramiDto> {
        return this.http.get<ApiResponse<KampProgramiDto>>(`${this.apiBaseUrl}/ui/kampprogrami/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi detayi alinamadi.');
            })
        );
    }

    createKampProgrami(payload: KampProgramiDto): Observable<KampProgramiDto> {
        return this.http.post<ApiResponse<KampProgramiDto>>(`${this.apiBaseUrl}/ui/kampprogrami`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi olusturulamadi.');
            })
        );
    }

    updateKampProgrami(id: number, payload: KampProgramiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kampprogrami/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi guncellenemedi.');
            })
        );
    }

    deleteKampProgrami(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kampprogrami/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp programi silinemedi.');
            })
        );
    }

    getKampPuanKuraliYonetimBaglam(): Observable<KampPuanKuraliYonetimBaglamDto> {
        return this.http.get<ApiResponse<KampPuanKuraliYonetimBaglamDto>>(`${this.apiBaseUrl}/ui/kamppuankurali/yonetim-baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp puan kurali baglami alinamadi.');
            })
        );
    }

    getKampDonemiAtamaKonaklamaTarifeleri(): Observable<KampKonaklamaTarifeYonetimDto[]> {
        return this.getKampPuanKuraliYonetimBaglam().pipe(
            map(x => x.konaklamaTarifeleri.filter(t => t.aktifMi))
        );
    }

    kaydetKampPuanKuraliYonetimBaglam(payload: KampPuanKuraliYonetimKaydetRequestDto): Observable<KampPuanKuraliYonetimBaglamDto> {
        return this.http.put<ApiResponse<KampPuanKuraliYonetimBaglamDto>>(`${this.apiBaseUrl}/ui/kamppuankurali/yonetim-baglam`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp puan kurallari kaydedilemedi.');
            })
        );
    }

    getKampDonemiYonetimBaglam(): Observable<KampDonemiYonetimBaglamDto> {
        return this.http.get<ApiResponse<KampDonemiYonetimBaglamDto>>(`${this.apiBaseUrl}/ui/kampdonemi/yonetim-baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi yonetim baglami alinamadi.');
            })
        );
    }

    getKampDonemleriPaged(pageNumber: number, pageSize: number, query: string, sortBy: string, sortDir: SortDirection): Observable<PagedResponseDto<KampDonemiDto>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize).set('sortBy', sortBy).set('sortDir', sortDir);
        const normalizedQuery = query.trim();
        if (normalizedQuery.length > 0) {
            params = params.set('q', normalizedQuery);
        }

        return this.http.get<ApiResponse<PagedResponseDto<KampDonemiDto>>>(`${this.apiBaseUrl}/ui/kampdonemi/paged`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return {
                        ...responseEnvelope.data,
                        items: responseEnvelope.data.items.map((item) => this.normalizeKampDonemi(item))
                    };
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi listesi alinamadi.');
            })
        );
    }

    getKampDonemleri(): Observable<KampDonemiDto[]> {
        return this.http.get<ApiResponse<KampDonemiDto[]>>(`${this.apiBaseUrl}/ui/kampdonemi`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data.map((item) => this.normalizeKampDonemi(item));
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi listesi alinamadi.');
            })
        );
    }

    getKampDonemiById(id: number): Observable<KampDonemiDto> {
        return this.http.get<ApiResponse<KampDonemiDto>>(`${this.apiBaseUrl}/ui/kampdonemi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeKampDonemi(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi detayi alinamadi.');
            })
        );
    }

    createKampDonemi(payload: KampDonemiDto): Observable<KampDonemiDto> {
        return this.http.post<ApiResponse<KampDonemiDto>>(`${this.apiBaseUrl}/ui/kampdonemi`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeKampDonemi(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi olusturulamadi.');
            })
        );
    }

    updateKampDonemi(id: number, payload: KampDonemiDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kampdonemi/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi guncellenemedi.');
            })
        );
    }

    deleteKampDonemi(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kampdonemi/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi silinemedi.');
            })
        );
    }

    getTesisAtamalari(kampDonemiId: number): Observable<KampDonemiTesisAtamaDto[]> {
        return this.http.get<ApiResponse<KampDonemiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/kampdonemi/${kampDonemiId}/tesis-atamalari`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi tesis atamalari alinamadi.');
            })
        );
    }

    kaydetTesisAtamalari(kampDonemiId: number, kayitlar: KampDonemiTesisAtamaDto[]): Observable<KampDonemiTesisAtamaDto[]> {
        return this.http.put<ApiResponse<KampDonemiTesisAtamaDto[]>>(`${this.apiBaseUrl}/ui/kampdonemi/${kampDonemiId}/tesis-atamalari`, {
            kayitlar: kayitlar.map((item) => ({
                tesisId: item.tesisId,
                atamaVarMi: item.atamaVarMi,
                donemdeAktifMi: item.donemdeAktifMi,
                basvuruyaAcikMi: item.basvuruyaAcikMi,
                toplamKontenjan: item.toplamKontenjan,
                aciklama: item.aciklama ?? null,
                konaklamaTarifeKodlari: item.konaklamaTarifeKodlari ?? []
            }))
        }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp donemi tesis atamalari kaydedilemedi.');
            })
        );
    }

    getKampBasvuruBaglam(): Observable<KampBasvuruBaglamDto> {
        return this.http.get<ApiResponse<KampBasvuruBaglamDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvuru baglami alinamadi.');
            })
        );
    }

    getBenimKampBasvurularim(): Observable<KampBasvuruDto[]> {
        return this.http.get<ApiResponse<KampBasvuruDto[]>>(`${this.apiBaseUrl}/ui/kampbasvuru/benim-basvurularim`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data.map((item) => this.normalizeBasvuru(item));
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvurulari alinamadi.');
            })
        );
    }

    getKampBasvuruById(id: number): Observable<KampBasvuruDto> {
        return this.http.get<ApiResponse<KampBasvuruDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeBasvuru(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvuru detayi alinamadi.');
            })
        );
    }

    getKampBasvuruByBasvuruNo(basvuruNo: string): Observable<KampBasvuruDto> {
        return this.http.get<ApiResponse<KampBasvuruDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/basvuru-no/${encodeURIComponent(basvuruNo)}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeBasvuru(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvurusu bulunamadi.');
            })
        );
    }

    onizleKampBasvurusu(payload: KampBasvuruRequestDto): Observable<KampBasvuruOnizlemeDto> {
        return this.http.post<ApiResponse<KampBasvuruOnizlemeDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/onizleme`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvuru onizlemesi alinamadi.');
            })
        );
    }

    createKampBasvurusu(payload: KampBasvuruRequestDto): Observable<KampBasvuruDto> {
        return this.http.post<ApiResponse<KampBasvuruDto>>(`${this.apiBaseUrl}/ui/kampbasvuru`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return this.normalizeBasvuru(responseEnvelope.data);
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp basvurusu olusturulamadi.');
            })
        );
    }

    hesaplaIadeKarari(payload: KampIadeHesaplamaRequestDto): Observable<KampIadeKarariDto> {
        return this.http.post<ApiResponse<KampIadeKarariDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/iade-karari`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp iade karari hesaplanamadi.');
            })
        );
    }

    katilimciIptalEt(kampBasvuruId: number, katilimciId: number): Observable<KampKatilimciIptalSonucDto> {
        return this.http.delete<ApiResponse<KampKatilimciIptalSonucDto>>(`${this.apiBaseUrl}/ui/kampbasvuru/${kampBasvuruId}/katilimci/${katilimciId}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Katilimci iptal islemi tamamlanamadi.');
            })
        );
    }

    getKampTahsisBaglam(): Observable<KampTahsisBaglamDto> {
        return this.http.get<ApiResponse<KampTahsisBaglamDto>>(`${this.apiBaseUrl}/ui/kamptahsis/baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp tahsis baglami alinamadi.');
            })
        );
    }

    getKampTahsisleri(kampDonemiId?: number | null, tesisId?: number | null, durum?: string | null): Observable<KampTahsisListeDto[]> {
        let params = new HttpParams();
        if (kampDonemiId && kampDonemiId > 0) {
            params = params.set('kampDonemiId', kampDonemiId);
        }

        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        if (durum && durum.trim().length > 0) {
            params = params.set('durum', durum.trim());
        }

        return this.http.get<ApiResponse<KampTahsisListeDto[]>>(`${this.apiBaseUrl}/ui/kamptahsis`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data.map((item) => ({
                        ...item,
                        createdAt: item.createdAt ? item.createdAt.slice(0, 19) : ''
                    }));
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp tahsis listesi alinamadi.');
            })
        );
    }

    kararVerKampTahsisi(kampBasvuruId: number, payload: KampTahsisKararRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kamptahsis/${kampBasvuruId}/karar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp tahsis karari kaydedilemedi.');
            })
        );
    }

    uygulaOtomatikKampTahsisi(payload: KampTahsisOtomatikKararRequestDto): Observable<KampTahsisOtomatikKararSonucDto> {
        return this.http.post<ApiResponse<KampTahsisOtomatikKararSonucDto>>(`${this.apiBaseUrl}/ui/kamptahsis/otomatik-karar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Otomatik kamp tahsis islemi tamamlanamadi.');
            })
        );
    }

    noShowIptalUygula(kampDonemiId: number): Observable<KampNoShowIptalSonucDto> {
        return this.http.post<ApiResponse<KampNoShowIptalSonucDto>>(`${this.apiBaseUrl}/ui/kamptahsis/${kampDonemiId}/noshow-iptal`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'No-show iptal islemi tamamlanamadi.');
            })
        );
    }

    getKampRezervasyonBaglam(): Observable<KampRezervasyonBaglamDto> {
        return this.http.get<ApiResponse<KampRezervasyonBaglamDto>>(`${this.apiBaseUrl}/ui/kamprezervasyon/baglam`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp rezervasyon baglami alinamadi.');
            })
        );
    }

    getKampRezervasyonlari(kampDonemiId?: number | null, tesisId?: number | null, durum?: string | null): Observable<KampRezervasyonListeDto[]> {
        let params = new HttpParams();
        if (kampDonemiId && kampDonemiId > 0) {
            params = params.set('kampDonemiId', kampDonemiId);
        }

        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        if (durum && durum.trim().length > 0) {
            params = params.set('durum', durum.trim());
        }

        return this.http.get<ApiResponse<KampRezervasyonListeDto[]>>(`${this.apiBaseUrl}/ui/kamprezervasyon`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp rezervasyon listesi alinamadi.');
            })
        );
    }

    uretKampRezervasyon(kampBasvuruId: number): Observable<KampRezervasyonUretSonucDto> {
        return this.http.post<ApiResponse<KampRezervasyonUretSonucDto>>(`${this.apiBaseUrl}/ui/kamprezervasyon/${kampBasvuruId}/uret`, {}).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp rezervasyonu uretilemedi.');
            })
        );
    }

    iptalEtKampRezervasyon(id: number, payload: KampRezervasyonIptalRequestDto): Observable<void> {
        return this.http.put<ApiResponse<unknown>>(`${this.apiBaseUrl}/ui/kamprezervasyon/${id}/iptal`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Kamp rezervasyonu iptal edilemedi.');
            })
        );
    }

    private normalizeKampDonemi(item: KampDonemiDto): KampDonemiDto {
        return {
            ...item,
            basvuruBaslangicTarihi: this.normalizeDate(item.basvuruBaslangicTarihi),
            basvuruBitisTarihi: this.normalizeDate(item.basvuruBitisTarihi),
            konaklamaBaslangicTarihi: this.normalizeDate(item.konaklamaBaslangicTarihi),
            konaklamaBitisTarihi: this.normalizeDate(item.konaklamaBitisTarihi),
            iptalSonGun: this.normalizeNullableDate(item.iptalSonGun)
        };
    }

    private normalizeDate(value: string): string {
        return value ? value.slice(0, 10) : '';
    }

    private normalizeNullableDate(value?: string | null): string | null {
        return value ? value.slice(0, 10) : null;
    }

    private normalizeBasvuru(item: KampBasvuruDto): KampBasvuruDto {
        return {
            ...item,
            konaklamaBaslangicTarihi: this.normalizeDate(item.konaklamaBaslangicTarihi),
            konaklamaBitisTarihi: this.normalizeDate(item.konaklamaBitisTarihi),
            createdAt: item.createdAt ? item.createdAt.slice(0, 19) : '',
            katilimcilar: item.katilimcilar.map((x) => ({
                ...x,
                dogumTarihi: this.normalizeDate(x.dogumTarihi)
            }))
        };
    }
}
