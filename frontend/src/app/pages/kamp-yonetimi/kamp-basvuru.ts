import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormArray, FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { debounceTime, Subject, takeUntil } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { AuthService } from '../auth';
import {
    KampAkrabalikTipiSecenekDto,
    KampBasvuruBaglamDto,
    KampBasvuruDonemSecenekDto,
    KampBasvuruDto,
    KampBasvuruOnizlemeDto,
    KampBasvuruRequestDto,
    KampBasvuruSahibiTipSecenekDto,
    KampBasvuruTesisSecenekDto,
    KampKonaklamaBirimiSecenekDto,
    KampSecenekDto
} from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

@Component({
    selector: 'app-kamp-basvuru',
    standalone: true,
    imports: [CommonModule, FormsModule, ReactiveFormsModule, RouterLink, ButtonModule],
    templateUrl: './kamp-basvuru.html',
    styleUrl: './kamp-basvuru.scss'
})
export class KampBasvuruPage implements OnInit, OnDestroy {
    private readonly fb = inject(FormBuilder);
    private readonly kampService = inject(KampYonetimiService);
    private readonly authService = inject(AuthService);
    private readonly destroy$ = new Subject<void>();

    baglam: KampBasvuruBaglamDto | null = null;
    seciliDonem: KampBasvuruDonemSecenekDto | null = null;
    seciliTesis: KampBasvuruTesisSecenekDto | null = null;
    seciliBirimler: KampKonaklamaBirimiSecenekDto[] = [];
    onizleme: KampBasvuruOnizlemeDto | null = null;
    benimBasvurularim: KampBasvuruDto[] = [];
    sonBasvuru: KampBasvuruDto | null = null;
    sorgulananBasvuru: KampBasvuruDto | null = null;
    seciliGecmisKatilimYillari = new Set<number>();
    sorguBasvuruNo = '';
    yukleniyor = false;
    kaydediliyor = false;
    sorgulaniyor = false;
    hataMesaji: string | null = null;
    sorguHataMesaji: string | null = null;

    readonly form = this.fb.group({
        kampDonemiId: [0, Validators.required],
        tesisId: [0, Validators.required],
        konaklamaBirimiTipi: ['', Validators.required],
        basvuruSahibiTipi: ['', Validators.required],
        hizmetYili: [0, [Validators.required, Validators.min(0)]],
        evcilHayvanGetirecekMi: [false],
        buzdolabiTalepEdildiMi: [false],
        televizyonTalepEdildiMi: [false],
        klimaTalepEdildiMi: [false],
        katilimcilar: this.fb.array([])
    });

    get katilimcilar(): FormArray<FormGroup> {
        return this.form.get('katilimcilar') as FormArray<FormGroup>;
    }

    get basvuruSahibiTipleri(): KampBasvuruSahibiTipSecenekDto[] {
        return this.baglam?.basvuruSahibiTipleri ?? [];
    }

    get katilimciTipleri(): KampSecenekDto[] {
        return this.baglam?.katilimciTipleri ?? [];
    }

    get akrabalikTipleri(): KampAkrabalikTipiSecenekDto[] {
        return this.baglam?.akrabalikTipleri ?? [];
    }

    get seciliGecmisYilSecenekleri(): number[] {
        return this.seciliDonem?.gecmisKatilimYillari ?? [];
    }

    get isAuthenticated(): boolean {
        return this.authService.isAuthenticated();
    }

    ngOnInit(): void {
        this.katilimcilar.push(this.createKatilimciForm(true));
        this.loadBaglam();
        if (this.isAuthenticated) {
            this.loadBasvurular();
        }

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
                first.patchValue({ katilimciTipi: this.getVarsayilanKatilimciTipiKodu(value ?? '') }, { emitEvent: false });
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
        this.triggerPreview();
    }

    removeKatilimci(index: number): void {
        if (this.katilimcilar.length === 1) {
            return;
        }

        this.katilimcilar.removeAt(index);
        this.triggerPreview();
    }

