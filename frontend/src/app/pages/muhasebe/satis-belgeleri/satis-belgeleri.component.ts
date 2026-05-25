import { CommonModule } from '@angular/common';
import { Component, effect, inject, OnInit, signal } from '@angular/core';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ConfirmationService, MessageService } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TabsModule } from 'primeng/tabs';
import { TagModule } from 'primeng/tag';
import { TextareaModule } from 'primeng/textarea';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ToolbarModule } from 'primeng/toolbar';
import { TooltipModule } from 'primeng/tooltip';
import { finalize } from 'rxjs';
import { SatisBelgesiService } from '../services/satis-belgesi.service';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { CariKartlarService } from '../cari-kartlar/cari-kartlar.service';
import { CariKartModel } from '../cari-kartlar/cari-kartlar.dto';
import { DepolarService } from '../depolar/depolar.service';
import { DepoModel } from '../depolar/depolar.dto';
import { TasinirKartlariService } from '../tasinir-kartlari/tasinir-kartlari.service';
import { TasinirKartModel } from '../tasinir-kartlari/tasinir-kartlari.dto';
import { KdvIstisnaTanimService } from '../services/kdv-istisna-tanim.service';
import { KdvIstisnaTanimDto, createDefaultKdvIstisnaTanimFilter } from '../models/kdv-istisna-tanim.model';
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
        AutoCompleteModule,
        ButtonModule,
        CardModule,
        ConfirmDialogModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TableModule,
        TabsModule,
        TagModule,
        TextareaModule,
        ToastModule,
        ToggleSwitchModule,
        ToolbarModule,
        TooltipModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    providers: [ConfirmationService, MessageService],
    templateUrl: './satis-belgeleri.component.html',
    styleUrl: './satis-belgeleri.component.scss'
})
export class SatisBelgeleriComponent implements OnInit {
    private readonly service = inject(SatisBelgesiService);
    readonly tesisContext = inject(MuhasebeTesisContextService);
    private readonly cariKartService = inject(CariKartlarService);
    private readonly depoService = inject(DepolarService);
    private readonly tasinirKartService = inject(TasinirKartlariService);
    private readonly kdvIstisnaTanimService = inject(KdvIstisnaTanimService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly messageService = inject(MessageService);
    private readonly router = inject(Router);

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

    // Cari seçim
    cariKartlar = signal<CariKartModel[]>([]);
    cariKartlarLoading = signal(false);
    selectedCari = signal<CariKartModel | null>(null);
    filteredCariKartlar: CariKartModel[] = [];
    manuelMusteriGirisi = signal(false);

    depoSecenekleri = signal<Array<{ label: string; value: number }>>([]);
    depolarLoading = signal(false);
    tasinirKartlar = signal<TasinirKartModel[]>([]);
    tasinirKartSecenekleri = signal<Array<{ label: string; value: number }>>([]);
    tasinirKartlarLoading = signal(false);
    kdvIstisnaTanimlari = signal<KdvIstisnaTanimDto[]>([]);
    kdvIstisnaTanimlariLoading = signal(false);

    // Reddetme
    redDialogVisible = signal(false);
    redNedeni = signal('');
    redBelgeId = signal<number | null>(null);
    redLoading = signal(false);

    // Detay dialog
    detayDialogVisible = signal(false);
    detayBelge = signal<SatisBelgesiDto | null>(null);

    // Tab navigation
    activeTab = signal('0');
    tesisHazir = signal(false);
    private lastLoadedTesisId: number | null = null;

    // ── Label/option maps (plain, not signals) ──
    durumLabels = SATIS_BELGESI_DURUMU_LABELS;
    durumSeverities = SATIS_BELGESI_DURUMU_SEVERITIES;
    durumSecenekleri = SATIS_BELGESI_DURUM_SECENEKLERI;
    belgeTipiLabels = SATIS_BELGESI_TIPI_LABELS;
    kaynakModulLabels = SATIS_KAYNAK_MODULU_LABELS;
    satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;
    kdvEnum = KdvUygulamaTipi;

    belgeTipiSecenekleri = Object.entries(this.belgeTipiLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    kaynakModulSecenekleri = Object.entries(this.kaynakModulLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    satirTipiSecenekleri = Object.entries(this.satirTipiLabels).map(([k, v]) => ({ value: Number(k), label: v }));
    kdvUygulamaTipiSecenekleri = Object.entries(this.kdvUygulamaTipiLabels)
        .map(([k, v]) => ({ value: Number(k), label: v }));
    tevkifatSecenekleri = [
        { label: '2/10', value: '2/10' },
        { label: '3/10', value: '3/10' },
        { label: '4/10', value: '4/10' },
        { label: '5/10', value: '5/10' },
        { label: '7/10', value: '7/10' },
        { label: '9/10', value: '9/10' },
        { label: '10/10', value: '10/10' }
    ];

    getMusteriDisplay = getMusteriDisplayName;

    private readonly tesisDegisimEffect = effect(() => {
        if (!this.tesisHazir()) {
            return;
        }

        const tesis = this.tesisContext.seciliTesis();
        const tesisId = tesis?.id ?? null;
        if (!tesisId) {
            return;
        }

        if (this.lastLoadedTesisId === tesisId) {
            return;
        }

        const tesisDegisti = this.lastLoadedTesisId !== null && this.lastLoadedTesisId !== tesisId;
        this.lastLoadedTesisId = tesisId;

        if (tesisDegisti && this.dialogVisible()) {
            this.dialogVisible.set(false);
            this.messageService.add({
                severity: 'warn',
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için açık form kapatıldı.'
            });
            this.formData.set(createEmptyCreateSatisBelgesiRequest());
            this.selectedCari.set(null);
            this.manuelMusteriGirisi.set(false);
        }

        this.loadLookupLists(tesisId);
        this.loadBelgeler();
    }, { allowSignalWrites: true });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => this.tesisHazir.set(true),
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Tesis listesi yüklenemedi: ' + err.message });
            }
        });
    }

    // ── Load ──

    loadBelgeler(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.loading.set(true);
        const currentFilter = this.filter();
        const filterToSend = { ...currentFilter, tesisId };
        this.service.filter(filterToSend).subscribe({
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

    loadLookupLists(tesisId: number): void {
        this.loadCariKartlar(tesisId);
        this.loadDepolar(tesisId);
        this.loadTasinirKartlar(tesisId);
        this.loadKdvIstisnaTanimlari();
    }

    loadCariKartlar(tesisId: number): void {
        this.cariKartlarLoading.set(true);
        this.cariKartService.getAll(tesisId).pipe(
            finalize(() => this.cariKartlarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const cariList = data
                    .filter(c => c.aktifMi)
                    .sort((a, b) => (a.cariKodu ?? '').localeCompare(b.cariKodu ?? ''));
                this.cariKartlar.set(cariList);
                this.filteredCariKartlar = [...cariList];
                this.syncSelectedCariWithLookup(cariList);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Cari kartlar yüklenemedi: ' + err.message });
            }
        });
    }

    loadDepolar(tesisId: number): void {
        this.depolarLoading.set(true);
        this.depoService.getAll(tesisId).pipe(
            finalize(() => this.depolarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const depolar = data
                    .filter(d => d.aktifMi)
                    .sort((a, b) => (a.kod ?? '').localeCompare(b.kod ?? ''));
                this.depoSecenekleri.set(
                    depolar
                        .filter(d => !!d.id)
                        .map(d => ({ label: `${d.kod} - ${d.ad}`, value: d.id! }))
                );
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Depolar yüklenemedi: ' + err.message });
            }
        });
    }

    loadTasinirKartlar(tesisId: number): void {
        this.tasinirKartlarLoading.set(true);
        this.tasinirKartService.getAll(tesisId).pipe(
            finalize(() => this.tasinirKartlarLoading.set(false))
        ).subscribe({
            next: (data) => {
                const kartlar = data
                    .filter(k => k.aktifMi)
                    .sort((a, b) => (a.stokKodu ?? '').localeCompare(b.stokKodu ?? ''));
                this.tasinirKartlar.set(kartlar);
                this.tasinirKartSecenekleri.set(
                    kartlar
                        .filter(k => !!k.id)
                        .map(k => ({ label: `${k.stokKodu} - ${k.ad} (${k.birim})`, value: k.id! }))
                );
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'Taşınır kartlar yüklenemedi: ' + err.message });
            }
        });
    }

    loadKdvIstisnaTanimlari(): void {
        this.kdvIstisnaTanimlariLoading.set(true);
        const filter = createDefaultKdvIstisnaTanimFilter();
        filter.aktifMi = true;
        filter.satisIslemlerindeKullanilirMi = true;
        this.kdvIstisnaTanimService.filter(filter).pipe(
            finalize(() => this.kdvIstisnaTanimlariLoading.set(false))
        ).subscribe({
            next: (data) => {
                const list = data
                    .filter(t => t.aktifMi && t.satisIslemlerindeKullanilirMi)
                    .sort((a, b) => `${a.kod} ${a.ad}`.localeCompare(`${b.kod} ${b.ad}`));
                this.kdvIstisnaTanimlari.set(list);
            },
            error: (err) => {
                this.messageService.add({ severity: 'error', summary: 'Hata', detail: 'KDV istisna tanımları yüklenemedi: ' + err.message });
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

    private syncSelectedCariWithLookup(cariList: CariKartModel[]): void {
        const current = this.selectedCari();
        if (!current) {
            return;
        }

        const matched = cariList.find(c =>
            c.id === current.id ||
            (!!c.vergiNoTckn && c.vergiNoTckn === current.vergiNoTckn && c.unvanAdSoyad === current.unvanAdSoyad));

        if (matched) {
            this.selectedCari.set(matched);
        } else if (!this.manuelMusteriGirisi()) {
            this.selectedCari.set(null);
            this.messageService.add({
                severity: 'warn',
                summary: 'Cari Kart Uyarısı',
                detail: 'Seçili cari kart mevcut çalışma tesisinde bulunamadığı için temizlendi.'
            });
        }
    }

    private syncSelectedCariFromBelge(belge: SatisBelgesiDto): void {
        const cariList = this.cariKartlar();
        const matched = cariList.find(c =>
            (belge.musteriVergiNo && c.vergiNoTckn === belge.musteriVergiNo) ||
            (belge.musteriTcKimlikNo && c.vergiNoTckn === belge.musteriTcKimlikNo) ||
            (belge.kurumsalMi && c.unvanAdSoyad === belge.musteriUnvan) ||
            (!belge.kurumsalMi && c.unvanAdSoyad === belge.musteriAdSoyad)
        );

        this.selectedCari.set(matched ?? null);
    }

    // ── Cari Kart AutoComplete ──

    filterCari(event: { query: string }): void {
        const query = (event.query ?? '').toLowerCase().trim();
        const allCari = this.cariKartlar();
        if (!query) {
            this.filteredCariKartlar = [...allCari];
            return;
        }
        this.filteredCariKartlar = allCari.filter(c =>
            (c.unvanAdSoyad ?? '').toLowerCase().includes(query) ||
            (c.vergiNoTckn ?? '').toLowerCase().includes(query) ||
            (c.cariKodu ?? '').toLowerCase().includes(query)
        );
    }

    onCariKartSecildi(cari: CariKartModel | null): void {
        if (!cari) {
            this.selectedCari.set(null);
            if (!this.manuelMusteriGirisi()) {
                this.formData.update(f => ({
                    ...f,
                    kurumsalMi: false,
                    musteriUnvan: null,
                    musteriAdSoyad: null,
                    musteriVergiNo: null,
                    musteriTcKimlikNo: null,
                    musteriVergiDairesi: null,
                    musteriAdres: null,
                    musteriEposta: null,
                    musteriTelefon: null
                }));
            }
            return;
        }

        this.selectedCari.set(cari);
        // Cari tipine göre kurumsal/bireysel belirle
        const kurumsalMi = cari.cariTipi === 'KurumsalMusteri';

        // Manuel müşteri girişi kapalıyken cari bilgilerini form alanlarına yaz
        if (!this.manuelMusteriGirisi()) {
            this.formData.update(f => ({
                ...f,
                kurumsalMi,
                musteriUnvan: kurumsalMi ? cari.unvanAdSoyad : null,
                musteriAdSoyad: !kurumsalMi ? cari.unvanAdSoyad : null,
                musteriVergiNo: kurumsalMi ? (cari.vergiNoTckn ?? null) : null,
                musteriTcKimlikNo: !kurumsalMi ? (cari.vergiNoTckn ?? null) : null,
                musteriVergiDairesi: cari.vergiDairesi ?? null,
                musteriAdres: cari.adres ?? null,
                musteriEposta: cari.eposta ?? null,
                musteriTelefon: cari.telefon ?? null
            }));
        }
    }

    onManuelMusteriGirisiChange(value: boolean): void {
        this.manuelMusteriGirisi.set(value);
        if (value) {
            // Manuel mod açıldı — form alanlarını temizleme, kullanıcı kendi girer
            // Cari seçimi hala referans olarak kalabilir
        } else if (this.selectedCari()) {
            // Manuel mod kapandı — seçili cari varsa bilgileri tekrar doldur
            this.onCariKartSecildi(this.selectedCari());
        }
    }

    // Cari display için format
    formatCariDisplay(cari: CariKartModel): string {
        const kod = cari.cariKodu || '-';
        const unvan = cari.unvanAdSoyad || '-';
        const vergi = cari.vergiNoTckn ? ` (${cari.vergiNoTckn})` : '';
        return `${kod} - ${unvan}${vergi}`;
    }

    // ── Create / Edit Dialog ──

    onTabChange(value: string | number | undefined): void {
        if (value !== undefined) {
            this.activeTab.set(String(value));
        }
    }

    openCreateDialog(): void {
        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.isEditing.set(false);
        this.editingBelge.set(null);
        const empty = createEmptyCreateSatisBelgesiRequest();
        empty.tesisId = tesisId;
        this.formData.set(empty);
        this.selectedCari.set(null);
        this.manuelMusteriGirisi.set(false);
        this.filteredCariKartlar = [...this.cariKartlar()];
        this.activeTab.set('0');
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
                tasinirKartId: s.tasinirKartId ?? null,
                depoId: s.depoId ?? null,
                birim: s.birim,
                miktar: s.miktar,
                birimFiyat: s.birimFiyat,
                indirimTutari: s.indirimTutari,
                kdvUygulamaTipi: s.kdvUygulamaTipi,
                kdvIstisnaTanimId: s.kdvIstisnaTanimId,
                kdvOrani: s.kdvOrani,
                tevkifatPay: s.tevkifatPay ?? null,
                tevkifatPayda: s.tevkifatPayda ?? null,
                kaynakSatirId: s.kaynakSatirId
            }))
        });
        this.syncSelectedCariFromBelge(belge);
        this.manuelMusteriGirisi.set(false);
        this.filteredCariKartlar = [...this.cariKartlar()];
        this.activeTab.set('0');
        this.dialogVisible.set(true);
    }

    saveBelge(): void {
        // Validation: Cari seçilmemiş ve manuel mod kapalıysa uyar
        if (!this.manuelMusteriGirisi() && !this.selectedCari()) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Eksik Bilgi',
                detail: 'Lütfen cari kart seçiniz veya "Manuel müşteri bilgisi gireceğim" seçeneğini açınız.'
            });
            return;
        }

        const tesisId = this.getSeciliTesisIdOrWarn();
        if (tesisId === null) {
            return;
        }

        this.dialogLoading.set(true);
        if (this.isEditing()) {
            const id = this.editingBelge()!.id;
            const payload = {
                ...this.formData(),
                tesisId,
                satirlar: this.formData().satirlar.map(s => ({ ...s }))
            };
            const updateReq: UpdateSatisBelgesiRequest = {
                belgeNo: payload.belgeNo,
                belgeTipi: payload.belgeTipi,
                tesisId: payload.tesisId,
                belgeTarihi: payload.belgeTarihi,
                vadeTarihi: payload.vadeTarihi,
                musteriUnvan: payload.musteriUnvan,
                musteriAdSoyad: payload.musteriAdSoyad,
                musteriVergiNo: payload.musteriVergiNo,
                musteriTcKimlikNo: payload.musteriTcKimlikNo,
                musteriVergiDairesi: payload.musteriVergiDairesi,
                musteriAdres: payload.musteriAdres,
                musteriEposta: payload.musteriEposta,
                musteriTelefon: payload.musteriTelefon,
                kurumsalMi: payload.kurumsalMi,
                aciklama: payload.aciklama,
                satirlar: payload.satirlar
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
            const payload = {
                ...this.formData(),
                tesisId,
                satirlar: this.formData().satirlar.map(s => ({ ...s }))
            };
            this.service.create(payload).subscribe({
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

    getSeciliTesisIdOrWarn(): number | null {
        try {
            return this.tesisContext.requireSeciliTesisId();
        } catch {
            this.messageService.add({
                severity: 'warn',
                summary: 'Çalışma Tesisi Seçilmedi',
                detail: 'Muhasebe işlemi için önce çalışma tesisini seçiniz.'
            });
            return null;
        }
    }

    getSatirIndirimTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return Math.max(0, satir.indirimTutari ?? 0);
    }

    getSatirMatrah(satir: CreateSatisBelgesiSatiriRequest): number {
        const brut = (satir.miktar ?? 0) * (satir.birimFiyat ?? 0);
        const indirim = this.getSatirIndirimTutari(satir);
        return Math.max(0, brut - indirim);
    }

    getSatirBrutKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.TamIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KismiIstisna ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.KdvKapsamDisi) {
            return 0;
        }

        return this.getSatirMatrah(satir) * (satir.kdvOrani ?? 0) / 100;
    }

    getSatirTevkifatTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        if (satir.kdvUygulamaTipi !== KdvUygulamaTipi.Tevkifatli) {
            return 0;
        }

        const brutKdv = this.getSatirBrutKdvTutari(satir);
        const pay = satir.tevkifatPay ?? 0;
        const payda = satir.tevkifatPayda ?? 0;
        if (pay <= 0 || payda <= 0) {
            return 0;
        }

        return brutKdv * pay / payda;
    }

    previewNetKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirBrutKdvTutari(satir) - this.getSatirTevkifatTutari(satir);
    }

    previewKdvTutari(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirBrutKdvTutari(satir);
    }

    previewSatirToplami(satir: CreateSatisBelgesiSatiriRequest): number {
        return this.getSatirMatrah(satir) + this.previewNetKdvTutari(satir);
    }

    previewToplamMatrah(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirMatrah(s), 0);
    }

    previewToplamIndirimTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirIndirimTutari(s), 0);
    }

    previewToplamKdv(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.previewKdvTutari(s), 0);
    }

    previewToplamTevkifatTutari(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.getSatirTevkifatTutari(s), 0);
    }

    previewToplamNetKdv(): number {
        return this.previewToplamKdv() - this.previewToplamTevkifatTutari();
    }

    previewGenelToplam(): number {
        return this.formData().satirlar.reduce((sum, s) => sum + this.previewSatirToplami(s), 0);
    }

    getTasinirKartLabel(tasinirKartId?: number | null): string {
        if (!tasinirKartId) {
            return '-';
        }
        return this.tasinirKartSecenekleri().find(x => x.value === tasinirKartId)?.label ?? `#${tasinirKartId}`;
    }

    getDepoLabel(depoId?: number | null): string {
        if (!depoId) {
            return '-';
        }
        return this.depoSecenekleri().find(x => x.value === depoId)?.label ?? `#${depoId}`;
    }

    getSelectedCariDisplay(): string {
        const cari = this.selectedCari();
        return cari ? this.formatCariDisplay(cari) : '-';
    }

    getKdvIstisnaSecenekleri(satir: CreateSatisBelgesiSatiriRequest): Array<{ label: string; value: number }> {
        if (
            satir.kdvUygulamaTipi === KdvUygulamaTipi.Kdvli ||
            satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli
        ) {
            return [];
        }

        return this.kdvIstisnaTanimlari()
            .filter(t => t.aktifMi && t.satisIslemlerindeKullanilirMi && (t.uygulamaTipi === satir.kdvUygulamaTipi))
            .map(t => ({ label: `${t.kod} - ${t.ad}`, value: t.id }));
    }

    getTevkifatSecimi(satir: CreateSatisBelgesiSatiriRequest): string | null {
        if (!satir.tevkifatPay || !satir.tevkifatPayda) {
            return null;
        }
        return `${satir.tevkifatPay}/${satir.tevkifatPayda}`;
    }

    setTevkifatSecimi(satir: CreateSatisBelgesiSatiriRequest, value: string | null): void {
        if (!value) {
            satir.tevkifatPay = null;
            satir.tevkifatPayda = null;
            return;
        }

        const [pay, payda] = value.split('/').map(part => Number(part));
        satir.tevkifatPay = Number.isFinite(pay) ? pay : null;
        satir.tevkifatPayda = Number.isFinite(payda) ? payda : null;
    }

    onSatirKdvTipiChange(satir: CreateSatisBelgesiSatiriRequest, value: KdvUygulamaTipi): void {
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

        satir.kdvOrani = 0;
        satir.kdvIstisnaTanimId = null;
    }

    onSatirTasinirKartChange(satir: CreateSatisBelgesiSatiriRequest, value: number | null): void {
        satir.tasinirKartId = value;
        if (!value) {
            return;
        }

        const kart = this.tasinirKartlar().find(k => k.id === value);
        if (kart && (!satir.birim || satir.birim === 'Adet')) {
            satir.birim = kart.birim;
        }
    }

    // ── Faz 66: Bağlı muhasebe fişi olan belgelerde tüm mutasyon aksiyonları engellenir ──

    canEdit(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak || belge.durum === SatisBelgesiDurumu.Reddedildi;
    }

    canDelete(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canGonder(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.Taslak;
    }

    canOnayla(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canReddet(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnayinda;
    }

    canIptal(belge: SatisBelgesiDto): boolean {
        if (belge.muhasebeFisId) return false;
        return belge.durum !== SatisBelgesiDurumu.IptalEdildi &&
            belge.durum !== SatisBelgesiDurumu.FaturaKesildi &&
            belge.durum !== SatisBelgesiDurumu.MusteriyeGonderildi;
    }

    canFisOlustur(belge: SatisBelgesiDto): boolean {
        return belge.durum === SatisBelgesiDurumu.MuhasebeOnaylandi && !belge.muhasebeFisId;
    }

    hasMuhasebeFisi(belge: SatisBelgesiDto): boolean {
        return !!belge.muhasebeFisId;
    }

    muhasebeFisineGit(belge: SatisBelgesiDto): void {
        if (!belge.muhasebeFisId) {
            return;
        }
        this.router.navigate(['/muhasebe/fisler'], { queryParams: { id: belge.muhasebeFisId } });
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
