import { StokSayimlariPage } from './stok-sayimlari';
import { of } from 'rxjs';

describe('StokSayimlariPage', () => {
    it('sadece fark olanlari filtreler', () => {
        const page = Object.create(StokSayimlariPage.prototype) as StokSayimlariPage;
        page.selectedSayim = {
            tesisId: 1,
            depoId: 10,
            sayimTarihi: '2026-08-23T10:00:00',
            durum: 'Taslak',
            satirlar: [
                { stokSayimId: 1, tasinirKartId: 100, takipTipi: 'Yok', stokKodu: 'A', tasinirKartAd: 'Kart A', birim: 'Adet', sistemMiktari: 10, sayilanMiktar: 10, farkMiktari: 0 },
                { stokSayimId: 1, tasinirKartId: 101, takipTipi: 'Yok', stokKodu: 'B', tasinirKartAd: 'Kart B', birim: 'Adet', sistemMiktari: 10, sayilanMiktar: 7, farkMiktari: -3 }
            ]
        };

        page.showOnlyDifferences = true;

        const result = page.getVisibleSatirlar();

        expect(result.length).toBe(1);
        expect(result[0].tasinirKartId).toBe(101);
    });

    it('kesinlestirirken once satirlari kaydeder sonra kesinlestirir', () => {
        const order: string[] = [];
        const savedItem = {
            id: 1,
            tesisId: 1,
            depoId: 10,
            sayimTarihi: '2026-08-23T10:00:00',
            durum: 'Taslak',
            satirlar: [
                { id: 11, stokSayimId: 1, tasinirKartId: 100, takipTipi: 'Yok', stokKodu: 'A', tasinirKartAd: 'Kart A', birim: 'Adet', sistemMiktari: 10, sayilanMiktar: 12, farkMiktari: 2 }
            ]
        };
        const finalizedItem = { ...savedItem, durum: 'Kesinlesti' };
        const page = Object.create(StokSayimlariPage.prototype) as any;

        page.selectedSayim = {
            id: 1,
            tesisId: 1,
            depoId: 10,
            sayimTarihi: '2026-08-23T10:00:00',
            durum: 'Taslak',
            satirlar: [
                { id: 11, stokSayimId: 1, tasinirKartId: 100, takipTipi: 'Yok', stokKodu: 'A', tasinirKartAd: 'Kart A', birim: 'Adet', sistemMiktari: 10, sayilanMiktar: 12, farkMiktari: 2 }
            ]
        };
        page.pageNumber = 1;
        page.pageSize = 10;
        page.load = () => undefined;
        page.cdr = { detectChanges: () => undefined };
        page.messageService = { add: () => undefined };
        page.service = {
            updateSatirlar: () => {
                order.push('save');
                return of(savedItem);
            },
            kesinlestir: () => {
                order.push('finalize');
                return of(finalizedItem);
            }
        };

        page.kesinlestir();

        expect(order).toEqual(['save', 'finalize']);
        expect(page.selectedSayim?.durum).toBe('Kesinlesti');
    });

    it('politika yoksa maliyet secim dialogunu acar', () => {
        const page = Object.create(StokSayimlariPage.prototype) as any;
        page.cdr = { detectChanges: () => undefined };
        page.showError = () => undefined;
        page.stokMaliyetPolitikasiService = {
            getCurrent: () => of({ tesisId: 1, maliYil: 2026, maliyetYontemi: null, politikaSecildiMi: false })
        };

        page.loadCurrentMaliyetPolitikasi(1, '2026-08-23T10:00:00');

        expect(page.maliyetPolitikasiDialogVisible).toBeTrue();
        expect(page.secilenMaliyetYontemi).toBe('AgirlikliOrtalama');
    });

    it('politika varsa maliyet secim dialogunu acmaz', () => {
        const page = Object.create(StokSayimlariPage.prototype) as any;
        page.cdr = { detectChanges: () => undefined };
        page.showError = () => undefined;
        page.stokMaliyetPolitikasiService = {
            getCurrent: () => of({ tesisId: 1, maliYil: 2026, maliyetYontemi: 'AgirlikliOrtalama', politikaSecildiMi: true })
        };

        page.loadCurrentMaliyetPolitikasi(1, '2026-08-23T10:00:00');

        expect(page.maliyetPolitikasiDialogVisible).toBeFalse();
        expect(page.currentMaliyetPolitikasi?.maliYil).toBe(2026);
    });
});
