export interface TasinirKodModel {
    id?: number;
    tamKod: string;
    duzey1Kod?: string | null;
    duzey2Kod?: string | null;
    duzey3Kod?: string | null;
    duzey4Kod?: string | null;
    duzey5Kod?: string | null;
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
    duzey1Kod?: string | null;
    duzey2Kod?: string | null;
    duzey3Kod?: string | null;
    duzey4Kod?: string | null;
    duzey5Kod?: string | null;
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
