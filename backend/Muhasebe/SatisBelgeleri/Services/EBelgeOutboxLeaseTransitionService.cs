using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri.Services;

public class EBelgeOutboxLeaseTransitionService : IEBelgeOutboxLeaseTransitionService
{
    private readonly StysAppDbContext _dbContext;

    public EBelgeOutboxLeaseTransitionService(StysAppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> TryCompleteAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default)
        => ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

UPDATE [muhasebe].[EBelgeOutboxMesajlari]
SET [Durum] = 3,
    [TamamlanmaZamaniUtc] = @NowUtc,
    [KilitToken] = NULL,
    [KilitBitisZamaniUtc] = NULL,
    [SonrakiDenemeZamaniUtc] = NULL,
    [SonHataKodu] = NULL,
    [SonHataMesaji] = NULL,
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT inserted.[Id]
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command => { },
            cancellationToken);

    public Task<bool> TryFailAsync(
        int outboxMesajiId,
        int kurumId,
        string kilitToken,
        string sonHataKodu,
        string sonHataMesaji,
        TimeSpan? retryDelay,
        CancellationToken cancellationToken = default)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(sonHataKodu, sonHataMesaji);
        var retryDelaySeconds = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateRetryDelaySeconds(retryDelay);

        return ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

UPDATE [muhasebe].[EBelgeOutboxMesajlari]
SET [Durum] = 4,
    [SonHataKodu] = @SonHataKodu,
    [SonHataMesaji] = @SonHataMesaji,
    [SonrakiDenemeZamaniUtc] = CASE
        WHEN @RetryDelaySeconds IS NULL THEN NULL
        ELSE DATEADD(SECOND, @RetryDelaySeconds, @NowUtc)
    END,
    [KilitToken] = NULL,
    [KilitBitisZamaniUtc] = NULL,
    [TamamlanmaZamaniUtc] = NULL,
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT inserted.[Id]
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command =>
            {
                command.Parameters.Add(new SqlParameter("@SonHataKodu", SqlDbType.NVarChar, 100) { Value = sonHataKodu });
                command.Parameters.Add(new SqlParameter("@SonHataMesaji", SqlDbType.NVarChar, 2000) { Value = sonHataMesaji });
                command.Parameters.Add(new SqlParameter("@RetryDelaySeconds", SqlDbType.Int) { Value = (object?)retryDelaySeconds ?? DBNull.Value });
            },
            cancellationToken);
    }

    public Task<bool> IsOwnedAsync(int outboxMesajiId, int kurumId, string kilitToken, CancellationToken cancellationToken = default)
        => ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

SELECT [Id]
FROM [muhasebe].[EBelgeOutboxMesajlari] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command => { },
            cancellationToken);

    public Task<bool> IsOwnedForArtifactAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, string kilitToken, CancellationToken cancellationToken = default)
        => ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

SELECT [Id]
FROM [muhasebe].[EBelgeOutboxMesajlari] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [EBelgeKaydiId] = @EBelgeKaydiId
  AND [IsTuru] = 1
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command => command.Parameters.Add(new SqlParameter("@EBelgeKaydiId", SqlDbType.Int) { Value = eBelgeKaydiId }),
            cancellationToken);

    public Task<bool> TryCompleteArtifactAsync(int outboxMesajiId, int kurumId, int eBelgeKaydiId, string kilitToken, CancellationToken cancellationToken = default)
        => ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

