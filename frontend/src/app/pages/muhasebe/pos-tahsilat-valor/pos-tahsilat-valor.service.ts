import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, PagedResponseDto, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    ManuelAktarimGuncellemeRequest,
    PosTahsilatValorAktarimSonucModel,
    PosTahsilatValorModel,
    PosTahsilatValorOzetModel,
    PosTahsilatValorToplamAktarimSonucModel,
    PosTahsilatValorTopluOnayBilgisiModel,
    PosTahsilatValorTopluOnayBilgisiRequest
} from './pos-tahsilat-valor.dto';

@Injectable({ providedIn: 'root' })
export class PosTahsilatValorService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly baseUrl = `${this.apiBaseUrl}/ui/muhasebe/pos-tahsilat-valor`;

    getPaged(pageNumber: number, pageSize: number, tesisId?: number | null): Observable<PagedResponseDto<PosTahsilatValorModel>> {
        let params = new HttpParams().set('pageNumber', pageNumber).set('pageSize', pageSize);
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        return this.http.get<ApiResponse<PagedResponseDto<PosTahsilatValorModel>>>(`${this.baseUrl}/paged`, { params }).pipe(map(this.unwrapOne));
    }

    getOzet(tesisId?: number | null): Observable<PosTahsilatValorOzetModel> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        return this.http.get<ApiResponse<PosTahsilatValorOzetModel>>(`${this.baseUrl}/ozet`, { params }).pipe(map(this.unwrapOne));
    }

    getTopluOnayBilgisi(request: PosTahsilatValorTopluOnayBilgisiRequest): Observable<PosTahsilatValorTopluOnayBilgisiModel> {
        return this.http.post<ApiResponse<PosTahsilatValorTopluOnayBilgisiModel>>(`${this.baseUrl}/toplu-onay-bilgisi`, request).pipe(map(this.unwrapOne));
    }

    hesabaAktar(id: number, guncelleme?: ManuelAktarimGuncellemeRequest | null): Observable<PosTahsilatValorAktarimSonucModel> {
        return this.http.post<ApiResponse<PosTahsilatValorAktarimSonucModel>>(`${this.baseUrl}/${id}/hesaba-aktar`, guncelleme ?? null).pipe(map(this.unwrapOne));
    }

    seciliHesaplaraAktar(valorIdler: number[]): Observable<PosTahsilatValorToplamAktarimSonucModel> {
        return this.http.post<ApiResponse<PosTahsilatValorToplamAktarimSonucModel>>(`${this.baseUrl}/secili-hesaplara-aktar`, valorIdler).pipe(map(this.unwrapOne));
    }

    valoruGelenleriHesabaAktar(tesisId?: number | null): Observable<PosTahsilatValorToplamAktarimSonucModel> {
        let params = new HttpParams();
        if (tesisId && tesisId > 0) {
            params = params.set('tesisId', tesisId);
        }
        return this.http.post<ApiResponse<PosTahsilatValorToplamAktarimSonucModel>>(`${this.baseUrl}/valoru-gelenleri-hesaba-aktar`, null, { params }).pipe(map(this.unwrapOne));
    }

    yenidenDene(id: number): Observable<PosTahsilatValorAktarimSonucModel> {
        return this.http.post<ApiResponse<PosTahsilatValorAktarimSonucModel>>(`${this.baseUrl}/${id}/yeniden-dene`, null).pipe(map(this.unwrapOne));
    }

    duzeltmeTersKayit(id: number, aciklama: string): Observable<PosTahsilatValorAktarimSonucModel> {
        return this.http.post<ApiResponse<PosTahsilatValorAktarimSonucModel>>(`${this.baseUrl}/${id}/duzeltme-ters-kayit`, { aciklama }).pipe(map(this.unwrapOne));
    }

    private unwrapOne<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data !== undefined && envelope.data !== null) {
            return envelope.data;
        }

        throw new Error(tryReadApiMessage(envelope) ?? 'Islem basarisiz.');
    }
}
