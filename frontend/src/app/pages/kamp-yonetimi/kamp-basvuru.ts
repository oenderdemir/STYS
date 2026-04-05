import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, Subject, takeUntil } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { KampBasvuruBaglamDto, KampBasvuruDto, KampBasvuruOnizlemeDto, KampBasvuruRequestDto, KampBasvuruTesisSecenekDto, KampBasvuruDonemSecenekDto, KampKonaklamaBirimiSecenekDto } from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-basvuru',
    standalone: true,
    imports: [CommonModule, ReactiveFormsModule, RouterLink, ButtonModule],
    templateUrl: './kamp-basvuru.html',
    styleUrl: './kamp-basvuru.scss'
})
export class KampBasvuruPage implements OnInit, OnDestroy {
    private readonly fb = inject(FormBuilder);
    private readonly kampService = inject(KampYonetimiService);
    private readonly destroy$ = new Subject<void>();

    readonly basvuruSahibiTipleri = [
        { label: 'Tarim ve Orman Personeli', value: 'TarimOrmanPersoneli' },
        { label: 'Tarim ve Orman Emeklisi', value: 'TarimOrmanEmeklisi' },
        { label: 'Bagli / Ilgili Kurulus Personeli', value: 'BagliKurulusPersoneli' },
        { label: 'Bagli / Ilgili Kurulus Emeklisi', value: 'BagliKurulusEmeklisi' },
        { label: 'Diger Kamu Personeli', value: 'DigerKamuPersoneli' },
        { label: 'Diger Kamu Emeklisi', value: 'DigerKamuEmeklisi' },
        { label: 'Diger', value: 'Diger' }
    ];

    readonly katilimciTipleri = [
        { label: 'Kamu', value: 'Kamu' },
        { label: 'Sehit/Gazi/Malul', value: 'SehitGaziMalul' },
        { label: 'Diger', value: 'Diger' }
    ];

    readonly akrabalikTipleri = [
        { label: 'Basvuru Sahibi', value: 'BasvuruSahibi' },
        { label: 'Es', value: 'Es' },
        { label: 'Cocuk', value: 'Cocuk' },
        { label: 'Anne', value: 'Anne' },
        { label: 'Baba', value: 'Baba' },
        { label: 'Kardes', value: 'Kardes' },
        { label: 'Diger', value: 'Diger' }
    ];

    baglam: KampBasvuruBaglamDto | null = null;
    seciliDonem: KampBasvuruDonemSecenekDto | null = null;
    seciliTesis: KampBasvuruTesisSecenekDto | null = null;
    seciliBirimler: KampKonaklamaBirimiSecenekDto[] = [];
    onizleme: KampBasvuruOnizlemeDto | null = null;
    benimBasvurularim: KampBasvuruDto[] = [];
    yukleniyor = false;
    kaydediliyor = false;
    hataMesaji: string | null = null;

    readonly form = this.fb.group({
        kampDonemiId: [0, Validators.required],
        tesisId: [0, Validators.required],
        konaklamaBirimiTipi: ['', Validators.required],
        basvuruSahibiTipi: ['TarimOrmanPersoneli', Validators.required],
        hizmetYili: [0, [Validators.required, Validators.min(0)]],
        kamp2023tenFaydalandiMi: [false],
        kamp2024tenFaydalandiMi: [false],
        evcilHayvanGetirecekMi: [false],
        buzdolabiTalepEdildiMi: [false],
        televizyonTalepEdildiMi: [false],
        klimaTalepEdildiMi: [false],
        katilimcilar: this.fb.array([])
    });

    get katilimcilar(): FormArray<FormGroup> {
        return this.form.get('katilimcilar') as FormArray<FormGroup>;
    }

