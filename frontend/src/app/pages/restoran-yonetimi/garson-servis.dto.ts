export interface GarsonMasaModel {
    masaId: number;
    masaNo: string;
    durum: string;
    aktifOturumId?: number | null;
    aktifOturumToplamTutar?: number | null;
    aktifKalemSayisi: number;
    sonIslemZamani?: string | null;
}

export interface MasaOturumuKalemiModel {
    id: number;
    urunId: number;
    urunAdi: string;
    birimFiyat: number;
    miktar: number;
    satirToplam: number;
    notlar?: string | null;
}

export interface MasaOturumuModel {
    oturumId: number;
    restoranId: number;
    masaId: number;
    masaNo: string;
    durum: string;
    notlar?: string | null;
    paraBirimi: string;
    toplamTutar: number;
    siparisTarihi: string;
    kalemler: MasaOturumuKalemiModel[];
}

export interface CreateMasaOturumuRequest {
    paraBirimi: string;
}

export interface AddMasaOturumuKalemiRequest {
    urunId: number;
    miktar: number;
    notlar?: string | null;
}

export interface UpdateMasaOturumuKalemiRequest {
    miktar: number;
    notlar?: string | null;
}

export interface UpdateMasaOturumuNotRequest {
    notlar?: string | null;
}

export interface UpdateMasaOturumuDurumRequest {
    durum: string;
}

export interface GarsonMenuModel {
    restoranId: number;
    kategoriler: GarsonMenuKategoriModel[];
}

export interface GarsonMenuKategoriModel {
    id: number;
    ad: string;
    siraNo: number;
    urunler: GarsonMenuUrunModel[];
}

export interface GarsonMenuUrunModel {
    id: number;
    kategoriId: number;
    ad: string;
    aciklama?: string | null;
    fiyat: number;
    paraBirimi: string;
    hazirlamaSuresiDakika: number;
}

export function getGarsonMasaDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (durum) {
        case 'Musait':
            return 'success';
        case 'Dolu':
            return 'warn';
        case 'Serviste':
            return 'info';
        case 'HesapIstendi':
            return 'danger';
        case 'Kapali':
            return 'secondary';
        default:
            return 'secondary';
    }
}

export function getMasaOturumuDurumSeverity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (durum) {
        case 'Taslak':
            return 'secondary';
        case 'Hazirlaniyor':
            return 'warn';
        case 'Serviste':
            return 'info';
        case 'HesapIstendi':
        case 'Hazir':
            return 'danger';
        case 'Tamamlandi':
            return 'success';
        case 'Iptal':
            return 'danger';
        default:
            return 'secondary';
    }
}
