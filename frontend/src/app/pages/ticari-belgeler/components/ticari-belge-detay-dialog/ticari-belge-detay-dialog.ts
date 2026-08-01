import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { DialogModule } from 'primeng/dialog';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import {
    FATURALAMA_DURUMU_LABELS,
    FATURALAMA_DURUMU_SEVERITIES,
    MUHASEBE_DURUMU_LABELS,
    MUHASEBE_DURUMU_SEVERITIES,
    SATIS_BELGESI_SATIR_TIPI_LABELS,
    SATIS_BELGESI_TIPI_LABELS,
    SATIS_KAYNAK_MODULU_LABELS,
    TICARI_BELGE_DURUMU_LABELS,
    TICARI_BELGE_DURUMU_SEVERITIES,
    SatisBelgesiSatirTipi,
    SatisBelgesiTipi,
    SatisKaynakModulu,
    TagSeverity,
    TicariBelgeDetayDto,
    TicariBelgeDurumu,
    TicariBelgeFaturalamaDurumu,
    TicariBelgeMuhasebeDurumu,
    getMusteriDisplayName
} from '../../ticari-belge.models';

@Component({
    selector: 'app-ticari-belge-detay-dialog',
    standalone: true,
    imports: [CommonModule, DialogModule, TableModule, TagModule],
    templateUrl: './ticari-belge-detay-dialog.html'
})
export class TicariBelgeDetayDialogComponent {
    @Input() visible = false;
    @Input() belge: TicariBelgeDetayDto | null = null;
    @Output() visibleChange = new EventEmitter<boolean>();

    private readonly belgeTipiLabels = SATIS_BELGESI_TIPI_LABELS;
    private readonly kaynakModulLabels = SATIS_KAYNAK_MODULU_LABELS;
    private readonly satirTipiLabels = SATIS_BELGESI_SATIR_TIPI_LABELS;
    private readonly ticariDurumLabels = TICARI_BELGE_DURUMU_LABELS;
    private readonly ticariDurumSeverities = TICARI_BELGE_DURUMU_SEVERITIES;
    private readonly muhasebeDurumuLabels = MUHASEBE_DURUMU_LABELS;
    private readonly muhasebeDurumuSeverities = MUHASEBE_DURUMU_SEVERITIES;
    private readonly faturalamaDurumuLabels = FATURALAMA_DURUMU_LABELS;
    private readonly faturalamaDurumuSeverities = FATURALAMA_DURUMU_SEVERITIES;

    getMusteriDisplayName = getMusteriDisplayName;

    getBelgeTipiLabel(belgeTipi: SatisBelgesiTipi): string {
        return this.belgeTipiLabels[belgeTipi] ?? String(belgeTipi);
    }

    getKaynakModulLabel(kaynakModul: SatisKaynakModulu): string {
        return this.kaynakModulLabels[kaynakModul] ?? String(kaynakModul);
    }

    getSatirTipiLabel(satirTipi: SatisBelgesiSatirTipi): string {
        return this.satirTipiLabels[satirTipi] ?? String(satirTipi);
    }

    getTicariDurumLabel(durum: TicariBelgeDurumu): string {
        return this.ticariDurumLabels[durum] ?? String(durum);
    }

    getTicariDurumSeverity(durum: TicariBelgeDurumu): TagSeverity {
        return this.ticariDurumSeverities[durum] ?? 'secondary';
    }

    getMuhasebeDurumuLabel(durum: TicariBelgeMuhasebeDurumu): string {
        return this.muhasebeDurumuLabels[durum] ?? String(durum);
    }

    getMuhasebeDurumuSeverity(durum: TicariBelgeMuhasebeDurumu): TagSeverity {
        return this.muhasebeDurumuSeverities[durum] ?? 'secondary';
    }

    getFaturalamaDurumuLabel(durum: TicariBelgeFaturalamaDurumu): string {
        return this.faturalamaDurumuLabels[durum] ?? String(durum);
    }

    getFaturalamaDurumuSeverity(durum: TicariBelgeFaturalamaDurumu): TagSeverity {
        return this.faturalamaDurumuSeverities[durum] ?? 'secondary';
    }

    onHide(): void {
        this.visibleChange.emit(false);
    }
}
