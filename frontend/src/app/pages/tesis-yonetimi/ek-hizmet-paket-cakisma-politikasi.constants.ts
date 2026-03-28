export const EkHizmetPaketCakismaPolitikalari = {
    Uyari: 'Uyari',
    OnayIste: 'OnayIste',
    Engelle: 'Engelle'
} as const;

export const EkHizmetPaketCakismaPolitikasiSecenekleri = [
    { label: 'Uyari Goster', value: EkHizmetPaketCakismaPolitikalari.Uyari },
    { label: 'Onay Iste', value: EkHizmetPaketCakismaPolitikalari.OnayIste },
    { label: 'Tamamen Engelle', value: EkHizmetPaketCakismaPolitikalari.Engelle }
];
