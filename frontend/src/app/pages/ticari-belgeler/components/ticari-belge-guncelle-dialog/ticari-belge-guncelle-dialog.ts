import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { MessageService } from 'primeng/api';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { toLocalDateString } from '../../../../core/utils/date-time.util';
import { TicariBelgeService } from '../../ticari-belge.service';
import {
    KDV_UYGULAMA_TIPI_LABELS,
    KdvUygulamaTipi,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    SATIS_BELGESI_TIPI_SECENEKLERI,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    TicariBelgeCariKartLookupDto,
    TicariBelgeGuncelleRequest,
    TicariBelgeGuncelleSatirRequest,
    TicariBelgeIadeAdayiDto,
    TicariBelgeKaynakSatirDto,
    TicariBelgeKdvIstisnaLookupDto,
    createEmptyTicariBelgeGuncelleSatiri
} from '../../ticari-belge.models';

function isAlisBelgeTipi(belgeTipi: SatisBelgesiTipi | null | undefined): boolean {
    return belgeTipi === SatisBelgesiTipi.AlisFaturasi || belgeTipi === SatisBelgesiTipi.AlisIadeFaturasi;
}

function isIadeBelgeTipi(belgeTipi: SatisBelgesiTipi | null | undefined): boolean {
    return belgeTipi === SatisBelgesiTipi.SatisIadeFaturasi || belgeTipi === SatisBelgesiTipi.AlisIadeFaturasi;
}

@Component({
    selector: 'app-ticari-belge-guncelle-dialog',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        AutoCompleteModule,
        ButtonModule,
        DatePickerModule,
        DialogModule,
        InputNumberModule,
        InputTextModule,
        SelectModule,
        TextareaModule,
        ToggleSwitchModule
    ],
    templateUrl: './ticari-belge-guncelle-dialog.html'
})
export class TicariBelgeGuncelleDialogComponent implements OnChanges {
    @Input() visible = false;
    @Input() saving = false;
    @Input() formData: TicariBelgeGuncelleRequest | null = null;
    /** Düzenlenmekte olan belgenin kendi Id'si - iade adayı/kaynak satır sorgularında kendisini
     * hariç tutmak ve kalan iade edilebilir miktarı doğru hesaplamak için gereklidir. */
    @Input() belgeId: number | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();
    @Output() save = new EventEmitter<void>();

    private readonly ticariBelgeService = inject(TicariBelgeService);
    private readonly messageService = inject(MessageService);

