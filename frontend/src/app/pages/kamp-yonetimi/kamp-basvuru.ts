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
    KampBasvuruTercihDto,
    KampBasvuruSahibiTipSecenekDto,
    KampBasvuruTesisSecenekDto,
    KampKonaklamaBirimiSecenekDto,
    KampSecenekDto
} from './kamp-yonetimi.dto';
import { KampYonetimiService } from './kamp-yonetimi.service';

interface KampTercihSatiri {
    tesisId: number;
    tesisAd: string;
    kapasiteOzeti: string;
    donemSecenekleri: KampBasvuruDonemSecenekDto[];
    tercihler: number[];
}

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
    tercihSatirlari: KampTercihSatiri[] = [];

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

    onTercihDegisti(tesisId: number, tercihIndex: number, value: number): void {
        const row = this.tercihSatirlari.find((x) => x.tesisId === tesisId);
        if (!row) {
            return;
        }

        const parsed = Number(value) || 0;
        if (parsed > 0) {
            for (let i = 0; i < row.tercihler.length; i += 1) {
                if (i !== tercihIndex && row.tercihler[i] === parsed) {
                    row.tercihler[i] = 0;
                }
            }
        }
        row.tercihler[tercihIndex] = parsed;

        this.syncPrimarySelectionFromTercihler();
        this.triggerPreview();
    }

    getTercihDegeri(tesisId: number, tercihIndex: number): number {
        const row = this.tercihSatirlari.find((x) => x.tesisId === tesisId);
        return row?.tercihler[tercihIndex] ?? 0;
    }

    isDonemSecilebilir(row: KampTercihSatiri, tercihIndex: number, kampDonemiId: number): boolean {
        const mevcutDeger = row.tercihler[tercihIndex];
        if (mevcutDeger === kampDonemiId) {
            return true;
        }

        return !row.tercihler.some((x, i) => i !== tercihIndex && x === kampDonemiId);
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
                    this.tercihSatirlari = this.buildTercihSatirlari(baglam);
                    const firstTercihRow = this.tercihSatirlari[0];
                    if (firstTercihRow && firstTercihRow.donemSecenekleri.length > 0) {
                        firstTercihRow.tercihler[0] = firstTercihRow.donemSecenekleri[0].id;
                    }
                    const firstDonem = baglam.donemler[0];
                    const firstSahipTipi = baglam.basvuruSahibiTipleri[0];

                    if (firstDonem) {
                        this.form.patchValue({ kampDonemiId: firstDonem.id }, { emitEvent: false });
                        this.updateDonemSelection(firstDonem.id);
                    }
                    this.syncPrimarySelectionFromTercihler();

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
        const seciliTesisId = Number(this.form.get('tesisId')?.value) || 0;
        const seciliDonemTesisi = this.seciliDonem?.tesisler.find((x) => x.tesisId === seciliTesisId) ?? null;
        const firstTesis = seciliDonemTesisi ?? this.seciliDonem?.tesisler[0] ?? null;
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
        const tercihler = this.collectTercihler(raw.konaklamaBirimiTipi ?? '');
        const ilkTercih = tercihler[0];

        if (!ilkTercih || !raw.konaklamaBirimiTipi || !raw.basvuruSahibiTipi) {
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
            kampDonemiId: ilkTercih.kampDonemiId,
            tesisId: ilkTercih.tesisId,
            tercihler,
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

    getTesisKapasiteOzeti(tesis: KampBasvuruTesisSecenekDto): string {
        const birimler = tesis.birimler ?? [];
        if (birimler.length === 0) {
            return '-';
        }

        return birimler
            .map((x) => `${x.minimumKisi}-${x.maksimumKisi}`)
            .filter((x, i, arr) => arr.indexOf(x) === i)
            .sort((a, b) => a.localeCompare(b, 'tr'))
            .join(', ');
    }

    getMusaitTesisler(): Array<{ tesisId: number; tesisAd: string; kapasiteOzeti: string; donemSayisi: number }> {
        return this.tercihSatirlari.map((x) => ({
            tesisId: x.tesisId,
            tesisAd: x.tesisAd,
            kapasiteOzeti: x.kapasiteOzeti,
            donemSayisi: x.donemSecenekleri.length
        }));
    }

    private buildTercihSatirlari(baglam: KampBasvuruBaglamDto): KampTercihSatiri[] {
        const map = new Map<number, KampTercihSatiri>();
        for (const donem of baglam.donemler ?? []) {
            for (const tesis of donem.tesisler ?? []) {
                const mevcut = map.get(tesis.tesisId) ?? {
                    tesisId: tesis.tesisId,
                    tesisAd: tesis.tesisAd,
                    kapasiteOzeti: this.getTesisKapasiteOzeti(tesis),
                    donemSecenekleri: [],
                    tercihler: [0, 0, 0, 0, 0]
                };

                mevcut.donemSecenekleri.push(donem);
                map.set(tesis.tesisId, mevcut);
            }
        }

        return Array.from(map.values())
            .map((x) => ({
                ...x,
                donemSecenekleri: x.donemSecenekleri
                    .sort((a, b) => {
                        const dateCmp = a.konaklamaBaslangicTarihi.localeCompare(b.konaklamaBaslangicTarihi);
                        return dateCmp !== 0 ? dateCmp : a.ad.localeCompare(b.ad, 'tr');
                    })
            }))
            .sort((a, b) => a.tesisAd.localeCompare(b.tesisAd, 'tr'));
    }

    private collectTercihler(defaultKonaklamaBirimi: string): KampBasvuruTercihDto[] {
        const result: KampBasvuruTercihDto[] = [];
        const used = new Set<string>();

        for (let tercihIndex = 0; tercihIndex < 5; tercihIndex += 1) {
            for (const row of this.tercihSatirlari) {
                const kampDonemiId = Number(row.tercihler[tercihIndex]) || 0;
                if (kampDonemiId <= 0) {
                    continue;
                }

                const key = `${kampDonemiId}-${row.tesisId}`;
                if (used.has(key)) {
                    continue;
                }

                used.add(key);
                result.push({
                    tercihSirasi: result.length + 1,
                    kampDonemiId,
                    tesisId: row.tesisId,
                    konaklamaBirimiTipi: defaultKonaklamaBirimi
                });
            }
        }

        return result;
    }

    private syncPrimarySelectionFromTercihler(): void {
        const tercihler = this.collectTercihler(this.form.get('konaklamaBirimiTipi')?.value ?? '');
        const ilk = tercihler[0];
        if (!ilk) {
            this.form.patchValue({ kampDonemiId: 0, tesisId: 0 }, { emitEvent: false });
            this.seciliDonem = null;
            this.seciliTesis = null;
            this.seciliBirimler = [];
            return;
        }

        this.form.patchValue(
            { kampDonemiId: ilk.kampDonemiId, tesisId: ilk.tesisId },
            { emitEvent: false });
        this.updateDonemSelection(ilk.kampDonemiId);
        this.updateTesisSelection(ilk.tesisId);
    }
}
