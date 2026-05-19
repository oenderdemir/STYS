import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    TasinirMuhasebeFisiOlusturRequestModel,
    TasinirMuhasebeFisiOlusturResultModel
} from '../models/tasinir-muhasebe-fis.model';

export interface MuhasebeTesisModel {
    id: number;
    ad: string;
}

@Injectable({ providedIn: 'root' })
export class TasinirMuhasebeFisService {
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

    createTasinirFisTaslagi(request: TasinirMuhasebeFisiOlusturRequestModel): Observable<TasinirMuhasebeFisiOlusturResultModel> {
        return this.http.post<TasinirMuhasebeFisiOlusturResultModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/tasinir-fis-taslagi-olustur`,
            request
        );
    }
}
