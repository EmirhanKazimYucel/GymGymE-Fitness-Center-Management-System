using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;

namespace WebProje.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RoleAuthorizeAttribute : Attribute, IAsyncActionFilter
{
    private readonly string[] _allowedRoles;

    public RoleAuthorizeAttribute(params string[] allowedRoles)
    {
        _allowedRoles = allowedRoles?.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray() ?? Array.Empty<string>();
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        if (userId is null)
        {
            context.Result = BuildLoginRedirect(context);
            return;
        }

        var dbContext = context.HttpContext.RequestServices.GetService(typeof(FitnessContext)) as FitnessContext;
        string? currentRole = null;

        if (dbContext is not null)
        {
            var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
            if (user is null)
            {
                session.Clear();
                context.Result = BuildLoginRedirect(context);
                return;
            }

            currentRole = user.Role;
            session.SetString(SessionKeys.UserRole, user.Role);
        }
        else
        {
            currentRole = session.GetString(SessionKeys.UserRole);
        }

        if (_allowedRoles.Length > 0)
        {
            if (string.IsNullOrWhiteSpace(currentRole) || !_allowedRoles.Contains(currentRole, StringComparer.Ordinal))
            {
                context.Result = new ForbidResult();
                return;
            }
        }

        await next();
    }

    private static IActionResult BuildLoginRedirect(ActionContext context)
    {
        var request = context.HttpContext.Request;
        var returnUrl = request.Path.HasValue
            ? string.Concat(request.Path.Value, request.QueryString.HasValue ? request.QueryString.Value : string.Empty)
            : "/";

        return new RedirectToActionResult("Login", "Account", new { returnUrl });
    }
}
