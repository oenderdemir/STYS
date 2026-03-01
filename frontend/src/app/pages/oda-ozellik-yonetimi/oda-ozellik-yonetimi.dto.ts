export type OdaOzellikVeriTipi = 'boolean' | 'number' | 'text';

export interface OdaOzellikDto {
    id?: number | null;
    kod: string;
    ad: string;
    veriTipi: OdaOzellikVeriTipi;
    aktifMi: boolean;
}

export const ODA_OZELLIK_VERI_TIPI_OPTIONS: Array<{ label: string; value: OdaOzellikVeriTipi }> = [
    { label: 'Boolean', value: 'boolean' },
    { label: 'Number', value: 'number' },
    { label: 'Text', value: 'text' }
];
