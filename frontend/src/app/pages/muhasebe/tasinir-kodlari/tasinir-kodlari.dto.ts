export interface TasinirKodModel {
    id?: number;
    tamKod: string;
    kod: string;
    ad: string;
    duzeyNo: number;
    ustKodId?: number | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface CreateTasinirKodRequest extends Omit<TasinirKodModel, 'id'> {}
export interface UpdateTasinirKodRequest extends Omit<TasinirKodModel, 'id'> {}

export interface ImportTasinirKodSatiriModel {
    tamKod: string;
    kod?: string | null;
    ad: string;
    duzeyNo: number;
    ustTamKod?: string | null;
    aktifMi: boolean;
    aciklama?: string | null;
}

export interface ImportTasinirKodlariRequest {
    mevcutlariGuncelle: boolean;
    pasiflestirilmeyenleriPasifYap: boolean;
    satirlar: ImportTasinirKodSatiriModel[];
}

export interface TasinirKodImportSonucModel {
    eklenen: number;
    guncellenen: number;
    pasifYapilan: number;
    toplamIslenen: number;
}
