using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

/// <summary>
/// Faz 2B.10.2 görev md.2 - <see cref="IEBelgeKurumPolitikaTransactionGuard.KilitleVeOkuAsync"/>'in
/// döndürdüğü, transaction sonuna kadar STABİL kalması GARANTİ edilen politika satırı anlık
/// görüntüsü. `KurumEBelgePolitikasi` entity'sinin KENDİSİ DEĞİLDİR - yalnız karar/serialization
/// İÇİN gereken az sayıda alanı taşıyan, EF tracking'e TABİ OLMAYAN saf bir kayıt.
/// </summary>
public sealed record EBelgeKilitliPolitikaSnapshot
{
    public required int Id { get; init; }

    public required int KurumId { get; init; }

    public required int PolitikaSurumu { get; init; }

    public required bool AktifMi { get; init; }

    public required EBelgeEntegrasyonYontemi EntegrasyonYontemi { get; init; }

    public required DateTime? AktivasyonYerelTarihi { get; init; }
}

/// <summary>
/// Faz 2B.10.2 görev md.2 - normal `AsNoTracking()`/READ COMMITTED okuma, "aynı transaction
/// içinde okundu" olsa BİLE bir SERIALIZATION garantisi VERMEZ: politika kontrolünden SONRA ama
/// artifact/SignedReady/immutable karar COMMIT'İNDEN ÖNCE, BAŞKA bir transaction politika satırını
/// güncelleyip COMMIT edebilir. Bu arayüz, kurum politika satırını AÇIK bir transaction'ın SONUNA
/// (commit/rollback) kadar STABİLİZE eden, küçük ve merkezi bir mekanizma sağlar - yalnız YALNIZ
/// zaten AÇIK bir ambient transaction İÇİNDE çağrılmalıdır (bkz. `EBelgeOutboxClaimLeaseService`/
/// `EBelgeOutboxLeaseTransitionService` İLE AYNI ambient-transaction deseni).
/// </summary>
public interface IEBelgeKurumPolitikaTransactionGuard
{
    /// <summary>
    /// Kurum politika satırını `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` ile kilitler ve okur - kilit,
    /// ambient transaction commit/rollback OLANA KADAR tutulur (yalnız bu SATIR/key-range İÇİN -
    /// tüm transaction isolation level'ı SERIALIZABLE'a YÜKSELTİLMEZ). Politika satırı hiç YOKSA
    /// `null` döner; `KurumId` üzerindeki UNIQUE index nedeniyle bu durumda dahi HOLDLOCK, "bu
    /// KurumId için YENİ bir satırın INSERT edilebileceği" key-range'i kilitler - phantom-insert
    /// yarışına karşı KORUR (bkz. görev md.2, "policy satırı bulunmuyorsa phantom insert/delete
    /// yarışını değerlendir").
    /// </summary>
    Task<EBelgeKilitliPolitikaSnapshot?> KilitleVeOkuAsync(int kurumId, CancellationToken cancellationToken = default);
}

public sealed class EBelgeKurumPolitikaTransactionGuard : IEBelgeKurumPolitikaTransactionGuard
{
    private readonly StysAppDbContext _dbContext;

    public EBelgeKurumPolitikaTransactionGuard(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EBelgeKilitliPolitikaSnapshot?> KilitleVeOkuAsync(int kurumId, CancellationToken cancellationToken = default)
    {
        if (kurumId <= 0)
        {
            throw new BaseException("KurumId pozitif olmalıdır.", 400);
        }

        // Faz 2B.10.3 görev md.13 - bu guard yalnız AÇIK bir ambient transaction İÇİNDE anlamlıdır
        // (bkz. sınıf XML doc'u): `HOLDLOCK`'un verdiği "transaction sonuna kadar tutulan kilit"
        // garantisi, kilit tutacak bir transaction YOKSA sessizce KAYBOLUR - `UPDLOCK` tek başına
        // yalnız İFADE ömrü boyunca sürer, `ExecuteReaderAsync` dönüşünde HEMEN serbest kalır. Bu,
        // güvenli bir business-hata DEĞİL, bir PROGRAMLAMA/çağıran hatasıdır - fail-closed bir
        // sonuç DÖNDÜRMEK YERİNE (ki bu, "kilitlendi" YANILSAMASI yaratır) fail-FAST bir exception
        // fırlatılır.
        var currentTransaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Kurum e-belge politika transaction guard açık bir transaction gerektirir.");

        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = currentTransaction.GetDbTransaction();

            // Faz 2B.10.2 görev md.2 - HOLDLOCK, UPDLOCK'un normalde ifade SONUNDA bıraktığı kilidi
            // transaction SONUNA kadar TUTAR (SERIALIZABLE'a EŞDEĞER bir garanti, ama yalnız BU
            // satır/key-range İÇİN - bkz. sınıf XML doc'u). `KurumId` üzerindeki UNIQUE index
            // sayesinde satır YOKSA bile SQL Server bu eşitlik yüklemi İÇİN bir key-range kilidi
            // alır - başka bir oturum AYNI KurumId ile YENİ bir satır INSERT edemez.
            command.CommandText = """
SELECT [Id], [KurumId], [PolitikaSurumu], [AktifMi], [EntegrasyonYontemi], [AktivasyonYerelTarihi]
FROM [muhasebe].[KurumEBelgePolitikalari] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
WHERE [KurumId] = @KurumId AND [IsDeleted] = 0;
""";
            command.Parameters.Add(new SqlParameter("@KurumId", SqlDbType.Int) { Value = kurumId });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new EBelgeKilitliPolitikaSnapshot
            {
                Id = reader.GetInt32(0),
                KurumId = reader.GetInt32(1),
                PolitikaSurumu = reader.GetInt32(2),
                AktifMi = reader.GetBoolean(3),
                EntegrasyonYontemi = (EBelgeEntegrasyonYontemi)reader.GetInt32(4),
                AktivasyonYerelTarihi = reader.IsDBNull(5) ? null : reader.GetDateTime(5),
            };
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }
}
