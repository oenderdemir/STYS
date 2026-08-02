using Microsoft.EntityFrameworkCore;
using STYS.Infrastructure.EntityFramework;
using STYS.Muhasebe.SatisBelgeleri.Entities;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Muhasebe.SatisBelgeleri;

/// <summary>
/// SatisBelgesi durum geçişi/içerik değişikliği yapan akışların (MuhasebeOnayinaGonderAsync,
/// MuhasebeOnaylaAsync, ReddetAsync, IptalEtAsync/OperasyonelIptalEtAsync,
/// SatisBelgesiMuhasebeFisService.MuhasebeFisiOlusturAsync) PAYLAŞTIĞI TEK, merkezi "güncel ve
/// güncelleme kilitli oku" yardımcısı (bkz. görev: "yalnız iptal tarafını kilitlemenin yeterli
/// olmadığı problemi düzelt").
///
/// Yalnızca iptal tarafını kilitlemek YETERSİZDİ: bu akışlardan biri (ör. onaylama), belgeyi
/// KİLİTSİZ okuyup kendi doğrulama zincirini çalıştırdıktan SONRA SaveChanges yaparsa - arada
/// başka bir işlem (ör. iptal) commit etmiş olsa bile - EF'in ürettiği "WHERE Id=@id" UPDATE'i
/// bunu FARK ETMEDEN eski (artık geçersiz) kararın sonucunu üzerine yazabilirdi. Bu yardımcı,
/// WITH (UPDLOCK, ROWLOCK) kilidini TÜM bu akışlar için TEK bir yerden, tutarlı şekilde uygular.
///
/// Kilit yalnızca GERÇEK SQL Server sağlayıcısında istenir - Database.IsSqlServer() ile AÇIKÇA
/// doğrulanır (Database.IsRelational() DEĞİL: UPDLOCK/ROWLOCK SQL Server'a özgü sözdizimidir,
/// ileride relational ama SQL Server olmayan bir sağlayıcı eklenirse IsRelational() yanlışlıkla
/// true dönüp bu sözdizimini o sağlayıcıya göndermeye çalışırdı). InMemory (birim testleri)
/// dahil ilişkisel olmayan/SQL-Server-olmayan sağlayıcılarda düz (kilitsiz) bir okumaya düşülür.
/// </summary>
public static class SatisBelgesiLockluOkumaHelper
{
    public static async Task<SatisBelgesi> OkuVeKilitleAsync(
        StysAppDbContext dbContext,
        int id,
        Func<IQueryable<SatisBelgesi>, IQueryable<SatisBelgesi>>? include = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<SatisBelgesi> query = dbContext.Database.IsSqlServer()
            ? dbContext.SatisBelgeleri.FromSqlInterpolated($@"
SELECT * FROM [muhasebe].[SatisBelgeleri] WITH (UPDLOCK, ROWLOCK)
WHERE [Id] = {id} AND [IsDeleted] = 0")
            : dbContext.SatisBelgeleri.Where(x => x.Id == id && !x.IsDeleted);

        if (include is not null)
        {
            query = include(query);
        }

        return await query.FirstOrDefaultAsync(cancellationToken)
            ?? throw new BaseException($"Satış belgesi bulunamadı. (Id: {id})", errorCode: 404);
    }
}
