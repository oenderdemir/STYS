import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { CreateMuhasebeFisRequestModel, MuhasebeFisFilterModel, MuhasebeFisModel, UpdateMuhasebeFisRequestModel } from '../models/muhasebe-fis.model';

@Injectable({ providedIn: 'root' })
export class MuhasebeFisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getFiltered(filter: MuhasebeFisFilterModel): Observable<MuhasebeFisModel[]> {
        return this.http.post<ApiResponse<MuhasebeFisModel[]>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/filter`,
            filter
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    countFiltered(filter: MuhasebeFisFilterModel): Observable<number> {
        return this.http.post<ApiResponse<number>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/filter/count`,
            filter
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    getById(id: number): Observable<MuhasebeFisModel> {
        return this.http.get<ApiResponse<MuhasebeFisModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    onayla(id: number): Observable<MuhasebeFisModel> {
        return this.http.post<ApiResponse<MuhasebeFisModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}/onayla`,
            {}
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    iptal(id: number, aciklama?: string | null): Observable<MuhasebeFisModel> {
        return this.http.post<ApiResponse<MuhasebeFisModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}/iptal`,
            { aciklama: aciklama ?? null }
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    update(id: number, request: UpdateMuhasebeFisRequestModel): Observable<MuhasebeFisModel> {
        return this.http.put<ApiResponse<MuhasebeFisModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`,
            request
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    delete(id: number): Observable<void> {
        return this.http.delete<ApiResponse<unknown>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`
        ).pipe(map((envelope) => {
            if (envelope.success) {
                return;
            }
            throw new Error(tryReadApiMessage(envelope) ?? 'Fiş silinemedi.');
        }));
    }

    create(request: CreateMuhasebeFisRequestModel): Observable<MuhasebeFisModel> {
        return this.http.post<ApiResponse<MuhasebeFisModel>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler`,
            request
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    getByKaynak(kaynakModul: string, kaynakId: number): Observable<MuhasebeFisModel[]> {
        return this.http.get<ApiResponse<MuhasebeFisModel[]>>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/by-kaynak`,
            { params: { kaynakModul, kaynakId: String(kaynakId) } }
        ).pipe(map((envelope) => this.unwrap(envelope)));
    }

    private unwrap<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data !== null && envelope.data !== undefined) {
            return envelope.data;
        }
        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
