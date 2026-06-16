using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using STYS.Kurumlar.Controllers;
using TOD.Platform.AspNetCore.Authorization;
using TOD.Platform.Identity.Infrastructure.EntityFramework;
using TOD.Platform.Identity.UserKurums.Dto;
using TOD.Platform.Identity.UserKurums.Services;
using TOD.Platform.Identity.Users.Controllers;
using TOD.Platform.Identity.Users.DTO;
using TOD.Platform.Identity.Users.Entities;
using TOD.Platform.Identity.Users.Services;
using TOD.Platform.Security.Auth.Services;
using TOD.Platform.SharedKernel.Exceptions;

namespace STYS.Tests;

public class TenantSecurityTests
{
    [Fact]
    public void KurumController_GetMyKurumlar_UsesAuthOnlyPermissionAttribute()
    {
        var method = typeof(KurumController).GetMethod(nameof(KurumController.GetMyKurumlar));
        Assert.NotNull(method);

        var attributeData = method!.GetCustomAttributesData()
            .SingleOrDefault(x => x.AttributeType == typeof(PermissionAttribute));

        Assert.NotNull(attributeData);
        Assert.Empty(attributeData!.ConstructorArguments);
    }

    [Fact]
    public async Task UserController_Create_RejectsKurumAdminEscalation_ForNonSuperAdmin()
    {
        await using var dbContext = CreateIdentityDbContext();
        var userService = new FakeUserService();
        var userKurumService = new FakeUserKurumService();
        var controller = new UserController(
            userService,
            userKurumService,
            dbContext,
            new FakeCurrentTenantAccessor(isSuperAdmin: false, isKurumAdmin: true, currentKurumId: 5),
            new FakeKurumLookupService());

        var ex = await Assert.ThrowsAsync<BaseException>(() => controller.Create(new UserDto
        {
            UserName = "demo",
            KurumId = 5,
            IsKurumAdmin = true
        }, CancellationToken.None));

        Assert.Equal(403, ex.ErrorCode);
        Assert.Equal("Kurum admini olusturma yetkiniz bulunmuyor.", ex.Message);
        Assert.Equal(0, userService.AddCallCount);
        Assert.Null(userKurumService.LastAssignRequest);
    }

    private static TodIdentityDbContext CreateIdentityDbContext()
    {
        var options = new DbContextOptionsBuilder<TodIdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new TodIdentityDbContext(options);
    }

    private sealed class FakeUserService : IUserService
    {
        public int AddCallCount { get; private set; }

        public Task<IEnumerable<UserDto>> GetAllAsync(Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<UserDto?> GetByIdAsync(Guid id, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<UserDto>> GetPagedAsync(TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request, System.Linq.Expressions.Expression<Func<User, bool>>? predicate = null, Func<IQueryable<User>, IQueryable<User>>? include = null, Func<IQueryable<User>, IOrderedQueryable<User>>? orderBy = null)
            => throw new NotSupportedException();

        public Task<UserDto> AddAsync(UserDto dto)
        {
            AddCallCount++;
            dto.Id ??= Guid.NewGuid();
            return Task.FromResult(dto);
        }

        public Task<UserDto> UpdateAsync(UserDto dto) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id) => throw new NotSupportedException();

        public Task<IEnumerable<UserDto>> WhereAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<User, bool>> predicate, Func<IQueryable<User>, IQueryable<User>>? include = null)
            => throw new NotSupportedException();

        public Task ResetPasswordAsync(Guid id, UserResetPasswordDto dto) => throw new NotSupportedException();
    }

    private sealed class FakeUserKurumService : IUserKurumService
    {
        public AssignUserKurumRequest? LastAssignRequest { get; private set; }

        public Task<IEnumerable<UserKurumDto>> GetAllAsync(Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? include = null)
            => throw new NotSupportedException();

        public Task<UserKurumDto?> GetByIdAsync(Guid id, Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? include = null)
            => throw new NotSupportedException();

        public Task<TOD.Platform.Persistence.Rdbms.Paging.PagedResult<UserKurumDto>> GetPagedAsync(TOD.Platform.Persistence.Rdbms.Paging.PagedRequest request, System.Linq.Expressions.Expression<Func<TOD.Platform.Identity.UserKurums.Entities.UserKurum, bool>>? predicate = null, Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? include = null, Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IOrderedQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? orderBy = null)
            => throw new NotSupportedException();

        public Task<List<UserKurumDto>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<List<UserKurumDto>> GetByKurumIdAsync(int kurumId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<UserKurumDto> AssignAsync(AssignUserKurumRequest request, CancellationToken cancellationToken = default)
        {
            LastAssignRequest = request;
            return Task.FromResult(new UserKurumDto
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                KurumId = request.KurumId,
                VarsayilanMi = request.VarsayilanMi,
                AktifMi = request.AktifMi,
                IsKurumAdmin = request.IsKurumAdmin
            });
        }

        public Task<UserKurumDto> UpdateAsync(Guid id, UpdateUserKurumRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IEnumerable<UserKurumDto>> WhereAsync(System.Linq.Expressions.Expression<Func<TOD.Platform.Identity.UserKurums.Entities.UserKurum, bool>> predicate, Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? include = null)
            => throw new NotSupportedException();

        public Task<bool> AnyAsync(System.Linq.Expressions.Expression<Func<TOD.Platform.Identity.UserKurums.Entities.UserKurum, bool>> predicate, Func<IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>, IQueryable<TOD.Platform.Identity.UserKurums.Entities.UserKurum>>? include = null)
            => throw new NotSupportedException();

        public Task<UserKurumDto> AddAsync(UserKurumDto dto) => throw new NotSupportedException();

        public Task<UserKurumDto> UpdateAsync(UserKurumDto dto) => throw new NotSupportedException();

        public Task DeleteAsync(Guid id) => throw new NotSupportedException();
    }

    private sealed class FakeCurrentTenantAccessor : ICurrentTenantAccessor
    {
        private readonly bool _isSuperAdmin;
        private readonly bool _isKurumAdmin;
        private readonly int? _currentKurumId;

        public FakeCurrentTenantAccessor(bool isSuperAdmin, bool isKurumAdmin, int? currentKurumId)
        {
            _isSuperAdmin = isSuperAdmin;
            _isKurumAdmin = isKurumAdmin;
            _currentKurumId = currentKurumId;
        }

        public int? GetCurrentKurumId() => _currentKurumId;

        public IReadOnlyList<int> GetAccessibleKurumIds() => [];

        public bool IsSuperAdmin() => _isSuperAdmin;

        public bool IsKurumAdmin() => _isKurumAdmin;
    }

    private sealed class FakeKurumLookupService : IKurumLookupService
    {
        public Task<bool> IsActiveKurumAsync(int kurumId, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }
}
