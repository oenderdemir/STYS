export interface ErisimTeshisKullaniciDto {
    id: string;
    kullaniciAdi: string;
    adSoyad: string;
    eposta?: string | null;
}

export interface ErisimTeshisTesisDto {
    id: number;
    ad: string;
}

export interface ErisimTeshisModulDto {
    anahtar: string;
    ad: string;
    route: string;
    tesisSecimiGerekli: boolean;
}

export interface ErisimTeshisReferansDto {
    kullanicilar: ErisimTeshisKullaniciDto[];
    tesisler: ErisimTeshisTesisDto[];
    moduller: ErisimTeshisModulDto[];
}

export interface ErisimTeshisIstekDto {
    kullaniciId: string;
    modulAnahtari: string;
    tesisId?: number | null;
}

export interface ErisimTeshisKullaniciGrupDto {
    grupAdi: string;
    roller: string[];
}

export interface ErisimTeshisScopeDto {
    adminMi: boolean;
    scopedMi: boolean;
    tesisler: ErisimTeshisTesisDto[];
    binaIdleri: number[];
    ozet: string;
}

export interface ErisimTeshisIslemSonucDto {
    islemAnahtari: string;
    islemAdi: string;
    gerekliYetki: string;
    yetkiVar: boolean;
    tesisScopeGerekli: boolean;
    tesisScopeUygun?: boolean | null;
    sonuc: boolean;
    durum: string;
    engelKodu: string;
    aciklama: string;
    oneri: string;
}

export interface ErisimTeshisSonucDto {
    kullanici: ErisimTeshisKullaniciDto;
    modul: ErisimTeshisModulDto;
    seciliTesis?: ErisimTeshisTesisDto | null;
    kullaniciGruplari: ErisimTeshisKullaniciGrupDto[];
    yetkiler: string[];
    scope: ErisimTeshisScopeDto;
    islemler: ErisimTeshisIslemSonucDto[];
    genelDurum: string;
    basariliIslemSayisi: number;
    uyariIslemSayisi: number;
    engelliIslemSayisi: number;
    eksikYetkiler: string[];
    onerilenAksiyonlar: string[];
    ozet: string;
}
