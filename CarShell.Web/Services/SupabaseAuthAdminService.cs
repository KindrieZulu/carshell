using System.Text.Json.Serialization;

namespace CarShell.Web.Services;

public class SupabaseAuthAdminService(HttpClient http) : ISupabaseAuthAdminService
{
    public async Task<CreatedAuthUser> CreateUserAsync(string email, string password, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync(
            "admin/users",
            new CreateUserRequest(email, password, EmailConfirm: true),
            ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Supabase rejected the new admin account: {body}");
        }

        var payload = await response.Content.ReadFromJsonAsync<CreateUserResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("Supabase Admin API returned an empty response.");

        return new CreatedAuthUser(payload.Id, payload.Email);
    }

    private record CreateUserRequest(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("password")] string Password,
        [property: JsonPropertyName("email_confirm")] bool EmailConfirm);

    private record CreateUserResponse(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("email")] string Email);
}
