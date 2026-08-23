using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Open.IdentityServer.EntityFramework.DbContexts;
using Open.IdentityServer.EntityFramework.Mappers;
using Open.IdentityServer.Models;
using System.ComponentModel.DataAnnotations;

namespace Quorum.Backend.Areas.Admin.Pages.Clients;

public class CreateModel : PageModel
{
    private readonly ConfigurationDbContext _context;

    public CreateModel(ConfigurationDbContext context)
    {
        _context = context;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "Pole Client ID jest wymagane")]
        public string ClientId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pole Nazwa Klienta jest wymagane")]
        public string ClientName { get; set; } = string.Empty;

        public string? ClientSecret { get; set; }

        [Required]
        public string GrantType { get; set; } = "client_credentials";

        public string AllowedScopes { get; set; } = "api1";

        public string? RedirectUris { get; set; }

        public bool RequirePkce { get; set; } = true;
    }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var client = new Client
        {
            ClientId = Input.ClientId.Trim(),
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
                client.RedirectUris = Input.RedirectUris.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            }
        }

        if (!string.IsNullOrWhiteSpace(Input.ClientSecret))
        {
            client.ClientSecrets = new List<Secret> { new Secret(Input.ClientSecret.Sha256()) };
        }

        if (!string.IsNullOrWhiteSpace(Input.AllowedScopes))
        {
            client.AllowedScopes = Input.AllowedScopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        _context.Clients.Add(client.ToEntity());
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Klient '{Input.ClientId}' został utworzony w bazie danych.";
        return RedirectToPage("Index");
    }
}
