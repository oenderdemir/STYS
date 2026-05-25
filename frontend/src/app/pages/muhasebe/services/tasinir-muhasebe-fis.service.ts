import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    TasinirMuhasebeFisiOlusturRequestModel,
    TasinirMuhasebeFisiOlusturResultModel
} from '../models/tasinir-muhasebe-fis.model';

@Injectable({ providedIn: 'root' })
export class TasinirMuhasebeFisService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();

    createTasinirFisTaslagi(request: TasinirMuhasebeFisiOlusturRequestModel): Observable<TasinirMuhasebeFisiOlusturResultModel> {
        return this.http.post<TasinirMuhasebeFisiOlusturResultModel>(
            `${this.apiBaseUrl}/ui/muhasebe/fisler/tasinir-fis-taslagi-olustur`,
            request
        );
    }
}
