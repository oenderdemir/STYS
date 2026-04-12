import { CommonModule } from '@angular/common';
import { ChangeDetectorRef, Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { CardModule } from 'primeng/card';
import { ChipModule } from 'primeng/chip';
import { DialogModule } from 'primeng/dialog';
import { DividerModule } from 'primeng/divider';
import { InputTextModule } from 'primeng/inputtext';
import { SkeletonModule } from 'primeng/skeleton';
import { TagModule } from 'primeng/tag';
import { MusteriMenuKategoriModel, MusteriMenuModel, MusteriMenuUrunModel } from './musteri-menu.model';
import { MusteriMenuService } from './musteri-menu.service';

@Component({
    selector: 'app-musteri-menu',
    standalone: true,
    imports: [CommonModule, FormsModule, CardModule, DialogModule, ButtonModule, InputTextModule, ChipModule, TagModule, SkeletonModule, DividerModule],
    templateUrl: './musteri-menu.html',
    styleUrl: './musteri-menu.scss'
})
export class MusteriMenuPage implements OnInit {
    private readonly route = inject(ActivatedRoute);
    private readonly service = inject(MusteriMenuService);
    private readonly cdr = inject(ChangeDetectorRef);
    loading = true;
    errorMessage: string | null = null;
    menu: MusteriMenuModel | null = null;

    seciliKategoriId: number | null = null;
    aramaMetni = '';
    gorunenKategoriler: MusteriMenuKategoriModel[] = [];
    seciliUrun: MusteriMenuUrunModel | null = null;
    urunDetayDialogVisible = false;
    private readonly urunGorselleri = [
        '/demo/images/product/blue-band.jpg',
        '/demo/images/product/green-t-shirt.jpg',
        '/demo/images/product/brown-purse.jpg',
        '/demo/images/product/game-controller.jpg',
        '/demo/images/product/mini-speakers.jpg',
        '/demo/images/product/grey-t-shirt.jpg',
        '/demo/images/product/headphones.jpg',
        '/demo/images/product/blue-t-shirt.jpg'
    ];

    ngOnInit(): void {
        const idParam = this.route.snapshot.paramMap.get('restoranId');
        const restoranId = Number(idParam);
        if (!restoranId || restoranId <= 0) {
            this.loading = false;
            this.errorMessage = 'Gecerli restoran bulunamadi.';
            return;
        }

        setTimeout(() => this.load(restoranId), 0);
    }

    selectKategori(kategoriId: number | null): void {
        this.seciliKategoriId = kategoriId;
        this.updateGorunenKategoriler();
    }

    onAramaDegisti(): void {
        this.updateGorunenKategoriler();
    }

    openUrunDetay(urun: MusteriMenuUrunModel): void {
        this.seciliUrun = urun;
        this.urunDetayDialogVisible = true;
    }

    getKategoriAdi(kategoriId: number | null | undefined): string {
        if (!kategoriId || !this.menu) {
            return '-';
        }

        return this.menu.kategoriler.find((x) => x.id === kategoriId)?.ad ?? '-';
    }

    getUrunGorselUrl(urun: MusteriMenuUrunModel): string {
        const idx = Math.abs(urun.id) % this.urunGorselleri.length;
        return this.urunGorselleri[idx];
    }

    onUrunGorselHata(event: Event): void {
        const img = event.target as HTMLImageElement | null;
        if (!img) {
            return;
        }

        img.src = '/demo/images/product/product-placeholder.svg';
    }

    private load(restoranId: number): void {
        this.loading = true;
        this.errorMessage = null;
        this.service
            .getByRestoranId(restoranId)
            .subscribe({
                next: (data) => {
                    setTimeout(() => {
                        this.seciliKategoriId = data.kategoriler[0]?.id ?? null;
                        this.menu = data;
                        this.updateGorunenKategoriler();
                        this.loading = false;
                        this.cdr.detectChanges();
                    });
                },
                error: (error: Error) => {
                    setTimeout(() => {
                        this.errorMessage = error.message;
                        this.loading = false;
                        this.cdr.detectChanges();
                    });
                }
            });
    }

    private updateGorunenKategoriler(): void {
        if (!this.menu) {
            this.gorunenKategoriler = [];
            return;
        }

        const query = this.aramaMetni.trim().toLocaleLowerCase('tr-TR');
        const base = this.seciliKategoriId
            ? this.menu.kategoriler.filter((x) => x.id === this.seciliKategoriId)
            : this.menu.kategoriler;

        if (!query) {
            this.gorunenKategoriler = base;
            return;
        }

        this.gorunenKategoriler = base
            .map((kategori) => ({
                ...kategori,
                urunler: kategori.urunler.filter((urun) => {
                    const ad = (urun.ad ?? '').toLocaleLowerCase('tr-TR');
                    const aciklama = (urun.aciklama ?? '').toLocaleLowerCase('tr-TR');
                    return ad.includes(query) || aciklama.includes(query);
                })
            }))
            .filter((x) => x.urunler.length > 0);
    }

    trackByKategoriId(_: number, item: MusteriMenuKategoriModel): number {
        return item.id;
    }

    trackByUrunId(_: number, item: MusteriMenuUrunModel): number {
        return item.id;
    }
}
