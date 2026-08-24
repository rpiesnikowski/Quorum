using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.AdminUI.Areas.Admin.Pages.Clients;

public class CreateModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public CreateModel(ConfigurationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class ScopeItemDto
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Type { get; set; } = "API"; // "Identity" or "API"
        public bool Emphasize { get; set; }
    }

    public List<ScopeItemDto> AvailableScopes { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Pole Client ID jest wymagane")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pole Nazwa Klienta jest wymagane")]
        public string ClientName { get; set; } = string.Empty;

        public string? ClientSecret { get; set; }

        [Required]
        public string GrantType { get; set; } = "client_credentials";

        public string AllowedScopes { get; set; } = "openid profile api1";

        public string? RedirectUris { get; set; }

        public bool RequirePkce { get; set; } = true;
    }

    public async Task OnGetAsync()
    {
        await LoadAvailableScopesAsync();
    }

    private async Task LoadAvailableScopesAsync()
    {
        var identityScopes = await _context.IdentityResources
            .AsNoTracking()
            .Where(r => r.Enabled)
            .OrderBy(r => r.Name)
            .Select(r => new ScopeItemDto
            {
                Name = r.Name,
                DisplayName = r.DisplayName ?? r.Name,
                Description = r.Description,
                Type = "Tożsamość (Identity)",
                Emphasize = r.Emphasize
            })
            .ToListAsync();

        var apiScopes = await _context.ApiScopes
            .AsNoTracking()
            .Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .Select(s => new ScopeItemDto
            {
                Name = s.Name,
                DisplayName = s.DisplayName ?? s.Name,
                Description = s.Description,
                Type = "API",
                Emphasize = s.Emphasize
            })
            .ToListAsync();

        AvailableScopes = identityScopes.Concat(apiScopes).ToList();

        // Dodaj offline_access jeśli nie ma go w liście
        if (!AvailableScopes.Any(s => s.Name == "offline_access"))
        {
            AvailableScopes.Add(new ScopeItemDto
            {
                Name = "offline_access",
                DisplayName = "Dostęp offline (Refresh Token)",
                Description = "Pozwala aplikacji na odświeżanie tokenów bez ponownego logowania",
                Type = "Protokół OIDC",
                Emphasize = false
            });
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            await LoadAvailableScopesAsync();
            return Page();
        }

        var normalizedClientId = Input.ClientId.Trim();

        var exists = await _context.Clients.AnyAsync(c => c.ClientId == normalizedClientId);
        if (exists)
        {
            ModelState.AddModelError("Input.ClientId", $"Klient o identyfikatorze '{normalizedClientId}' już istnieje w bazie danych.");
            await LoadAvailableScopesAsync();
            return Page();
        }

        var client = new Client
        {
            ClientId = normalizedClientId,
            ClientName = Input.ClientName.Trim(),
            Enabled = true,
            RequirePkce = Input.RequirePkce
        };

        if (Input.GrantType == "client_credentials")
        {
            client.AllowedGrantTypes = GrantTypes.ClientCredentials;
        }
        else
        {
            client.AllowedGrantTypes = GrantTypes.Code;
            client.AllowOfflineAccess = true;
            if (!string.IsNullOrWhiteSpace(Input.RedirectUris))
            {
                client.RedirectUris = Input.RedirectUris.Split(new[] { ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(Input.ClientSecret))
        {
            client.ClientSecrets = new List<Secret> { new Secret(Input.ClientSecret.Sha256()) };
        }

        if (!string.IsNullOrWhiteSpace(Input.AllowedScopes))
        {
            client.AllowedScopes = Input.AllowedScopes.Split(new[] { ' ', ',', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        try
        {
            _context.Clients.Add(client.ToEntity());
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            var isDuplicate = await _context.Clients.AnyAsync(c => c.ClientId == normalizedClientId);
            if (isDuplicate)
            {
                ModelState.AddModelError("Input.ClientId", $"Klient o identyfikatorze '{normalizedClientId}' już istnieje w bazie.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Wystąpił błąd podczas zapisywania klienta do bazy danych.");
            }
            await LoadAvailableScopesAsync();
            return Page();
        }

        TempData["SuccessMessage"] = $"Klient '{Input.ClientId}' został utworzony w bazie danych.";
        return RedirectToPage("Index");
    }
}
