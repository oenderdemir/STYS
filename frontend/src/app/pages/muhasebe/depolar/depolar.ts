import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
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
import {
    DepoCikisGrupModel,
    DepoModel,
    MALZEME_KAYIT_TIPI_OPTIONS,
    MalzemeKayitTipi,
    MuhasebeHesapLookupModel,
    MuhasebeTesisModel
} from './depolar.dto';
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
        TreeTableModule
    ],
    templateUrl: './depolar.html',
    providers: [MessageService, ConfirmationService]
})
export class DepolarPage implements OnInit {
    private readonly service = inject(DepolarService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: DepoModel[] = [];
    treeRecords: DepoTreeNode[] = [];
    model: DepoModel = this.createEmpty();

    tesisler: MuhasebeTesisModel[] = [];
    tesisSecenekleri: Array<{ label: string; value: number | null }> = [];
    formTesisSecenekleri: Array<{ label: string; value: number }> = [];
    selectedTesisId: number | null = null;

    parentDepoSecenekleri: Array<{ label: string; value: number | null }> = [{ label: 'Ana Depo', value: null }];
    muhasebeKodSecenekleri: Array<{ label: string; value: number | null }> = [{ label: 'Seciniz', value: null }];

    readonly malzemeKayitTipiOptions = MALZEME_KAYIT_TIPI_OPTIONS;

    ngOnInit(): void {
        this.loadTesisler();
        this.loadMuhasebeKodlari();
    }

    load(): void {
        this.loading = true;
        this.service.getTree(this.selectedTesisId).pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
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
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.model.tesisId = this.selectedTesisId;
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
        if (!this.model.kod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Depo kodu ve depo adi zorunludur.' });
            return;
        }

        const invalidCikisGrup = (this.model.cikisGruplari ?? []).find((x) => !x.cikisGrupAdi?.trim());
        if (invalidCikisGrup) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Cikis gruplarinda cikis grup adi zorunludur.' });
            return;
        }

        this.saving = true;
        const payload = {
            tesisId: this.model.tesisId ?? null,
            ustDepoId: this.model.ustDepoId ?? null,
            muhasebeHesapPlaniId: this.model.muhasebeHesapPlaniId ?? null,
            kod: this.model.kod.trim(),
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

        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload)
            : this.service.create(payload);

        request$.pipe(finalize(() => {
            this.saving = false;
            this.cdr.detectChanges();
        })).subscribe({
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

    onTesisFilterChange(): void {
        this.load();
    }

    onFormTesisChange(): void {
        this.model.ustDepoId = null;
        this.refreshParentDepoOptions();
    }

    getTesisAdi(tesisId?: number | null): string {
        if (!tesisId) {
            return '-';
        }
        return this.tesisler.find((x) => x.id === tesisId)?.ad ?? `#${tesisId}`;
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

        const found = this.muhasebeKodSecenekleri.find((x) => x.value === muhasebeHesapPlaniId);
        return found?.label ?? `#${muhasebeHesapPlaniId}`;
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

    private loadTesisler(): void {
        this.service.getTesisler().subscribe({
            next: (items) => {
                this.tesisler = [...items].sort((a, b) => (a.ad ?? '').localeCompare(b.ad ?? ''));
                this.tesisSecenekleri = [{ label: 'Tum Tesisler', value: null }, ...this.tesisler.map((x) => ({ label: x.ad, value: x.id }))];
                this.formTesisSecenekleri = this.tesisler.map((x) => ({ label: x.ad, value: x.id }));
                if (!this.selectedTesisId && this.tesisler.length > 0) {
                    this.selectedTesisId = this.tesisler[0].id;
                }
                this.load();
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.load();
                this.cdr.detectChanges();
            }
        });
    }

    private loadMuhasebeKodlari(): void {
        this.service.getMuhasebeKodlari().subscribe({
            next: (items: MuhasebeHesapLookupModel[]) => {
                this.muhasebeKodSecenekleri = [{ label: 'Seciniz', value: null }, ...items.map((x) => ({ label: `${x.kod} - ${x.ad}`, value: x.id }))];
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
