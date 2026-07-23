using STYS.Muhasebe.PosTahsilatValorleri.Dtos;

namespace STYS.Muhasebe.PosTahsilatValorleri.Services;

public interface IPosTahsilatValorAktarimService
{
    Task<PosTahsilatValorAktarimSonucDto> HesabaAktarAsync(int id, ManuelAktarimGuncellemeDto? guncelleme, CancellationToken cancellationToken = default);

    Task<PosTahsilatValorToplamAktarimSonucDto> SeciliHesaplaraAktarAsync(List<int> valorIdler, CancellationToken cancellationToken = default);

    Task<PosTahsilatValorToplamAktarimSonucDto> ValoruGelenleriHesabaAktarAsync(int? tesisId, CancellationToken cancellationToken = default);

    Task<PosTahsilatValorAktarimSonucDto> YenidenDeneAsync(int id, CancellationToken cancellationToken = default);

    Task<PosTahsilatValorAktarimSonucDto> DuzeltmeTersKayitAsync(int id, string aciklama, CancellationToken cancellationToken = default);
}