    ngOnInit(): void {
        this.katilimcilar.push(this.createKatilimciForm(true));
        this.loadBaglam();
        this.loadBasvurular();

        this.form.get('kampDonemiId')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value) => {
            this.updateDonemSelection(Number(value));
            this.triggerPreview();
        });

        this.form.get('tesisId')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value) => {
            this.updateTesisSelection(Number(value));
            this.triggerPreview();
        });

        this.form.get('basvuruSahibiTipi')?.valueChanges.pipe(takeUntil(this.destroy$)).subscribe((value) => {
            const first = this.katilimcilar.at(0);
            if (first) {
                first.patchValue({ katilimciTipi: value === 'Diger' ? 'Diger' : 'Kamu' }, { emitEvent: false });
            }
            this.triggerPreview();
        });

        this.form.valueChanges.pipe(debounceTime(350), takeUntil(this.destroy$)).subscribe(() => this.triggerPreview());
    }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    addKatilimci(): void {
        this.katilimcilar.push(this.createKatilimciForm(false));
    }

    removeKatilimci(index: number): void {
        if (this.katilimcilar.length === 1) {
            return;
        }

        this.katilimcilar.removeAt(index);
        this.triggerPreview();
    }

    submit(): void {
        const payload = this.buildRequest();
        if (!payload) {
            this.hataMesaji = 'Basvuru formu eksik veya gecersiz.';
            return;
        }

        this.kaydediliyor = true;
        this.hataMesaji = null;
        this.kampService.createKampBasvurusu(payload)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (result) => {
                    this.benimBasvurularim = [result, ...this.benimBasvurularim];
                    this.kaydediliyor = false;
                },
                error: (error: Error) => {
                    this.hataMesaji = error.message;
                    this.kaydediliyor = false;
                }
            });
    }

    private loadBaglam(): void {
        this.yukleniyor = true;
        this.kampService.getKampBasvuruBaglam()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (baglam) => {
                    this.baglam = baglam;
                    const firstDonem = baglam.donemler[0];
                    if (firstDonem) {
                        this.form.patchValue({ kampDonemiId: firstDonem.id }, { emitEvent: false });
                        this.updateDonemSelection(firstDonem.id);
                    }
                    this.yukleniyor = false;
                    this.triggerPreview();
                },
                error: (error: Error) => {
                    this.hataMesaji = error.message;
                    this.yukleniyor = false;
                }
            });
    }

    private loadBasvurular(): void {
        this.kampService.getBenimKampBasvurularim()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (items) => this.benimBasvurularim = items,
                error: () => {
                    this.benimBasvurularim = [];
                }
            });
    }

    private updateDonemSelection(donemId: number): void {
        this.seciliDonem = this.baglam?.donemler.find((x) => x.id === donemId) ?? null;
        const firstTesis = this.seciliDonem?.tesisler[0] ?? null;
        this.form.patchValue({ tesisId: firstTesis?.tesisId ?? 0 }, { emitEvent: false });
        this.updateTesisSelection(firstTesis?.tesisId ?? 0);
    }

    private updateTesisSelection(tesisId: number): void {
        this.seciliTesis = this.seciliDonem?.tesisler.find((x) => x.tesisId === tesisId) ?? null;
        this.seciliBirimler = this.seciliTesis?.birimler ?? [];
        const firstBirim = this.seciliBirimler[0]?.kod ?? '';
        this.form.patchValue({ konaklamaBirimiTipi: firstBirim }, { emitEvent: false });
    }

    private createKatilimciForm(basvuruSahibiMi: boolean): FormGroup {
        return this.fb.group({
            adSoyad: ['', Validators.required],
            tcKimlikNo: [''],
            dogumTarihi: ['', Validators.required],
            basvuruSahibiMi: [basvuruSahibiMi],
            katilimciTipi: [basvuruSahibiMi ? 'Kamu' : 'Kamu', Validators.required],
            akrabalikTipi: [basvuruSahibiMi ? 'BasvuruSahibi' : 'Es', Validators.required],
            kimlikBilgileriDogrulandiMi: [basvuruSahibiMi],
            yemekTalepEdiyorMu: [true]
        });
    }

    private triggerPreview(): void {
        const payload = this.buildRequest();
        if (!payload) {
            this.onizleme = null;
            return;
        }

        this.kampService.onizleKampBasvurusu(payload)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (result) => this.onizleme = result,
                error: () => this.onizleme = null
            });
    }

    private buildRequest(): KampBasvuruRequestDto | null {
        if (this.form.invalid) {
            return null;
        }

        const raw = this.form.getRawValue();
        if (!raw.kampDonemiId || !raw.tesisId || !raw.konaklamaBirimiTipi) {
            return null;
        }

        const katilimcilar = (raw.katilimcilar ?? []) as Array<{
            adSoyad?: string | null;
            tcKimlikNo?: string | null;
            dogumTarihi?: string | null;
            basvuruSahibiMi?: boolean | null;
            katilimciTipi?: string | null;
            akrabalikTipi?: string | null;
            kimlikBilgileriDogrulandiMi?: boolean | null;
            yemekTalepEdiyorMu?: boolean | null;
        }>;

        return {
            kampDonemiId: raw.kampDonemiId ?? 0,
            tesisId: raw.tesisId ?? 0,
            konaklamaBirimiTipi: raw.konaklamaBirimiTipi ?? '',
            basvuruSahibiTipi: raw.basvuruSahibiTipi ?? 'TarimOrmanPersoneli',
            hizmetYili: raw.hizmetYili ?? 0,
            kamp2023tenFaydalandiMi: raw.kamp2023tenFaydalandiMi ?? false,
            kamp2024tenFaydalandiMi: raw.kamp2024tenFaydalandiMi ?? false,
            evcilHayvanGetirecekMi: raw.evcilHayvanGetirecekMi ?? false,
            buzdolabiTalepEdildiMi: raw.buzdolabiTalepEdildiMi ?? false,
            televizyonTalepEdildiMi: raw.televizyonTalepEdildiMi ?? false,
            klimaTalepEdildiMi: raw.klimaTalepEdildiMi ?? false,
            katilimcilar: katilimcilar.map((x) => ({
                adSoyad: x.adSoyad ?? '',
                tcKimlikNo: x.tcKimlikNo ?? null,
                dogumTarihi: x.dogumTarihi ?? '',
                basvuruSahibiMi: !!x.basvuruSahibiMi,
                katilimciTipi: x.katilimciTipi ?? 'Kamu',
                akrabalikTipi: x.akrabalikTipi ?? 'Diger',
                kimlikBilgileriDogrulandiMi: !!x.kimlikBilgileriDogrulandiMi,
                yemekTalepEdiyorMu: !!x.yemekTalepEdiyorMu
            }))
        };
    }
}