    readonly belgeTipiSecenekleri = SATIS_BELGESI_TIPI_SECENEKLERI;
    readonly satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    readonly satirTipiSecenekleri = Object.entries(SATIS_BELGESI_SATIR_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as SatisBelgesiSatirTipi,
        label
    }));
    readonly kdvUygulamaTipiLabels = KDV_UYGULAMA_TIPI_LABELS;
    readonly kdvUygulamaTipiSecenekleri = Object.entries(KDV_UYGULAMA_TIPI_LABELS).map(([key, label]) => ({
        value: Number(key) as KdvUygulamaTipi,
        label
    }));

    // ── Cari kart seçimi — operasyonel lookup (ui/ticari-belgeler/lookups/cari-kartlar) ──
    cariKartlar: TicariBelgeCariKartLookupDto[] = [];
    filteredCariKartlar: TicariBelgeCariKartLookupDto[] = [];
    selectedCari: TicariBelgeCariKartLookupDto | null = null;

    // ── KDV istisna tanımı seçimi (satır bazlı, KdvUygulamaTipi'ne göre lazy cache) ──
    private kdvIstisnaCache = new Map<KdvUygulamaTipi, TicariBelgeKdvIstisnaLookupDto[]>();

    // ── İade edilen belge referansı ──
    iadeEdilenBelgeSuggestions: TicariBelgeIadeAdayiDto[] = [];
    iadeEdilenBelgeGosterim: { id: number; belgeNo: string; belgeTarihi: string } | null = null;

    // ── İade kaynak satırları — seçilen kaynağın satırlarına KİLİTLİ, kullanıcı yalnızca
    // miktar/açıklama değiştirebilir (bkz. görev F). ──
    kaynakSatirlar: TicariBelgeKaynakSatirDto[] = [];

    /** Mevcut (kayıtlı) bir iade satırının KaynakSatirId'si güncel kaynakta ARTIK bulunamadığında,
     * kilitli mali alanları (birim fiyat/indirim/KDV/tevkifat) kaynakla UYUMSUZ hale geldiğinde,
     * ya da yeni bir kaynak seçimi/yükleme sırasında hata oluştuğunda dolar - kaydetme bu mesaj
     * varken ENGELLENİR (bkz. görev 1-3/onSaveClick). */
    kaynakSatirHataMesaji: string | null = null;

    /** Kaynak satır lookup isteği DEVAM EDERKEN true - bu sırada Kaydet devre dışı bırakılır
     * (bkz. görev 1). Yalnızca başarılı sonuç, hata VEYA referans kaldırma ile false'a döner;
     * kullanıcının aynı anda başka bir kaynak seçmesi tek başına loading'i KAPATMAZ (yeni istek
     * kendi sonuçlanana kadar loading true kalmaya devam eder). */
    kaynakSatirlarYukleniyor = false;

    /** Eski/asenkron kaynak satır isteklerinin sonucunun, kullanıcı bu arada BAŞKA bir kaynak
     * seçmiş/referansı kaldırmışsa sessizce UYGULANMAMASI için kullanılan monoton sayaç (request
     * token) - hem mevcut belge açılışındaki hem de yeni kaynak seçimindeki lookup AYNI sayacı
     * kullanır (bkz. görev 2). Her yeni istek başlatılırken artırılır; yanıt geldiğinde sayaç hâlâ
     * AYNIYSA uygulanır, DEĞİŞMİŞSE (daha yeni bir istek başlamış demektir) sessizce YOK SAYILIR. */
    private kaynakSatirRequestToken = 0;

    /** Cari kart lookup isteklerinin AYNI request-token yaklaşımı - belge tipi/tesis hızlı ardışık
     * değiştirildiğinde eski bir isteğin yeni listenin ÜZERİNE yazmasını engeller (bkz. görev 2). */
    private cariKartRequestToken = 0;

    /** İade adayı arama isteklerinin AYNI request-token yaklaşımı - eski (stale) bir arama
     * yanıtının, kullanıcı bu arada belge tipi/cari/belge tarihini değiştirdikten SONRA gelip
     * artık geçersiz önerileri göstermesini engeller (bkz. görev 3). */
    private iadeAdayiRequestToken = 0;

    private oncekiBelgeTipi: SatisBelgesiTipi | null | undefined = undefined;

    ngOnChanges(changes: SimpleChanges): void {
        if (changes['visible'] && this.visible && this.formData) {
            this.oncekiBelgeTipi = this.formData.belgeTipi;
            this.kdvIstisnaCache.clear();
            this.kaynakSatirHataMesaji = null;
            // Dialog (yeniden) açılıyor - önceki belgeye ait, artık ALAKASIZ bir bekleyen kaynak
            // isteği varsa geçersiz kılınır (bkz. token doc'u); bu belge için gerekiyorsa aşağıdaki
            // loader zaten kendi token'ını alıp loading'i yeniden başlatacaktır.
            this.kaynakSatirRequestToken++;
            this.kaynakSatirlarYukleniyor = false;
            this.loadCariKartlar();
            this.resolveIadeEdilenBelgeGosterim();
            if (this.formData.iadeEdilenBelgeId) {
                // DİKKAT: dialog AÇILIRKEN (mevcut, zaten kayıtlı bir iade belgesi) kaynak lookup
                // yalnızca kalan miktar/mali alan doğrulaması İÇİN yüklenir - kayıtlı satırlar
                // (miktar/açıklama/eşleşme) SESSİZCE yeniden oluşturulmaz (bkz. görev 1). Satırların
                // sıfırdan yeniden eşlenmesi YALNIZCA kullanıcı FARKLI bir kaynak seçtiğinde
                // (onIadeEdilenBelgeSecildi) yapılır.
                this.loadKaynakSatirlarForMevcutBelge(this.formData.iadeEdilenBelgeId);
            } else {
                this.kaynakSatirlar = [];
            }
            for (const satir of this.formData.satirlar ?? []) {
                this.ensureKdvIstisnalarLoaded(satir.kdvUygulamaTipi);
            }
        }
    }

    belgeTarihiValue(): Date | null {
        return this.formData?.belgeTarihi ? new Date(this.formData.belgeTarihi) : null;
    }

    /** Belge tarihi değiştiğinde: KDV istisna önbelleği geçersiz kılınır; eski (artık ilgisiz)
     * iade adayı önerileri temizlenir ve bekleyen bir arama isteği geçersiz kılınır (bkz. görev
     * 3). Bir iade referansı ZATEN seçiliyse, o referans yeni belge tarihine göre artık geçerli
     * olmayabilir (ör. asıl belge artık iade tarihinden SONRA kalmış olabilir) - güvenli tarafta
     * kalınıp referans+kaynak satırlar birlikte temizlenir, yeni bir kaynak seçilmeden kaydetme
     * engellenir. */
    onBelgeTarihiChange(value: Date | null): void {
        if (!this.formData) return;
        const yeniTarih = value ? toLocalDateString(value) : null;
        const tarihDegisti = this.formData.belgeTarihi !== yeniTarih;
        this.formData.belgeTarihi = yeniTarih;
        this.kdvIstisnaCache.clear();

        if (tarihDegisti) {
            this.invalidateIadeAdayiAramasi();
            if (this.belgeIadeTipiMi() && this.formData.iadeEdilenBelgeId) {
                this.iadeReferansiVeKaynagiTemizle(
                    'Belge tarihi değişti - mevcut iade kaynağı artık geçerli olmayabilir, lütfen yeniden seçin.'
                );
            }
        }
    }

    vadeTarihiValue(): Date | null {
        return this.formData?.vadeTarihi ? new Date(this.formData.vadeTarihi) : null;
    }

    onVadeTarihiChange(value: Date | null): void {
        if (this.formData) {
            this.formData.vadeTarihi = value ? toLocalDateString(value) : null;
        }
    }

    // ── Belge tipi (yön) değişimi ──

    /** Şablonda İade Edilen Belge Referansı alanının yalnızca gerçek iade belge tiplerinde
     * (SatisIadeFaturasi/AlisIadeFaturasi) gösterilmesi için kullanılır (bkz. görev 1). */
    belgeIadeTipiMi(): boolean {
        return isIadeBelgeTipi(this.formData?.belgeTipi);
    }

    /** Belge tipi alış/satış yönleri arasında değiştirilirse: mevcut cari yeni yöne uygun
     * değilse temizlenir, lookup yeniden yüklenir ve kullanıcıya uyarı gösterilir (bkz. görev D).
     * Belge tipi iade ↔ normal arasında değiştirilirse eski referans/KaynakSatirId'ler/kaynak
     * satırlar SESSİZCE taşınmaz - bkz. iadeNormalGecisiniIsle (görev 1). SatisIadeFaturasi ↔
     * AlisIadeFaturasi (iki iade alt tipi ARASINDA) değiştirilirse de aynı şekilde eski referans/
     * KaynakSatirId/kaynak satırlar temizlenir - bkz. iadeYonuGecisiniIsle (görev 2). */
    onBelgeTipiChange(value: SatisBelgesiTipi): void {
        if (!this.formData) return;
        const oncekiTip = this.oncekiBelgeTipi;
        const oncekiAlisMi = isAlisBelgeTipi(oncekiTip);
        const yeniAlisMi = isAlisBelgeTipi(value);
        const oncekiIadeMi = isIadeBelgeTipi(oncekiTip);
        const yeniIadeMi = isIadeBelgeTipi(value);
        this.formData.belgeTipi = value;
        this.oncekiBelgeTipi = value;

        const yonDegisti = oncekiAlisMi !== yeniAlisMi;
        if (yonDegisti) {
            // selectedCari, cari kart lookup'ı ASENKRON tamamlanana kadar HENÜZ yüklenmemiş
            // olabilir - bu durumda mevcut CariKartId'nin yeni yöne uygunluğu KANITLANAMAZ; kanıt
            // yoksa güvenli tarafta kalınıp cari HER HALÜKARDA temizlenir (bkz. görev 2). selectedCari
            // zaten yüklenmişse gerçek uygunluk kontrolü kullanılır (gereksiz temizlik yapılmaz).
            const cariBilgisiYuklendiMi = !!this.selectedCari;
            const cariUyumsuz = this.formData.cariKartId
                ? cariBilgisiYuklendiMi
                    ? !this.cariYeniYonaUygunMu(this.selectedCari!, value)
                    : true
                : false;
            if (cariUyumsuz) {
                this.cariyiTemizle();
                this.messageService.add({
                    severity: 'warn',
                    summary: 'Cari Kart Temizlendi',
                    detail: 'Belge yönü değişti, seçili cari kart yeni yöne uygun olmadığı için temizlendi.'
                });
            }
            this.loadCariKartlar();
        }

        if (oncekiIadeMi !== yeniIadeMi) {
            this.iadeNormalGecisiniIsle(yeniIadeMi);
        } else if (oncekiIadeMi && yeniIadeMi && oncekiTip !== value) {
            this.iadeYonuGecisiniIsle();
        }

        this.invalidateIadeAdayiAramasi();
        this.kdvIstisnaCache.clear();
    }

    /** Eski (artık ilgisiz) iade adayı önerilerinin temizlenmesi ve bekleyen bir arama isteğinin
     * geçersiz kılınması için ortak yardımcı - belge tipi/cari/belge tarihi değiştiğinde çağrılır
     * (bkz. görev 3). */
    private invalidateIadeAdayiAramasi(): void {
        this.iadeAdayiRequestToken++;
        this.iadeEdilenBelgeSuggestions = [];
    }

    private cariYeniYonaUygunMu(cari: TicariBelgeCariKartLookupDto, belgeTipi: SatisBelgesiTipi): boolean {
        return isAlisBelgeTipi(belgeTipi) ? cari.cariTipi === 'Tedarikci' : cari.cariTipi !== 'Tedarikci';
    }

    /** Belge tipi iade ↔ normal arasında değiştiğinde eski referans, KaynakSatirId ve kaynak
     * satırların SESSİZCE taşınmasını engeller (bkz. görev 1):
     * - Normalden iadeye: eski (kaynaksız) satırlar bir iade kaynağını temsil EDEMEZ, bu yüzden
     *   temizlenir; kullanıcı YENİ bir kaynak seçene kadar kaydetme (kaynakSatirHataMesaji ile)
     *   engellenir.
     * - İadeden normale: eski iade satırları normal satıra DÖNÜŞTÜRÜLMEZ - referansla (ve
     *   KaynakSatirId'leriyle) BİRLİKTE TAMAMEN temizlenir; kullanıcı YENİ bir normal satır
     *   ekleyene kadar (bkz. addSatir) kaydetme engellenir.
     * Her iki yönde de bekleyen bir kaynak satır isteği geçersiz kılınır (request token). */
    private iadeNormalGecisiniIsle(yeniIadeMi: boolean): void {
        if (!this.formData) return;

        this.kaynakSatirRequestToken++;
        this.kaynakSatirlarYukleniyor = false;
        this.kaynakSatirlar = [];
        this.formData.satirlar = [];

        if (yeniIadeMi) {
            this.kaynakSatirHataMesaji = 'İade faturası için önce bir kaynak (iade edilen) belge seçmelisiniz.';
        } else {
            this.formData.iadeEdilenBelgeId = null;
            this.formData.iadeEdilenBelgeReferansiKaldir = true;
            this.iadeEdilenBelgeGosterim = null;
            this.kaynakSatirHataMesaji = 'Normal bir belge en az bir satır içermelidir.';
        }
    }

    /** SatisIadeFaturasi ↔ AlisIadeFaturasi arasında (iki iade alt tipi arasında) değiştiğinde:
     * eski iade referansı, KaynakSatirId'ler ve kaynak satırlar TAMAMEN temizlenir - eski yönün
     * kaynağı (ör. bir SatisFaturasi) yeni yönde (AlisIadeFaturasi için AlisFaturasi) GEÇERSİZDİR
     * (bkz. görev 2). Yeni yöne uygun bir cari seçilmeden searchIadeEdilenBelge zaten aday
     * getirmediğinden (bkz. doc'u), yeni bir kaynak seçilmesi DOLAYLI olarak uygun cari
     * seçilmesini de zorunlu kılar - kaydetme, kullanıcı YENİ bir kaynak seçene kadar
     * (kaynakSatirHataMesaji ile) engellenir. Bekleyen eski bir kaynak satır isteği geçersiz kılınır. */
    private iadeYonuGecisiniIsle(): void {
        this.iadeReferansiVeKaynagiTemizle(
            'Belge yönü değişti - yeni yöne uygun bir cari ve kaynak (iade edilen) belge seçmeden kaydedemezsiniz.'
        );
    }

    /** İade referansı, KaynakSatirId'ler ve kaynak satırlar BİRLİKTE temizlenir; kullanıcı YENİ
     * bir kaynak seçene kadar (kaynakSatirHataMesaji ile) kaydetme engellenir. Mevcut referansın
     * artık NİHAİ değerlere (belge yönü/cari/belge tarihi) uygun olmayabileceği HER durumda
     * çağrılır - bkz. iadeYonuGecisiniIsle/onCariKartSecildi/onBelgeTarihiChange (görev 2/3).
     * Bekleyen eski bir kaynak satır isteği geçersiz kılınır (request token). */
    private iadeReferansiVeKaynagiTemizle(mesaj: string): void {
        if (!this.formData) return;

        this.kaynakSatirRequestToken++;
        this.kaynakSatirlarYukleniyor = false;
        this.kaynakSatirlar = [];
        this.formData.satirlar = [];
        this.formData.iadeEdilenBelgeId = null;
        this.formData.iadeEdilenBelgeReferansiKaldir = true;
        this.iadeEdilenBelgeGosterim = null;
        this.kaynakSatirHataMesaji = mesaj;
    }

    // ── Cari kart ──

    /** Cari kart lookup'ı bir request-token ile korunur - belge tipi/tesis hızlı ardışık
     * değiştirildiğinde eski (stale) bir isteğin yanıtı, yeni belge tipinin listesinin ÜZERİNE
     * SESSİZCE yazamaz (bkz. görev 2). */
    private loadCariKartlar(): void {
        const tesisId = this.formData?.tesisId ?? null;
        const belgeTipi = this.formData?.belgeTipi ?? null;
        const token = ++this.cariKartRequestToken;
        if (!tesisId || !belgeTipi) {
            this.cariKartlar = [];
            this.filteredCariKartlar = [];
            return;
        }
        this.ticariBelgeService.getCariKartLookup(tesisId, belgeTipi).subscribe({
            next: list => {
                if (token !== this.cariKartRequestToken) {
                    return;
                }
                this.cariKartlar = list;
                this.filteredCariKartlar = [...this.cariKartlar];
                this.selectedCari = this.cariKartlar.find(c => c.id === this.formData?.cariKartId) ?? null;
            },
            error: () => {
                if (token !== this.cariKartRequestToken) {
                    return;
                }
                this.cariKartlar = [];
                this.filteredCariKartlar = [];
            }
        });
    }

    filterCari(event: { query: string }): void {
        const query = (event.query ?? '').toLowerCase().trim();
        this.filteredCariKartlar = !query
            ? [...this.cariKartlar]
            : this.cariKartlar.filter(
                  c =>
                      (c.unvanAdSoyad ?? '').toLowerCase().includes(query) ||
                      (c.vergiNoTckn ?? '').toLowerCase().includes(query) ||
                      (c.cariKodu ?? '').toLowerCase().includes(query)
              );
    }

    /** Yalnızca AutoComplete'in GERÇEK seçim ((onSelect)) ve temizleme ((onClear)) olaylarında
     * çağrılır (bkz. şablon/görev 1) - kullanıcının arama kutusuna YAZDIĞI serbest metin
     * (ngModelChange) burayı ASLA tetiklemez; aksi halde yazılan metin bir STRING olarak buraya
     * gelip cariKartId'yi ve müşteri snapshot alanlarını (unvan/ad-soyad/vergi no vb.) sessizce
     * BOZARDI. Cari seçildiğinde yalnızca cariKartId DEĞİL, mevcut satış belgesi ekranıyla uyumlu
     * şekilde tüm müşteri snapshot alanları da doldurulur (bkz. görev D). Cari GERÇEKTEN
     * değiştiyse (eski cariKartId'den farklıysa): eski (artık ilgisiz) iade adayı önerileri
     * temizlenir; bir iade referansı ZATEN seçiliyse bu referans yeni cari ile artık uyumlu
     * olmayabileceğinden referans+kaynak satırlar birlikte temizlenip yeniden seçim zorunlu
     * kılınır (bkz. görev 3). */
    onCariKartSecildi(cari: TicariBelgeCariKartLookupDto | null): void {
        this.selectedCari = cari;
        if (!this.formData) return;

        const cariDegisti = this.formData.cariKartId !== (cari?.id ?? null);

        if (!cari) {
            this.cariyiTemizle();
        } else {
            this.formData.cariKartId = cari.id;
            this.formData.kurumsalMi = cari.kurumsalMi;
            this.formData.musteriUnvan = cari.kurumsalMi ? cari.unvanAdSoyad : null;
            this.formData.musteriAdSoyad = cari.kurumsalMi ? null : cari.unvanAdSoyad;
            this.formData.musteriVergiNo = cari.kurumsalMi ? (cari.vergiNoTckn ?? null) : null;
            this.formData.musteriTcKimlikNo = cari.kurumsalMi ? null : (cari.vergiNoTckn ?? null);
            this.formData.musteriVergiDairesi = cari.vergiDairesi ?? null;
            this.formData.musteriAdres = cari.adres ?? null;
            this.formData.musteriEposta = cari.eposta ?? null;
            this.formData.musteriTelefon = cari.telefon ?? null;
        }

        if (cariDegisti) {
            this.invalidateIadeAdayiAramasi();
            if (this.belgeIadeTipiMi() && this.formData.iadeEdilenBelgeId) {
                this.iadeReferansiVeKaynagiTemizle(
                    'Cari değişti - mevcut iade kaynağı artık geçerli olmayabilir, lütfen yeniden seçin.'
                );
            }
        }
    }

    /** Cari seçimi temizlendiğinde eski cari kimliğiyle yeni/eskimiş müşteri snapshot'ının
     * BİRLİKTE gönderilmemesi için ikisi de birlikte, açıkça temizlenir (bkz. görev D). */
    private cariyiTemizle(): void {
        this.selectedCari = null;
        if (!this.formData) return;
        this.formData.cariKartId = null;
        this.formData.musteriUnvan = null;
        this.formData.musteriAdSoyad = null;
        this.formData.musteriVergiNo = null;
        this.formData.musteriTcKimlikNo = null;
        this.formData.musteriVergiDairesi = null;
        this.formData.musteriAdres = null;
        this.formData.musteriEposta = null;
        this.formData.musteriTelefon = null;
    }

    formatCariDisplay(cari: TicariBelgeCariKartLookupDto): string {
        const kod = cari.cariKodu || '-';
        const unvan = cari.unvanAdSoyad || '-';
        const vergi = cari.vergiNoTckn ? ` (${cari.vergiNoTckn})` : '';
        return `${kod} - ${unvan}${vergi}`;
    }

    // ── KDV istisna tanımı ──

    private ensureKdvIstisnalarLoaded(kdvUygulamaTipi: KdvUygulamaTipi): void {
        if (kdvUygulamaTipi === KdvUygulamaTipi.Kdvli || kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
            return;
        }
        if (this.kdvIstisnaCache.has(kdvUygulamaTipi)) {
            return;
        }
        const belgeTipi = this.formData?.belgeTipi;
        const belgeTarihi = this.formData?.belgeTarihi;
        if (!belgeTipi || !belgeTarihi) {
            return;
        }
        this.kdvIstisnaCache.set(kdvUygulamaTipi, []);
        this.ticariBelgeService.getKdvIstisnaLookup(belgeTipi, kdvUygulamaTipi, belgeTarihi).subscribe({
            next: list => this.kdvIstisnaCache.set(kdvUygulamaTipi, list),
            error: () => this.kdvIstisnaCache.set(kdvUygulamaTipi, [])
        });
    }

    getKdvIstisnaSecenekleri(satir: TicariBelgeGuncelleSatirRequest): Array<{ label: string; value: number }> {
        if (satir.kdvUygulamaTipi === KdvUygulamaTipi.Kdvli || satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli) {
            return [];
        }
        const tanimlar = this.kdvIstisnaCache.get(satir.kdvUygulamaTipi) ?? [];
        return tanimlar.map(t => ({ label: `${t.kod} - ${t.ad}`, value: t.id }));
    }

    /** KdvUygulamaTipi her değiştiğinde eski istisna referansı temizlenir (kullanıcı yeniden
     * seçmelidir); Tevkifatli dışına çıkıldığında tevkifat pay/payda da temizlenir. Bu satır bir
     * kaynak satıra kilitliyse (iade belgesi) mali alan DEĞİŞİKLİĞİ burada yapılmaz. */
    onSatirKdvTipiChange(satir: TicariBelgeGuncelleSatirRequest, value: KdvUygulamaTipi): void {
        if (this.satirKaynagaKilitliMi(satir)) {
            return;
        }
        satir.kdvUygulamaTipi = value;
        this.ensureKdvIstisnalarLoaded(value);

        if (value === KdvUygulamaTipi.Tevkifatli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            return;
        }

        satir.tevkifatPay = null;
        satir.tevkifatPayda = null;

        if (value === KdvUygulamaTipi.Kdvli) {
            satir.kdvIstisnaTanimId = null;
            if (!satir.kdvOrani || satir.kdvOrani <= 0) {
                satir.kdvOrani = 20;
            }
            return;
        }

        // TamIstisna / KismiIstisna / KdvKapsamDisi
        satir.kdvOrani = 0;
        satir.kdvIstisnaTanimId = null;
    }

    /** Satır, bir iade kaynağına (KaynakSatirId) kilitliyse mali alanlar salt-okunurdur -
     * yalnızca miktar/açıklama değiştirilebilir (bkz. görev F). */
    satirKaynagaKilitliMi(satir: TicariBelgeGuncelleSatirRequest): boolean {
        return isIadeBelgeTipi(this.formData?.belgeTipi) && !!satir.kaynakSatirId;
    }

    kaynakSatirIadeEdilebilirKalanMiktar(satir: TicariBelgeGuncelleSatirRequest): number | null {
        if (!satir.kaynakSatirId) return null;
        const kaynakId = Number(satir.kaynakSatirId);
        const kaynak = this.kaynakSatirlar.find(k => k.id === kaynakId);
        return kaynak?.iadeEdilebilirKalanMiktar ?? null;
    }

    // ── İade edilen belge referansı ──

    /** Dialog açılırken, mevcut iadeEdilenBelgeId için GÖSTERİM amaçlı (belgeNo/belgeTarihi)
     * bilgiyi getirir. Bu istek DEVAM EDERKEN kullanıcı BAŞKA bir kaynak seçmiş veya referansı
     * kaldırmış olabilir - bu durumda geç gelen yanıt, formData.iadeEdilenBelgeId'nin isteğin
     * başlatıldığı id ile HÂLÂ AYNI olup olmadığı kontrol edilerek sessizce YOK SAYILIR (bkz.
     * görev 3) - aksi halde stale yanıt, ekranda artık geçerli olmayan bir belgenin gösterimini
     * yeni seçimin ÜZERİNE yazabilirdi. */
    private resolveIadeEdilenBelgeGosterim(): void {
        const id = this.formData?.iadeEdilenBelgeId;
        if (!id) {
            this.iadeEdilenBelgeGosterim = null;
            return;
        }
        this.ticariBelgeService.getById(id).subscribe({
            next: belge => {
                if (this.formData?.iadeEdilenBelgeId !== id) {
                    return;
                }
                this.iadeEdilenBelgeGosterim = { id: belge.id, belgeNo: belge.belgeNo, belgeTarihi: belge.belgeTarihi };
            },
            error: () => {
                if (this.formData?.iadeEdilenBelgeId !== id) {
                    return;
                }
                this.iadeEdilenBelgeGosterim = null;
            }
        });
    }

    /** İade adayı araması, yeni sınırlandırılmış/sunucu-taraflı iade-adaylari uç noktasını kullanır
     * (bkz. görev E) - genel TicariBelge filter endpointi autocomplete olarak KULLANILMAZ. Bir
     * request-token ile korunur: kullanıcı hızlıca yazmaya devam ederse veya bu arada belge tipi/
     * cari/belge tarihini değiştirirse, eski (stale) bir aramanın GEÇ gelen yanıtı artık güncel
     * olmayan önerileri SESSİZCE göstermez (bkz. görev 3). */
    searchIadeEdilenBelge(event: { query: string }): void {
        const tesisId = this.formData?.tesisId;
        const cariKartId = this.formData?.cariKartId;
        const belgeTipi = this.formData?.belgeTipi;
        const belgeTarihi = this.formData?.belgeTarihi;
        const token = ++this.iadeAdayiRequestToken;
        if (!tesisId || !cariKartId || !belgeTipi || !belgeTarihi || !isIadeBelgeTipi(belgeTipi)) {
            this.iadeEdilenBelgeSuggestions = [];
            return;
        }

        this.ticariBelgeService
            .getIadeAdaylari({
                mevcutBelgeId: this.belgeId,
                tesisId,
                belgeTipi,
                cariKartId,
                belgeTarihi,
                belgeNoArama: event.query || null
            })
            .subscribe({
                next: list => {
                    if (token !== this.iadeAdayiRequestToken) {
                        return;
                    }
                    this.iadeEdilenBelgeSuggestions = list;
                },
                error: () => {
                    if (token !== this.iadeAdayiRequestToken) {
                        return;
                    }
                    this.iadeEdilenBelgeSuggestions = [];
                }
            });
    }

    /** Yalnızca AutoComplete'in GERÇEK seçim olayında (p-autoComplete (onSelect), bkz. şablon)
     * çağrılır - kullanıcının arama kutusuna YAZDIĞI serbest metin (ngModelChange/her tuş
     * vuruşu) burayı ASLA tetiklemez (bkz. görev 1). Bu ayrım kritiktir: (ngModelChange) bir
     * STRING ile de tetiklenebildiğinden, önceki bir hatalı sürümde yazılan arama metni
     * `belge.id`'yi `undefined` yaparak satırları sessizce temizliyor ve kaynak satır servisine
     * `undefined` id'li geçersiz bir istek gönderiyordu - (onSelect) yalnızca listeden GERÇEKTEN
     * seçilen bir TicariBelgeIadeAdayiDto nesnesiyle tetiklenir, bu yüzden `belge` burada asla
     * null/undefined ya da bir string olamaz.
     *
     * Kullanıcı FARKLI bir iade kaynağı seçtiğinde: eski satırlar HEMEN (istek sonucunu
     * BEKLEMEDEN) temizlenir - kaynak satır isteği başarısız olursa dahi eski KaynakSatirId'lerin
     * yeni referansla BİRLİKTE gönderilmesi asla mümkün olmaz (bkz. görev 3 - önceki tur). Satırlar
     * seçilen YENİ kaynağın satırlarıyla SIFIRDAN, AÇIKÇA yeniden eşlenir (bkz. görev F/1). Bu,
     * yalnızca kullanıcının burada bilinçli bir seçim yaptığı yoldur - dialog mevcut bir belgeyi
     * AÇARKEN bu yol ASLA çağrılmaz (bkz. ngOnChanges/loadKaynakSatirlarForMevcutBelge). */
    onIadeEdilenBelgeSecildi(belge: TicariBelgeIadeAdayiDto): void {
        this.iadeEdilenBelgeGosterim = { id: belge.id, belgeNo: belge.belgeNo, belgeTarihi: belge.belgeTarihi };
        if (!this.formData) return;

        this.formData.iadeEdilenBelgeId = belge.id;
        this.formData.iadeEdilenBelgeReferansiKaldir = false;
        this.kaynakSatirHataMesaji = null;
        this.kaynakSatirlar = [];
        // Eski satırlar İSTEK SONUCUNU BEKLEMEDEN, HEMEN temizlenir (bkz. görev 3 doc'u - önceki tur).
        this.formData.satirlar = [];

        this.loadKaynakSatirlarForYeniSecim(belge.id);
    }

    clearIadeEdilenBelgeReferansi(): void {
        this.iadeEdilenBelgeGosterim = null;
        this.kaynakSatirlar = [];
        this.kaynakSatirHataMesaji = null;
        // Bekleyen (eskimiş olacak) bir kaynak satır isteği varsa geçersiz kılınır - yanıtı
        // geldiğinde artık hiçbir şeyi etkilemeyecektir (bkz. token doc'u).
        this.kaynakSatirRequestToken++;
        this.kaynakSatirlarYukleniyor = false;
        if (this.formData) {
            this.formData.iadeEdilenBelgeId = null;
            this.formData.iadeEdilenBelgeReferansiKaldir = true;
            // Referans kaldırıldı - geçersiz/gizli bir kaynak referansı bırakılmaması için
            // kaynağa kilitli satırlar da temizlenir (bkz. görev F).
            this.formData.satirlar = [];
        }
    }

    /** Kullanıcı YENİ bir kaynak seçtiğinde çağrılır - satırlar TAMAMEN, sıfırdan yeniden
     * oluşturulur (bkz. onIadeEdilenBelgeSecildi doc'u). İstek BAŞARISIZ olursa (ör. kaynak
     * belge/satır artık erişilemez), eski satırların yeni referansla birlikte sessizce
     * gönderilmesine izin VERİLMEZ: satırlar zaten boş bırakılmıştır (onIadeEdilenBelgeSecildi'nin
     * hemen-temizleme adımı), burada yalnızca açık bir hata mesajı eklenip Kaydet devre dışı
     * bırakılır (bkz. görev 3). Yanıt, kullanıcının bu arada BAŞKA bir kaynak seçmiş/referansı
     * kaldırmış olabileceği ihtimaline karşı geçerliliğini bir request token ile kontrol eder -
     * eskimiş (stale) bir yanıt sessizce UYGULANMAZ (bkz. görev 2). */
    private loadKaynakSatirlarForYeniSecim(kaynakBelgeId: number): void {
        const token = ++this.kaynakSatirRequestToken;
        this.kaynakSatirlarYukleniyor = true;
        this.ticariBelgeService.getKaynakSatirlar(kaynakBelgeId, this.belgeId).subscribe({
            next: satirlar => {
                if (token !== this.kaynakSatirRequestToken) {
                    return;
                }
                this.kaynakSatirlarYukleniyor = false;
                this.kaynakSatirlar = satirlar;
                this.remapSatirlarFromKaynak(satirlar);
            },
            error: () => {
                if (token !== this.kaynakSatirRequestToken) {
                    return;
                }
                this.kaynakSatirlarYukleniyor = false;
                this.kaynakSatirlar = [];
                this.formData!.satirlar = [];
                this.kaynakSatirHataMesaji =
                    'Kaynak belge satırları yüklenemedi. Bu belge bu haliyle kaydedilemez - farklı bir kaynak seçin veya tekrar deneyin.';
            }
        });
    }

    /** Dialog, MEVCUT (zaten kayıtlı) bir iade belgesi için AÇILDIĞINDA çağrılır - kaynak lookup
     * YALNIZCA kalan miktar ve kilitli mali alan DOĞRULAMASI için yüklenir; kayıtlı satırlar
     * (miktar/açıklama/eşleşme) KORUNUR, sessizce yeniden oluşturulmaz/üzerine yazılmaz VE
     * kaynakta bulunup mevcut iadede bulunmayan satırlar forma OTOMATİK EKLENMEZ (bkz. görev 1).
     * AYNI request-token mekanizmasını kullanır (bkz. görev 2) - dialog'un bu istek devam ederken
     * kullanıcı tarafından farklı bir kaynak seçilmesi/referansın kaldırılması durumunda eskimiş
     * yanıt sessizce yok sayılır. */
    private loadKaynakSatirlarForMevcutBelge(kaynakBelgeId: number): void {
        const token = ++this.kaynakSatirRequestToken;
        this.kaynakSatirlarYukleniyor = true;
        this.ticariBelgeService.getKaynakSatirlar(kaynakBelgeId, this.belgeId).subscribe({
            next: satirlar => {
                if (token !== this.kaynakSatirRequestToken) {
                    return;
                }
                this.kaynakSatirlarYukleniyor = false;
                this.kaynakSatirlar = satirlar;
                this.dogrulaKayitliSatirlarKaynaklaUyumluMu(satirlar);
            },
            error: () => {
                if (token !== this.kaynakSatirRequestToken) {
                    return;
                }
                this.kaynakSatirlarYukleniyor = false;
                this.kaynakSatirlar = [];
                this.kaynakSatirHataMesaji =
                    'Kaynak belge satırları yüklenemedi. Kaydetmeden önce sayfayı yeniden açıp tekrar deneyin.';
            }
        });
    }

    /** Her kayıtlı satırın KaynakSatirId'sinin güncel kaynakta bulunduğunu VE kilitli mali
     * alanlarının (birim fiyat/indirim oranı/KDV uygulama tipi-oranı/tevkifat) kaynakla birebir
     * UYUMLU olduğunu doğrular - kayıtlı satırların kendisi (miktar/açıklama dahil) burada
     * DEĞİŞTİRİLMEZ, yalnızca doğrulanır (bkz. görev 1/2). Bulunamayan veya uyumsuz bir satır
     * varsa kaydetmeyi engelleyen açık bir hata mesajı üretilir. */
    private dogrulaKayitliSatirlarKaynaklaUyumluMu(kaynakSatirlar: TicariBelgeKaynakSatirDto[]): void {
        if (!this.formData) return;

        const kaynakMap = new Map(kaynakSatirlar.map(k => [k.id, k]));
        const bulunamayanSiraNolar: number[] = [];
        const uyumsuzSiraNolar: number[] = [];

        for (const satir of this.formData.satirlar ?? []) {
            if (!satir.kaynakSatirId) {
                continue;
            }
            const kaynakId = Number(satir.kaynakSatirId);
            if (Number.isNaN(kaynakId)) {
                continue;
            }
            const kaynak = kaynakMap.get(kaynakId);
            if (!kaynak) {
                bulunamayanSiraNolar.push(satir.siraNo);
            } else if (!this.kilitliAlanlarKaynaklaUyumluMu(satir, kaynak)) {
                uyumsuzSiraNolar.push(satir.siraNo);
            }
        }

        const mesajParcalari: string[] = [];
        if (bulunamayanSiraNolar.length > 0) {
            mesajParcalari.push(`Satır ${bulunamayanSiraNolar.join(', ')} için kaynak satır bulunamadı`);
        }
        if (uyumsuzSiraNolar.length > 0) {
            mesajParcalari.push(`Satır ${uyumsuzSiraNolar.join(', ')} kaynak satırla artık uyumsuz (birim fiyat/KDV/tevkifat değişmiş)`);
        }

        this.kaynakSatirHataMesaji =
            mesajParcalari.length > 0
                ? `${mesajParcalari.join('; ')} - bu belge bu haliyle kaydedilemez. Lütfen sistem yöneticinizle iletişime geçin.`
                : null;
    }

    /** Kilitli mali alanların (birim fiyat/indirim oranı/KDV uygulama tipi-oranı/tevkifat
     * pay-payda) satırla kaynak arasında BİREBİR eşleştiğini kontrol eder - backend'in
     * SatisBelgesiService.ValidateIadeSatirlariAsync'teki AYNI eşleşme kuralının frontend
     * tarafındaki erken/bilgilendirici yansımasıdır; nihai doğrulama YİNE backend'de otoriterdir. */
    private kilitliAlanlarKaynaklaUyumluMu(satir: TicariBelgeGuncelleSatirRequest, kaynak: TicariBelgeKaynakSatirDto): boolean {
        return (
            satir.birimFiyat === kaynak.birimFiyat &&
            satir.indirimOrani === kaynak.indirimOrani &&
            satir.kdvUygulamaTipi === kaynak.kdvUygulamaTipi &&
            satir.kdvOrani === kaynak.kdvOrani &&
            (satir.tevkifatPay ?? null) === (kaynak.tevkifatPay ?? null) &&
            (satir.tevkifatPayda ?? null) === (kaynak.tevkifatPayda ?? null)
        );
    }

    /** İade satırlarını seçilen kaynağın satırlarıyla SIFIRDAN, AÇIKÇA yeniden eşler - her satırın
     * mali alanları kaynaktan gelir ve frontend'de başka bir değere DÖNÜŞMEZ; kullanıcı yalnızca
     * miktar/açıklama değiştirebilir (bkz. görev F). Yalnızca YENİ kaynak seçiminde çağrılır.
     * Yalnızca İADE EDİLEBİLİR KALAN MİKTARI > 0 olan kaynak satırları satıra dönüştürülür (bkz.
     * görev 3) - kalan miktarı tükenmiş bir kaynak satırı seçilemeyecek/iade edilemeyecek bir
     * satır olarak forma eklenmez. Hiç uygun (kalan miktarı > 0) satır kalmamışsa, kullanıcıya
     * açık bir hata gösterilir ve kaydetme (mevcut kaynakSatirHataMesaji mekanizmasıyla) engellenir. */
    private remapSatirlarFromKaynak(kaynakSatirlar: TicariBelgeKaynakSatirDto[]): void {
        if (!this.formData) return;

        const uygunKaynakSatirlar = kaynakSatirlar.filter(k => k.iadeEdilebilirKalanMiktar > 0);

        if (uygunKaynakSatirlar.length === 0) {
            this.formData.satirlar = [];
            this.kaynakSatirHataMesaji =
                'Seçilen kaynak belgede iade edilebilir miktarı kalan hiçbir satır bulunmuyor - bu kaynak seçilemez.';
            return;
        }

        this.formData.satirlar = uygunKaynakSatirlar.map((k, index) => {
            const satir = this.kaynakSatirdanSatirOlustur(k, Math.min(1, k.iadeEdilebilirKalanMiktar));
            satir.siraNo = index + 1;
            return satir;
        });
        this.kaynakSatirHataMesaji = null;
        for (const satir of this.formData.satirlar) {
            this.ensureKdvIstisnalarLoaded(satir.kdvUygulamaTipi);
        }
    }

    private kaynakSatirdanSatirOlustur(k: TicariBelgeKaynakSatirDto, miktar: number): TicariBelgeGuncelleSatirRequest {
        return {
            siraNo: 0,
            satirTipi: SatisBelgesiSatirTipi.Iade,
            aciklama: k.aciklama,
            birim: k.birim,
            miktar,
            birimFiyat: k.birimFiyat,
            indirimOrani: k.indirimOrani,
            indirimTutari: 0,
            kdvUygulamaTipi: k.kdvUygulamaTipi,
            kdvIstisnaTanimId: k.kdvIstisnaTanimId ?? null,
            kdvOrani: k.kdvOrani,
            tevkifatPay: k.tevkifatPay ?? null,
            tevkifatPayda: k.tevkifatPayda ?? null,
            otvOrani: 0,
            otvTutari: 0,
            oivOrani: 0,
            oivTutari: 0,
            konaklamaVergisiOrani: 0,
            konaklamaVergisiTutari: 0,
            kaynakSatirId: String(k.id)
        };
    }

    // ── Satırlar ──

    addSatir(): void {
        if (!this.formData) return;
        if (isIadeBelgeTipi(this.formData.belgeTipi)) {
            // İade belgesinde satırlar yalnızca kaynak seçimiyle yeniden eşlenir - serbest satır
            // eklenmez (bkz. görev F).
            return;
        }
        const satirlar = this.formData.satirlar ?? [];
        const yeniSatir = createEmptyTicariBelgeGuncelleSatiri();
        yeniSatir.siraNo = satirlar.length + 1;
        this.formData.satirlar = [...satirlar, yeniSatir];
        // Yeni bir normal satır eklendi - iadeden normale geçişten kalan "en az bir satır"
        // kaydetme engeli artık geçerli DEĞİLDİR (bkz. görev 1).
        this.kaynakSatirHataMesaji = null;
    }

    removeSatir(index: number): void {
        if (!this.formData?.satirlar) return;
        const satir = this.formData.satirlar[index];
        if (this.satirKaynagaKilitliMi(satir)) {
            return;
        }
        this.formData.satirlar = this.formData.satirlar
            .filter((_, i) => i !== index)
            .map((s, i) => ({ ...s, siraNo: i + 1 }));
    }

    trackBySatirIndex(index: number): number {
        return index;
    }

    isTevkifatli(satir: TicariBelgeGuncelleSatirRequest): boolean {
        return satir.kdvUygulamaTipi === KdvUygulamaTipi.Tevkifatli;
    }

    onHide(): void {
        this.visibleChange.emit(false);
    }

    onSaveClick(): void {
        if (this.kaynakSatirlarYukleniyor) {
            this.messageService.add({
                severity: 'warn',
                summary: 'Bekleyin',
                detail: 'Kaynak belge satırları yükleniyor, lütfen bekleyin.'
            });
            return;
        }
        if (this.kaynakSatirHataMesaji) {
            this.messageService.add({ severity: 'error', summary: 'Kaydedilemedi', detail: this.kaynakSatirHataMesaji });
            return;
        }
        // Normal (iade OLMAYAN) bir belge en az bir geçerli satır olmadan kaydedilemez (bkz.
        // görev 2) - iade belgeler için bu zaten remapSatirlarFromKaynak/kaynakSatirHataMesaji
        // mekanizmasıyla ayrıca engellenir.
        if (!this.belgeIadeTipiMi() && (this.formData?.satirlar?.length ?? 0) === 0) {
            this.messageService.add({
                severity: 'error',
                summary: 'Kaydedilemedi',
                detail: 'Normal bir belge en az bir satır içermelidir.'
            });
            return;
        }
        this.save.emit();
    }
}
