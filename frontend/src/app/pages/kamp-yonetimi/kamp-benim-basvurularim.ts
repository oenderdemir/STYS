import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { finalize } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToolbarModule } from 'primeng/toolbar';
import { KampBasvuruDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-benim-basvurularim',
    standalone: true,
    imports: [CommonModule, ButtonModule, DialogModule, TableModule, TagModule, ToolbarModule],
    templateUrl: './kamp-benim-basvurularim.html',
    styleUrl: './kamp-benim-basvurularim.scss'
})
export class KampBenimBasvurularimPage implements OnInit {
    private readonly service = inject(KampYonetimiService);
    private readonly cdr = inject(ChangeDetectorRef);

    kayitlar: KampBasvuruDto[] = [];
    selectedKayit: KampBasvuruDto | null = null;
    detayVisible = false;
    loading = false;
    hataMesaji: string | null = null;

    ngOnInit(): void {
        this.load();
    }

    refresh(): void {
        this.load();
    }

    acDetay(item: KampBasvuruDto): void {
        this.selectedKayit = item;
        this.detayVisible = true;
    }

    durumSeverity(durum: string): 'success' | 'danger' | 'secondary' | 'warn' | 'info' | 'contrast' {
        switch (durum) {
            case 'TahsisEdildi':
                return 'success';
            case 'TahsisEdilemedi':
            case 'Reddedildi':
                return 'danger';
            case 'IptalEdildi':
                return 'secondary';
            default:
                return 'warn';
        }
    }

    private load(): void {
        this.loading = true;
        this.hataMesaji = null;

        this.service.getBenimKampBasvurularim()
            .pipe(finalize(() => {
                this.loading = false;
                this.cdr.detectChanges();
            }))
            .subscribe({
                next: (items) => {
                    this.kayitlar = items;
                    this.cdr.detectChanges();
                },
                error: (error: Error) => {
                    this.kayitlar = [];
                    this.hataMesaji = error.message;
                    this.cdr.detectChanges();
                }
            });
    }
}
