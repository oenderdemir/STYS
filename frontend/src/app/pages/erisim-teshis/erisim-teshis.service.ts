import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../core/api';
import { getApiBaseUrl } from '../../core/config';
import { ErisimTeshisIstekDto, ErisimTeshisReferansDto, ErisimTeshisSonucDto } from './erisim-teshis.dto';

@Injectable({ providedIn: 'root' })
export class ErisimTeshisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    getReferanslar(): Observable<ErisimTeshisReferansDto> {
        return this.http.get<ApiResponse<ErisimTeshisReferansDto>>(`${this.apiBaseUrl}/ui/erisimteshis/referanslar`).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Erisim teshis referanslari alinamadi.');
            })
        );
    }

    teshisEt(payload: ErisimTeshisIstekDto): Observable<ErisimTeshisSonucDto> {
        return this.http.post<ApiResponse<ErisimTeshisSonucDto>>(`${this.apiBaseUrl}/ui/erisimteshis/teshis-et`, payload).pipe(
            map((responseEnvelope) => {
                if (responseEnvelope.success && responseEnvelope.data) {
                    return responseEnvelope.data;
                }

                throw new Error(tryReadApiMessage(responseEnvelope) ?? 'Erisim teshisi olusturulamadi.');
            })
        );
    }
}
