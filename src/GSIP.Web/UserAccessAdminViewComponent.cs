using GSIP.Infrastructure.Authorization;
using GSIP.Infrastructure.Setup;
using GSIP.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GSIP.Web;

[ViewComponent(Name = "UserAccessAdmin")]
public sealed class UserAccessAdminViewComponent(GsipDbContext dbContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Guid? userId)
    {
        var entities = await dbContext.CatalogEntities
            .AsNoTracking()
            .Where(entity => entity.Active)
            .OrderBy(entity => entity.DisplayOrder)
            .ThenBy(entity => entity.Code)
            .Select(entity => new UserAccessEntityOptionViewModel(
                entity.Code,
                entity.NameAr,
                entity.NameEn))
            .ToListAsync();

        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new UserAccessRoleOptionViewModel(
                role.Id,
                role.Name ?? string.Empty))
            .ToListAsync();

        Guid? selectedUserId = null;
        var selectedDisplayName = string.Empty;
        var selectedUsername = string.Empty;
        var scope = new EntityAccessScope(true, new HashSet<string>(StringComparer.Ordinal));

        if (userId is Guid exactUserId && exactUserId != Guid.Empty)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .Where(candidate => candidate.Id == exactUserId)
                .Select(candidate => new
                {
                    candidate.Id,
                    candidate.DisplayName,
                    Username = candidate.UserName ?? string.Empty
                })
                .SingleOrDefaultAsync();
            if (user is not null)
            {
                selectedUserId = user.Id;
                selectedDisplayName = user.DisplayName;
                selectedUsername = user.Username;
                scope = await EntityAccessScopeStore.GetForUserAsync(dbContext, user.Id);
            }
        }

        return View(new UserAccessAdminViewModel
        {
            Entities = entities,
            Roles = roles,
            SelectedUserId = selectedUserId,
            SelectedUserDisplayName = selectedDisplayName,
            SelectedUsername = selectedUsername,
            ScopeIsUnrestricted = scope.IsUnrestricted,
            SelectedEntityCodes = scope.EntityCodes
        });
    }
}