UPDATE [muhasebe].[EBelgeOutboxMesajlari]
SET [Durum] = 3,
    [TamamlanmaZamaniUtc] = @NowUtc,
    [KilitToken] = NULL,
    [KilitBitisZamaniUtc] = NULL,
    [SonrakiDenemeZamaniUtc] = NULL,
    [SonHataKodu] = NULL,
    [SonHataMesaji] = NULL,
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT inserted.[Id]
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [EBelgeKaydiId] = @EBelgeKaydiId
  AND [IsTuru] = 1
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command => command.Parameters.Add(new SqlParameter("@EBelgeKaydiId", SqlDbType.Int) { Value = eBelgeKaydiId }),
            cancellationToken);

    public Task<bool> TryFailArtifactAsync(
        int outboxMesajiId,
        int kurumId,
        int eBelgeKaydiId,
        string kilitToken,
        string sonHataKodu,
        string sonHataMesaji,
        TimeSpan? retryDelay,
        CancellationToken cancellationToken = default)
    {
        EBelgeOutboxLeaseValidationHelper.ValidateHataAlanlari(sonHataKodu, sonHataMesaji);
        var retryDelaySeconds = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateRetryDelaySeconds(retryDelay);

        return ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

UPDATE [muhasebe].[EBelgeOutboxMesajlari]
SET [Durum] = 4,
    [SonHataKodu] = @SonHataKodu,
    [SonHataMesaji] = @SonHataMesaji,
    [SonrakiDenemeZamaniUtc] = CASE
        WHEN @RetryDelaySeconds IS NULL THEN NULL
        ELSE DATEADD(SECOND, @RetryDelaySeconds, @NowUtc)
    END,
    [KilitToken] = NULL,
    [KilitBitisZamaniUtc] = NULL,
    [TamamlanmaZamaniUtc] = NULL,
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT inserted.[Id]
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [EBelgeKaydiId] = @EBelgeKaydiId
  AND [IsTuru] = 1
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command =>
            {
                command.Parameters.Add(new SqlParameter("@EBelgeKaydiId", SqlDbType.Int) { Value = eBelgeKaydiId });
                command.Parameters.Add(new SqlParameter("@SonHataKodu", SqlDbType.NVarChar, 100) { Value = sonHataKodu });
                command.Parameters.Add(new SqlParameter("@SonHataMesaji", SqlDbType.NVarChar, 2000) { Value = sonHataMesaji });
                command.Parameters.Add(new SqlParameter("@RetryDelaySeconds", SqlDbType.Int) { Value = (object?)retryDelaySeconds ?? DBNull.Value });
            },
            cancellationToken);
    }

    public Task<bool> TryRenewAsync(int outboxMesajiId, int kurumId, string kilitToken, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var leaseSeconds = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateLeaseSeconds(leaseDuration);

        return ExecuteTransitionAsync(
            """
DECLARE @NowUtc DATETIME2(7) = SYSUTCDATETIME();

UPDATE [muhasebe].[EBelgeOutboxMesajlari]
SET [KilitBitisZamaniUtc] = DATEADD(SECOND, @LeaseSeconds, @NowUtc),
    [UpdatedAt] = @NowUtc,
    [UpdatedBy] = N'system'
OUTPUT inserted.[Id]
WHERE [Id] = @OutboxMesajiId
  AND [KurumId] = @KurumId
  AND [IsDeleted] = 0
  AND [Durum] = 2
  AND [KilitToken] = @KilitToken
  AND [KilitBitisZamaniUtc] IS NOT NULL
  AND [KilitBitisZamaniUtc] > @NowUtc;
""",
            outboxMesajiId,
            kurumId,
            kilitToken,
            configure: command =>
            {
                command.Parameters.Add(new SqlParameter("@LeaseSeconds", SqlDbType.Int) { Value = leaseSeconds });
            },
            cancellationToken);
    }

    private async Task<bool> ExecuteTransitionAsync(
        string sql,
        int outboxMesajiId,
        int kurumId,
        string kilitToken,
        Action<SqlCommand> configure,
        CancellationToken cancellationToken)
    {
        var normalizedToken = EBelgeOutboxLeaseValidationHelper.NormalizeAndValidateKilitToken(kilitToken);

        var connection = _dbContext.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = (SqlCommand)connection.CreateCommand();
            var currentTransaction = _dbContext.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                command.Transaction = (SqlTransaction)currentTransaction.GetDbTransaction();
            }

            command.CommandText = sql;
            command.Parameters.Add(new SqlParameter("@OutboxMesajiId", SqlDbType.Int) { Value = outboxMesajiId });
            command.Parameters.Add(new SqlParameter("@KurumId", SqlDbType.Int) { Value = kurumId });
            command.Parameters.Add(new SqlParameter("@KilitToken", SqlDbType.NVarChar, 36) { Value = normalizedToken });
            configure(command);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            return await reader.ReadAsync(cancellationToken);
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
