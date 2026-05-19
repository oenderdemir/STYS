import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import { MizanFilterModel, MizanKarsilastirmaModel, MizanModel } from '../models/mizan.model';

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

@Injectable({ providedIn: 'root' })
export class MuhasebeRaporService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getTesisler(): Observable<MuhasebeTesisModel[]> {
        return this.http.get<ApiResponse<MuhasebeTesisModel[]>>(`${this.apiBaseUrl}/ui/rezervasyon/tesisler`).pipe(
            map((envelope) => {
                if (envelope.success && envelope.data) {
                    return envelope.data;
                }
                throw new Error(tryReadApiMessage(envelope) ?? 'Tesis listesi alinamadi.');
            })
        );
    }

    getHizliMizan(filter: MizanFilterModel): Observable<MizanModel> {
        return this.http
            .post<ApiResponse<MizanModel>>(`${this.apiBaseUrl}/ui/muhasebe/fisler/mizan-bakiye`, filter)
            .pipe(
                map((envelope) => {
                    if (envelope.success && envelope.data) {
                        return envelope.data;
                    }
                    throw new Error(tryReadApiMessage(envelope) ?? 'Hizli mizan alinamadi.');
                })
            );
        }
    
        karsilastirMizan(filter: MizanFilterModel): Observable<MizanKarsilastirmaModel> {
            return this.http.post<MizanKarsilastirmaModel>(
                `${this.apiBaseUrl}/ui/muhasebe/fisler/mizan-karsilastir`,
                filter
            );
        }
    }
