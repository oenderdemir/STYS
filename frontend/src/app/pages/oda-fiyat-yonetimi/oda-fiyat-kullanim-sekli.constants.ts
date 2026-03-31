export const ODA_FIYAT_KULLANIM_SEKLI_OPTIONS = [
    { label: 'Kisi Basi', value: 'KisiBasi' },
    { label: 'Ozel Kullanim', value: 'OzelKullanim' }
];

export function getOdaFiyatKullanimSekliLabel(value: string | null | undefined): string {
    return ODA_FIYAT_KULLANIM_SEKLI_OPTIONS.find((option) => option.value === value)?.label ?? value ?? '-';
}
