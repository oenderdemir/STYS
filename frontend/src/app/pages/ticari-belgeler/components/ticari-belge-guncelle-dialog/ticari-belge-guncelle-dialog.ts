import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { toLocalDateString } from '../../../../core/utils/date-time.util';
import { CariKartModel } from '../../../muhasebe/cari-kartlar/cari-kartlar.dto';
import { CariKartlarService } from '../../../muhasebe/cari-kartlar/cari-kartlar.service';
import { KdvIstisnaTanimDto, createDefaultKdvIstisnaTanimFilter } from '../../../muhasebe/models/kdv-istisna-tanim.model';
import { KdvIstisnaTanimService } from '../../../muhasebe/services/kdv-istisna-tanim.service';
import { TicariBelgeService } from '../../ticari-belge.service';
import {
    KDV_UYGULAMA_TIPI_LABELS,
    KdvUygulamaTipi,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    SATIS_BELGESI_TIPI_SECENEKLERI,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    TicariBelgeDto,
    TicariBelgeGuncelleRequest,
    TicariBelgeGuncelleSatirRequest,
    createDefaultTicariBelgeFilter,
    createEmptyTicariBelgeGuncelleSatiri,
    getMusteriDisplayName
} from '../../ticari-belge.models';

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
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() save = new EventEmitter<void>();

    private readonly cariKartService = inject(CariKartlarService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly ticariBelgeService = inject(TicariBelgeService);

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

    getMusteriDisplayName = getMusteriDisplayName;

    // ── Cari kart seçimi (bkz. muhasebe/satis-belgeleri ile aynı p-autoComplete deseni) ──
    cariKartlar: CariKartModel[] = [];
    filteredCariKartlar: CariKartModel[] = [];
    selectedCari: CariKartModel | null = null;

    // ── KDV istisna tanımı seçimi (satır bazlı) ──
    private kdvIstisnaTanimlari: KdvIstisnaTanimDto[] = [];

    // ── İade edilen belge referansı ──
    iadeEdilenBelgeSuggestions: TicariBelgeDto[] = [];
    iadeEdilenBelgeGosterim: TicariBelgeDto | null = null;

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible'] && this.visible && this.formData) {
            this.loadCariKartlar();
            this.loadKdvIstisnaTanimlari();
            this.resolveIadeEdilenBelgeGosterim();
        }
    }

    belgeTarihiValue(): Date | null {
        return this.formData?.belgeTarihi ? new Date(this.formData.belgeTarihi) : null;
    }

    onBelgeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.belgeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    vadeTarihiValue(): Date | null {
        return this.formData?.vadeTarihi ? new Date(this.formData.vadeTarihi) : null;
    }

    onVadeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.vadeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    // ── Cari kart ──

    private loadCariKartlar(): void {
        const tesisId = this.formData?.tesisId ?? null;
        this.cariKartService.getAll(tesisId).subscribe({
            next: list => {
                this.cariKartlar = list.filter(c => c.aktifMi);
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

    onCariKartSecildi(cari: CariKartModel | null): void {
        this.selectedCari = cari;
        if (this.formData) {
            this.formData.cariKartId = cari?.id ?? null;
        }
    }

    formatCariDisplay(cari: CariKartModel): string {
        const kod = cari.cariKodu || '-';
        const unvan = cari.unvanAdSoyad || '-';
        const vergi = cari.vergiNoTckn ? ` (${cari.vergiNoTckn})` : '';
        return `${kod} - ${unvan}${vergi}`;
    }

    // ── KDV istisna tanımı ──

    private loadKdvIstisnaTanimlari(): void {
        const filter = createDefaultKdvIstisnaTanimFilter();
        filter.aktifMi = true;
        this.kdvIstisnaTanimService.filter(filter).subscribe({
            next: list => (this.kdvIstisnaTanimlari = list),
            error: () => (this.kdvIstisnaTanimlari = [])
        });
    }

    private isAlisBelgeTipi(): boolean {
        const tip = this.formData?.belgeTipi;
        return tip === SatisBelgesiTipi.AlisFaturasi || tip === SatisBelgesiTipi.AlisIadeFaturasi;
    }

    getKdvIstisnaSecenekleri(satir: TicariBelgeGuncelleSatirRequest): Array<{ label: string; value: number }> {
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.Kdvli || satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
            return [];
        }
        const alis = this.isAlisBelgeTipi();
        return this.kdvIstisnaTanimlari
            .filter(
                t =>
                    t.aktifMi &&
                    (alis ? t.alisIslemlerindeKullanilirMi : t.satisIslemlerindeKullanilirMi) &&
                    // İki modülün KdvUygulamaTipi enum'ları ayrı tiplerdir ama sayısal olarak
                    // BİREBİR aynıdır (backend ortak enum) - number'a cast edilerek karşılaştırılır.
                    (t.uygulamaTipi as number) === (satir.kdvUygulamaTipi as number)
            )
            .map(t => ({ label: `${t.kod} - ${t.ad}`, value: t.id }));
    }

    /** KdvUygulamaTipi her değiştiğinde eski istisna referansı temizlenir (kullanıcı yeniden
     * seçmelidir); Tevkifatli dışına çıkıldığında tevkifat pay/payda da temizlenir. */
    onSatirKdvTipiChange(satir: TicariBelgeGuncelleSatirRequest, value: KdvUygulamaTipi): void {
        satir.kdvUygulamaTipi = value;

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

    // ── İade edilen belge referansı ──

    private resolveIadeEdilenBelgeGosterim(): void {
        const id = this.formData?.iadeEdilenBelgeId;
        if (!id) {
            this.iadeEdilenBelgeGosterim = null;
            return;
        }
        this.ticariBelgeService.getById(id).subscribe({
            next: belge => (this.iadeEdilenBelgeGosterim = belge),
            error: () => (this.iadeEdilenBelgeGosterim = null)
        });
    }

    searchIadeEdilenBelge(event: { query: string }): void {
        const filter = createDefaultTicariBelgeFilter();
        filter.tesisId = this.formData?.tesisId ?? null;
        filter.belgeNo = event.query || null;
        this.ticariBelgeService.filter(filter).subscribe({
            next: list => (this.iadeEdilenBelgeSuggestions = list),
            error: () => (this.iadeEdilenBelgeSuggestions = [])
        });
    }

    onIadeEdilenBelgeSecildi(belge: TicariBelgeDto | null): void {
        this.iadeEdilenBelgeGosterim = belge;
        if (this.formData) {
            this.formData.iadeEdilenBelgeId = belge?.id ?? null;
            this.formData.iadeEdilenBelgeReferansiKaldir = false;
        }
    }

    clearIadeEdilenBelgeReferansi(): void {
        this.iadeEdilenBelgeGosterim = null;
        if (this.formData) {
            this.formData.iadeEdilenBelgeId = null;
            this.formData.iadeEdilenBelgeReferansiKaldir = true;
        }
    }

    // ── Satırlar ──

    addSatir(): void {
        if (!this.formData) return;
        const satirlar = this.formData.satirlar ?? [];
        const yeniSatir = createEmptyTicariBelgeGuncelleSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        this.formData.satirlar = [...satirlar, yeniSatir];
    }

    removeSatir(index: number): void {
        if (!this.formData?.satirlar) return;
        this.formData.satirlar = this.formData.satirlar
            .filter((_, i) => i !== index)
            .map((satir, i) => ({ ...satir, siraNo: i + 1 }));
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
