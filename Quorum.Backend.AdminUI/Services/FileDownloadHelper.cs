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

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));
        
        var jsScript = $@"
            (function() {{
                try {{
                    const link = document.createElement('a');
                    link.download = '{filename}';
                    link.href = 'data:application/json;charset=utf-8;base64,{base64}';
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
