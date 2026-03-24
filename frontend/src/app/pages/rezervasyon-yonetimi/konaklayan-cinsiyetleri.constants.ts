export const KonaklayanCinsiyetleri = {
    Kadin: 'Kadin',
    Erkek: 'Erkek'
} as const;

export type KonaklayanCinsiyetValue = (typeof KonaklayanCinsiyetleri)[keyof typeof KonaklayanCinsiyetleri];
