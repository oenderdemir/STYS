import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { map, Observable } from 'rxjs';
import { ApiResponse, tryReadApiMessage } from '../../../core/api';
import { getApiBaseUrl } from '../../../core/config';
import {
    BankaValorTakvimiModel,
    NakitBankaPozisyonuFilterModel,
    NakitBankaPozisyonuModel,
    PagedResultModel,
    ValorDetayModel
} from './nakit-banka-pozisyonu.dto';

@Injectable({ providedIn: 'root' })
export class NakitBankaPozisyonuService {
    private readonly http = inject(HttpClient);
    private readonly apiBaseUrl = getApiBaseUrl();
    private readonly baseUrl = `${this.apiBaseUrl}/ui/muhasebe/nakit-banka-pozisyonu`;

    /** Ozet + kasa/banka hesap listeleri + uyarilari TEK cagride getirir. */
    getPozisyon(filter: NakitBankaPozisyonuFilterModel): Observable<NakitBankaPozisyonuModel> {
        const params = this.buildParams(filter);
        return this.http.get<ApiResponse<NakitBankaPozisyonuModel>>(this.baseUrl, { params }).pipe(map(this.unwrapOne));
    }

    /** Gun bazinda OZET listesi (detay satirlari icermez) - kullanici bir gunu actiginda ayrica
     * getValorGunDetaylari cagrilir. */
    getValorTakvimi(kasaBankaHesapId: number, raporTarihi?: string | null): Observable<BankaValorTakvimiModel> {
        let params = new HttpParams();
        if (raporTarihi) {
            params = params.set('raporTarihi', raporTarihi);
        }
        return this.http
            .get<ApiResponse<BankaValorTakvimiModel>>(`${this.baseUrl}/banka-hesaplari/${kasaBankaHesapId}/valor-takvimi`, { params })
            .pipe(map(this.unwrapOne));
    }

    /** Tek bir gunun sayfali detay kayitlari. */
    getValorGunDetaylari(
        kasaBankaHesapId: number,
        valorTarihi: string,
        sayfa: number,
        sayfaBoyutu: number,
        valorDurumu?: string | null
    ): Observable<PagedResultModel<ValorDetayModel>> {
        let params = new HttpParams().set('sayfa', sayfa).set('sayfaBoyutu', sayfaBoyutu);
        if (valorDurumu) {
            params = params.set('valorDurumu', valorDurumu);
        }
        return this.http
            .get<ApiResponse<PagedResultModel<ValorDetayModel>>>(`${this.baseUrl}/banka-hesaplari/${kasaBankaHesapId}/valor-takvimi/${valorTarihi}/detaylar`, { params })
            .pipe(map(this.unwrapOne));
    }

    private buildParams(filter: NakitBankaPozisyonuFilterModel): HttpParams {
        let params = new HttpParams();
        if (filter.tesisId && filter.tesisId > 0) {
            params = params.set('tesisId', filter.tesisId);
        }
        if (filter.raporTarihi) {
            params = params.set('raporTarihi', filter.raporTarihi);
        }
        if (filter.maliYil) {
            params = params.set('maliYil', filter.maliYil);
        }
        if (filter.donem) {
            params = params.set('donem', filter.donem);
        }
        if (filter.hesapTuru) {
            params = params.set('hesapTuru', filter.hesapTuru);
        }
        if (filter.bankaHesapId && filter.bankaHesapId > 0) {
            params = params.set('bankaHesapId', filter.bankaHesapId);
        }
        if (filter.paraBirimi) {
            params = params.set('paraBirimi', filter.paraBirimi);
        }
        if (filter.valorDurumu) {
            params = params.set('valorDurumu', filter.valorDurumu);
        }
        return params;
    }

    private unwrapOne<T>(envelope: ApiResponse<T>): T {
        if (envelope.success && envelope.data !== undefined && envelope.data !== null) {
            return envelope.data;
        }

        throw new Error(tryReadApiMessage(envelope) ?? 'İşlem başarısız.');
    }
}
