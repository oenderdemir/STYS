export interface OdemeDurumuOzetDto {
    toplamRezervasyonSayisi: number;
    borcluRezervasyonSayisi: number;
    odemesiOlmayanRezervasyonSayisi: number;
    kismiOdendiRezervasyonSayisi: number;
    tamamenOdendiRezervasyonSayisi: number;
    cikisYapmisBorcluRezervasyonSayisi: number;
    toplamUcret: number;
    toplamOdenenTutar: number;
    toplamKalanTutar: number;
    paraBirimi: string;
}

export interface OdemeDurumuRezervasyonDto {
    rezervasyonId: number;
    referansNo: string;
    misafirAdiSoyadi: string;
    kurumUnite: string | null;
    girisTarihi: string;
    cikisTarihi: string;
    rezervasyonDurumu: string;
    rezervasyonDurumuLabel: string;
    odaNolari: string[];
    kisiSayisi: number;
    toplamUcret: number;
    odenenTutar: number;
    kalanTutar: number;
    paraBirimi: string;
    odemeDurumu: string;
    odemeDurumuLabel: string;
    sonOdemeTarihi: string | null;
    odemeSayisi: number;
    borcluMu: boolean;
    cikisYapmisMi: boolean;
    cikisYapmisBorcluMu: boolean;
}

export interface OdemeDurumuRaporDto {
    tesisId: number;
    tesisAdi: string | null;
    baslangic: string;
    bitis: string;
    odemeDurumu: string;
    ozet: OdemeDurumuOzetDto;
    rezervasyonlar: OdemeDurumuRezervasyonDto[];
}
