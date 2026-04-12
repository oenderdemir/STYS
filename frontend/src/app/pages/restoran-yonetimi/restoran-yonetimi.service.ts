import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { ManagerCandidateDto } from '../../core/identity';
import { CreateRestoranRequest, RestoranIsletmeAlaniSecenekModel, RestoranModel, TesisSecenekModel, UpdateRestoranRequest } from './restoran-yonetimi.dto';

@Injectable({ providedIn: 'root' })
export class RestoranYonetimiService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getAll(tesisId?: number | null): Observable<RestoranModel[]> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }

        return this.http.get<ApiResponse<RestoranModel[]>>(`${this.apiBaseUrl}/api/restoranlar`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran listesi alinamadi.');
            })
        );
    }

    getById(id: number): Observable<RestoranModel> {
        return this.http.get<ApiResponse<RestoranModel>>(`${this.apiBaseUrl}/api/restoranlar/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran detayi alinamadi.');
            })
        );
    }

    create(payload: CreateRestoranRequest): Observable<RestoranModel> {
        return this.http.post<ApiResponse<RestoranModel>>(`${this.apiBaseUrl}/api/restoranlar`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran olusturulamadi.');
            })
        );
    }

    update(id: number, payload: UpdateRestoranRequest): Observable<RestoranModel> {
        return this.http.put<ApiResponse<RestoranModel>>(`${this.apiBaseUrl}/api/restoranlar/${id}`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran guncellenemedi.');
            })
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(`${this.apiBaseUrl}/api/restoranlar/${id}`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success) {
                    return;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Restoran silinemedi.');
            })
        );
    }

    getTesisler(): Observable<TesisSecenekModel[]> {
        return this.http.get<ApiResponse<TesisSecenekModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Tesisler alinamadi.');
            })
        );
    }

    getIsletmeAlanlariByTesisId(tesisId: number): Observable<RestoranIsletmeAlaniSecenekModel[]> {
        const params = new HttpParams().set('tesisId', tesisId);
        return this.http.get<ApiResponse<RestoranIsletmeAlaniSecenekModel[]>>(`${this.apiBaseUrl}/api/restoranlar/isletme-alanlari`, { params }).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }
                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Isletme alanlari alinamadi.');
            })
        );
    }

    getYoneticiAdaylari(): Observable<ManagerCandidateDto[]> {
        return this.http.get<ApiResponse<ManagerCandidateDto[]>>(`${this.apiBaseUrl}/ui/yoneticiaday/restoran-yoneticileri`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Yonetici aday listesi alinamadi.');
            })
        );
    }

    getGarsonAdaylari(): Observable<ManagerCandidateDto[]> {
        return this.http.get<ApiResponse<ManagerCandidateDto[]>>(`${this.apiBaseUrl}/ui/yoneticiaday/restoran-garsonlari`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Garson aday listesi alinamadi.');
            })
        );
    }
}
