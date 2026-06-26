import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import {
    ChangeDetectionStrategy,
    ChangeDetectorRef,
    Component,
    EventEmitter,
    Input,
    OnChanges,
    Output,
    SimpleChanges,
    inject
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { tryReadApiMessage } from '../../../../core/api';
import { UiSeverity } from '../../../../core/ui/ui-severity.constants';
import { EkHizmetPaketCakismaPolitikalari } from '../../../tesis-yonetimi/ek-hizmet-paket-cakisma-politikasi.constants';
import {
    RezervasyonEkHizmetDto,
    RezervasyonEkHizmetMisafirSecenekDto,
    RezervasyonEkHizmetSecenekleriDto,
    RezervasyonEkHizmetTarifeSecenekDto,
    RezervasyonOdemeDto,
    RezervasyonOdemeOzetDto
} from '../../rezervasyon-yonetimi.dto';
import { RezervasyonYonetimiService } from '../../rezervasyon-yonetimi.service';

@Component({
    selector: 'app-rezervasyon-odeme-dialog',
    standalone: true,
    changeDetection: ChangeDetectionStrategy.OnPush,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        TooltipModule
    ],
    providers: [ConfirmationService],
    templateUrl: './rezervasyon-odeme-dialog.html',
    styleUrl: './rezervasyon-odeme-dialog.scss'
})
export class RezervasyonOdemeDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() rezervasyonId: number | null = null;
    @Input() referansNo = '';
    @Input() rezervasyonDurumu: string | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() saved = new EventEmitter<RezervasyonOdemeOzetDto>();

    private readonly service = inject(RezervasyonYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    ekHizmetSaving = false;
    odemeOzeti: RezervasyonOdemeOzetDto | null = null;
    odemeEkHizmetPanelExpanded = false;
    odemeRestoranUcretPanelExpanded = false;
    odemeEkHizmetSecenekleri: RezervasyonEkHizmetSecenekleriDto | null = null;
    selectedEkHizmetKonaklayanId: number | null = null;
    selectedEkHizmetTarifeId: number | null = null;
    ekHizmetTarihi = '';
    ekHizmetMiktar: number | null = 1;
    ekHizmetBirimFiyat: number | null = null;
    ekHizmetAciklama = '';
    editingEkHizmetId: number | null = null;
    odemeTutari: number | null = null;
    odemeTipi = 'Nakit';
    odemeAciklama = '';

    private odemeLoadSeq = 0;
    private ekHizmetSecenekLoadSeq = 0;

    private readonly durumCheckInTamamlandi = 'CheckInTamamlandi';
    private readonly durumCheckOutTamamlandi = 'CheckOutTamamlandi';
    private readonly durumIptal = 'Iptal';

    readonly odemeTipleri = [
        { label: 'Nakit', value: 'Nakit' },
        { label: 'Kredi Karti', value: 'KrediKarti' }
    ];

    get ekHizmetMisafirSecenekleri(): RezervasyonEkHizmetMisafirSecenekDto[] {
        return this.odemeEkHizmetSecenekleri?.misafirler ?? [];
    }

    get ekHizmetTarifeSecenekleri(): RezervasyonEkHizmetTarifeSecenekDto[] {
        return this.odemeEkHizmetSecenekleri?.tarifeler ?? [];
    }

    get canModifyEkHizmet(): boolean {
        return !!this.odemeOzeti && this.rezervasyonDurumu === this.durumCheckInTamamlandi;
    }

    get ekHizmetBilgiMesaji(): string | null {
        const reason = this.getEkHizmetModificationDisabledReason();
        if (reason === 'Check-in bekleniyor') {
            return 'Ek hizmet kalemleri yalnizca check-in tamamlandiktan sonra eklenebilir veya guncellenebilir.';
        }

        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemleri yalnizca goruntulenebilir.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemleri yalnizca goruntulenebilir.';
        }

        if ((this.odemeOzeti?.odenenTutar ?? 0) > 0) {
            return 'Odeme alinmis rezervasyonda ek hizmet eklenebilir. Ancak silme veya tutar dusuren guncelleme fazla tahsilat olusturacaksa engellenir.';
        }

        return null;
    }

    get odemeEkHizmetToggleLabel(): string {
        return this.odemeEkHizmetPanelExpanded ? 'Ek Hizmetleri Gizle' : 'Ek Hizmetleri Goster';
    }

    get odemeRestoranUcretToggleLabel(): string {
        return this.odemeRestoranUcretPanelExpanded ? 'Restoran Ucretlendirmelerini Gizle' : 'Restoran Ucretlendirmelerini Goster';
    }

    get restoranUcretlendirmeKayitlari(): RezervasyonOdemeDto[] {
        if (!this.odemeOzeti) {
            return [];
        }

        return this.odemeOzeti.odemeler.filter((x) => x.odemeTipi === 'OdayaEkle' || x.odemeTutari < 0);
    }

    get tahsilatKayitlari(): RezervasyonOdemeDto[] {
        if (!this.odemeOzeti) {
            return [];
        }

        return this.odemeOzeti.odemeler.filter((x) => !(x.odemeTipi === 'OdayaEkle' || x.odemeTutari < 0));
    }

    get restoranUcretlendirmeToplami(): number {
        return this.restoranUcretlendirmeKayitlari.reduce((sum, x) => sum + Math.abs(Number(x.odemeTutari ?? 0)), 0);
    }

    get odemeDialogToplamBorc(): number {
        if (!this.odemeOzeti) {
            return 0;
        }

        return (this.odemeOzeti.konaklamaUcreti ?? 0) + (this.odemeOzeti.ekHizmetToplami ?? 0) + this.restoranUcretlendirmeToplami;
    }

    get odemeDialogOdenenToplam(): number {
        return this.tahsilatKayitlari.reduce((sum, x) => sum + Number(x.odemeTutari ?? 0), 0);
    }

    get odemeDialogKalanTutar(): number {
        return Math.max(0, this.odemeDialogToplamBorc - this.odemeDialogOdenenToplam);
    }

    get odemeKaydetDisabled(): boolean {
        return !this.odemeOzeti || this.loading || this.odemeDialogKalanTutar <= 0;
    }

    ngOnChanges(changes: SimpleChanges): void {
        const shouldLoad =
            this.visible &&
            !!this.rezervasyonId &&
            (changes['visible'] || changes['rezervasyonId']);

        if (shouldLoad) {
            this.resetFormState();
            this.loadOdemeOzeti(this.rezervasyonId!);
            this.loadEkHizmetSecenekleri(this.rezervasyonId!);
            return;
        }

        if (changes['visible'] && !this.visible) {
            this.reset();
        }
    }

    kapat(): void {
        this.visibleChange.emit(false);
        this.reset();
    }

    kaydetEkHizmet(): void {
        if (!this.rezervasyonId || !this.odemeOzeti || this.ekHizmetSaving) {
            return;
        }

        if (this.rezervasyonDurumu !== this.durumCheckInTamamlandi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Check-in Bekleniyor', detail: 'Ek hizmet eklemek icin once check-in tamamlanmalidir.' });
            return;
        }

        if (!this.selectedEkHizmetKonaklayanId || this.selectedEkHizmetKonaklayanId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir konaklayan seciniz.' });
            return;
        }

        if (!this.selectedEkHizmetTarifeId || this.selectedEkHizmetTarifeId <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Lutfen bir ek hizmet seciniz.' });
            return;
        }

        const miktar = Number(this.ekHizmetMiktar ?? 0);
        if (!Number.isFinite(miktar) || miktar <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Miktar', detail: 'Miktar sifirdan buyuk olmalidir.' });
            return;
        }

        const birimFiyat = Number(this.ekHizmetBirimFiyat ?? 0);
        if (!Number.isFinite(birimFiyat) || birimFiyat < 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Fiyat', detail: 'Birim fiyat sifirdan kucuk olamaz.' });
            return;
        }

        if (!this.ekHizmetTarihi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Hizmet tarihi zorunludur.' });
            return;
        }

        const request = {
            rezervasyonKonaklayanId: this.selectedEkHizmetKonaklayanId,
            ekHizmetTarifeId: this.selectedEkHizmetTarifeId,
            hizmetTarihi: this.normalizeDateTimeLocalInput(this.ekHizmetTarihi),
            miktar,
            birimFiyat,
            aciklama: this.normalizeOptional(this.ekHizmetAciklama)
        };

        const paketUyarisi = this.getSelectedEkHizmetTarifePaketUyarisi();
        const paketCakismaPolitikasi = this.odemeEkHizmetSecenekleri?.paketCakismaPolitikasi ?? EkHizmetPaketCakismaPolitikalari.OnayIste;

        if (paketUyarisi) {
            if (paketCakismaPolitikasi === EkHizmetPaketCakismaPolitikalari.Engelle) {
                this.messageService.add({ severity: UiSeverity.Error, summary: 'Paket Cakismasi', detail: `${paketUyarisi} Bu tesis politikasina gore ek satis engellendi.` });
                return;
            }

            if (paketCakismaPolitikasi === EkHizmetPaketCakismaPolitikalari.OnayIste) {
                this.confirmationService.confirm({
                    header: 'Paket Icerigi Uyarisi',
                    message: `${paketUyarisi} Yine de ek hizmet satisina devam etmek istiyor musunuz?`,
                    icon: 'pi pi-exclamation-triangle',
                    acceptLabel: 'Devam Et',
                    rejectLabel: 'Vazgec',
                    accept: () => this.executeKaydetEkHizmet(request)
                });
                return;
            }

            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Paket Cakismasi', detail: paketUyarisi });
        }

        this.executeKaydetEkHizmet(request);
    }

    private executeKaydetEkHizmet(request: {
        rezervasyonKonaklayanId: number;
        ekHizmetTarifeId: number;
        hizmetTarihi: string;
        miktar: number;
        birimFiyat: number;
        aciklama: string | null;
    }): void {
        if (!this.rezervasyonId) {
            return;
        }

        const rezervasyonId = this.rezervasyonId;
        this.ekHizmetSaving = true;

        const request$ = this.editingEkHizmetId
            ? this.service.guncelleEkHizmet(rezervasyonId, this.editingEkHizmetId, request)
            : this.service.kaydetEkHizmet(rezervasyonId, request);

        request$
            .pipe(
                finalize(() => {
                    this.ekHizmetSaving = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.odemeTutari = this.getOdemeGorunumKalanTutar(result) > 0 ? this.getOdemeGorunumKalanTutar(result) : null;
                    const message = this.editingEkHizmetId
                        ? 'Ek hizmet kaydi guncellendi.'
                        : 'Ek hizmet hesaba eklendi.';
                    this.resetEkHizmetForm();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: message });
                    this.saved.emit(result);
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    duzenleEkHizmet(hizmet: RezervasyonEkHizmetDto): void {
        if (this.getEkHizmetEditDisabledReason(hizmet) !== null) {
            return;
        }

        this.odemeEkHizmetPanelExpanded = true;
        this.editingEkHizmetId = hizmet.id;
        this.selectedEkHizmetKonaklayanId = hizmet.rezervasyonKonaklayanId;
        this.selectedEkHizmetTarifeId = hizmet.ekHizmetTarifeId;
        this.ekHizmetTarihi = this.toDateTimeLocalInputValue(hizmet.hizmetTarihi);
        this.ekHizmetMiktar = hizmet.miktar;
        this.ekHizmetBirimFiyat = hizmet.birimFiyat;
        this.ekHizmetAciklama = hizmet.aciklama ?? '';
    }

    onEkHizmetTarifeChange(): void {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        this.ekHizmetBirimFiyat = selectedTarife?.birimFiyat ?? null;
    }

    toggleOdemeEkHizmetPanel(): void {
        this.odemeEkHizmetPanelExpanded = !this.odemeEkHizmetPanelExpanded;
    }

    toggleOdemeRestoranUcretPanel(): void {
        this.odemeRestoranUcretPanelExpanded = !this.odemeRestoranUcretPanelExpanded;
    }

    ekHizmetDuzenlemeyiIptalEt(): void {
        this.resetEkHizmetForm();
    }

    silEkHizmet(hizmet: RezervasyonEkHizmetDto): void {
        if (!this.rezervasyonId || this.getEkHizmetDeleteDisabledReason(hizmet) !== null || this.ekHizmetSaving) {
            return;
        }

        if (!window.confirm(`'${hizmet.tarifeAdi}' ek hizmet kaydi silinsin mi?`)) {
            return;
        }

        this.ekHizmetSaving = true;
        this.service
            .silEkHizmet(this.rezervasyonId, hizmet.id)
            .pipe(
                finalize(() => {
                    this.ekHizmetSaving = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.odemeTutari = this.getOdemeGorunumKalanTutar(result) > 0 ? this.getOdemeGorunumKalanTutar(result) : null;
                    if (this.editingEkHizmetId === hizmet.id) {
                        this.resetEkHizmetForm();
                    }

                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Ek hizmet kaydi silindi.' });
                    this.saved.emit(result);
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    kaydetOdeme(): void {
        if (!this.rezervasyonId || !this.odemeOzeti || this.saving) {
            return;
        }

        if (this.rezervasyonDurumu !== this.durumCheckInTamamlandi) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Check-in Bekleniyor', detail: 'Odeme almak icin once check-in tamamlanmalidir.' });
            return;
        }

        const tutar = Number(this.odemeTutari ?? 0);
        if (!Number.isFinite(tutar) || tutar <= 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Gecersiz Tutar', detail: 'Odeme tutari sifirdan buyuk olmalidir.' });
            return;
        }

        this.saving = true;
        this.service
            .kaydetOdeme(this.rezervasyonId, {
                odemeTutari: tutar,
                odemeTipi: this.odemeTipi,
                aciklama: this.normalizeOptional(this.odemeAciklama)
            })
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.markForCheck();
                })
            )
            .subscribe({
                next: (result) => {
                    this.odemeOzeti = result;
                    this.odemeTutari = this.getOdemeGorunumKalanTutar(result) > 0 ? this.getOdemeGorunumKalanTutar(result) : null;
                    this.odemeAciklama = '';
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Odeme kaydedildi.' });
                    this.saved.emit(result);
                    this.cdr.markForCheck();
                },
                error: (error: unknown) => {
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            });
    }

    getEkHizmetEditDisabledReason(_hizmet: RezervasyonEkHizmetDto): string | null {
        return this.getEkHizmetModificationDisabledReason();
    }

    getEkHizmetEditDisabledMessage(hizmet: RezervasyonEkHizmetDto): string {
        const reason = this.getEkHizmetEditDisabledReason(hizmet);
        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Check-in bekleniyor') {
            return 'Check-in tamamlanmadan ek hizmet kalemi guncellenemez.';
        }

        if (reason === 'Odeme ozeti yok') {
            return 'Ek hizmet islemi icin odeme ozeti yuklenemedi.';
        }

        return 'Ek hizmet kalemi guncellenemez.';
    }

    getEkHizmetDeleteDisabledReason(hizmet: RezervasyonEkHizmetDto): string | null {
        const reservationReason = this.getEkHizmetModificationDisabledReason();
        if (reservationReason) {
            return reservationReason;
        }

        if (!this.odemeOzeti) {
            return 'Odeme ozeti yok';
        }

        const kalanTutar = this.odemeOzeti.kalanTutar ?? 0;
        if (kalanTutar <= 0) {
            return 'Odeme bakiyesi sifirlandi';
        }

        if (hizmet.toplamTutar > kalanTutar) {
            return 'Fazla odeme olusur';
        }

        return null;
    }

    getEkHizmetDeleteDisabledMessage(hizmet: RezervasyonEkHizmetDto): string {
        const reason = this.getEkHizmetDeleteDisabledReason(hizmet);
        if (reason === 'Check-out tamamlandi') {
            return 'Check-out tamamlanan rezervasyonda ek hizmet kalemi silinemez.';
        }

        if (reason === 'Iptal edildi') {
            return 'Iptal edilen rezervasyonda ek hizmet kalemi silinemez.';
        }

        if (reason === 'Check-in bekleniyor') {
            return 'Check-in tamamlanmadan ek hizmet kalemi silinemez.';
        }

        if (reason === 'Odeme bakiyesi sifirlandi') {
            return 'Odeme bakiyesi sifirlandigi icin bu ek hizmet kalemi silinemez.';
        }

        if (reason === 'Fazla odeme olusur') {
            return `Bu kalem silinirse tahsil edilen tutar yeni toplamdan buyuk kalir. Kalan tutar: ${this.formatCurrency(this.odemeOzeti?.kalanTutar ?? 0, this.odemeOzeti?.paraBirimi ?? 'TRY')}.`;
        }

        if (reason === 'Odeme ozeti yok') {
            return 'Ek hizmet islemi icin odeme ozeti yuklenemedi.';
        }

        return 'Ek hizmet kalemi silinemez.';
    }

    getSelectedEkHizmetTarifeVarsayilanFiyat(): string | null {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        if (!selectedTarife) {
            return null;
        }

        return this.formatCurrency(selectedTarife.birimFiyat, selectedTarife.paraBirimi);
    }

    getSelectedEkHizmetTarifePaketUyarisi(): string | null {
        return this.getSelectedEkHizmetTarife()?.paketIcerigiUyariMesaji ?? null;
    }

    getEkHizmetToplamOnizleme(): string | null {
        const selectedTarife = this.getSelectedEkHizmetTarife();
        const miktar = Number(this.ekHizmetMiktar ?? 0);
        const birimFiyat = Number(this.ekHizmetBirimFiyat ?? 0);
        if (!selectedTarife || !Number.isFinite(miktar) || miktar <= 0 || !Number.isFinite(birimFiyat) || birimFiyat < 0) {
            return null;
        }

        return this.formatCurrency(Math.round(birimFiyat * miktar * 100) / 100, selectedTarife.paraBirimi);
    }

    getOdemeTipiEtiket(odemeTipi: string): string {
        if (odemeTipi === 'OdayaEkle') {
            return 'Restoran Ucretlendirmesi';
        }

        return odemeTipi;
    }

    formatCurrency(value: number, currency: string): string {
        const safeValue = Number.isFinite(value) ? value : 0;
        const safeCurrency = (currency || 'TRY').toUpperCase();
        return `${safeValue.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${safeCurrency}`;
    }

    formatDateTime(value: string | Date | null | undefined): string {
        const date = this.parseApiDateTime(value);
        if (!date) {
            return '-';
        }

        return new Intl.DateTimeFormat('tr-TR', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).format(date);
    }

    toPositiveAmount(value: number): number {
        return Math.abs(Number(value ?? 0));
    }

    private loadOdemeOzeti(rezervasyonId: number): void {
        const seq = ++this.odemeLoadSeq;
        this.loading = true;
        this.service
            .getOdemeOzeti(rezervasyonId)
            .pipe(
                finalize(() => {
                    if (seq === this.odemeLoadSeq) {
                        this.loading = false;
                        this.cdr.markForCheck();
                    }
                })
            )
            .subscribe({
                next: (ozet) => {
                    if (seq === this.odemeLoadSeq) {
                        this.odemeOzeti = ozet;
                        this.odemeTutari = this.getOdemeGorunumKalanTutar(ozet) > 0 ? this.getOdemeGorunumKalanTutar(ozet) : null;
                        if (ozet.odemeler.length > 0 && this.editingEkHizmetId) {
                            this.resetEkHizmetForm();
                        }

                        this.cdr.markForCheck();
                    }
                },
                error: (error: unknown) => {
                    if (seq === this.odemeLoadSeq) {
                        this.odemeOzeti = null;
                        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                        this.cdr.markForCheck();
                    }
                }
            });
    }

    private loadEkHizmetSecenekleri(rezervasyonId: number): void {
        const seq = ++this.ekHizmetSecenekLoadSeq;
        this.service.getEkHizmetSecenekleri(rezervasyonId).subscribe({
            next: (result) => {
                if (seq === this.ekHizmetSecenekLoadSeq) {
                    this.odemeEkHizmetSecenekleri = result;
                    if (!this.selectedEkHizmetKonaklayanId || !result.misafirler.some((x) => x.rezervasyonKonaklayanId === this.selectedEkHizmetKonaklayanId)) {
                        this.selectedEkHizmetKonaklayanId = result.misafirler[0]?.rezervasyonKonaklayanId ?? null;
                    }

                    if (!this.selectedEkHizmetTarifeId || !result.tarifeler.some((x) => x.id === this.selectedEkHizmetTarifeId)) {
                        this.selectedEkHizmetTarifeId = result.tarifeler[0]?.id ?? null;
                    }

                    this.cdr.markForCheck();
                }
            },
            error: (error: unknown) => {
                if (seq === this.ekHizmetSecenekLoadSeq) {
                    this.odemeEkHizmetSecenekleri = null;
                    this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: this.resolveErrorMessage(error) });
                    this.cdr.markForCheck();
                }
            }
        });
    }

    private getOdemeGorunumKalanTutar(ozet: RezervasyonOdemeOzetDto): number {
        const restoranUcretlendirmeToplami = (ozet.odemeler ?? [])
            .filter((x) => x.odemeTipi === 'OdayaEkle' || x.odemeTutari < 0)
            .reduce((sum, x) => sum + Math.abs(Number(x.odemeTutari ?? 0)), 0);

        const tahsilatToplami = (ozet.odemeler ?? [])
            .filter((x) => !(x.odemeTipi === 'OdayaEkle' || x.odemeTutari < 0))
            .reduce((sum, x) => sum + Number(x.odemeTutari ?? 0), 0);

        const toplamBorc = (ozet.konaklamaUcreti ?? 0) + (ozet.ekHizmetToplami ?? 0) + restoranUcretlendirmeToplami;
        return Math.max(0, toplamBorc - tahsilatToplami);
    }

    private getSelectedEkHizmetTarife(): RezervasyonEkHizmetTarifeSecenekDto | null {
        if (!this.selectedEkHizmetTarifeId) {
            return null;
        }

        return this.ekHizmetTarifeSecenekleri.find((x) => x.id === this.selectedEkHizmetTarifeId) ?? null;
    }

    private getEkHizmetModificationDisabledReason(): string | null {
        if (!this.odemeOzeti) {
            return 'Odeme ozeti yok';
        }

        if (this.rezervasyonDurumu !== this.durumCheckInTamamlandi) {
            if (this.rezervasyonDurumu === this.durumCheckOutTamamlandi) {
                return 'Check-out tamamlandi';
            }

            if (this.rezervasyonDurumu === this.durumIptal) {
                return 'Iptal edildi';
            }

            return 'Check-in bekleniyor';
        }

        return null;
    }

    private resetFormState(): void {
        this.odemeOzeti = null;
        this.odemeEkHizmetPanelExpanded = false;
        this.odemeRestoranUcretPanelExpanded = false;
        this.odemeEkHizmetSecenekleri = null;
        this.selectedEkHizmetKonaklayanId = null;
        this.selectedEkHizmetTarifeId = null;
        this.ekHizmetTarihi = this.nowInput();
        this.ekHizmetMiktar = 1;
        this.ekHizmetBirimFiyat = null;
        this.ekHizmetAciklama = '';
        this.editingEkHizmetId = null;
        this.odemeTutari = null;
        this.odemeTipi = 'Nakit';
        this.odemeAciklama = '';
    }

    private reset(): void {
        ++this.odemeLoadSeq;
        ++this.ekHizmetSecenekLoadSeq;
        this.resetFormState();
        this.loading = false;
        this.saving = false;
        this.ekHizmetSaving = false;
    }

    private resetEkHizmetForm(): void {
        this.editingEkHizmetId = null;
        this.ekHizmetMiktar = 1;
        this.ekHizmetBirimFiyat = this.getSelectedEkHizmetTarife()?.birimFiyat ?? null;
        this.ekHizmetAciklama = '';
        this.ekHizmetTarihi = this.nowInput();
    }

    private nowInput(): string {
        return this.toDateTimeLocalInput(new Date());
    }

    private toDateTimeLocalInput(date: Date): string {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        const hour = String(date.getHours()).padStart(2, '0');
        const minute = String(date.getMinutes()).padStart(2, '0');
        return `${year}-${month}-${day}T${hour}:${minute}`;
    }

    private toDateTimeLocalInputValue(value: string): string {
        const date = this.parseApiDateTime(value);
        if (!date) {
            return value;
        }

        return this.toDateTimeLocalInput(date);
    }

    private normalizeDateTimeLocalInput(value: string | null | undefined): string {
        if (!value) {
            return '';
        }

        const normalized = value.trim();
        if (normalized.length === 0) {
            return '';
        }

        if (normalized.length === 16) {
            return `${normalized}:00`;
        }

        return normalized;
    }

    private normalizeOptional(value: string): string | null {
        const normalized = value.trim();
        return normalized.length > 0 ? normalized : null;
    }

    private parseApiDateTime(value: string | Date | null | undefined): Date | null {
        if (!value) {
            return null;
        }

        if (value instanceof Date) {
            return Number.isNaN(value.getTime()) ? null : new Date(value.getTime());
        }

        const normalized = value.trim();
        if (normalized.length === 0) {
            return null;
        }

        if (/^\d{4}-\d{2}-\d{2}$/.test(normalized)) {
            const [yearText, monthText, dayText] = normalized.split('-');
            const year = Number.parseInt(yearText, 10);
            const month = Number.parseInt(monthText, 10);
            const day = Number.parseInt(dayText, 10);
            const localDate = new Date(year, month - 1, day);
            return Number.isNaN(localDate.getTime()) ? null : localDate;
        }

        const parsed = new Date(normalized);
        return Number.isNaN(parsed.getTime()) ? null : parsed;
    }

    private resolveErrorMessage(error: unknown): string {
        if (error instanceof HttpErrorResponse) {
            const apiMessage = tryReadApiMessage(error.error);
            if (apiMessage) {
                return apiMessage;
            }
        }

        if (error instanceof Error && error.message.trim().length > 0) {
            return error.message;
        }

        return 'Beklenmeyen bir hata olustu.';
    }
}
