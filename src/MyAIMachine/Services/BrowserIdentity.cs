using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
namespace MyAIMachine.Services;

// Anonymous browser identity, protected against forgery and preserved across restarts.
// Retain ASP.NET Core Data Protection keys to preserve access to old conversations.
public sealed class BrowserIdentity(IDataProtectionProvider provider)
{
    private readonly IDataProtector protector = provider.CreateProtector("MyAIMachine.BrowserIdentity.v1");
    public string GetOwner(HttpContext context)
    {
        if (context.Items["BrowserOwner"] is string cached) return cached;
        string? owner = null;
        if (context.Request.Cookies.TryGetValue("MyAIMachine.Owner", out var cookie))
        {
            try
            {
                var candidate = protector.Unprotect(cookie);
                if (Guid.TryParseExact(candidate, "N", out _)) owner = candidate;
            }
            catch (CryptographicException) { /* Expired or invalid protection key: establish a new identity. */ }
        }
        if (owner is null)
        {
            owner = Guid.NewGuid().ToString("N");
            context.Response.Cookies.Append("MyAIMachine.Owner", protector.Protect(owner), new CookieOptions
            {
                HttpOnly = true, SameSite = SameSiteMode.Strict, Secure = context.Request.IsHttps,
                IsEssential = true, MaxAge = TimeSpan.FromDays(365)
            });
        }
        context.Items["BrowserOwner"] = owner;
        return owner;
    }
}
