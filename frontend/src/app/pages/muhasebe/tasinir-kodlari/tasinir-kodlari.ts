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
import { ImportTasinirKodlariRequest, TasinirKodModel } from './tasinir-kodlari.dto';
import { TasinirKodlariService } from './tasinir-kodlari.service';

type TasinirKodTreeRow = TasinirKodModel & { isVirtual?: boolean };

@Component({
    selector: 'app-tasinir-kodlari-page',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputNumberModule, InputTextModule, SelectModule, TagModule, ToastModule, ToolbarModule, TreeTableModule],
    templateUrl: './tasinir-kodlari.html',
    providers: [MessageService, ConfirmationService]
})
export class TasinirKodlariPage implements OnInit {
    private readonly service = inject(TasinirKodlariService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);
    private readonly cdr = inject(ChangeDetectorRef);
    loading = false;
    saving = false;
    dialogVisible = false;
    dialogMode: 'create' | 'edit' = 'create';

    records: TasinirKodModel[] = [];
    treeRecords: TreeNode<TasinirKodTreeRow>[] = [];
    parentOptions: Array<{ label: string; value: number }> = [];
    currentParentLabel = '-';
    model: TasinirKodModel = this.createEmpty();

    ngOnInit(): void {
        setTimeout(() => this.load());
    }

