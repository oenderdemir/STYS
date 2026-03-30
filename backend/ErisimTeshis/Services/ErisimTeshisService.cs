using Microsoft.EntityFrameworkCore;
using STYS;
using STYS.AccessScope;
using STYS.ErisimTeshis.Dto;
using STYS.Infrastructure.EntityFramework;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.ErisimTeshis.Services;

public class ErisimTeshisService : IErisimTeshisService
{
    private readonly StysAppDbContext _stysDbContext;
    private readonly TodIdentityDbContext _identityDbContext;
    private readonly IAccessScopeProvider _accessScopeProvider;

    public ErisimTeshisService(
        StysAppDbContext stysDbContext,
        TodIdentityDbContext identityDbContext,
        IAccessScopeProvider accessScopeProvider)
    {
        _stysDbContext = stysDbContext;
        _identityDbContext = identityDbContext;
        _accessScopeProvider = accessScopeProvider;
    }

    public async Task<ErisimTeshisReferansDto> GetReferanslarAsync(CancellationToken cancellationToken = default)
    {
        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync(cancellationToken);

        var usersQuery = _identityDbContext.Users
            .AsNoTracking()
            .Include(x => x.UserUserGroups)
            .ThenInclude(x => x.UserGroup)
            .AsQueryable();

        if (actorScope.IsTesisManagerScoped)
        {
            usersQuery = usersQuery.Where(x => actorScope.VisibleUserIds.Contains(x.Id));
        }

        var users = await usersQuery
            .OrderBy(x => x.UserName)
            .Select(x => new
            {
                x.Id,
                x.UserName,
                x.FirstName,
                x.LastName,
                x.Email
            })
            .ToListAsync(cancellationToken);

        var userDtos = users.Select(x => new ErisimTeshisKullaniciDto
        {
            Id = x.Id,
            KullaniciAdi = x.UserName,
            AdSoyad = string.Join(" ", new[] { x.FirstName, x.LastName }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim(),
            Eposta = x.Email
        }).ToList();

        var domainScope = await _accessScopeProvider.GetDomainAccessScopeAsync(cancellationToken);
        var tesisQuery = _stysDbContext.Tesisler.AsNoTracking().Where(x => x.AktifMi);
        if (domainScope.IsScoped)
        {
            tesisQuery = tesisQuery.Where(x => domainScope.TesisIds.Contains(x.Id));
        }

        var tesisler = await tesisQuery
            .OrderBy(x => x.Ad)
            .Select(x => new ErisimTeshisTesisDto
            {
                Id = x.Id,
                Ad = x.Ad
            })
            .ToListAsync(cancellationToken);

        return new ErisimTeshisReferansDto
        {
            Kullanicilar = userDtos,
            Tesisler = tesisler,
            Moduller = ErisimTeshisModulTanimlari.Tumu
                .OrderBy(x => x.Ad)
                .Select(ToModulDto)
                .ToList()
        };
    }

    public async Task<ErisimTeshisSonucDto> TeshisEtAsync(ErisimTeshisIstekDto request, CancellationToken cancellationToken = default)
    {
        if (request.KullaniciId == Guid.Empty)
        {
            throw new BaseException("Gecerli bir kullanici seciniz.", 400);
        }

        if (string.IsNullOrWhiteSpace(request.ModulAnahtari))
        {
            throw new BaseException("Gecerli bir modul seciniz.", 400);
        }

        var actorScope = await _accessScopeProvider.GetUserActorScopeAsync(cancellationToken);
        if (actorScope.IsTesisManagerScoped && !actorScope.VisibleUserIds.Contains(request.KullaniciId))
        {
            throw new BaseException("Bu kullanici icin teshis yapma yetkiniz bulunmuyor.", 403);
        }

        var modul = ErisimTeshisModulTanimlari.Tumu.FirstOrDefault(x => string.Equals(x.Anahtar, request.ModulAnahtari.Trim(), StringComparison.OrdinalIgnoreCase));
        if (modul is null)
        {
            throw new BaseException("Desteklenmeyen modul secimi.", 400);
        }

        var user = await _identityDbContext.Users
            .AsNoTracking()
            .Include(x => x.UserUserGroups)
            .ThenInclude(x => x.UserGroup)
            .ThenInclude(x => x.UserGroupRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(x => x.Id == request.KullaniciId, cancellationToken);

        if (user is null)
        {
            throw new BaseException("Kullanici bulunamadi.", 404);
        }

        var permissionSet = user.UserUserGroups
            .SelectMany(x => x.UserGroup?.UserGroupRoles ?? [])
            .Select(x => ToPermission(x.Role.Domain, x.Role.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var userScopeSnapshot = await BuildUserScopeSnapshotAsync(request.KullaniciId, permissionSet, cancellationToken);

        ErisimTeshisTesisDto? selectedTesis = null;
        if (request.TesisId.HasValue)
        {
            selectedTesis = await _stysDbContext.Tesisler
                .AsNoTracking()
                .Where(x => x.Id == request.TesisId.Value)
                .Select(x => new ErisimTeshisTesisDto
                {
                    Id = x.Id,
                    Ad = x.Ad
                })
                .FirstOrDefaultAsync(cancellationToken);
        }

        var operations = BuildOperationResults(modul, permissionSet, userScopeSnapshot, selectedTesis);
        var menuGorunumu = await BuildMenuVisibilityAsync(modul, permissionSet, cancellationToken);
        var missingPermissions = operations
            .Where(x => string.Equals(x.EngelKodu, "YetkiEksik", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.GerekliYetki)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var recommendations = BuildRecommendations(operations, selectedTesis, userScopeSnapshot);
        var basariliIslemSayisi = operations.Count(x => string.Equals(x.Durum, "Basarili", StringComparison.OrdinalIgnoreCase));
        var uyariIslemSayisi = operations.Count(x => string.Equals(x.Durum, "Uyari", StringComparison.OrdinalIgnoreCase));
        var engelliIslemSayisi = operations.Count(x => string.Equals(x.Durum, "Engelli", StringComparison.OrdinalIgnoreCase));
        var destekNotu = BuildSupportNote(user.UserName, modul.Ad, selectedTesis, menuGorunumu, operations);

        return new ErisimTeshisSonucDto
        {
            Kullanici = new ErisimTeshisKullaniciDto
            {
                Id = user.Id,
                KullaniciAdi = user.UserName,
                AdSoyad = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(v => !string.IsNullOrWhiteSpace(v))).Trim(),
                Eposta = user.Email
            },
            Modul = ToModulDto(modul),
            SeciliTesis = selectedTesis,
            KullaniciGruplari = user.UserUserGroups
                .Where(x => x.UserGroup is not null)
                .OrderBy(x => x.UserGroup!.Name)
                .Select(x => new ErisimTeshisKullaniciGrupDto
                {
                    GrupAdi = x.UserGroup!.Name,
                    Roller = x.UserGroup.UserGroupRoles
                        .Select(y => ToPermission(y.Role.Domain, y.Role.Name))
                        .OrderBy(y => y)
                        .ToList()
                })
                .ToList(),
            Yetkiler = permissionSet.OrderBy(x => x).ToList(),
            Scope = new ErisimTeshisScopeDto
            {
                AdminMi = userScopeSnapshot.AdminMi,
                ScopedMi = userScopeSnapshot.DomainScope.IsScoped,
                Tesisler = userScopeSnapshot.ScopeTesisleri,
                BinaIdleri = userScopeSnapshot.DomainScope.BinaIds.OrderBy(x => x).ToList(),
                Ozet = BuildScopeSummary(userScopeSnapshot)
            },
            MenuGorunumu = menuGorunumu,
            Islemler = operations,
            GenelDurum = engelliIslemSayisi > 0 ? "Engelli" : (uyariIslemSayisi > 0 ? "Uyari" : "Basarili"),
            BasariliIslemSayisi = basariliIslemSayisi,
            UyariIslemSayisi = uyariIslemSayisi,
            EngelliIslemSayisi = engelliIslemSayisi,
            EksikYetkiler = missingPermissions,
            OnerilenAksiyonlar = recommendations,
            DestekNotu = destekNotu,
            Ozet = BuildOverallSummary(user.UserName, modul.Ad, operations, selectedTesis)
        };
    }

    private async Task<ErisimTeshisMenuGorunumDto> BuildMenuVisibilityAsync(
        ErisimTeshisModulTanimi modul,
        IReadOnlySet<string> permissionSet,
        CancellationToken cancellationToken)
    {
        var normalizedRoute = NormalizeMenuRoute(modul.Route);
        if (string.IsNullOrWhiteSpace(normalizedRoute))
        {
            return new ErisimTeshisMenuGorunumDto
            {
                MenuKaydiBulundu = false,
                MenuYolu = modul.Ad,
                Route = modul.Route,
                SidebardaGorunur = false,
                MenuYetkisiVar = false,
                MenuZinciri = [],
                Aciklama = "Bu modul icin route tanimi bulunmadigi icin menu kaydi eslestirilemedi."
            };
        }

        var menuItem = await _identityDbContext.MenuItems
            .AsNoTracking()
            .Include(x => x.MenuItemRoles)
            .ThenInclude(x => x.Role)
            .FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.Route != null
                     && (x.Route == normalizedRoute || x.Route == "/" + normalizedRoute),
                cancellationToken);

        if (menuItem is null)
        {
            return new ErisimTeshisMenuGorunumDto
            {
                MenuKaydiBulundu = false,
                MenuYolu = modul.Ad,
                Route = modul.Route,
                SidebardaGorunur = false,
                MenuYetkisiVar = false,
                MenuZinciri = [],
                Aciklama = "Bu modul icin DB tarafinda aktif bir menu kaydi bulunamadi."
            };
        }

        var menuChain = await BuildMenuChainAsync(menuItem, permissionSet, cancellationToken);
        var menuPath = string.Join(" > ", menuChain.Select(x => x.Etiket));
        var menuPermissions = menuItem.MenuItemRoles
            .Select(x => ToPermission(x.Role.Domain, x.Role.Name))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var menuPermissionGranted = menuPermissions.Count == 0 || menuPermissions.Any(x => HasPermission(permissionSet, x));

        return new ErisimTeshisMenuGorunumDto
        {
            MenuKaydiBulundu = true,
            MenuYolu = menuPath,
            Route = menuItem.Route ?? modul.Route,
            SidebardaGorunur = menuPermissionGranted,
            MenuYetkisiVar = menuPermissionGranted,
            GerekliMenuYetkileri = menuPermissions,
            MenuZinciri = menuChain,
            Aciklama = menuPermissionGranted
                ? "Secili modulun menu yetkisi mevcut. Parent menu gruplari cocuk kayit uzerinden sidebarda gorunur."
                : $"Secili modulun menu kaydi bulundu ancak gerekli menu yetkisi eksik: {string.Join(", ", menuPermissions)}"
        };
    }

    private async Task<List<ErisimTeshisMenuSeviyeDto>> BuildMenuChainAsync(
        MenuItem menuItem,
        IReadOnlySet<string> permissionSet,
        CancellationToken cancellationToken)
    {
        var chain = new List<ErisimTeshisMenuSeviyeDto>();
        MenuItem? current = menuItem;

        while (current is not null)
        {
            var permissions = current.MenuItemRoles
                .Select(x => ToPermission(x.Role.Domain, x.Role.Name))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!string.IsNullOrWhiteSpace(current.Label))
            {
                chain.Add(new ErisimTeshisMenuSeviyeDto
                {
                    Etiket = current.Label.Trim(),
                    Route = current.Route ?? string.Empty,
                    GerekliYetkiler = permissions,
                    Gorunur = permissions.Count == 0 || permissions.Any(x => HasPermission(permissionSet, x))
                });
            }

            if (!current.ParentId.HasValue)
            {
                break;
            }

            current = await _identityDbContext.MenuItems
                .AsNoTracking()
                .Include(x => x.MenuItemRoles)
                .ThenInclude(x => x.Role)
                .FirstOrDefaultAsync(x => x.Id == current.ParentId.Value, cancellationToken);
        }

        chain.Reverse();
        return chain;
    }

    private static string NormalizeMenuRoute(string route)
    {
        return route.Trim().TrimStart('/');
    }

    private static ErisimTeshisModulDto ToModulDto(ErisimTeshisModulTanimi module)
    {
        return new ErisimTeshisModulDto
        {
            Anahtar = module.Anahtar,
            Ad = module.Ad,
            Route = module.Route,
            TesisSecimiGerekli = module.TesisScopeGerekli
        };
    }

    private List<ErisimTeshisIslemSonucDto> BuildOperationResults(
        ErisimTeshisModulTanimi modul,
        IReadOnlySet<string> permissionSet,
        UserScopeSnapshot scopeSnapshot,
        ErisimTeshisTesisDto? selectedTesis)
    {
        return
        [
            BuildOperation("menu", "Menuyu Gorebilir", modul.MenuPermission, false, permissionSet, scopeSnapshot, selectedTesis),
            BuildOperation("liste", "Kayitlari Gorebilir", modul.ViewPermission, modul.TesisScopeGerekli, permissionSet, scopeSnapshot, selectedTesis),
            BuildOperation("yeni", "Yeni Kayit Ekleyebilir", modul.ManagePermission, modul.TesisScopeGerekli, permissionSet, scopeSnapshot, selectedTesis),
            BuildOperation("guncelle", "Kayit Guncelleyebilir", modul.ManagePermission, modul.TesisScopeGerekli, permissionSet, scopeSnapshot, selectedTesis),
            BuildOperation("sil", "Kayit Silebilir", modul.ManagePermission, modul.TesisScopeGerekli, permissionSet, scopeSnapshot, selectedTesis)
        ];
    }

    private ErisimTeshisIslemSonucDto BuildOperation(
        string key,
        string label,
        string requiredPermission,
        bool requiresTesisScope,
        IReadOnlySet<string> permissionSet,
        UserScopeSnapshot scopeSnapshot,
        ErisimTeshisTesisDto? selectedTesis)
    {
        var permissionGranted = HasPermission(permissionSet, requiredPermission);
        bool? scopeSatisfied = null;
        string message;
        string status;
        var result = permissionGranted;

        if (!permissionGranted)
        {
            message = $"{requiredPermission} yetkisi bulunmuyor.";
            status = "Engelli";
            result = false;
            return new ErisimTeshisIslemSonucDto
            {
                IslemAnahtari = key,
                IslemAdi = label,
                GerekliYetki = requiredPermission,
                YetkiVar = false,
                TesisScopeGerekli = requiresTesisScope,
                TesisScopeUygun = null,
                Sonuc = false,
                Durum = status,
                EngelKodu = "YetkiEksik",
                Aciklama = message,
                Oneri = $"{requiredPermission} yetkisini veya bu yetkiyi kapsayan yonetici rolunu kullaniciya atayin."
            };
        }

        if (!requiresTesisScope)
        {
            message = "Gerekli yetki mevcut. Bu islem icin tesis scope kontrolu gerekmiyor.";
            status = "Basarili";
        }
        else if (selectedTesis is null)
        {
            message = "Gerekli yetki mevcut. Tesis secilmedigi icin scope kontrolu yapilmadi.";
            status = "Uyari";
            return new ErisimTeshisIslemSonucDto
            {
                IslemAnahtari = key,
                IslemAdi = label,
                GerekliYetki = requiredPermission,
                YetkiVar = permissionGranted,
                TesisScopeGerekli = true,
                TesisScopeUygun = null,
                Sonuc = result,
                Durum = status,
                EngelKodu = "TesisSecilmedi",
                Aciklama = message,
                Oneri = "Tesis secip tekrar teshis edin. Scope engeli varsa secili tesis uzerinden net olarak gorunur."
            };
        }
        else
        {
            scopeSatisfied = !scopeSnapshot.DomainScope.IsScoped || scopeSnapshot.DomainScope.TesisIds.Contains(selectedTesis.Id);
            result = permissionGranted && scopeSatisfied.Value;
            if (scopeSatisfied.Value)
            {
                message = $"{selectedTesis.Ad} kullanicinin erisim scope'unda oldugu icin islem yapabilir.";
                status = "Basarili";
            }
            else
            {
                message = $"{selectedTesis.Ad} kullanicinin erisim scope'unda olmadigi icin islem yapamaz.";
                status = "Engelli";
                return new ErisimTeshisIslemSonucDto
                {
                    IslemAnahtari = key,
                    IslemAdi = label,
                    GerekliYetki = requiredPermission,
                    YetkiVar = permissionGranted,
                    TesisScopeGerekli = requiresTesisScope,
                    TesisScopeUygun = false,
                    Sonuc = false,
                    Durum = status,
                    EngelKodu = "ScopeDisi",
                    Aciklama = message,
                    Oneri = $"{selectedTesis.Ad} tesisini kullanicinin erisim kapsamina alin veya ilgili tesis/bina atamasini yapin."
                };
            }
        }

        return new ErisimTeshisIslemSonucDto
        {
            IslemAnahtari = key,
            IslemAdi = label,
            GerekliYetki = requiredPermission,
            YetkiVar = permissionGranted,
            TesisScopeGerekli = requiresTesisScope,
            TesisScopeUygun = scopeSatisfied,
            Sonuc = result,
            Durum = status,
            EngelKodu = string.Empty,
            Aciklama = message,
            Oneri = "Ek aksiyon gerekmiyor."
        };
    }

    private static List<string> BuildRecommendations(
        IReadOnlyList<ErisimTeshisIslemSonucDto> operations,
        ErisimTeshisTesisDto? selectedTesis,
        UserScopeSnapshot scopeSnapshot)
    {
        var recommendations = new List<string>();

        var missingPermissions = operations
            .Where(x => string.Equals(x.EngelKodu, "YetkiEksik", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.GerekliYetki)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        foreach (var permission in missingPermissions)
        {
            recommendations.Add($"{permission} yetkisini veya bu yetkiyi kapsayan rol/grup atamasini ekleyin.");
        }

        if (selectedTesis is null && operations.Any(x => string.Equals(x.EngelKodu, "TesisSecilmedi", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add("Tesis secip tekrar teshis edin. Bu sayede scope kaynakli engel netlesir.");
        }

        if (selectedTesis is not null && operations.Any(x => string.Equals(x.EngelKodu, "ScopeDisi", StringComparison.OrdinalIgnoreCase)))
        {
            recommendations.Add($"{selectedTesis.Ad} icin kullaniciya tesis, bina veya ilgili gorev atamasi yapin.");
        }

        if (scopeSnapshot.DomainScope.IsScoped && scopeSnapshot.ScopeTesisleri.Count == 0)
        {
            recommendations.Add("Kullanici scoped bir grupta ancak atanmis tesis/bina kaydi yok. Once kapsam atamasini tamamlayin.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("Yetki ve scope acisindan belirgin bir engel gorunmuyor.");
        }

        return recommendations
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<UserScopeSnapshot> BuildUserScopeSnapshotAsync(Guid userId, IReadOnlySet<string> permissionSet, CancellationToken cancellationToken)
    {
        var adminMi = permissionSet.Any(permission =>
            permission.Equals(TodPlatformAuthorizationConstants.AdminPermission, StringComparison.OrdinalIgnoreCase)
            || permission.EndsWith(".Admin", StringComparison.OrdinalIgnoreCase));

        if (adminMi)
        {
            return new UserScopeSnapshot(true, DomainAccessScope.Unscoped(), [], []);
        }

        var isTesisManager = HasPermission(permissionSet, StructurePermissions.KullaniciAtama.TesisYoneticisiAtanabilir)
            || HasPermission(permissionSet, StructurePermissions.KullaniciAtama.TesisYoneticisiAtayabilir);
        var isTemizlikGorevlisi = HasPermission(permissionSet, StructurePermissions.OdaTemizlikYonetimi.View)
            || HasPermission(permissionSet, StructurePermissions.OdaTemizlikYonetimi.Manage);
        var belongsToScopedGroup = isTesisManager
            || isTemizlikGorevlisi
            || HasPermission(permissionSet, StructurePermissions.KullaniciAtama.BinaYoneticisiAtanabilir)
            || HasPermission(permissionSet, StructurePermissions.KullaniciAtama.BinaYoneticisiAtayabilir)
            || HasPermission(permissionSet, StructurePermissions.KullaniciAtama.ResepsiyonistAtanabilir)
            || HasPermission(permissionSet, StructurePermissions.KullaniciAtama.ResepsiyonistAtayabilir);

        var managedTesisIds = await _stysDbContext.TesisYoneticileri
            .Where(x => x.UserId == userId)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var receptionistTesisIds = await _stysDbContext.TesisResepsiyonistleri
            .Where(x => x.UserId == userId)
            .Select(x => x.TesisId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var directBinaIds = await _stysDbContext.BinaYoneticileri
            .Where(x => x.UserId == userId)
            .Select(x => x.BinaId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var ownedTesisIdsForTemizlik = isTemizlikGorevlisi
            ? await _stysDbContext.KullaniciTesisSahiplikleri
                .Where(x => x.UserId == userId && x.TesisId.HasValue)
                .Select(x => x.TesisId!.Value)
                .Distinct()
                .ToListAsync(cancellationToken)
            : [];

        var directTesisIds = managedTesisIds
            .Concat(receptionistTesisIds)
            .Concat(ownedTesisIdsForTemizlik)
            .Distinct()
            .ToHashSet();

        var hasTesisLevelScope = directTesisIds.Count > 0;
        var domainScope = await BuildDomainScopeAsync(
            directTesisIds,
            directBinaIds,
            belongsToScopedGroup,
            hasTesisLevelScope,
            cancellationToken);

        var scopeTesisleri = domainScope.IsScoped
            ? await _stysDbContext.Tesisler
                .AsNoTracking()
                .Where(x => domainScope.TesisIds.Contains(x.Id))
                .OrderBy(x => x.Ad)
                .Select(x => new ErisimTeshisTesisDto
                {
                    Id = x.Id,
                    Ad = x.Ad
                })
                .ToListAsync(cancellationToken)
            : [];

        return new UserScopeSnapshot(false, domainScope, scopeTesisleri, directBinaIds);
    }

    private async Task<DomainAccessScope> BuildDomainScopeAsync(
        HashSet<int> directTesisIds,
        IReadOnlyCollection<int> directBinaIds,
        bool belongsToScopedGroup,
        bool hasTesisLevelScope,
        CancellationToken cancellationToken)
    {
        if (directTesisIds.Count == 0 && directBinaIds.Count == 0)
        {
            return belongsToScopedGroup
                ? DomainAccessScope.Scoped([], [], [])
                : DomainAccessScope.Unscoped();
        }

        var tesisIds = directTesisIds;
        var binaIds = directBinaIds.ToHashSet();

        if (binaIds.Count > 0)
        {
            var tesisIdsFromBina = await _stysDbContext.Binalar
                .Where(x => binaIds.Contains(x.Id))
                .Select(x => x.TesisId)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var tesisId in tesisIdsFromBina)
            {
                tesisIds.Add(tesisId);
            }
        }

        if (hasTesisLevelScope && tesisIds.Count > 0)
        {
            var binaIdsFromTesis = await _stysDbContext.Binalar
                .Where(x => tesisIds.Contains(x.TesisId))
                .Select(x => x.Id)
                .Distinct()
                .ToListAsync(cancellationToken);

            foreach (var binaId in binaIdsFromTesis)
            {
                binaIds.Add(binaId);
            }
        }

        var ilIds = await _stysDbContext.Tesisler
            .Where(x => tesisIds.Contains(x.Id))
            .Select(x => x.IlId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return DomainAccessScope.Scoped(ilIds, tesisIds, binaIds);
    }

    private static bool HasPermission(IReadOnlySet<string> permissionSet, string permission)
    {
        if (permissionSet.Contains(permission))
        {
            return true;
        }

        if (permission.EndsWith(".View", StringComparison.OrdinalIgnoreCase))
        {
            var managePermission = permission[..^".View".Length] + ".Manage";
            return permissionSet.Contains(managePermission);
        }

        return false;
    }

    private static string ToPermission(string? domain, string? name)
    {
        return $"{domain}.{name}";
    }

    private static string BuildScopeSummary(UserScopeSnapshot scopeSnapshot)
    {
        if (scopeSnapshot.AdminMi)
        {
            return "Kullanici admin kapsaminda. Tum tesislere erisimi var.";
        }

        if (!scopeSnapshot.DomainScope.IsScoped)
        {
            return "Kullanici scoped bir grupta olmadigi icin veri erisimi kisitsiz.";
        }

        if (scopeSnapshot.ScopeTesisleri.Count == 0)
        {
            return "Kullanici scoped grupta ancak atanmis tesis veya bina kaydi bulunmuyor.";
        }

        return $"Kullanici {scopeSnapshot.ScopeTesisleri.Count} tesis kapsaminda erisim sahibi.";
    }

    private static string BuildOverallSummary(
        string userName,
        string moduleName,
        IReadOnlyList<ErisimTeshisIslemSonucDto> operations,
        ErisimTeshisTesisDto? selectedTesis)
    {
        var blocked = operations.Where(x => !x.Sonuc).ToList();
        if (blocked.Count == 0)
        {
            return selectedTesis is null
                ? $"{userName} kullanicisi {moduleName} icin gerekli temel yetkilere sahip."
                : $"{userName} kullanicisi {moduleName} modulu icin {selectedTesis.Ad} uzerinde islem yapabilir.";
        }

        var firstBlocked = blocked[0];
        return $"{userName} kullanicisi {moduleName} modulu icin engelli. Ilk neden: {firstBlocked.Aciklama}";
    }

    private static string BuildSupportNote(
        string userName,
        string moduleName,
        ErisimTeshisTesisDto? selectedTesis,
        ErisimTeshisMenuGorunumDto menuGorunumu,
        IReadOnlyList<ErisimTeshisIslemSonucDto> operations)
    {
        if (!menuGorunumu.MenuKaydiBulundu)
        {
            return $"{userName} icin {moduleName} menusu DB'de bulunamadi. Route: {menuGorunumu.Route}.";
        }

        if (!menuGorunumu.MenuYetkisiVar)
        {
            var missingPermission = menuGorunumu.GerekliMenuYetkileri.FirstOrDefault() ?? "menu yetkisi";
            return $"{userName} {moduleName} menusunu goremez; eksik menu yetkisi: {missingPermission}.";
        }

        var firstBlocked = operations.FirstOrDefault(x => !x.Sonuc);
        if (firstBlocked is null)
        {
            return selectedTesis is null
                ? $"{userName} icin {moduleName} menusu ve temel islem yetkileri uygun."
                : $"{userName} icin {moduleName} menusu gorunur ve {selectedTesis.Ad} tesisinde temel islem yetkileri uygun.";
        }

        return $"{userName} {moduleName} menusunu gorebilir; ancak {firstBlocked.IslemAdi.ToLowerInvariant()} islemi engelli. Neden: {firstBlocked.Aciklama}";
    }

    private sealed record UserScopeSnapshot(
        bool AdminMi,
        DomainAccessScope DomainScope,
        List<ErisimTeshisTesisDto> ScopeTesisleri,
        IReadOnlyCollection<int> DirectBinaIds);
}
