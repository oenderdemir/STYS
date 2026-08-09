import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { ConfirmationService, MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToolbarModule } from 'primeng/toolbar';
import { SelectModule } from 'primeng/select';
import { PosCihaziDto, PosCihaziKaydetRequest, SaglayiciLabels } from './pos-yonetimi.dto';
import { PosYonetimiService } from './pos-yonetimi.service';

@Component({
    selector: 'app-pos-yonetimi',
    standalone: true,
    imports: [CommonModule, FormsModule, ButtonModule, ConfirmDialogModule, DialogModule, InputTextModule, TableModule, TagModule, ToastModule, ToolbarModule, SelectModule],
    providers: [ConfirmationService, MessageService],
    templateUrl: './pos-yonetimi.html'
})
export class PosYonetimiComponent implements OnInit {
    private readonly service = inject(PosYonetimiService);
    private readonly messageService = inject(MessageService);
    private readonly confirmationService = inject(ConfirmationService);

    cihazlar = signal<PosCihaziDto[]>([]);
    loading = signal(false);
    dialogVisible = signal(false);
    submitted = signal(false);
    saglayiciOptions = [{ label: 'PAVO', value: 0 }, { label: 'Diğer', value: 1 }];

    form: PosCihaziKaydetRequest & { id?: number } = { tesisId: 0, saglayici: 0, ad: '', seriNo: '' };

    ngOnInit(): void { this.load(); }

    load(): void {
        this.loading.set(true);
        this.service.getAll().pipe(finalize(() => this.loading.set(false))).subscribe({
            next: d => this.cihazlar.set(d),
            error: e => this.messageService.add({ severity: 'error', summary: 'Hata', detail: e.message })
        });
    }

    openNew(): void {
        this.form = { tesisId: 0, saglayici: 0, ad: '', seriNo: '' };
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    edit(cihaz: PosCihaziDto): void {
        this.form = { id: cihaz.id, tesisId: cihaz.tesisId, agentId: cihaz.agentId, saglayici: cihaz.saglayici, ad: cihaz.ad, seriNo: cihaz.seriNo, ipAdresi: cihaz.ipAdresi, httpPort: cihaz.httpPort, httpsPort: cihaz.httpsPort, fingerprint: cihaz.fingerprint, aciklama: cihaz.aciklama };
        this.submitted.set(false);
        this.dialogVisible.set(true);
    }

    save(): void {
        this.submitted.set(true);
        if (!this.form.ad || !this.form.seriNo) return;
        const req: PosCihaziKaydetRequest = { tesisId: this.form.tesisId, agentId: this.form.agentId, saglayici: this.form.saglayici, ad: this.form.ad, seriNo: this.form.seriNo, ipAdresi: this.form.ipAdresi, httpPort: this.form.httpPort, httpsPort: this.form.httpsPort, fingerprint: this.form.fingerprint, aciklama: this.form.aciklama };
        const action$ = this.form.id ? this.service.update(this.form.id, req) : this.service.create(req);
        action$.subscribe({
            next: () => { this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Kaydedildi.' }); this.dialogVisible.set(false); this.load(); },
            error: e => this.messageService.add({ severity: 'error', summary: 'Hata', detail: e.message })
        });
    }

    deleteItem(cihaz: PosCihaziDto): void {
        this.confirmationService.confirm({
            message: `"${cihaz.ad}" cihazını silmek istediğinize emin misiniz?`, header: 'Onay', icon: 'pi pi-exclamation-triangle',
            accept: () => this.service.delete(cihaz.id).subscribe({
                next: () => { this.messageService.add({ severity: 'success', summary: 'Başarılı', detail: 'Cihaz silindi.' }); this.load(); },
                error: e => this.messageService.add({ severity: 'error', summary: 'Hata', detail: e.message })
            })
        });
    }

    getSaglayiciLabel(s: number): string { return SaglayiciLabels[s] ?? '?'; }
}
