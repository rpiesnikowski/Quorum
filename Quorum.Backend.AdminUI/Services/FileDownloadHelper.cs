using Microsoft.JSInterop;
using System.Text;

namespace Quorum.Backend.AdminUI.Services;

public static class FileDownloadHelper
{
    /// <summary>
    /// Pobiera plik JSON bezpośrednio w przeglądarce użytkownika.
    /// </summary>
    public static async Task DownloadJsonFileAsync(IJSRuntime jsRuntime, string filename, string jsonContent)
    {
        if (string.IsNullOrEmpty(filename))
        {
            filename = $"export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
        }
        else if (!filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            filename += ".json";
        }

        await DownloadTextFileAsync(jsRuntime, filename, jsonContent, "application/json");
    }

    /// <summary>
    /// Pobiera dowolny plik tekstowy (SQL, Liquibase XML, YAML) w przeglądarce użytkownika.
    /// </summary>
    public static async Task DownloadTextFileAsync(IJSRuntime jsRuntime, string filename, string content, string mimeType = "text/plain")
    {
        if (string.IsNullOrEmpty(filename))
        {
            filename = $"script-{DateTime.UtcNow:yyyyMMdd-HHmmss}.txt";
        }

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        
        var jsScript = $@"
            (function() {{
                try {{
                    const link = document.createElement('a');
                    link.download = '{filename}';
                    link.href = 'data:{mimeType};charset=utf-8;base64,{base64}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                }} catch (e) {{
                    console.error('Błąd pobierania pliku:', e);
                }}
            }})();
        ";

        await jsRuntime.InvokeVoidAsync("eval", jsScript);
    }
}
