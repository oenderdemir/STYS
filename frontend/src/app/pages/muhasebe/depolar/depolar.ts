import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, effect, inject, ChangeDetectionStrategy } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService, TreeNode } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { RadioButtonModule } from 'primeng/radiobutton';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TreeTableModule } from 'primeng/treetable';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { MuhasebeTesisContextService } from '../services/muhasebe-tesis-context.service';
import { MuhasebeTesisSecimDialogComponent } from '../components/muhasebe-tesis-secim-dialog/muhasebe-tesis-secim-dialog.component';
import { MuhasebeTesisContextBarComponent } from '../components/muhasebe-tesis-context-bar/muhasebe-tesis-context-bar.component';
import { DepoCikisGrupModel, DepoModel, MALZEME_KAYIT_TIPI_OPTIONS, MalzemeKayitTipi } from './depolar.dto';
import { DepolarService } from './depolar.service';

type DepoTreeNode = TreeNode<DepoModel>;

@Component({
    selector: 'app-depolar-page',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        ButtonModule,
        CheckboxModule,
        ConfirmDialogModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        RadioButtonModule,
        SelectModule,
        TableModule,
        TagModule,
        ToastModule,
        ToolbarModule,
        TreeTableModule,
        MuhasebeTesisSecimDialogComponent,
        MuhasebeTesisContextBarComponent
    ],
    templateUrl: './depolar.html',
    changeDetection: ChangeDetectionStrategy.Eager,
    providers: [MessageService, ConfirmationService]
})
export class DepolarPage implements OnInit {
    private readonly service = inject(DepolarService);
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

    records: DepoModel[] = [];
    treeRecords: DepoTreeNode[] = [];
    model: DepoModel = this.createEmpty();

    parentDepoSecenekleri: Array<{ label: string; value: number | null }> = [{ label: 'Ana Depo', value: null }];
    readonly malzemeKayitTipiOptions = MALZEME_KAYIT_TIPI_OPTIONS;

    private readonly tesisChangeEffect = effect(() => {
        const tesisId = this.tesisContext.seciliTesis()?.id ?? null;
        if (!this.contextInitialized || this.currentTesisId === tesisId) {
            return;
        }

        this.currentTesisId = tesisId;
        if (tesisId) {
            this.closeOpenDialogForTesisChange();
            this.load();
            this.messageService.add({
                severity: UiSeverity.Warn,
                summary: 'Çalışma Tesisi Değişti',
                detail: 'Çalışma tesisi değiştiği için depo ağacı yenilendi.'
            });
        }
    });

