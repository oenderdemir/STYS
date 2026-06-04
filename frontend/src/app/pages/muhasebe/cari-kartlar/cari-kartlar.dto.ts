export const CARI_TIPLERI = {
  Musteri: 'Musteri',
  Tedarikci: 'Tedarikci',
  KurumsalMusteri: 'KurumsalMusteri'
} as const;

export const CARI_KART_TIPLERI = CARI_TIPLERI;

export interface CariBakiyeModel {
  cariKartId: number;
  cariKodu: string;
  unvanAdSoyad: string;
  toplamBorc: number;
  toplamAlacak: number;
  bakiye: number;
  paraBirimi: string;
}

export interface CariKartAcilisBakiyesiDuzeltRequest {
  yeniTutar: number;
  yeniYonu?: string | null;
  duzeltmeTarihi?: string | null;
}

export interface CariKartBankaHesabiModel {
  id?: number | null;
  cariKartId?: number | null;
  bankaAdi?: string | null;
  subeAdi?: string | null;
  hesapNo?: string | null;
  iban?: string | null;
  aciklama?: string | null;
}

export interface CariKartYetkiliKisiModel {
  id?: number | null;
  cariKartId?: number | null;
  adSoyad: string;
  gorevUnvan?: string | null;
  telefon?: string | null;
  eposta?: string | null;
  aciklama?: string | null;
}

export interface CariKartModel {
  id?: number | null;
  tesisId?: number | null;
  cariTipi: string;
  cariKodu: string;
  muhasebeHesapPlaniId?: number | null;
  anaMuhasebeHesapKodu?: string | null;
  muhasebeHesapSiraNo?: number | null;
  unvanAdSoyad: string;
  vergiNoTckn?: string | null;
  vergiDairesi?: string | null;
  telefon?: string | null;
  eposta?: string | null;
  adres?: string | null;
  il?: string | null;
  ilce?: string | null;
  aktifMi: boolean;
  eFaturaMukellefiMi: boolean;
  eArsivKapsamindaMi: boolean;
  aciklama?: string | null;
  acilisBakiyeTarihi?: string | null;
  acilisBakiyeTutari?: number | null;
  acilisBakiyeYonu?: string | null;
  acilisBakiyeDuzeltilebilirMi?: boolean;
  bankaHesaplari: CariKartBankaHesabiModel[];
  yetkiliKisiler: CariKartYetkiliKisiModel[];
}

export interface CreateCariKartRequest {
  tesisId?: number | null;
  cariTipi: string;
  cariKodu: string | null;
  muhasebeHesapPlaniId?: number | null;
  anaMuhasebeHesapKodu?: string | null;
  muhasebeHesapSiraNo?: number | null;
  unvanAdSoyad: string;
  vergiNoTckn?: string | null;
  vergiDairesi?: string | null;
  telefon?: string | null;
  eposta?: string | null;
  adres?: string | null;
  il?: string | null;
  ilce?: string | null;
  aktifMi: boolean;
  eFaturaMukellefiMi: boolean;
  eArsivKapsamindaMi: boolean;
  aciklama?: string | null;
  acilisBakiyeTarihi?: string | null;
  acilisBakiyeTutari?: number | null;
  acilisBakiyeYonu?: string | null;
  bankaHesaplari: CariKartBankaHesabiModel[];
  yetkiliKisiler: CariKartYetkiliKisiModel[];
}

export interface UpdateCariKartRequest extends CreateCariKartRequest {
  id: number | null;
}
