export const KonaklamaTipiIcerikHizmetKodlari = {
    Kahvalti: 'Kahvalti',
    OgleYemegi: 'OgleYemegi',
    AksamYemegi: 'AksamYemegi',
    Wifi: 'Wifi',
    Otopark: 'Otopark',
    HavaalaniTransferi: 'HavaalaniTransferi',
    GunlukTemizlik: 'GunlukTemizlik'
} as const;

export const KonaklamaTipiIcerikPeriyotlari = {
    Gunluk: 'Gunluk',
    KonaklamaBoyunca: 'KonaklamaBoyunca'
} as const;

export const KonaklamaTipiIcerikKullanimTipleri = {
    Adetli: 'Adetli',
    Sinirsiz: 'Sinirsiz'
} as const;

export const KonaklamaTipiIcerikKullanimNoktalari = {
    Genel: 'Genel',
    Restoran: 'Restoran',
    Bar: 'Bar',
    OdaServisi: 'OdaServisi'
} as const;

export const KonaklamaTipiIcerikHizmetSecenekleri = [
    { label: 'Kahvalti', value: KonaklamaTipiIcerikHizmetKodlari.Kahvalti },
    { label: 'Ogle Yemegi', value: KonaklamaTipiIcerikHizmetKodlari.OgleYemegi },
    { label: 'Aksam Yemegi', value: KonaklamaTipiIcerikHizmetKodlari.AksamYemegi },
    { label: 'Wi-Fi', value: KonaklamaTipiIcerikHizmetKodlari.Wifi },
    { label: 'Otopark', value: KonaklamaTipiIcerikHizmetKodlari.Otopark },
    { label: 'Havaalani Transferi', value: KonaklamaTipiIcerikHizmetKodlari.HavaalaniTransferi },
    { label: 'Gunluk Temizlik', value: KonaklamaTipiIcerikHizmetKodlari.GunlukTemizlik }
];

export const KonaklamaTipiIcerikPeriyotSecenekleri = [
    { label: 'Gunluk', value: KonaklamaTipiIcerikPeriyotlari.Gunluk },
    { label: 'Konaklama Boyunca', value: KonaklamaTipiIcerikPeriyotlari.KonaklamaBoyunca }
];

export const KonaklamaTipiIcerikKullanimTipiSecenekleri = [
    { label: 'Adetli', value: KonaklamaTipiIcerikKullanimTipleri.Adetli },
    { label: 'Sinirsiz', value: KonaklamaTipiIcerikKullanimTipleri.Sinirsiz }
];

export const KonaklamaTipiIcerikKullanimNoktasiSecenekleri = [
    { label: 'Genel', value: KonaklamaTipiIcerikKullanimNoktalari.Genel },
    { label: 'Restoran', value: KonaklamaTipiIcerikKullanimNoktalari.Restoran },
    { label: 'Bar', value: KonaklamaTipiIcerikKullanimNoktalari.Bar },
    { label: 'Oda Servisi', value: KonaklamaTipiIcerikKullanimNoktalari.OdaServisi }
];
