import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { toLocalDateString } from '../../../../core/utils/date-time.util';
import { TicariBelgeService } from '../../ticari-belge.service';
import {
    KDV_UYGULAMA_TIPI_LABELS,
    KdvUygulamaTipi,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    SATIS_BELGESI_TIPI_SECENEKLERI,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    TicariBelgeCariKartLookupDto,
    TicariBelgeGuncelleRequest,
    TicariBelgeGuncelleSatirRequest,
    TicariBelgeIadeAdayiDto,
    TicariBelgeKaynakSatirDto,
    TicariBelgeKdvIstisnaLookupDto,
    createEmptyTicariBelgeGuncelleSatiri
} from '../../ticari-belge.models';

function isAlisBelgeTipi(belgeTipi: SatisBelgesiTipi | null | undefined): boolean {
    return belgeTipi === SatisBelgesiTipi.AlisFaturasi || belgeTipi === SatisBelgesiTipi.AlisIadeFaturasi;
}

function isIadeBelgeTipi(belgeTipi: SatisBelgesiTipi | null | undefined): boolean {
    return belgeTipi === SatisBelgesiTipi.SatisIadeFaturasi || belgeTipi === SatisBelgesiTipi.AlisIadeFaturasi;
}

@Component({
    selector: 'app-ticari-belge-guncelle-dialog',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        AutoCompleteModule,
        ButtonModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TextareaModule,
        ToggleSwitchModule
    ],
    templateUrl: './ticari-belge-guncelle-dialog.html'
})
export class TicariBelgeGuncelleDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() saving = false;
    @Input() formData: TicariBelgeGuncelleRequest | null = null;
    /** Düzenlenmekte olan belgenin kendi Id'si - iade adayı/kaynak satır sorgularında kendisini
     * hariç tutmak ve kalan iade edilebilir miktarı doğru hesaplamak için gereklidir. */
    @Input() belgeId: number | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() save = new EventEmitter<void>();

    private readonly ticariBelgeService = inject(TicariBelgeService);
    private readonly messageService = inject(MessageService);

    readonly belgeTipiSecenekleri = SATIS_BELGESI_TIPI_SECENEKLERI;
    readonly satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    readonly satirTipiSecenekleri = Object.entries(SATIS_BELGESI_SATIR_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as SatisBelgesiSatirTipi,
        label
    }));
    readonly kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;
    readonly kdvUygulamaTipiSecenekleri = Object.entries(KDV_UYGULAMA_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as KdvUygulamaTipi,
        label
    }));

    // ── Cari kart seçimi — operasyonel lookup (ui/ticari-belgeler/lookups/cari-kartlar) ──
    cariKartlar: TicariBelgeCariKartLookupDto[] = [];
    filteredCariKartlar: TicariBelgeCariKartLookupDto[] = [];
    selectedCari: TicariBelgeCariKartLookupDto | null = null;

    // ── KDV istisna tanımı seçimi (satır bazlı, KdvUygulamaTipi'ne göre lazy cache) ──
    private kdvIstisnaCache = new Map<KdvUygulamaTipi, TicariBelgeKdvIstisnaLookupDto[]>();

    // ── İade edilen belge referansı ──
    iadeEdilenBelgeSuggestions: TicariBelgeIadeAdayiDto[] = [];
    iadeEdilenBelgeGosterim: { id: number; belgeNo: string; belgeTarihi: string } | null = null;

    // ── İade kaynak satırları — seçilen kaynağın satırlarına KİLİTLİ, kullanıcı yalnızca
    // miktar/açıklama değiştirebilir (bkz. görev F). ──
    kaynakSatirlar: TicariBelgeKaynakSatirDto[] = [];

    private oncekiBelgeTipi: SatisBelgesiTipi | null | undefined = undefined;

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible'] && this.visible && this.formData) {
            this.oncekiBelgeTipi = this.formData.belgeTipi;
            this.kdvIstisnaCache.clear();
            this.loadCariKartlar();
            this.resolveIadeEdilenBelgeGosterim();
            if (this.formData.iadeEdilenBelgeId) {
                this.loadKaynakSatirlar(this.formData.iadeEdilenBelgeId);
            } else {
                this.kaynakSatirlar = [];
            }
            for (const satir of this.formData.satirlar ?? []) {
                this.ensureKdvIstisnalarLoaded(satir.kdvUygulamaTipi);
            }
        }
    }

    belgeTarihiValue(): Date | null {
        return this.formData?.belgeTarihi ? new Date(this.formData.belgeTarihi) : null;
    }

    onBelgeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.belgeTarihi = value ? toLocalDateString(value) : null;
        }
        // Belge tarihi, KDV istisna geçerlilik penceresini etkiler - önbellek artık GEÇERSİZDİR.
        this.kdvIstisnaCache.clear();
    }

    vadeTarihiValue(): Date | null {
        return this.formData?.vadeTarihi ? new Date(this.formData.vadeTarihi) : null;
    }

    onVadeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.vadeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    // ── Belge tipi (yön) değişimi ──

    /** Belge tipi alış/satış yönleri arasında değiştirilirse: mevcut cari yeni yöne uygun
     * değilse temizlenir, lookup yeniden yüklenir ve kullanıcıya uyarı gösterilir (bkz. görev D). */
    onBelgeTipiChange(value: SatisBelgesiTipi): void {
        if (!this.formData) return;
        const oncekiAlisMi = isAlisBelgeTipi(this.oncekiBelgeTipi);
        const yeniAlisMi = isAlisBelgeTipi(value);
        this.formData.belgeTipi = value;
        this.oncekiBelgeTipi = value;

        const yonDegisti = oncekiAlisMi !== yeniAlisMi;
        if (yonDegisti) {
            const cariUyumsuz = this.selectedCari
                ? !this.cariYeniYonaUygunMu(this.selectedCari, value)
                : false;
            if (cariUyumsuz) {
                this.cariyiTemizle();
                this.messageService.add({
                    severity: 'warn',
                    summary: 'Cari Kart Temizlendi',
                    detail: 'Belge yönü değişti, seçili cari kart yeni yöne uygun olmadığı için temizlendi.'
                });
            }
            this.loadCariKartlar();
        }

        this.kdvIstisnaCache.clear();
    }

    private cariYeniYonaUygunMu(cari: TicariBelgeCariKartLookupDto, belgeTipi: SatisBelgesiTipi): boolean {
        return isAlisBelgeTipi(belgeTipi) ? cari.cariTipi === 'Tedarikci' : cari.cariTipi !== 'Tedarikci';
    }

    // ── Cari kart ──

    private loadCariKartlar(): void {
        const tesisId = this.formData?.tesisId ?? null;
        const belgeTipi = this.formData?.belgeTipi ?? null;
        if (!tesisId || !belgeTipi) {
            this.cariKartlar = [];
            this.filteredCariKartlar = [];
            return;
        }
        this.ticariBelgeService.getCariKartLookup(tesisId, belgeTipi).subscribe({
            next: list => {
                this.cariKartlar = list;
                this.filteredCariKartlar = [...this.cariKartlar];
                this.selectedCari = this.cariKartlar.find(c => c.id === this.formData?.cariKartId) ?? null;
            },
            error: () => {
                this.cariKartlar = [];
                this.filteredCariKartlar = [];
            }
        });
    }

    filterCari(event: { query: string }): void {
        const query = (event.query ?? '').toLowerCase().trim();
        this.filteredCariKartlar = !query
            ? [...this.cariKartlar]
            : this.cariKartlar.filter(
                  c =>
                      (c.unvanAdSoyad ?? '').toLowerCase().includes(query) ||
                      (c.vergiNoTckn ?? '').toLowerCase().includes(query) ||
                      (c.cariKodu ?? '').toLowerCase().includes(query)
              );
    }

    /** Cari seçildiğinde yalnızca cariKartId DEĞİL, mevcut satış belgesi ekranıyla uyumlu şekilde
     * tüm müşteri snapshot alanları da doldurulur (bkz. görev D). */
    onCariKartSecildi(cari: TicariBelgeCariKartLookupDto | null): void {
        this.selectedCari = cari;
        if (!this.formData) return;

        if (!cari) {
            this.cariyiTemizle();
            return;
        }

        this.formData.cariKartId = cari.id;
        this.formData.kurumsalMi = cari.kurumsalMi;
        this.formData.musteriUnvan = cari.kurumsalMi ? cari.unvanAdSoyad : null;
        this.formData.musteriAdSoyad = cari.kurumsalMi ? null : cari.unvanAdSoyad;
        this.formData.musteriVergiNo = cari.kurumsalMi ? (cari.vergiNoTckn ?? null) : null;
        this.formData.musteriTcKimlikNo = cari.kurumsalMi ? null : (cari.vergiNoTckn ?? null);
        this.formData.musteriVergiDairesi = cari.vergiDairesi ?? null;
        this.formData.musteriAdres = cari.adres ?? null;
        this.formData.musteriEposta = cari.eposta ?? null;
        this.formData.musteriTelefon = cari.telefon ?? null;
    }

    /** Cari seçimi temizlendiğinde eski cari kimliğiyle yeni/eskimiş müşteri snapshot'ının
     * BİRLİKTE gönderilmemesi için ikisi de birlikte, açıkça temizlenir (bkz. görev D). */
    private cariyiTemizle(): void {
        this.selectedCari = null;
        if (!this.formData) return;
        this.formData.cariKartId = null;
        this.formData.musteriUnvan = null;
        this.formData.musteriAdSoyad = null;
        this.formData.musteriVergiNo = null;
        this.formData.musteriTcKimlikNo = null;
        this.formData.musteriVergiDairesi = null;
        this.formData.musteriAdres = null;
        this.formData.musteriEposta = null;
        this.formData.musteriTelefon = null;
    }

    formatCariDisplay(cari: TicariBelgeCariKartLookupDto): string {
        const kod = cari.cariKodu || '-';
        const unvan = cari.unvanAdSoyad || '-';
        const vergi = cari.vergiNoTckn ? ` (${cari.vergiNoTckn})` : '';
        return `${kod} - ${unvan}${vergi}`;
    }

    // ── KDV istisna tanımı ──

    private ensureKdvIstisnalarLoaded(kdvUygulamaTipi: KdvUygulamaTipi): void {
        if (kdvUygulamaTipi === KdvUygulamaTipi.Kdvli || kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
            return;
        }
        if (this.kdvIstisnaCache.has(kdvUygulamaTipi)) {
            return;
        }
        const belgeTipi = this.formData?.belgeTipi;
        const belgeTarihi = this.formData?.belgeTarihi;
        if (!belgeTipi || !belgeTarihi) {
            return;
        }
        this.kdvIstisnaCache.set(kdvUygulamaTipi, []);
        this.ticariBelgeService.getKdvIstisnaLookup(belgeTipi, kdvUygulamaTipi, belgeTarihi).subscribe({
            next: list => this.kdvIstisnaCache.set(kdvUygulamaTipi, list),
            error: () => this.kdvIstisnaCache.set(kdvUygulamaTipi, [])
        });
    }

    getKdvIstisnaSecenekleri(satir: TicariBelgeGuncelleSatirRequest): Array<{ label: string; value: number }> {
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.Kdvli || satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
            return [];
        }
        const tanimlar = this.kdvIstisnaCache.get(satir.kdvUygulamaTipi) ?? [];
        return tanimlar.map(t => ({ label: `${t.kod} - ${t.ad}`, value: t.id }));
    }

    /** KdvUygulamaTipi her değiştiğinde eski istisna referansı temizlenir (kullanıcı yeniden
     * seçmelidir); Tevkifatli dışına çıkıldığında tevkifat pay/payda da temizlenir. Bu satır bir
     * kaynak satıra kilitliyse (iade belgesi) mali alan DEĞİŞİKLİĞİ burada yapılmaz. */
    onSatirKdvTipiChange(satir: TicariBelgeGuncelleSatirRequest, value: KdvUygulamaTipi): void {
        if (this.satirKaynagaKilitliMi(satir)) {
            return;
        }
        satir.kdvUygulamaTipi = value;
        this.ensureKdvIstisnalarLoaded(value);

        if (value === KdvUygulamaTipi.Tevkifatli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            return;
        }

        satir.tevkifatPay = null;
        satir.tevkifatPayda = null;

        if (value === KdvUygulamaTipi.Kdvli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            return;
        }

        // TamIstisna / KismiIstisna / KdvKapsamDisi
        satir.kdvOrani = 0;
        satir.kdvIstisnaTanimId = null;
    }

    /** Satır, bir iade kaynağına (KaynakSatirId) kilitliyse mali alanlar salt-okunurdur -
     * yalnızca miktar/açıklama değiştirilebilir (bkz. görev F). */
    satirKaynagaKilitliMi(satir: TicariBelgeGuncelleSatirRequest): boolean {
        return isIadeBelgeTipi(this.formData?.belgeTipi) && !!satir.kaynakSatirId;
    }

    kaynakSatirIadeEdilebilirKalanMiktar(satir: TicariBelgeGuncelleSatirRequest): number | null {
        if (!satir.kaynakSatirId) return null;
        const kaynakId = Number(satir.kaynakSatirId);
        const kaynak = this.kaynakSatirlar.find(k => k.id === kaynakId);
        return kaynak?.iadeEdilebilirKalanMiktar ?? null;
    }

    // ── İade edilen belge referansı ──

    private resolveIadeEdilenBelgeGosterim(): void {
        const id = this.formData?.iadeEdilenBelgeId;
        if (!id) {
            this.iadeEdilenBelgeGosterim = null;
            return;
        }
        this.ticariBelgeService.getById(id).subscribe({
            next: belge => (this.iadeEdilenBelgeGosterim = { id: belge.id, belgeNo: belge.belgeNo, belgeTarihi: belge.belgeTarihi }),
            error: () => (this.iadeEdilenBelgeGosterim = null)
        });
    }

    /** İade adayı araması, yeni sınırlandırılmış/sunucu-taraflı iade-adaylari uç noktasını kullanır
     * (bkz. görev E) - genel TicariBelge filter endpointi autocomplete olarak KULLANILMAZ. */
    searchIadeEdilenBelge(event: { query: string }): void {
        const tesisId = this.formData?.tesisId;
        const cariKartId = this.formData?.cariKartId;
        const belgeTipi = this.formData?.belgeTipi;
        const belgeTarihi = this.formData?.belgeTarihi;
        if (!tesisId || !cariKartId || !belgeTipi || !belgeTarihi || !isIadeBelgeTipi(belgeTipi)) {
            this.iadeEdilenBelgeSuggestions = [];
            return;
        }

        this.ticariBelgeService
            .getIadeAdaylari({
                mevcutBelgeId: this.belgeId,
                tesisId,
                belgeTipi,
                cariKartId,
                belgeTarihi,
                belgeNoArama: event.query || null
            })
            .subscribe({
                next: list => (this.iadeEdilenBelgeSuggestions = list),
                error: () => (this.iadeEdilenBelgeSuggestions = [])
            });
    }

    /** Yeni bir iade kaynağı seçildiğinde: eski KaynakSatirId'ler SESSİZCE taşınmaz - satırlar
     * seçilen kaynağın satırlarıyla AÇIKÇA yeniden eşlenir (bkz. görev F). */
    onIadeEdilenBelgeSecildi(belge: TicariBelgeIadeAdayiDto | null): void {
        this.iadeEdilenBelgeGosterim = belge ? { id: belge.id, belgeNo: belge.belgeNo, belgeTarihi: belge.belgeTarihi } : null;
        if (!this.formData) return;

        this.formData.iadeEdilenBelgeId = belge?.id ?? null;
        this.formData.iadeEdilenBelgeReferansiKaldir = false;

        if (belge) {
            this.loadKaynakSatirlar(belge.id);
        } else {
            this.kaynakSatirlar = [];
            this.formData.satirlar = [];
        }
    }

    clearIadeEdilenBelgeReferansi(): void {
        this.iadeEdilenBelgeGosterim = null;
        this.kaynakSatirlar = [];
        if (this.formData) {
            this.formData.iadeEdilenBelgeId = null;
            this.formData.iadeEdilenBelgeReferansiKaldir = true;
            // Referans kaldırıldı - geçersiz/gizli bir kaynak referansı bırakılmaması için
            // kaynağa kilitli satırlar da temizlenir (bkz. görev F).
            this.formData.satirlar = [];
        }
    }

    private loadKaynakSatirlar(kaynakBelgeId: number): void {
        this.ticariBelgeService.getKaynakSatirlar(kaynakBelgeId, this.belgeId).subscribe({
            next: satirlar => {
                this.kaynakSatirlar = satirlar;
                this.remapSatirlarFromKaynak(satirlar);
            },
            error: () => {
                this.kaynakSatirlar = [];
            }
        });
    }

    /** İade satırlarını seçilen kaynağın satırlarıyla AÇIKÇA yeniden eşler - her satırın mali
     * alanları kaynaktan gelir ve frontend'de başka bir değere DÖNÜŞMEZ; kullanıcı yalnızca
     * miktar/açıklama değiştirebilir (bkz. görev F). */
    private remapSatirlarFromKaynak(kaynakSatirlar: TicariBelgeKaynakSatirDto[]): void {
        if (!this.formData) return;
        this.formData.satirlar = kaynakSatirlar.map((k, index) => ({
            siraNo: index + 1,
            satirTipi: SatisBelgesiSatirTipi.Iade,
            aciklama: k.aciklama,
            birim: k.birim,
            miktar: k.iadeEdilebilirKalanMiktar > 0 ? Math.min(1, k.iadeEdilebilirKalanMiktar) : 0,
            birimFiyat: k.birimFiyat,
            indirimOrani: k.indirimOrani,
            indirimTutari: 0,
            kdvUygulamaTipi: k.kdvUygulamaTipi,
            kdvIstisnaTanimId: k.kdvIstisnaTanimId ?? null,
            kdvOrani: k.kdvOrani,
            tevkifatPay: k.tevkifatPay ?? null,
            tevkifatPayda: k.tevkifatPayda ?? null,
            otvOrani: 0,
            otvTutari: 0,
            oivOrani: 0,
            oivTutari: 0,
            konaklamaVergisiOrani: 0,
            konaklamaVergisiTutari: 0,
            kaynakSatirId: String(k.id)
        }));
        for (const satir of this.formData.satirlar) {
            this.ensureKdvIstisnalarLoaded(satir.kdvUygulamaTipi);
        }
    }

    // ── Satırlar ──

    addSatir(): void {
        if (!this.formData) return;
        if (isIadeBelgeTipi(this.formData.belgeTipi)) {
            // İade belgesinde satırlar yalnızca kaynak seçimiyle yeniden eşlenir - serbest satır
            // eklenmez (bkz. görev F).
            return;
        }
        const satirlar = this.formData.satirlar ?? [];
        const yeniSatir = createEmptyTicariBelgeGuncelleSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        this.formData.satirlar = [...satirlar, yeniSatir];
    }

    removeSatir(index: number): void {
        if (!this.formData?.satirlar) return;
        const satir = this.formData.satirlar[index];
        if (this.satirKaynagaKilitliMi(satir)) {
            return;
        }
        this.formData.satirlar = this.formData.satirlar
            .filter((_, i) => i !== index)
            .map((s, i) => ({ ...s, siraNo: i + 1 }));
    }

    trackBySatirIndex(index: number): number {
        return index;
    }

    isTevkifatli(satir: TicariBelgeGuncelleSatirRequest): boolean {
        return satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli;
    }

    onHide(): void {
        this.visibleChange.emit(false);
    }

    onSaveClick(): void {
        this.save.emit();
    }
}
