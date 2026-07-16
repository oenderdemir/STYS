export interface TahsilatOdemeBelgesiModel {
    id?: number;
    belgeNo: string;
    belgeTarihi: string;
    belgeTipi: string;
    cariKartId: number;
    tutar: number;
    paraBirimi: string;
    odemeYontemi: string;
    aciklama?: string | null;
    kaynakModul?: string | null;
    kaynakId?: number | null;
    kapatilacakCariHareketId?: number | null;
    durum: string;
    muhasebeFisId?: number | null;
    muhasebeFisOlusturmaTarihi?: string | null;
    /** Bagli MuhasebeFis'in guncel durumu (Taslak/Onayli/Iptal/TersKayit). muhasebeFisId dolu
     * olsa bile bu deger 'Iptal' ise belge yeniden fislenebilir. */
    muhasebeFisDurumu?: string | null;
}

export interface CreateTahsilatOdemeBelgesiRequest extends Omit<TahsilatOdemeBelgesiModel, 'id'> {}
export interface UpdateTahsilatOdemeBelgesiRequest extends Omit<TahsilatOdemeBelgesiModel, 'id'> {}

export interface TahsilatOdemeOzetModel {
    gun: string;
    toplamTahsilat: number;
    toplamOdeme: number;
    net: number;
    paraBirimi: string;
}

export const BELGE_TIPLERI = [{ label: 'Tahsilat', value: 'Tahsilat' }, { label: 'Odeme', value: 'Odeme' }];
export const ODEME_YONTEMLERI = [
    { label: 'Nakit', value: 'Nakit' },
    { label: 'Kredi Karti', value: 'KrediKarti' },
    { label: 'Havale/EFT', value: 'HavaleEft' },
    { label: 'Odaya Ekle', value: 'OdayaEkle' },
    { label: 'Mahsup', value: 'Mahsup' }
];

