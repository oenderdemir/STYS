using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Dtos;
using STYS.Muhasebe.SatisBelgeleri.Enums;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public class EBelgeOutboxClaimLeaseService : IEBelgeOutboxClaimLeaseService
{
    private readonly StysAppDbContext _dbContext;

    public EBelgeOutboxClaimLeaseService(
        StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<EBelgeOutboxClaimLeaseResultDto?> TryClaimNextAsync(TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var leaseSeconds = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateLeaseSeconds(leaseDuration);
        var kilitToken = Guid.NewGuid().ToString("D");

        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            var currentTransaction = _dbContext.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            command.CommandText = """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();
DECLARE @EnEskiTarih DATETIME2(7) = CONVERT(DATETIME2(7), '0001-01-01T00:00:00', 126);
-- Faz 2B.10.1 görev md.1 - Türkiye 2016'dan beri sabit UTC+3 kullanır (yaz saati YOKTUR, bkz.
-- TurkeyTimeZoneHelper XML doc'u) - bu yüzden burada AYNI sonucu güvenle veren sabit bir ofset
-- kullanılır; SQL Server'ın AT TIME ZONE/IANA-Windows tz veritabanına BAĞIMLI OLUNMAZ.
DECLARE @BuguneTrTarih DATE = CAST(DATEADD(HOUR, 3, @NowUtc) AS DATE);

;WITH Adaylar AS
(
    SELECT TOP (1)
        outbox.[Id],
        outbox.[KurumId],
        outbox.[EBelgeKaydiId],
        outbox.[IsTuru],
        outbox.[Durum],
        outbox.[DenemeSayisi],
        outbox.[SonrakiDenemeZamaniUtc],
        outbox.[KilitBitisZamaniUtc],
        outbox.[KilitToken],
        outbox.[IslemBaslamaZamaniUtc],
        outbox.[UpdatedAt],
        outbox.[UpdatedBy],
        CASE
            WHEN outbox.[Durum] = 1 THEN COALESCE(outbox.[SonrakiDenemeZamaniUtc], outbox.[CreatedAt], @EnEskiTarih)
            WHEN outbox.[Durum] = 4 THEN outbox.[SonrakiDenemeZamaniUtc]
            WHEN outbox.[Durum] = 2 THEN outbox.[KilitBitisZamaniUtc]
        END AS UygunlukZamaniUtc
    FROM [muhasebe].[EBelgeOutboxMesajlari] AS outbox WITH (UPDLOCK, READPAST, ROWLOCK)
    -- Faz 2B.10.1 görev md.1 - bir mesaj yalnız KENDİ immutable kararı VE kurumun GÜNCEL, aktif,
    -- AYNI yöntemdeki politikası VARSA aday olabilir. Otoriter iş yeteneği (bu iş türü İÇİN
    -- yerel pipeline gerekip gerekmediği) immutable karardaki SNAPSHOT alanlarından okunur -
    -- yöntem -> yetenek matrisi burada İKİNCİ KEZ hard-code EDİLMEZ (bkz. görev md.1, "Otoriter
    -- iş yeteneği immutable karardaki snapshot alanlarından alınmalı").
    INNER JOIN [muhasebe].[SatisBelgesiEBelgeKararlari] AS karar WITH (READPAST)
        ON karar.[EBelgeKaydiId] = outbox.[EBelgeKaydiId]
       AND karar.[KurumId] = outbox.[KurumId]
       AND karar.[IsDeleted] = 0
    INNER JOIN [muhasebe].[KurumEBelgePolitikalari] AS politika WITH (READPAST)
        ON politika.[KurumId] = outbox.[KurumId]
       AND politika.[IsDeleted] = 0
    WHERE outbox.[IsDeleted] = 0
      AND politika.[AktifMi] = 1
      AND politika.[EntegrasyonYontemi] = karar.[EntegrasyonYontemi]
      AND politika.[AktivasyonYerelTarihi] IS NOT NULL
      AND politika.[AktivasyonYerelTarihi] <= @BuguneTrTarih
      AND (
            (outbox.[IsTuru] = 1 AND karar.[YerelSnapshotOlustur] = 1 AND karar.[YerelUnsignedUblOlustur] = 1)
         OR (outbox.[IsTuru] = 2 AND karar.[YerelImzaOlustur] = 1)
          )
      AND (
            (outbox.[Durum] = 1 AND (outbox.[SonrakiDenemeZamaniUtc] IS NULL OR outbox.[SonrakiDenemeZamaniUtc] <= @NowUtc))
         OR (outbox.[Durum] = 4 AND outbox.[SonrakiDenemeZamaniUtc] IS NOT NULL AND outbox.[SonrakiDenemeZamaniUtc] <= @NowUtc)
         OR (outbox.[Durum] = 2 AND outbox.[KilitBitisZamaniUtc] IS NOT NULL AND outbox.[KilitBitisZamaniUtc] <= @NowUtc)
      )
    ORDER BY UygunlukZamaniUtc, outbox.[Id]
)
UPDATE Adaylar
SET [Durum] = 2,
    [KilitToken] = @KilitToken,
    [KilitBitisZamaniUtc] = DATEADD(SECOND, @LeaseSeconds, @NowUtc),
    [IslemBaslamaZamaniUtc] = @NowUtc,
    [DenemeSayisi] = [DenemeSayisi] + 1,
    [SonrakiDenemeZamaniUtc] = NULL,
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT
    inserted.[Id],
    inserted.[KurumId],
    inserted.[EBelgeKaydiId],
    inserted.[IsTuru],
    inserted.[Durum],
    inserted.[DenemeSayisi],
    inserted.[KilitToken],
    inserted.[KilitBitisZamaniUtc],
    inserted.[IslemBaslamaZamaniUtc],
    inserted.[SonrakiDenemeZamaniUtc];
""";

            command.Parameters.Add(new SqlParameter("@LeaseSeconds", SqlDbType.Int) { Value = leaseSeconds });
            command.Parameters.Add(new SqlParameter("@KilitToken", SqlDbType.NVarChar, 36) { Value = kilitToken });

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return new EBelgeOutboxClaimLeaseResultDto
            {
                OutboxMesajiId = reader.GetInt32(0),
                KurumId = reader.GetInt32(1),
                EBelgeKaydiId = reader.GetInt32(2),
                IsTuru = (EBelgeOutboxIsTuru)reader.GetInt32(3),
                Durum = (EBelgeOutboxDurumu)reader.GetInt32(4),
                DenemeSayisi = reader.GetInt32(5),
                KilitToken = reader.GetString(6),
                KilitBitisZamaniUtc = reader.GetDateTime(7),
                IslemBaslamaZamaniUtc = reader.GetDateTime(8),
                SonrakiDenemeZamaniUtc = reader.IsDBNull(9) ? null : reader.GetDateTime(9)
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
