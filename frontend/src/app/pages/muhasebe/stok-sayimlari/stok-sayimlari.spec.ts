import { StokSayimlariPage } from './stok-sayimlari';

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
});
