using System.Text.Json.Serialization;

namespace STYS.Muhasebe.Depolar.Entities;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DepoMalzemeKayitTipleri
{
    MalzemeleriAyriKayittaTut = 0,
    FiyatFarkliMalzemeleriAyriKayittaTut = 1,
    MalzemeleriAyniKayittaTut = 2
}
