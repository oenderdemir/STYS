import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService, TreeNode } from 'primeng/api';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TreeTableModule } from 'primeng/treetable';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { TasinirKodModel } from './tasinir-kodlari.dto';
import { TasinirKodlariService } from './tasinir-kodlari.service';

type TasinirKodTreeNode = TreeNode<TasinirKodModel> & { loadingChildren?: boolean };
type ParentOption = { label: string; value: number };

@Component({
    selector: 'app-tasinir-kodlari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, AutoCompleteModule, ButtonModule, CheckboxModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, TagModule, ToastModule, ToolbarModule, TreeTableModule],
    templateUrl: './tasinir-kodlari.html',
    providers: [MessageService, ConfirmationService]
})
export class TasinirKodlariPage implements OnInit {
    private readonly service = inject(TasinirKodlariService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = true;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    treeRecords: TasinirKodTreeNode[] = [];
    model: TasinirKodModel = this.createEmpty();

    parentSearchResults: ParentOption[] = [];
    selectedParentOption: ParentOption | null = null;
    private parentSearchSeq = 0;

    ngOnInit(): void {
        this.load();
    }

    load(): void {
        this.loading = true;
        this.service.getTreeRoots().subscribe({
            next: (items) => {
                this.treeRecords = items.map((item) => this.mapToNode(item));
                this.loading = false;
                this.cdr.detectChanges();
            },
            error: (error: unknown) => 
                {
                    this.loading = false;
                    this.showError(error);
                    this.cdr.detectChanges();
                }
        });
    }

    onNodeExpand(event: { node: TreeNode<TasinirKodModel> }): void {
        const node = event.node as TasinirKodTreeNode;
        const nodeId = node.data?.id ?? null;
        if (!nodeId || (node.children && node.children.length > 0)) {
            return;
        }

        this.service.getTreeChildren(nodeId).subscribe({
            next: (items) => {
                node.children = items.map((item) => this.mapToNode(item));
                node.leaf = node.children.length === 0;
                this.treeRecords = [...this.treeRecords];
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    openCreate(): void {
        this.dialogMode = 'create';
        this.model = this.createEmpty();
        this.selectedParentOption = null;
        this.parentSearchResults = [];
        this.dialogVisible = true;
        this.cdr.markForCheck();
    }

    openEdit(item: TasinirKodModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.selectedParentOption = null;
        this.parentSearchResults = [];

        if (this.model.ustKodId) {
            this.service.getById(this.model.ustKodId).subscribe({
                next: (parent) => {
                    const option: ParentOption = { label: `${parent.tamKod} - ${parent.ad}`, value: parent.id! };
                    this.selectedParentOption = option;
                    this.parentSearchResults = [option];
                  this.cdr.detectChanges();
                },
                error: (error: unknown) => this.showError(error)
            });
        }

        this.dialogVisible = true;
         this.cdr.detectChanges();
    }

    searchParent(event: { query: string }): void {
        const query = event.query ?? '';
        const requestSeq = ++this.parentSearchSeq;

        if (query.trim().length < 2) {
            this.parentSearchResults = [];
             this.cdr.detectChanges();
            return;
        }

        this.service.searchPaged(1, 30, query).subscribe({
            next: (paged) => {
                if (requestSeq !== this.parentSearchSeq) {
                    return;
                }

                this.parentSearchResults = paged.items
                    .filter((x) => x.aktifMi && x.id !== this.model.id)
                    .map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id! }));
                this.cdr.markForCheck();
            },
            error: (error: unknown) => {
                if (requestSeq !== this.parentSearchSeq) {
                    return;
                }
                this.showError(error);
            }
        });
    }

    onParentSelect(): void {
        this.model.ustKodId = this.selectedParentOption?.value ?? null;
        this.cdr.markForCheck();
    }

    onParentModelChange(value: unknown): void {
        if (typeof value === 'string') {
            this.model.ustKodId = null;
        } else if (value === null || value === undefined) {
            this.model.ustKodId = null;
            this.selectedParentOption = null;
        }
        this.cdr.markForCheck();
    }

    onTamKodChange(value: string): void {
        const normalized = value?.trim() ?? '';
        if (!normalized) {
            return;
        }

        const parts = normalized.split('.').filter((p) => p.length > 0);
        if (parts.length > 0) {
            this.model.duzeyNo = parts.length;
            this.model.kod = parts[parts.length - 1];
        }
        this.cdr.markForCheck();
    }

    save(): void {
        if (!this.model.tamKod?.trim() || !this.model.kod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tam kod, kod ve ad zorunludur.' });
            return;
        }

        this.saving = true;
        this.cdr.markForCheck();
        
        const payload = {
            tamKod: this.model.tamKod.trim(),
            kod: this.model.kod.trim(),
            ad: this.model.ad.trim(),
            duzeyNo: this.model.duzeyNo,
            ustKodId: this.model.ustKodId ?? null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload)
            : this.service.create(payload);

        request$.pipe(finalize(() => {
            this.saving = false;
            this.cdr.markForCheck();
        })).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
                this.cdr.markForCheck();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.markForCheck();
            }
        });
    }

    delete(item: TasinirKodModel): void {
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
                        this.cdr.markForCheck();
                    },
                    error: (error: unknown) => {
                        this.showError(error);
                        this.cdr.markForCheck();
                    }
                });
            }
        });
    }

    private mapToNode(item: TasinirKodModel): TasinirKodTreeNode {
        return {
            key: item.id?.toString() ?? item.tamKod,
            data: item,
            leaf: !item.hasChildren,
            children: []
        };
    }

    private createEmpty(): TasinirKodModel {
        return {
            tamKod: '',
            kod: '',
            ad: '',
            duzeyNo: 1,
            aktifMi: true,
            ustKodId: null,
            aciklama: null,
            hasChildren: false
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
