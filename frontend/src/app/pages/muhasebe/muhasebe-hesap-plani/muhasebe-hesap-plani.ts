import { HttpErrorResponse } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService, TreeNode } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { TreeTableModule } from 'primeng/treetable';
import { tryReadApiMessage } from '../../../core/api';
import { UiSeverity } from '../../../core/ui/ui-severity.constants';
import { CreateMuhasebeHesapPlaniRequest, MuhasebeHesapPlaniModel, UpdateMuhasebeHesapPlaniRequest } from './muhasebe-hesap-plani.dto';
import { MuhasebeHesapPlaniService } from './muhasebe-hesap-plani.service';

@Component({
    selector: 'app-muhasebe-hesap-plani-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TagModule, ToastModule, ToolbarModule, TreeTableModule],
    templateUrl: './muhasebe-hesap-plani.html',
    providers: [MessageService, ConfirmationService]
})
export class MuhasebeHesapPlaniPage implements OnInit {
    private readonly service = inject(MuhasebeHesapPlaniService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);

    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    treeRecords: TreeNode<MuhasebeHesapPlaniModel>[] = [];
    model: MuhasebeHesapPlaniModel = this.createEmpty();
    ustHesapSecenekleri: Array<{ label: string; value: number }> = [];

    ngOnInit(): void {
        setTimeout(() => this.load());
    }

    load(): void {
        this.loading = true;
        this.service.getTreeRoots().pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (items) => {
                this.treeRecords = items.map((item) => this.mapToNode(item));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => {
                this.showError(error);
                this.cdr.detectChanges();
            }
        });
    }

    onNodeExpand(event: { node: TreeNode<MuhasebeHesapPlaniModel> }): void {
        const node = event.node;
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
        this.dialogVisible = true;
        this.loadParentOptions();
    }

    openEdit(item: MuhasebeHesapPlaniModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.dialogVisible = true;
        this.loadParentOptions(item.id);
    }

    save(): void {
        if (!this.model.kod?.trim() || !this.model.tamKod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Kod, tam kod ve ad zorunludur.' });
            return;
        }

        const payload: CreateMuhasebeHesapPlaniRequest | UpdateMuhasebeHesapPlaniRequest = {
            kod: this.model.kod.trim(),
            tamKod: this.model.tamKod.trim(),
            ad: this.model.ad.trim(),
            seviyeNo: this.model.seviyeNo,
            ustHesapId: this.model.ustHesapId ?? null,
            aktifMi: this.model.aktifMi,
            aciklama: this.model.aciklama?.trim() || null
        };

        this.saving = true;
        const request$ = this.dialogMode === 'edit' && this.model.id
            ? this.service.update(this.model.id, payload as UpdateMuhasebeHesapPlaniRequest)
            : this.service.create(payload as CreateMuhasebeHesapPlaniRequest);

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    delete(item: MuhasebeHesapPlaniModel): void {
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

    private loadParentOptions(excludeId: number | null = null): void {
        this.service.getPaged(1, 1000).subscribe({
            next: (paged) => {
                this.ustHesapSecenekleri = (paged.items ?? [])
                    .filter((x) => x.id && x.id !== excludeId)
                    .sort((a, b) => a.tamKod.localeCompare(b.tamKod))
                    .map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id! }));
                this.cdr.detectChanges();
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private mapToNode(item: MuhasebeHesapPlaniModel): TreeNode<MuhasebeHesapPlaniModel> {
        return {
            key: item.id?.toString() ?? item.tamKod,
            data: item,
            leaf: !item.hasChildren,
            children: []
        };
    }

    private createEmpty(): MuhasebeHesapPlaniModel {
        return {
            kod: '',
            tamKod: '',
            ad: '',
            seviyeNo: 1,
            ustHesapId: null,
            aktifMi: true,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }
}