    ngOnInit(): void {
        this.tesisContext.initialize().subscribe({
            next: () => {
                this.contextInitialized = true;
                this.currentTesisId = this.tesisContext.seciliTesis()?.id ?? null;
                this.load();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    load(): void {
        const tesisId = this.currentTesisId ?? this.tesisContext.seciliTesis()?.id ?? null;
        if (!tesisId) {
            return;
        }

        this.loading = true;
        this.service
            .getTree(tesisId)
            .pipe(
                finalize(() => {
                    this.loading = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: (items) => {
                    this.records = items;
                    this.treeRecords = this.buildTree(items);
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
        this.refreshParentDepoOptions();
        this.dialogVisible = true;
        this.cdr.detectChanges();
    }

    openEdit(item: DepoModel): void {
        this.dialogMode = 'edit';
        this.model = {
            ...item,
            cikisGruplari: (item.cikisGruplari ?? []).map((x) => ({ ...x }))
        };
        this.refreshParentDepoOptions();
        this.dialogVisible = true;
        this.cdr.detectChanges();
    }

    save(): void {
        if (!this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Depo adi zorunludur.' });
            return;
        }

        const tesisId = this.dialogMode === 'create' ? this.getSeciliTesisIdOrWarn() : (this.model.tesisId ?? this.getSeciliTesisIdOrWarn());
        if (tesisId === null) {
            return;
        }

        const invalidCikisGrup = (this.model.cikisGruplari ?? []).find((x) => !x.cikisGrupAdi?.trim());
        if (invalidCikisGrup) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Cikis gruplarinda cikis grup adi zorunludur.' });
            return;
        }

        this.saving = true;
        const payload = {
            tesisId,
            ustDepoId: this.model.ustDepoId ?? null,
            muhasebeHesapPlaniId: null,
            kod: null,
            ad: this.model.ad.trim(),
            malzemeKayitTipi: this.model.malzemeKayitTipi,
            satisFiyatlariniGoster: this.model.satisFiyatlariniGoster,
            avansGenel: this.model.avansGenel,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null,
            cikisGruplari: (this.model.cikisGruplari ?? []).map((x) => ({
                id: x.id,
                depoId: x.depoId,
                cikisGrupAdi: x.cikisGrupAdi?.trim(),
                karOrani: x.karOrani ?? 0,
                lokasyonId: x.lokasyonId ?? null
            }))
        };

        const request$ = this.dialogMode === 'edit' && this.model.id ? this.service.update(this.model.id, payload) : this.service.create(payload);

        request$
            .pipe(
                finalize(() => {
                    this.saving = false;
                    this.cdr.detectChanges();
                })
            )
            .subscribe({
                next: () => {
                    this.dialogVisible = false;
                    this.load();
                    this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
                    this.cdr.detectChanges();
                },
                error: (error: unknown) => {
                    this.showError(error);
                    this.cdr.detectChanges();
                }
            });
    }

    delete(item: DepoModel): void {
        if (!item.id) {
            return;
        }

        this.confirmationService.confirm({
            message: 'Depo kaydi silinsin mi?',
            header: 'Onay',
            icon: 'pi pi-exclamation-triangle',
            acceptLabel: 'Evet',
            rejectLabel: 'Hayir',
            accept: () => {
                this.service.delete(item.id!).subscribe({
                    next: () => {
                        this.load();
                        this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit silindi.' });
                        this.cdr.detectChanges();
                    },
                    error: (error: unknown) => {
                        this.showError(error);
                        this.cdr.detectChanges();
                    }
                });
            }
        });
    }

    addCikisGrupRow(): void {
        this.model.cikisGruplari = this.model.cikisGruplari ?? [];
        this.model.cikisGruplari.push({
            cikisGrupAdi: '',
            karOrani: 0,
            lokasyonId: null
        });
    }

    removeCikisGrupRow(index: number): void {
        if (!this.model.cikisGruplari) {
            return;
        }

        this.model.cikisGruplari.splice(index, 1);
        this.model.cikisGruplari = [...this.model.cikisGruplari];
    }

    getTesisAdi(tesisId?: number | null): string {
        return this.tesisContext.seciliTesis()?.ad ?? (tesisId ? `#${tesisId}` : '-');
    }

    getParentDepoAdi(ustDepoId?: number | null): string {
        if (!ustDepoId) {
            return '-';
        }

        const parent = this.records.find((x) => x.id === ustDepoId);
        if (!parent) {
            return `#${ustDepoId}`;
        }

        return `${parent.kod} - ${parent.ad}`;
    }

    getMuhasebeKodLabel(muhasebeHesapPlaniId?: number | null): string {
        if (!muhasebeHesapPlaniId) {
            return '-';
        }
        const found = this.records.find((x) => x.muhasebeHesapPlaniId === muhasebeHesapPlaniId);
        return found?.kod ?? `#${muhasebeHesapPlaniId}`;
    }

    private buildTree(items: DepoModel[]): DepoTreeNode[] {
        const map = new Map<number, DepoTreeNode>();
        const roots: DepoTreeNode[] = [];

        const sorted = [...items].sort((a, b) => (a.kod ?? '').localeCompare(b.kod ?? '') || (a.ad ?? '').localeCompare(b.ad ?? ''));

        for (const item of sorted) {
            if (!item.id) {
                continue;
            }
            map.set(item.id, { key: item.id.toString(), data: item, children: [] });
        }

        for (const item of sorted) {
            if (!item.id) {
                continue;
            }

            const node = map.get(item.id);
            if (!node) {
                continue;
            }

            if (item.ustDepoId && map.has(item.ustDepoId)) {
                map.get(item.ustDepoId)!.children = map.get(item.ustDepoId)!.children ?? [];
                map.get(item.ustDepoId)!.children!.push(node);
            } else {
                roots.push(node);
            }
        }

        return roots;
    }

    private refreshParentDepoOptions(): void {
        const options: Array<{ label: string; value: number | null }> = [{ label: 'Ana Depo', value: null }];
        const selectedTesisId = this.model.tesisId ?? null;
        const currentId = this.model.id ?? null;
        const blockedIds = currentId ? this.getDescendantIds(currentId) : new Set<number>();

        const candidates = this.records
            .filter((x) => x.id && x.tesisId === selectedTesisId)
            .filter((x) => !currentId || (x.id !== currentId && !blockedIds.has(x.id!)))
            .sort((a, b) => (a.kod ?? '').localeCompare(b.kod ?? '') || (a.ad ?? '').localeCompare(b.ad ?? ''));

        for (const depo of candidates) {
            options.push({
                label: `${depo.kod} - ${depo.ad}`,
                value: depo.id!
            });
        }

        this.parentDepoSecenekleri = options;
    }

    private getDescendantIds(rootId: number): Set<number> {
        const allByParent = new Map<number, number[]>();
        for (const depo of this.records) {
            if (!depo.id || !depo.ustDepoId) {
                continue;
            }

            const list = allByParent.get(depo.ustDepoId) ?? [];
            list.push(depo.id);
            allByParent.set(depo.ustDepoId, list);
        }

        const visited = new Set<number>();
        const stack: number[] = [rootId];
        while (stack.length > 0) {
            const current = stack.pop()!;
            const children = allByParent.get(current) ?? [];
            for (const child of children) {
                if (visited.has(child)) {
                    continue;
                }
                visited.add(child);
                stack.push(child);
            }
        }

        return visited;
    }

    private createEmpty(): DepoModel {
        return {
            tesisId: null,
            ustDepoId: null,
            muhasebeHesapPlaniId: null,
            kod: '',
            ad: '',
            malzemeKayitTipi: 'MalzemeleriAyriKayittaTut',
            satisFiyatlariniGoster: false,
            avansGenel: false,
            aktifMi: true,
            aciklama: null,
            cikisGruplari: []
        };
    }

    private closeOpenDialogForTesisChange(): void {
        if (!this.dialogVisible) {
            return;
        }

        this.dialogVisible = false;
        this.model = this.createEmpty();
        this.parentDepoSecenekleri = [{ label: 'Ana Depo', value: null }];
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
