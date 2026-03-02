using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TOD.Platform.Identity.MenuItemRoles.Entities;
using TOD.Platform.Identity.MenuItems.DTO;
using TOD.Platform.Identity.MenuItems.Entities;
using TOD.Platform.Identity.MenuItems.Repositories;
using TOD.Platform.Identity.Roles.Repositories;
using TOD.Platform.Persistence.Rdbms.Services;

namespace TOD.Platform.Identity.MenuItems.Services;

public class MenuItemService : BaseRdbmsService<MenuItemDto, MenuItem>, IMenuItemService
{
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly IRoleRepository _roleRepository;

    public MenuItemService(IMenuItemRepository menuItemRepository, IRoleRepository roleRepository, IMapper mapper)
        : base(menuItemRepository, mapper)
    {
        _menuItemRepository = menuItemRepository;
        _roleRepository = roleRepository;
    }

    public async Task<IEnumerable<MenuItemDto>> GetMenuTreeAsync()
    {
        var menuItems = await _menuItemRepository.GetAllAsync(q => q.Include(x => x.MenuItemRoles).ThenInclude(x => x.Role));

        var roots = menuItems.Where(x => x.ParentId is null).OrderBy(x => x.MenuOrder).ToList();
        var list = new List<MenuItemDto>();

        foreach (var root in roots)
        {
            var rootDto = Mapper.Map<MenuItemDto>(root);
            var children = menuItems
                .Where(x => x.ParentId == root.Id)
                .OrderBy(x => x.MenuOrder)
                .Select(Mapper.Map<MenuItemDto>)
                .ToList();

            rootDto.Items = children.Count > 0 ? children : null;
            list.Add(rootDto);
        }

        return list;
    }

    public override async Task<MenuItemDto> AddAsync(MenuItemDto dto)
    {
        var menuItem = Mapper.Map<MenuItem>(dto);
        menuItem.MenuItemRoles = new List<MenuItemRole>();

        foreach (var roleId in dto.Roles?.Select(x => x.Id).Where(x => x.HasValue).Select(x => x!.Value).Distinct() ?? Enumerable.Empty<Guid>())
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null || !string.Equals(role.Name, "Menu", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            menuItem.MenuItemRoles.Add(new MenuItemRole
            {
                MenuItem = menuItem,
                Role = role
            });
        }

        await Repository.AddAsync(menuItem);
        await Repository.SaveChangesAsync();

        return Mapper.Map<MenuItemDto>(menuItem);
    }

    public override async Task<MenuItemDto> UpdateAsync(MenuItemDto dto)
    {
        if (!dto.Id.HasValue)
        {
            throw new InvalidOperationException("Id cannot be empty.");
        }

        var menuItem = await Repository.GetByIdAsync(dto.Id.Value, q => q.IgnoreQueryFilters().Include(x => x.MenuItemRoles));
        if (menuItem is null)
        {
            throw new InvalidOperationException("Menu item was not found.");
        }

        menuItem.Label = dto.Label;
        menuItem.Icon = dto.Icon;
        menuItem.Route = dto.Route;
        menuItem.QueryParams = dto.QueryParams;
        menuItem.MenuOrder = dto.MenuOrder;
        menuItem.ParentId = dto.ParentId;

        var desiredRoleIds = new HashSet<Guid>();
        foreach (var roleId in dto.Roles?.Select(x => x.Id).Where(x => x.HasValue).Select(x => x!.Value).Distinct() ?? Enumerable.Empty<Guid>())
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null || !string.Equals(role.Name, "Menu", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            desiredRoleIds.Add(roleId);
        }

        var existingRoleIds = menuItem.MenuItemRoles.Select(x => x.RoleId).ToHashSet();

        foreach (var menuItemRole in menuItem.MenuItemRoles)
        {
            menuItemRole.IsDeleted = !desiredRoleIds.Contains(menuItemRole.RoleId);
        }

        foreach (var roleId in desiredRoleIds.Except(existingRoleIds))
        {
            var role = await _roleRepository.GetByIdAsync(roleId);
            if (role is null)
            {
                continue;
            }

            menuItem.MenuItemRoles.Add(new MenuItemRole
            {
                MenuItemId = menuItem.Id,
                RoleId = roleId,
                MenuItem = menuItem,
                Role = role,
                IsDeleted = false
            });
        }

        Repository.Update(menuItem);
        await Repository.SaveChangesAsync();

        return dto;
    }
}
