import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { getApiBaseUrl } from '../../../core/config';
import { MuhasebeFisFilterModel, MuhasebeFisModel } from '../models/muhasebe-fis.model';

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
}