    toggleGecmisKatilimYili(yil: number, checked: boolean): void {
        if (checked) {
            this.seciliGecmisKatilimYillari.add(yil);
        } else {
            this.seciliGecmisKatilimYillari.delete(yil);
        }

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
                    this.sonBasvuru = result;
                    this.sorgulananBasvuru = result;
                    this.sorguBasvuruNo = result.basvuruNo;
                    this.syncGecmisKatilimYillari(result.gecmisKatilimYillari ?? []);
                    this.benimBasvurularim = [result, ...this.benimBasvurularim];
                    this.kaydediliyor = false;
                },
                error: (error: Error) => {
                    this.hataMesaji = error.message;
                    this.kaydediliyor = false;
                }
            });
    }

    sorgulaBasvuru(): void {
        const basvuruNo = this.sorguBasvuruNo.trim();
        if (!basvuruNo) {
            this.sorguHataMesaji = 'Basvuru numarasi giriniz.';
            this.sorgulananBasvuru = null;
            return;
        }

        this.sorgulaniyor = true;
        this.sorguHataMesaji = null;
        this.kampService.getKampBasvuruByBasvuruNo(basvuruNo)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (result) => {
                    this.sorgulananBasvuru = result;
                    this.sorgulaniyor = false;
                },
                error: (error: Error) => {
                    this.sorguHataMesaji = error.message;
                    this.sorgulananBasvuru = null;
                    this.sorgulaniyor = false;
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
                    const firstSahipTipi = baglam.basvuruSahibiTipleri[0];

                    if (firstDonem) {
                        this.form.patchValue({ kampDonemiId: firstDonem.id }, { emitEvent: false });
                        this.updateDonemSelection(firstDonem.id);
                    }

                    if (firstSahipTipi) {
                        this.form.patchValue({ basvuruSahibiTipi: firstSahipTipi.kod }, { emitEvent: false });
                        const first = this.katilimcilar.at(0);
                        if (first) {
                            first.patchValue({ katilimciTipi: this.getVarsayilanKatilimciTipiKodu(firstSahipTipi.kod) }, { emitEvent: false });
                        }
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
        this.syncGecmisKatilimYillari(Array.from(this.seciliGecmisKatilimYillari));
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
            katilimciTipi: [basvuruSahibiMi ? this.getVarsayilanKatilimciTipiKodu(this.form.get('basvuruSahibiTipi')?.value ?? '') : this.getVarsayilanEkKatilimciTipiKodu(), Validators.required],
            akrabalikTipi: [basvuruSahibiMi ? this.getBasvuruSahibiAkrabalikKodu() : this.getVarsayilanEkKatilimciAkrabalikKodu(), Validators.required],
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
                next: (result) => {
                    this.onizleme = result;
                    this.syncGecmisKatilimYillari(result.gecmisKatilimYillari ?? []);
                },
                error: () => this.onizleme = null
            });
    }

    private buildRequest(): KampBasvuruRequestDto | null {
        if (this.form.invalid) {
            return null;
        }

        const raw = this.form.getRawValue();
        if (!raw.kampDonemiId || !raw.tesisId || !raw.konaklamaBirimiTipi || !raw.basvuruSahibiTipi) {
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
            basvuruSahibiTipi: raw.basvuruSahibiTipi ?? '',
            hizmetYili: raw.hizmetYili ?? 0,
            gecmisKatilimYillari: Array.from(this.seciliGecmisKatilimYillari).sort((a, b) => b - a),
            evcilHayvanGetirecekMi: raw.evcilHayvanGetirecekMi ?? false,
            buzdolabiTalepEdildiMi: raw.buzdolabiTalepEdildiMi ?? false,
            televizyonTalepEdildiMi: raw.televizyonTalepEdildiMi ?? false,
            klimaTalepEdildiMi: raw.klimaTalepEdildiMi ?? false,
            katilimcilar: katilimcilar.map((x) => ({
                adSoyad: x.adSoyad ?? '',
                tcKimlikNo: x.tcKimlikNo ?? null,
                dogumTarihi: x.dogumTarihi ?? '',
                basvuruSahibiMi: !!x.basvuruSahibiMi,
                katilimciTipi: x.katilimciTipi ?? this.getVarsayilanEkKatilimciTipiKodu(),
                akrabalikTipi: x.akrabalikTipi ?? this.getVarsayilanEkKatilimciAkrabalikKodu(),
                kimlikBilgileriDogrulandiMi: !!x.kimlikBilgileriDogrulandiMi,
                yemekTalepEdiyorMu: !!x.yemekTalepEdiyorMu
            }))
        };
    }

    private syncGecmisKatilimYillari(yillar: number[]): void {
        const izinliYillar = new Set(this.seciliGecmisYilSecenekleri);
        const yeniSecim = new Set<number>();

        for (const yil of yillar) {
            if (izinliYillar.has(yil)) {
                yeniSecim.add(yil);
            }
        }

        this.seciliGecmisKatilimYillari = yeniSecim;
    }

    private getVarsayilanKatilimciTipiKodu(basvuruSahibiTipiKodu: string): string {
        const sahipTipi = this.basvuruSahibiTipleri.find((x) => x.kod === basvuruSahibiTipiKodu);
        return sahipTipi?.varsayilanKatilimciTipiKodu
            ?? this.katilimciTipleri[0]?.kod
            ?? '';
    }

    private getVarsayilanEkKatilimciTipiKodu(): string {
        return this.katilimciTipleri[0]?.kod ?? '';
    }

    private getBasvuruSahibiAkrabalikKodu(): string {
        return this.akrabalikTipleri.find((x) => x.basvuruSahibiAkrabaligiMi)?.kod
            ?? this.akrabalikTipleri[0]?.kod
            ?? '';
    }

    private getVarsayilanEkKatilimciAkrabalikKodu(): string {
        return this.akrabalikTipleri.find((x) => !x.basvuruSahibiAkrabaligiMi)?.kod
            ?? this.getBasvuruSahibiAkrabalikKodu();
    }
}
