/**
 * Faz 2B.11 görev md.37 - backend enum'unun gerçek (int) değerleriyle BİREBİR eşleşir (bkz.
 * STYS.Muhasebe.SatisBelgeleri.Enums.EBelgeEntegrasyonYontemi). Magic integer'lar component
 * içine DAĞITILMAZ - her yerde bu enum kullanılır.
 */
export enum EBelgeEntegrasyonYontemi {
    Yapilandirilmadi = 0,
    Kullanilmayacak = 1,
    HariciMuhasebeSistemi = 2,
    GibPortal = 3,
    OzelEntegrator = 4,
    DogrudanGib = 5
}

/** Yalnız GÖRÜNTÜLEME etiketleri - business capability (operasyonel mi, hangi yetenek gerekir) BURADA TANIMLANMAZ, tamamı backend'den (`Yontemler`/readiness) gelir. */
export const EBELGE_YONTEM_LABELS: Readonly<Record<EBelgeEntegrasyonYontemi, string>> = {
    [EBelgeEntegrasyonYontemi.Yapilandirilmadi]: 'Yapılandırılmadı',
    [EBelgeEntegrasyonYontemi.Kullanilmayacak]: 'Kullanılmayacak',
    [EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi]: 'Harici Muhasebe Sistemi',
    [EBelgeEntegrasyonYontemi.GibPortal]: 'GİB Portal',
    [EBelgeEntegrasyonYontemi.OzelEntegrator]: 'Özel Entegratör',
    [EBelgeEntegrasyonYontemi.DogrudanGib]: 'Doğrudan GİB'
};

export const EBELGE_YONTEM_ACIKLAMALARI: Readonly<Record<EBelgeEntegrasyonYontemi, string>> = {
    [EBelgeEntegrasyonYontemi.Yapilandirilmadi]: 'Bu kurum için e-belge politikası henüz yapılandırılmamış.',
    [EBelgeEntegrasyonYontemi.Kullanilmayacak]: "STYS bu kurum için yerel e-belge pipeline'ı oluşturmayacak.",
    [EBelgeEntegrasyonYontemi.HariciMuhasebeSistemi]: 'Mali belge harici muhasebe sistemi tarafından yönetilecek.',
    [EBelgeEntegrasyonYontemi.GibPortal]: "STYS canonical snapshot ve doğrulanmış unsigned UBL oluşturur. Belge yerel olarak imzalanmaz ve otomatik olarak GİB'e gönderilmez.",
    [EBelgeEntegrasyonYontemi.OzelEntegrator]: 'Özel entegratör adaptörü henüz uygulanmadı - "Henüz desteklenmiyor".',
    [EBelgeEntegrasyonYontemi.DogrudanGib]: 'Doğrudan GİB (HSM/mali mühür) entegrasyonu henüz uygulanmadı - "Henüz desteklenmiyor".'
};

/**
 * Faz 2B.11 görev md.11/md.32 - backend'in döndürdüğü SABİT, güvenli kod sözlüğü İÇİN
 * kullanıcı-dostu Türkçe etiket haritası. Ham değer/VKN/müşteri bilgisi BURADA YOKTUR - yalnız
 * backend'in ZATEN güvenli kod olarak döndürdüğü sabitlerin GÖRÜNTÜLEME karşılığı.
 */
export const EBELGE_BLOKAJ_NEDENI_LABELS: Readonly<Record<string, string>> = {
    VERGI_NO: 'Vergi No',
    VERGI_DAIRESI: 'Vergi Dairesi',
    ADRES: 'Adres',
    ILCE: 'İlçe',
    IL: 'İl',
    EBELGE_KURUM_POLICY_NOT_CONFIGURED: 'Politika yapılandırılmadı',
    EBELGE_KURUM_POLICY_INACTIVE: 'Politika pasif',
    EBELGE_KURUM_POLICY_BEFORE_ACTIVATION_DATE: 'Aktivasyon tarihi gelmedi',
    EBELGE_KURUM_POLICY_METHOD_NOT_IMPLEMENTED: 'Yöntem desteklenmiyor',
    EBELGE_GLOBAL_PROCESSING_DISABLED: 'Genel işlem kapısı kapalı',
    EBELGE_KURUM_POLICY_SELLER_DATA_INCOMPLETE: 'Satıcı ana verileri eksik',
    EBELGE_SIGNING_GATE_DISABLED: 'İmzalama kapısı kapalı'
};

export function blokajNedeniLabel(kod: string): string {
    return EBELGE_BLOKAJ_NEDENI_LABELS[kod] ?? kod;
}

export interface KurumEBelgePolitikasiModel {
    kurumId: number;
    entegrasyonYontemi: EBelgeEntegrasyonYontemi;
    aktifMi: boolean;
    aktivasyonYerelTarihi?: string | null;
    hariciSistemKodu?: string | null;
    politikaSurumu: number;
    /** UI'da HİÇBİR ZAMAN gösterilmez - yalnız state içinde saklanıp bir sonraki PUT'a geri gönderilir (optimistic concurrency). */
    rowVersion: string;
}

export interface KurumEBelgePolitikasiGuncellemeRequest {
    entegrasyonYontemi: EBelgeEntegrasyonYontemi;
    aktifMi: boolean;
    aktivasyonYerelTarihi?: string | null;
    hariciSistemKodu?: string | null;
    degisiklikNedeni: string;
    rowVersion: string;
}

export interface KurumEBelgePolitikaRevizyonuModel {
    eskiSurum: number;
    yeniSurum: number;
    eskiYontem: EBelgeEntegrasyonYontemi;
    yeniYontem: EBelgeEntegrasyonYontemi;
    eskiAktifMi: boolean;
    yeniAktifMi: boolean;
    degisiklikZamaniUtc: string;
    degisiklikNedeni: string;
    degistirenKullanici?: string | null;
}

export interface EBelgeYontemReadinessModel {
    yontem: EBelgeEntegrasyonYontemi;
    operasyonelMi: boolean;
    hariciSistemKoduGerekliMi: boolean;
    yerelSnapshotOlustur: boolean;
    yerelUnsignedUblOlustur: boolean;
    yerelImzaOlustur: boolean;
    otomatikGonderimYap: boolean;
}

/** Faz 2B.11 görev md.6/md.31 - backend `KurumEBelgeReadinessDto`'sunun BİREBİR TypeScript karşılığı - tüm iş kuralı sonucu, hesaplama DEĞİL. */
export interface KurumEBelgeReadinessModel {
    kurumId: number;
    politikaYapilandirilmisMi: boolean;
    politikaAktifMi: boolean;
    entegrasyonYontemi: EBelgeEntegrasyonYontemi;
    yontemOperasyonelMi: boolean;
    kurumAktivasyonYerelTarihi?: string | null;
    globalMinimumAktivasyonYerelTarihi?: string | null;
    globalProcessingAktifMi: boolean;
    globalProcessingDurumu: string;
    yerelSnapshotGerekliMi: boolean;
    yerelUnsignedUblGerekliMi: boolean;
    yerelImzaGerekliMi: boolean;
    otomatikGonderimGerekliMi: boolean;
    saticiAnaVerileriHazirMi: boolean;
    eksikSaticiAlanlari: string[];
    signingGateUygulanabilirMi: boolean;
    signingSuAnMumkunMu: boolean;
    islemeHazirMi: boolean;
    blokajNedenleri: string[];
    yontemler: EBelgeYontemReadinessModel[];
}
