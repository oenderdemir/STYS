import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { SatisBelgesiService } from '../services/satis-belgesi.service';
import {
    SatisBelgesiDto,
    SatisBelgesiDurumu,
    SatisBelgesiTipi,
    SatisKaynakModulu,
    SatisBelgesiSatirTipi,
    KdvUygulamaTipi,
    CreateSatisBelgesiRequest,
    UpdateSatisBelgesiRequest,
    SatisBelgesiFilterDto,
    CreateSatisBelgesiSatiriRequest,
    SatisBelgesiRedRequest,
    SATIS_BELGESI_DURUMU_LABELS,
    SATIS_BELGESI_DURUMU_SEVERITIES,
    SATIS_BELGESI_TIPI_LABELS,
    SATIS_KAYNAK_MODULU_LABELS,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    KDV_UYGULAMA_TIPI_LABELS,
    SATIS_BELGESI_DURUM_SECENEKLERI,
    createDefaultSatisBelgesiFilter,
    createEmptySatisBelgesiSatiri,
    createEmptyCreateSatisBelgesiRequest,
    getMusteriDisplayName
} from '../models/satis-belgesi.model';

@Component({
    selector: 'app-satis-belgeleri',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        ButtonModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TagModule,
        TextareaModule,
        ToastModule,
        ToggleSwitchModule,
        ToolbarModule,
        TooltipModule
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './satis-belgeleri.component.html',
    styleUrl: './satis-belgeleri.component.scss'
})
export class SatisBelgeleriComponent implements OnInit {
    private readonly service = inject(SatisBelgesiService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly messageService = inject(MessageService);

    // ── State ──
    belgeler = signal<SatisBelgesiDto[]>([]);
    loading = signal(false);
    filter = signal<SatisBelgesiFilterDto>(createDefaultSatisBelgesiFilter());

    // Dialog
    dialogVisible = signal(false);
    dialogLoading = signal(false);
    isEditing = signal(false);
    editingBelge = signal<SatisBelgesiDto | null>(null);
    formData = signal<CreateSatisBelgesiRequest>(createEmptyCreateSatisBelgesiRequest());

    // Reddetme
    redDialogVisible = signal(false);
    redNedeni = signal('');
    redBelgeId = signal<number | null>(null);
    redLoading = signal(false);

    // Detay dialog
    detayDialogVisible = signal(false);
    detayBelge = signal<SatisBelgesiDto | null>(null);

    // ── Label/option maps (plain, not signals) ──
    durumLabels = SATIS_BELGESI_DURUMU_LABELS;
    durumSeverities = SATIS_BELGESI_DURUMU_SEVERITIES;
    durumSecenekleri = SATIS_BELGESI_DURUM_SECENEKLERI;
    belgeTipiLabels = SATIS_BELGESI_TIPI_LABELS;
    kaynakModulLabels = SATIS_KAYNAK_MODULU_LABELS;
    satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;

    belgeTipiSecenekleri = Object.entries(this.belgeTipiLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    kaynakModulSecenekleri = Object.entries(this.kaynakModulLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    satirTipiSecenekleri = Object.entries(this.satirTipiLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    kdvUygulamaTipiSecenekleri = Object.entries(this.kdvUygulamaTipiLabels)
        .filter(([k]) => Number(k) !== KdvUygulamaTipi.Tevkifatli)
        .map(([k, v]) => ({ value: Number(k), label: v }));

    getMusteriDisplay = getMusteriDisplayName;

    ngOnInit(): void {
        this.loadBelgeler();
    }

    // ── Load ──

    loadBelgeler(): void {
        this.loading.set(true);
        this.service.filter(this.filter()).subscribe({
            next: (data) => {
                this.belgeler.set(data);
                this.loading.set(false);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                this.loading.set(false);
            }
        });
    }

    onFilterFieldChange<K extends keyof SatisBelgesiFilterDto>(key: K, value: SatisBelgesiFilterDto[K]): void {
        this.filter.update(f => ({ ...f, [key]: value }));
        this.loadBelgeler();
    }

    updateFormField<K extends keyof CreateSatisBelgesiRequest>(key: K, value: CreateSatisBelgesiRequest[K]): void {
        this.formData.update(f => ({ ...f, [key]: value }));
    }

    onFilterChange(): void {
        this.loadBelgeler();
    }

    clearFilter(): void {
        this.filter.set(createDefaultSatisBelgesiFilter());
        this.loadBelgeler();
    }

    // ── Create / Edit Dialog ──

    openCreateDialog(): void {
        this.isEditing.set(false);
        this.editingBelge.set(null);
        this.formData.set(createEmptyCreateSatisBelgesiRequest());
        this.dialogVisible.set(true);
    }

    openEditDialog(belge: SatisBelgesiDto): void {
        if (belge.durum !== SatisBelgesiDurumu.Taslak && belge.durum !== SatisBelgesiDurumu.Reddedildi) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Sadece Taslak veya Reddedildi durumundaki belgeler düzenlenebilir.' });
            return;
        }
        this.isEditing.set(true);
        this.editingBelge.set(belge);
        this.formData.set({
            belgeTipi: belge.belgeTipi,
            kaynakModul: belge.kaynakModul,
            kaynakTipi: belge.kaynakTipi,
            kaynakId: belge.kaynakId,
            tesisId: belge.tesisId,
            belgeTarihi: belge.belgeTarihi?.split('T')[0] ?? new Date().toISOString().split('T')[0],
            vadeTarihi: belge.vadeTarihi?.split('T')[0] ?? null,
            musteriUnvan: belge.musteriUnvan,
            musteriAdSoyad: belge.musteriAdSoyad,
            musteriVergiNo: belge.musteriVergiNo,
            musteriTcKimlikNo: belge.musteriTcKimlikNo,
            musteriVergiDairesi: belge.musteriVergiDairesi,
            musteriAdres: belge.musteriAdres,
            musteriEposta: belge.musteriEposta,
            musteriTelefon: belge.musteriTelefon,
            kurumsalMi: belge.kurumsalMi,
            aciklama: belge.aciklama,
            belgeNo: belge.belgeNo,
            satirlar: belge.satirlar.map(s => ({
                siraNo: s.siraNo,
                satirTipi: s.satirTipi,
                aciklama: s.aciklama,
                miktar: s.miktar,
                birimFiyat: s.birimFiyat,
                kdvUygulamaTipi: s.kdvUygulamaTipi,
                kdvIstisnaTanimId: s.kdvIstisnaTanimId,
                kdvOrani: s.kdvOrani,
                kaynakSatirId: s.kaynakSatirId
            }))
        });
        this.dialogVisible.set(true);
    }

    saveBelge(): void {
        this.dialogLoading.set(true);
        if (this.isEditing()) {
            const id = this.editingBelge()!.id;
            const updateReq: UpdateSatisBelgesiRequest = {
                belgeNo: this.formData().belgeNo,
                belgeTipi: this.formData().belgeTipi,
                tesisId: this.formData().tesisId,
                belgeTarihi: this.formData().belgeTarihi,
                vadeTarihi: this.formData().vadeTarihi,
                musteriUnvan: this.formData().musteriUnvan,
                musteriAdSoyad: this.formData().musteriAdSoyad,
                musteriVergiNo: this.formData().musteriVergiNo,
                musteriTcKimlikNo: this.formData().musteriTcKimlikNo,
                musteriVergiDairesi: this.formData().musteriVergiDairesi,
                musteriAdres: this.formData().musteriAdres,
                musteriEposta: this.formData().musteriEposta,
                musteriTelefon: this.formData().musteriTelefon,
                kurumsalMi: this.formData().kurumsalMi,
                aciklama: this.formData().aciklama,
                satirlar: this.formData().satirlar
            };
            this.service.update(id, updateReq).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge güncellendi.' });
                    this.dialogVisible.set(false);
                    this.dialogLoading.set(false);
                    this.loadBelgeler();
                },
                error: (err) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                    this.dialogLoading.set(false);
                }
            });
        } else {
            this.service.create(this.formData()).subscribe({
                next: () => {
                    this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge oluşturuldu.' });
                    this.dialogVisible.set(false);
                    this.dialogLoading.set(false);
                    this.loadBelgeler();
                },
                error: (err) => {
                    this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                    this.dialogLoading.set(false);
                }
            });
        }
    }

    // ── Sil ──

    confirmDelete(belge: SatisBelgesiDto): void {
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" belgesini silmek istediğinize emin misiniz?`,
            header: 'Silme Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.delete(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge silindi.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Durum Aksiyonları ──

    muhasebeOnayinaGonder(belge: SatisBelgesiDto): void {
        this.service.muhasebeOnayinaGonder(belge.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge muhasebe onayına gönderildi.' });
                this.loadBelgeler();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    muhasebeOnayla(belge: SatisBelgesiDto): void {
        this.service.muhasebeOnayla(belge.id).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge muhasebe tarafından onaylandı.' });
                this.loadBelgeler();
            },
            error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
        });
    }

    openRedDialog(belge: SatisBelgesiDto): void {
        this.redBelgeId.set(belge.id);
        this.redNedeni.set('');
        this.redDialogVisible.set(true);
    }

    reddet(): void {
        if (!this.redNedeni().trim()) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'Ret nedeni zorunludur.' });
            return;
        }
        this.redLoading.set(true);
        const req: SatisBelgesiRedRequest = { redNedeni: this.redNedeni() };
        this.service.reddet(this.redBelgeId()!, req).subscribe({
            next: () => {
                this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge reddedildi.' });
                this.redDialogVisible.set(false);
                this.redLoading.set(false);
                this.loadBelgeler();
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message });
                this.redLoading.set(false);
            }
        });
    }

    iptalEt(belge: SatisBelgesiDto): void {
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" belgesini iptal etmek istediğinize emin misiniz?`,
            header: 'İptal Onayı',
            icon: 'pi pi-exclamation-triangle',
            accept: () => {
                this.service.iptalEt(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Belge iptal edildi.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Detay ──

    openDetayDialog(belge: SatisBelgesiDto): void {
        this.detayBelge.set(belge);
        this.detayDialogVisible.set(true);
    }

    // ── Satır Yönetimi ──

    addSatir(): void {
        const satirlar = [...this.formData().satirlar];
        const yeniSatir = createEmptySatisBelgesiSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        satirlar.push(yeniSatir);
        this.formData.update(f => ({ ...f, satirlar }));
    }

    removeSatir(index: number): void {
        if (this.formData().satirlar.length <= 1) {
            this.messageService.add({ severity: 'warn', summary: 'Uyarı', detail: 'En az bir satır olmalıdır.' });
            return;
        }
        const satirlar = this.formData().satirlar.filter((_, i) => i !== index);
        satirlar.forEach((s, i) => s.siraNo = i + 1);
        this.formData.update(f => ({ ...f, satirlar }));
    }

    // ── Helpers ──

    previewKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        const matrah = satir.miktar * satir.birimFiyat;
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.TamIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KismiIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KdvKapsamDisi) {
            return 0;
        }
        return matrah * satir.kdvOrani / 100;
    }

    previewSatirToplami(satir: CreateSatisBelgesiSatiriRequest): number {
        return satir.miktar * satir.birimFiyat + this.previewKdvTutari(satir);
    }

    previewGenelToplam(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.previewSatirToplami(s), 0);
    }

    canEdit(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.Taslak || belge.durum === SatisBelgesiDurumu.Reddedildi;
    }

    canDelete(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canGonder(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canOnayla(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canReddet(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canIptal(belge: SatisBelgesiDto): boolean {
        return belge.durum !== SatisBelgesiDurumu.IptalEdildi &&
            belge.durum !== SatisBelgesiDurumu.FaturaKesildi &&
            belge.durum !== SatisBelgesiDurumu.MusteriyeGonderildi;
    }

    canFisOlustur(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnaylandi && !belge.muhasebeFisId;
    }

    muhasebeFisiOlustur(belge: SatisBelgesiDto): void {
        this.confirmationService.confirm({
            message: `"${belge.belgeNo}" için muhasebe fişi oluşturmak istediğinize emin misiniz?`,
            header: 'Fiş Oluşturma Onayı',
            icon: 'pi pi-file',
            accept: () => {
                this.service.muhasebeFisiOlustur(belge.id).subscribe({
                    next: () => {
                        this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Muhasebe fişi oluşturuldu.' });
                        this.loadBelgeler();
                    },
                    error: (err) => this.messageService.add({ severity: 'error', summary: 'Hata', detail: err.message })
                });
            }
        });
    }

    // ── Template-safe label/severity helpers (avoids TS indexed-access errors in HTML) ──

    getDurumLabel(durum: SatisBelgesiDurumu): string {
        return this.durumLabels[durum] ?? String(durum);
    }

    getDurumSeverity(durum: SatisBelgesiDurumu): 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' {
        return (this.durumSeverities[durum] as 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast') ?? 'info';
    }

    getBelgeTipiLabel(tip: SatisBelgesiTipi): string {
        return this.belgeTipiLabels[tip] ?? String(tip);
    }

    getKaynakModulLabel(modul: SatisKaynakModulu): string {
        return this.kaynakModulLabels[modul] ?? String(modul);
    }

    getSatirTipiLabel(tip: SatisBelgesiSatirTipi): string {
        return this.satirTipiLabels[tip] ?? String(tip);
    }
}