    load(): void {
        this.loading = true;
        this.service.getAll().pipe(finalize(() => {
            this.loading = false;
            this.cdr.detectChanges();
        })).subscribe({
            next: (items) => {
                this.records = [...items].sort((a, b) => a.tamKod.localeCompare(b.tamKod));
                this.treeRecords = this.buildTree(this.records);
                this.parentOptions = this.buildParentOptions();
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
        this.parentOptions = this.buildParentOptions();
        this.currentParentLabel = '-';
        this.dialogVisible = true;
    }

    openEdit(item: TasinirKodModel): void {
        if (!item.id) {
            return;
        }

        this.dialogMode = 'edit';
        this.model = { ...item };
        this.parentOptions = this.buildParentOptions(item.id ?? null);
        this.currentParentLabel = item.ustKodId ? this.parentOptions.find((x) => x.value === item.ustKodId)?.label ?? '-' : '-';
        this.dialogVisible = true;
    }

    save(): void {
        if (!this.model.tamKod?.trim() || !this.model.kod?.trim() || !this.model.ad?.trim()) {
            this.messageService.add({ severity: UiSeverity.Warn, summary: 'Eksik Bilgi', detail: 'Tam kod, kod ve ad zorunludur.' });
            return;
        }

        this.saving = true;
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

        request$.pipe(finalize(() => (this.saving = false))).subscribe({
            next: () => {
                this.dialogVisible = false;
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Basarili', detail: 'Kayit kaydedildi.' });
            },
            error: (error: unknown) => this.showError(error)
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
                    },
                    error: (error: unknown) => this.showError(error)
                });
            }
        });
    }

    importOrnekVeri(): void {
        const payload: ImportTasinirKodlariRequest = {
            mevcutlariGuncelle: true,
            pasiflestirilmeyenleriPasifYap: false,
            satirlar: [
                { tamKod: '150.01', kod: '01', ad: 'Tuketim Malzemeleri', duzeyNo: 2, aktifMi: true },
                { tamKod: '150.01.01', kod: '01', ad: 'Temizlik Malzemeleri', duzeyNo: 3, ustTamKod: '150.01', aktifMi: true },
                { tamKod: '253.01', kod: '01', ad: 'Makine ve Cihazlar', duzeyNo: 2, aktifMi: true }
            ]
        };

        this.service.import(payload).subscribe({
            next: (sonuc) => {
                this.load();
                this.messageService.add({ severity: UiSeverity.Success, summary: 'Import Tamamlandi', detail: `Eklenen: ${sonuc.eklenen}, Guncellenen: ${sonuc.guncellenen}` });
            },
            error: (error: unknown) => this.showError(error)
        });
    }

    private createEmpty(): TasinirKodModel {
        return {
            tamKod: '',
            kod: '',
            ad: '',
            duzeyNo: 1,
            aktifMi: true,
            ustKodId: null,
            aciklama: null
        };
    }

    private showError(error: unknown): void {
        const message = tryReadApiMessage(error as HttpErrorResponse) ?? 'Islem basarisiz.';
        this.messageService.add({ severity: UiSeverity.Error, summary: 'Hata', detail: message });
    }

    onParentChange(value: number | null): void {
        this.model.ustKodId = value;
        this.currentParentLabel = value ? this.parentOptions.find((x) => x.value === value)?.label ?? '-' : '-';
    }

    private buildTree(items: TasinirKodModel[]): TreeNode<TasinirKodTreeRow>[] {
        const map = new Map<number, TreeNode<TasinirKodTreeRow>>();
        const byTamKod = new Map<string, TreeNode<TasinirKodTreeRow>>();
        const roots: TreeNode<TasinirKodTreeRow>[] = [];
        let virtualSeed = -1;

        for (const item of items) {
            if (!item.id) {
                continue;
            }

            const node: TreeNode<TasinirKodTreeRow> = {
                key: item.id.toString(),
                data: item,
                children: []
            };

            map.set(item.id, node);
            byTamKod.set(item.tamKod, node);
        }

        for (const item of items) {
            if (!item.id) {
                continue;
            }

            const node = map.get(item.id);
            if (!node) {
                continue;
            }

            let parentNode: TreeNode<TasinirKodTreeRow> | null = null;

            if (item.ustKodId && map.has(item.ustKodId)) {
                parentNode = map.get(item.ustKodId)!;
            } else {
                // UstKodId eksikse ara kirilimlari olusturup hiyerarsiyi tamamla.
                const segments = item.tamKod.split('.').filter((x) => x.length > 0);
                if (segments.length > 1) {
                    let currentParent: TreeNode<TasinirKodTreeRow> | null = null;
                    const chain: string[] = [];

                    for (let i = 0; i < segments.length - 1; i += 1) {
                        chain.push(segments[i]);
                        const partialTamKod = chain.join('.');
                        let chainNode = byTamKod.get(partialTamKod);

                        if (!chainNode) {
                            const partialKod = segments[i];
                            chainNode = {
                                key: `virtual-${Math.abs(virtualSeed)}`,
                                data: {
                                    id: virtualSeed--,
                                    tamKod: partialTamKod,
                                    kod: partialKod,
                                    ad: '(Ara Kirilim)',
                                    duzeyNo: i + 1,
                                    aktifMi: true,
                                    ustKodId: this.tryGetNodeId(currentParent),
                                    isVirtual: true
                                },
                                children: []
                            };

                            byTamKod.set(partialTamKod, chainNode);
                            if (currentParent) {
                                currentParent.children!.push(chainNode);
                            } else {
                                roots.push(chainNode);
                            }
                        }

                        currentParent = chainNode;
                    }

                    parentNode = currentParent;
                }
            }

            if (parentNode) {
                parentNode.children!.push(node);
                continue;
            }

            roots.push(node);
        }

        const sortNodes = (nodes: TreeNode<TasinirKodTreeRow>[]): void => {
            nodes.sort((a, b) => (a.data?.tamKod ?? '').localeCompare(b.data?.tamKod ?? ''));
            for (const node of nodes) {
                if (node.children?.length) {
                    sortNodes(node.children);
                }
            }
        };

        sortNodes(roots);
        return roots;
    }

    private buildParentOptions(excludeId: number | null = null): Array<{ label: string; value: number }> {
        return this.records
            .filter((x) => x.id && x.id !== excludeId)
            .map((x) => ({ label: `${x.tamKod} - ${x.ad}`, value: x.id! }));
    }

    private tryGetNodeId(node: TreeNode<TasinirKodTreeRow> | null): number | null {
        if (!node || !node.data || typeof node.data.id !== 'number') {
            return null;
        }

        return node.data.id;
    }
}
