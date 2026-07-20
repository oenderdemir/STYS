import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { MessageService } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { PaginatorModule, PaginatorState } from 'primeng/paginator';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ToastModule } from 'primeng/toast';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { AuthService } from '../auth';
import { KbsBildirim, KbsGunlukOzet, KbsTesisAyari } from './kbs-bildirim-merkezi.dto';
import { KbsBildirimMerkeziService } from './kbs-bildirim-merkezi.service';

@Component({ selector: 'app-kbs-bildirim-merkezi', standalone: true, imports: [CommonModule, FormsModule, ButtonModule, InputTextModule, PaginatorModule, SelectModule, TableModule, TagModule, ToastModule, ToggleSwitchModule], templateUrl: './kbs-bildirim-merkezi.html', styleUrl: './kbs-bildirim-merkezi.scss', providers: [MessageService] })
export class KbsBildirimMerkezi implements OnInit {
    private readonly service = inject(KbsBildirimMerkeziService); private readonly messages = inject(MessageService); readonly auth = inject(AuthService);
    tesisler: { label: string; value: number }[] = []; tesisId: number | null = null; durum: string | null = null; kayitlar: KbsBildirim[] = []; toplam = 0; sayfa = 1; sayfaBoyutu = 25; loading = false;
    ozet: KbsGunlukOzet = { basarili: 0, bekleyen: 0, hatali: 0, mudahaleGerekli: 0 }; ayar: KbsTesisAyari | null = null;
    sonExcelManifesti: string | null = null;
    islem: { id: number; tur: 'Islendi' | 'Islenmedi' | 'EgmBasarili' | 'EgmBasarisiz' } | null = null; islemAciklamasi = ''; kurumReferansNo = '';
    readonly durumlar = [null, 'Hazir', 'Gonderiliyor', 'TekrarBekliyor', 'Basarili', 'SonucuBelirsiz', 'MudahaleGerekli', 'DosyaUretildi', 'YuklemeOnayiBekliyor'].map(value => ({ label: value ?? 'Tum durumlar', value }));
    readonly kolluklar = [{ label: 'EGM', value: 'EGM' }, { label: 'Jandarma', value: 'Jandarma' }]; readonly entegrasyonlar = ['Fake', 'Excel', 'Soap'].map(value => ({ label: value, value }));
    ngOnInit(): void { this.service.tesisler().subscribe({ next: xs => { this.tesisler = xs.map(x => ({ label: x.ad, value: x.id })); this.tesisId = this.tesisler[0]?.value ?? null; this.yenile(); }, error: e => this.error(e) }); }
    yenile(): void { this.loading = true; forkJoin({ liste: this.service.liste(this.tesisId, this.durum, this.sayfa, this.sayfaBoyutu), ozet: this.service.ozet(this.tesisId) }).pipe(finalize(() => this.loading = false)).subscribe({ next: x => { this.kayitlar = x.liste.kayitlar; this.toplam = x.liste.toplam; this.ozet = x.ozet; this.loadAyar(); }, error: e => this.error(e) }); }
    tesisDegisti(): void { this.sayfa = 1; this.yenile(); }
    paginate(e: PaginatorState): void { this.sayfa = (e.page ?? 0) + 1; this.sayfaBoyutu = e.rows ?? 25; this.yenile(); }
    retry(id: number): void { this.service.tekrarDene(id).subscribe({ next: () => { this.success('Bildirim tekrar kuyruga alindi.'); this.yenile(); }, error: e => this.error(e) }); }
    excel(tip: 'Giris' | 'Cikis'): void { if (!this.tesisId) return; this.service.excel(this.tesisId, tip).subscribe({ next: r => { this.sonExcelManifesti = r.headers.get('X-Kbs-Manifest-Hash'); const url = URL.createObjectURL(r.body!); const a = document.createElement('a'); a.href = url; a.download = `egm-${tip.toLowerCase()}.xlsx`; a.click(); URL.revokeObjectURL(url); this.success('EGM Excel dosyasi olusturuldu; yukleme onayi verilmeden basarili sayilmaz.'); this.yenile(); }, error: e => this.error(e) }); }
    yuklemeOnayla(): void { if (!this.tesisId || !this.sonExcelManifesti) return; this.service.yuklemeOnayla(this.tesisId, this.sonExcelManifesti).subscribe({ next: () => { this.sonExcelManifesti = null; this.success('Yukleme onayi kaydedildi; bildirimler henuz basarili/dogrulanmis sayilmadi.'); this.yenile(); }, error: e => this.error(e) }); }
    islemAc(id: number, tur: 'Islendi' | 'Islenmedi' | 'EgmBasarili' | 'EgmBasarisiz'): void { this.islem = { id, tur }; this.islemAciklamasi = ''; this.kurumReferansNo = ''; }
    islemKaydet(): void {
        if (!this.islem || !this.islemAciklamasi.trim()) { this.messages.add({ severity: 'warn', summary: 'Aciklama gerekli', detail: 'Hassas veri icermeyen bir mutabakat aciklamasi girin.' }); return; }
        const ref = this.kurumReferansNo.trim() || null; const action = this.islem.tur.startsWith('Egm')
            ? this.service.egmDogrula(this.islem.id, this.islem.tur === 'EgmBasarili', this.islemAciklamasi.trim(), ref)
            : this.service.mutabakat(this.islem.id, this.islem.tur as 'Islendi' | 'Islenmedi', this.islemAciklamasi.trim(), ref);
        action.subscribe({ next: () => { this.islem = null; this.success('Mutabakat sonucu denetim kaydi ile kaydedildi.'); this.yenile(); }, error: e => this.error(e) });
    }
    kaydetAyar(): void { if (!this.ayar) return; this.service.ayarKaydet(this.ayar).subscribe({ next: x => { this.ayar = x; this.success('KBS ayari kaydedildi.'); }, error: e => this.error(e) }); }
    can(permission: string): boolean { return this.auth.hasPermission(permission); }
    severity(durum: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' { if (durum === 'Basarili' || durum === 'Dogrulandi') return 'success'; if (durum === 'MudahaleGerekli') return 'danger'; if (durum === 'SonucuBelirsiz' || durum === 'TekrarBekliyor') return 'warn'; return 'info'; }
    private loadAyar(): void { if (!this.tesisId) { this.ayar = null; return; } this.service.ayar(this.tesisId).subscribe(x => this.ayar = x ?? { tesisId: this.tesisId!, kollukSistemi: 'EGM', entegrasyonTipi: 'Fake', tesisKodu: null, secretReference: null, aktifMi: false, canliGonderimAktifMi: false, sonBaglantiKontrolTarihi: null, sonBaglantiKontrolSonucu: null }); }
    private success(detail: string): void { this.messages.add({ severity: 'success', summary: 'Basarili', detail }); } private error(e: unknown): void { this.messages.add({ severity: 'error', summary: 'Hata', detail: e instanceof Error ? e.message : 'Islem tamamlanamadi.' }); }
}
