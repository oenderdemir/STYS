import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { MuhasebeFisFilterModel, MuhasebeFisModel, UpdateMuhasebeFisRequestModel } from '../models/muhasebe-fis.model';

@Injectable({ providedIn: 'root' })
export class MuhasebeFisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getFiltered(filter: MuhasebeFisFilterModel): Observable<MuhasebeFisModel[]> {
        return this.http.post<MuhasebeFisModel[]>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/filter`,
            filter
        );
    }

    countFiltered(filter: MuhasebeFisFilterModel): Observable<number> {
        return this.http.post<number>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/filter/count`,
            filter
        );
    }

    getById(id: number): Observable<MuhasebeFisModel> {
        return this.http.get<MuhasebeFisModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`
        );
    }

    onayla(id: number): Observable<MuhasebeFisModel> {
        return this.http.post<MuhasebeFisModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}/onayla`,
            {}
        );
    }

    iptal(id: number, aciklama?: string | null): Observable<MuhasebeFisModel> {
        return this.http.post<MuhasebeFisModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}/iptal`,
            { aciklama: aciklama ?? null }
        );
    }

    update(id: number, request: UpdateMuhasebeFisRequestModel): Observable<MuhasebeFisModel> {
        return this.http.put<MuhasebeFisModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`,
            request
        );
    }

    delete(id: number): Observable<void> {
        return this.http.delete<void>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/${id}`
        );
    }

    getByKaynak(kaynakModul: string, kaynakId: number): Observable<MuhasebeFisModel[]> {
        return this.http.get<MuhasebeFisModel[]>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/by-kaynak`,
            { params: { kaynakModul, kaynakId: String(kaynakId) } }
        );
    }
}
