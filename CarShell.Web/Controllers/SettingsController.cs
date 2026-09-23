using CarShell.Web.Auth;
using CarShell.Web.Data;
using CarShell.Web.Data.Entities;
using CarShell.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CarShell.Web.Controllers;

// Self-service account settings for the signed-in admin: display name,
// public contact details, and avatar. Deliberately does not touch the
// Supabase Auth login email or password -- changing the identity an admin
// actually signs in with is a separate, more sensitive flow (Supabase's own
// email-change confirmation) that this endpoint set doesn't attempt.
[ApiController]
[Route("api/settings/me")]
[Authorize(Policy = "AdminOnly")]
public class SettingsController(CarShellDbContext db, ISupabaseStorageService storage) : ControllerBase
{
    private static readonly Dictionary<string, string> AllowedImageContentTypes = new()
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };
    private const long MaxAvatarSizeBytes = 5 * 1024 * 1024;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(new SettingsResponse(
            user.Email, user.Username, user.ContactEmail, user.ContactPhone, user.ProfilePictureStorageKey,
            user.IsSuperAdmin));
    }

    [HttpPatch]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return NotFound();
        }

        var username = string.IsNullOrWhiteSpace(request.Username) ? null : request.Username.Trim();
        if (username is not null)
        {
            var taken = await db.Users.AnyAsync(u => u.Id != userId && u.Username == username, ct);
            if (taken)
            {
                return Conflict("That username is already taken.");
            }
        }

        var contactEmail = string.IsNullOrWhiteSpace(request.ContactEmail) ? null : request.ContactEmail.Trim();
        if (contactEmail is not null && !contactEmail.Contains('@'))
        {
            return BadRequest("Contact email doesn't look valid.");
        }

        user.Username = username;
        user.ContactEmail = contactEmail;
        user.ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim();

        await db.SaveChangesAsync(ct);

        return Ok(new SettingsResponse(
            user.Email, user.Username, user.ContactEmail, user.ContactPhone, user.ProfilePictureStorageKey,
            user.IsSuperAdmin));
    }

    [HttpPost("avatar/upload-url")]
    public async Task<IActionResult> GetAvatarUploadUrl([FromBody] RequestAvatarUploadRequest request, CancellationToken ct)
    {
        if (!AllowedImageContentTypes.TryGetValue(request.ContentType, out var extension))
        {
            return BadRequest("Content type must be one of: " + string.Join(", ", AllowedImageContentTypes.Keys));
        }

        var userId = User.GetUserId();
        var storageKey = $"avatars/{userId}/{Guid.NewGuid():N}{extension}";
        var signed = await storage.CreateSignedUploadUrlAsync(storageKey, ct);

        return Ok(new { uploadUrl = signed.UploadUrl, storageKey = signed.StorageKey });
    }

    [HttpPost("avatar/confirm")]
    public async Task<IActionResult> ConfirmAvatarUpload([FromBody] ConfirmAvatarUploadRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (!request.StorageKey.StartsWith($"avatars/{userId}/", StringComparison.Ordinal))
        {
            return BadRequest("Storage key does not belong to this account.");
        }

        var info = await storage.GetObjectInfoAsync(request.StorageKey, ct);
        if (info is null)
        {
            return BadRequest("No uploaded object was found at that storage key.");
        }
        if (!AllowedImageContentTypes.ContainsKey(info.ContentType))
        {
            return BadRequest("Unsupported content type.");
        }
        if (info.SizeBytes > MaxAvatarSizeBytes)
        {
            return BadRequest($"Image exceeds the maximum allowed size of {MaxAvatarSizeBytes / (1024 * 1024)}MB.");
        }

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
        {
            return NotFound();
        }

        user.ProfilePictureStorageKey = request.StorageKey;
        await db.SaveChangesAsync(ct);

        return Ok(new { user.ProfilePictureStorageKey });
    }
}

public record SettingsResponse(
    string Email, string? Username, string? ContactEmail, string? ContactPhone, string? ProfilePictureStorageKey,
    bool IsSuperAdmin);

public record UpdateSettingsRequest(string? Username, string? ContactEmail, string? ContactPhone);

public record RequestAvatarUploadRequest(string ContentType);

public record ConfirmAvatarUploadRequest(string StorageKey);
