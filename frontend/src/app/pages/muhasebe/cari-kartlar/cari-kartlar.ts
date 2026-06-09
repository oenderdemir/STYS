import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { LazyLoadPayload, tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { CARI_TIPLERI, CariKartAcilisBakiyesiDuzeltRequest, CariKartBankaHesabiModel, CariKartModel, CariKartYetkiliKisiModel, CreateCariKartRequest, UpdateCariKartRequest } from './cari-kartlar.dto';
import { CariKartlarService } from './cari-kartlar.service';

@Component({
    selector: 'app-cari-kartlar-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, DatePickerModule, SelectModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule, MuhasebeTesisSecimDialogComponent, MuhasebeTesisContextBarComponent],
    templateUrl: './cari-kartlar.html',
    providers: [MessageService, ConfirmationService]
})
export class CariKartlarPage implements OnInit {
    private readonly service = inject(CariKartlarService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    private contextInitialized = false;
    private currentTesisId: number | null = null;

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';
    duzeltmeDialogVisible = false;
    duzeltmeSaving = false;
    duzeltmeModel: CariKartAcilisBakiyesiDuzeltRequest = {
        yeniTutar: 0,
        yeniYonu: null,
        duzeltmeTarihi: null
    };
    duzeltmeCariKart: CariKartModel | null = null;

    records: CariKartModel[] = [];
    filteredRecords: CariKartModel[] = [];
    model: CariKartModel = this.createEmpty();
    pageNumber = 1;
    pageSize = 10;
    totalRecords = 0;

    readonly cariTipleri = [
        { label: 'Müşteri', value: CARI_TIPLERI.Musteri },
        { label: 'Tedarikçi', value: CARI_TIPLERI.Tedarikci },
        { label: 'Kurumsal Müşteri', value: CARI_TIPLERI.KurumsalMusteri }
    ];
    readonly acilisBakiyeYonleri = [
        { label: 'Borç', value: 'Borc' },
        { label: 'Alacak', value: 'Alacak' }
    ];

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.pageNumber = 1;
            this.closeOpenDialogForTesisChange();
            this.load(1, this.pageSize);
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için cari kart listesi yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.pageNumber = 1;
                this.load(1, this.pageSize);
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    onLazyLoad(event: LazyLoadPayload): void {
        const nextPageSize = event.rows && event.rows > 0 ? event.rows : this.pageSize;
        const nextFirst = event.first && event.first >= 0 ? event.first : 0;
        const nextPageNumber = Math.floor(nextFirst / nextPageSize) + 1;
        this.load(nextPageNumber, nextPageSize);
    }

    load(pageNumber = this.pageNumber, pageSize = this.pageSize): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service.getPaged(pageNumber, pageSize, tesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (paged) => {
                this.records = paged.items;
                this.filteredRecords = [...paged.items];
                this.pageNumber = paged.pageNumber;
                this.pageSize = paged.pageSize;
                this.totalRecords = paged.totalCount;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    openCreate(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = tesisId;
        this.dialogVisible = true;
    }

    openEdit(item: CariKartModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.loading = true;
        this.service.getById(item.id).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (detail) => {
                this.model = this.mapToModel(detail);
                this.dialogVisible = true;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    save(): void {
        if (!this.model.unvanAdSoyad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Unvan zorunludur.' });
            return;
        }

        if (!this.validateAcilisBakiye()) {
            return;
        }

        const tesisId = this.dialogMode === 'create'
            ? this.getSeciliTesisIdOrWarn()
            : (this.model.tesisId ?? this.getSeciliTesisIdOrWarn());
        if (tesisId === null) {
            return;
        }

        if (!this.isCariKoduReadOnly() && !this.model.cariKodu?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Cari kodu zorunludur.' });
            return;
        }

        let bankaHesaplari: CariKartBankaHesabiModel[];
        try {
            bankaHesaplari = this.normalizeBankaHesaplari(this.model.bankaHesaplari);
        } catch (error: unknown) {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Eksik Bilgi',
                detail: error instanceof Error ? error.message : 'Banka hesabı bilgileri geçersiz.'
            });
            return;
        }

        const payload: CreateCariKartRequest | UpdateCariKartRequest = {
            tesisId,
            cariTipi: this.model.cariTipi,
            cariKodu: this.isCariKoduReadOnly() ? null : (this.model.cariKodu?.trim() || null),
            unvanAdSoyad: this.model.unvanAdSoyad.trim(),
            vergiNoTckn: this.model.vergiNoTckn?.trim() || null,
            vergiDairesi: this.model.vergiDairesi?.trim() || null,
            telefon: this.model.telefon?.trim() || null,
            eposta: this.model.eposta?.trim() || null,
            adres: this.model.adres?.trim() || null,
            il: this.model.il?.trim() || null,
            ilce: this.model.ilce?.trim() || null,
            aktifMi: this.model.aktifMi,
            eFaturaMukellefiMi: this.model.eFaturaMukellefiMi,
            eArsivKapsamindaMi: this.model.eArsivKapsamindaMi,
            aciklama: this.model.aciklama?.trim() || null,
            acilisBakiyeTarihi: this.model.acilisBakiyeTarihi || null,
            acilisBakiyeTutari: this.model.acilisBakiyeTutari ?? null,
            acilisBakiyeYonu: this.model.acilisBakiyeYonu || null,
            bankaHesaplari,
            yetkiliKisiler: this.normalizeYetkiliKisiler(this.model.yetkiliKisiler)
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateCariKartRequest)
            : this.service.create(payload as CreateCariKartRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: CariKartModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Kayit silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    openAcilisBakiyesiDuzelt(item: CariKartModel): void {
        if (!item.id) {
            return;
        }

        this.duzeltmeCariKart = item;
        this.duzeltmeModel = {
            yeniTutar: item.acilisBakiyeTutari ?? 0,
            yeniYonu: item.acilisBakiyeYonu ?? null,
            duzeltmeTarihi: item.acilisBakiyeTarihi ?? null
        };
        this.duzeltmeDialogVisible = true;
    }

    saveAcilisBakiyesiDuzelt(): void {
        if (!this.duzeltmeCariKart?.id) {
            return;
        }

        if ((this.duzeltmeModel.yeniTutar ?? 0) < 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Yeni tutar negatif olamaz.' });
            return;
        }

        if ((this.duzeltmeModel.yeniTutar ?? 0) > 0 && !this.duzeltmeModel.yeniYonu) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Yeni yön zorunludur.' });
            return;
        }

        this.duzeltmeSaving = true;
        this.service.acilisBakiyesiDuzelt(this.duzeltmeCariKart.id, this.duzeltmeModel).pipe(finalize(() => {
            this.duzeltmeSaving = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: () => {
                this.duzeltmeDialogVisible = false;
                this.duzeltmeCariKart = null;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Açılış bakiyesi düzeltildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): CariKartModel {
        return {
            tesisId: null,
            cariTipi: 'Musteri',
            cariKodu: '',
            muhasebeHesapPlaniId: null,
            anaMuhasebeHesapKodu: null,
            muhasebeHesapSiraNo: null,
            unvanAdSoyad: '',
            vergiNoTckn: null,
            vergiDairesi: null,
            telefon: null,
            eposta: null,
            adres: null,
            il: null,
            ilce: null,
            aktifMi: true,
            eFaturaMukellefiMi: false,
            eArsivKapsamindaMi: false,
            aciklama: null,
            acilisBakiyeTarihi: null,
            acilisBakiyeTutari: null,
            acilisBakiyeYonu: null,
            bankaHesaplari: [],
            yetkiliKisiler: []
        };
    }

    addYetkiliKisi(): void {
        this.model.yetkiliKisiler = [...(this.model.yetkiliKisiler ?? []), this.createEmptyYetkiliKisi()];
    }

    removeYetkiliKisi(index: number): void {
        this.model.yetkiliKisiler = (this.model.yetkiliKisiler ?? []).filter((_, i) => i !== index);
    }

    isCariKoduReadOnly(): boolean {
        return this.isAutoCariTipi(this.model.cariTipi);
    }

    isCariTipiLocked(): boolean {
        return this.dialogMode === 'edit' && !!this.model.muhasebeHesapPlaniId;
    }

    getTesisAdi(tesisId?: number | null): string {
        return this.tesisContext.seciliTesis()?.ad ?? (tesisId ? `#${tesisId}` : '-');
    }

    private isAutoCariTipi(cariTipi: string | null | undefined): boolean {
        return cariTipi === 'Tedarikci' || cariTipi === 'Musteri' || cariTipi === 'KurumsalMusteri';
    }

    private closeOpenDialogForTesisChange(): void {
        if (!this.dialogVisible) {
            return;
        }

        this.dialogVisible = false;
        this.model = this.createEmpty();
    }

    private mapToModel(item: CariKartModel): CariKartModel {
        const bankaHesaplari = this.normalizeBankaHesaplari(item.bankaHesaplari ?? []);
        return {
            ...item,
            yetkiliKisiler: item.yetkiliKisiler ?? [],
            bankaHesaplari,
            acilisBakiyeTarihi: item.acilisBakiyeTarihi ? item.acilisBakiyeTarihi.slice(0, 10) : null,
            acilisBakiyeTutari: item.acilisBakiyeTutari ?? null,
            acilisBakiyeYonu: item.acilisBakiyeYonu ?? null
        };
    }

    addBankaHesabi(): void {
        this.model.bankaHesaplari = [...(this.model.bankaHesaplari ?? []), this.createEmptyBankaHesabi()];
    }

    removeBankaHesabi(index: number): void {
        this.model.bankaHesaplari = (this.model.bankaHesaplari ?? []).filter((_, i) => i !== index);
    }

    canDuzeltAcilisBakiye(item: CariKartModel): boolean {
        return item.acilisBakiyeDuzeltilebilirMi === true;
    }

    private createEmptyYetkiliKisi(): CariKartYetkiliKisiModel {
        return {
            id: null,
            cariKartId: null,
            adSoyad: '',
            gorevUnvan: null,
            telefon: null,
            eposta: null,
            aciklama: null
        };
    }

    private validateAcilisBakiye(): boolean {
        const tutar = this.model.acilisBakiyeTutari ?? null;
        if (tutar !== null && tutar < 0) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Açılış bakiyesi negatif olamaz.' });
            return false;
        }

        if ((tutar ?? 0) > 0) {
            if (!this.model.acilisBakiyeTarihi) {
                this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Açılış bakiyesi tarihi zorunludur.' });
                return false;
            }

            if (!this.model.acilisBakiyeYonu) {
                this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Açılış bakiyesi yönü zorunludur.' });
                return false;
            }
        }

        return true;
    }

    private normalizeYetkiliKisiler(kisiler: CariKartYetkiliKisiModel[]): CariKartYetkiliKisiModel[] {
        return (kisiler ?? [])
            .filter((kisi) => !!kisi && (
                !!kisi.adSoyad?.trim() ||
                !!kisi.gorevUnvan?.trim() ||
                !!kisi.telefon?.trim() ||
                !!kisi.eposta?.trim() ||
                !!kisi.aciklama?.trim()
            ))
            .map((kisi) => ({
                ...kisi,
                id: kisi.id ?? null,
                cariKartId: kisi.cariKartId ?? null,
                adSoyad: kisi.adSoyad?.trim() || '',
                gorevUnvan: kisi.gorevUnvan?.trim() || null,
                telefon: kisi.telefon?.trim() || null,
                eposta: kisi.eposta?.trim() || null,
                aciklama: kisi.aciklama?.trim() || null
            }));
    }

    private normalizeBankaHesaplari(hesaplar: CariKartBankaHesabiModel[]): CariKartBankaHesabiModel[] {
        const result = new Map<string, CariKartBankaHesabiModel>();

        for (const hesap of hesaplar ?? []) {
            if (!hesap) {
                continue;
            }

            const bankaAdi = hesap.bankaAdi?.trim() || null;
            const subeAdi = hesap.subeAdi?.trim() || null;
            const hesapNo = hesap.hesapNo?.trim() || null;
            const iban = this.normalizeIban(hesap.iban);
            const aciklama = hesap.aciklama?.trim() || null;

            const hasAnyValue = !!bankaAdi || !!subeAdi || !!hesapNo || !!iban || !!aciklama;
            if (!hasAnyValue) {
                continue;
            }

            if (!iban && (!bankaAdi || !subeAdi || !hesapNo)) {
                throw new Error('IBAN yoksa banka adı, şube adı ve hesap no zorunludur.');
            }

            const normalized: CariKartBankaHesabiModel = {
                id: hesap.id ?? null,
                cariKartId: hesap.cariKartId ?? null,
                bankaAdi,
                subeAdi,
                hesapNo,
                iban,
                aciklama
            };

            result.set(this.bankaHesabiKey(normalized), normalized);
        }

        return [...result.values()];
    }

    private normalizeIban(iban?: string | null): string | null {
        if (!iban?.trim()) {
            return null;
        }

        return iban.replace(/\s+/g, '').toUpperCase();
    }

    private createEmptyBankaHesabi(): CariKartBankaHesabiModel {
        return {
            id: null,
            cariKartId: null,
            bankaAdi: null,
            subeAdi: null,
            hesapNo: null,
            iban: null,
            aciklama: null
        };
    }

    private bankaHesabiKey(hesap: CariKartBankaHesabiModel): string {
        if (hesap.iban?.trim()) {
            return `IBAN:${hesap.iban.trim().toUpperCase()}`;
        }

        return `KOMBO:${(hesap.bankaAdi ?? '').trim().toUpperCase()}|${(hesap.subeAdi ?? '').trim().toUpperCase()}|${(hesap.hesapNo ?? '').trim().toUpperCase()}`;
    }

    parseDateOnly(value: string | Date | null | undefined): Date | null {
        if (!value) {
            return null;
        }

        if (value instanceof Date) {
            return isNaN(value.getTime()) ? null : value;
        }

        const normalized = value.trim();
        if (!normalized) {
            return null;
        }

        const isoMatch = normalized.match(/^(\d{4})-(\d{2})-(\d{2})$/);
        if (isoMatch) {
            const [, year, month, day] = isoMatch;
            return new Date(Number(year), Number(month) - 1, Number(day));
        }

        const trMatch = normalized.match(/^(\d{2})\.(\d{2})\.(\d{4})$/);
        if (trMatch) {
            const [, day, month, year] = trMatch;
            return new Date(Number(year), Number(month) - 1, Number(day));
        }

        const parsed = new Date(normalized);
        return isNaN(parsed.getTime()) ? null : parsed;
    }

    formatDateOnly(value: Date | string | null | undefined): string | null {
        if (!value) {
            return null;
        }

        if (typeof value === 'string') {
            const trimmed = value.trim();
            if (!trimmed) {
                return null;
            }

            const isoMatch = trimmed.match(/^(\d{4})-(\d{2})-(\d{2})$/);
            if (isoMatch) {
                return trimmed;
            }

            const trMatch = trimmed.match(/^(\d{2})\.(\d{2})\.(\d{4})$/);
            if (trMatch) {
                const [, day, month, year] = trMatch;
                return `${year}-${month}-${day}`;
            }

            const parsed = new Date(trimmed);
            if (isNaN(parsed.getTime())) {
                return trimmed;
            }

            return this.formatDateOnly(parsed);
        }

        if (isNaN(value.getTime())) {
            return null;
        }

        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    private getSeciliTesisIdOrWarn(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe işlemi için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
