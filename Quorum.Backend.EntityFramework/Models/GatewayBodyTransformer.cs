using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Fluid;
using Fluid.Values;

namespace Quorum.Backend.EntityFramework.Models;

/// <summary>
/// Serwis transformacji treści żądania (Body) dla API Gateway.
/// Obsługuje silnik Fluid (Liquid: {{ body.JsonProperty }}) oraz JUST.net (JSON Under Simple Transformation).
/// </summary>
public static class GatewayBodyTransformer
{
    private static readonly FluidParser FluidParser = new();

    /// <summary>
    /// Transformuje wejściowe Body żądania zgodnie ze zdefiniowanym szablonem w regule trasy.
    /// </summary>
    /// <param name="inputBody">Oryginalna treść żądania HTTP otrzymana od klienta.</param>
    /// <param name="bodyTemplate">Szablon transformacji z konfiguracji trasy (route.Body).</param>
    /// <param name="transformType">Silnik transformacji: "Fluid" lub "JUST".</param>
    /// <param name="match">Dopasowanie Regex dla trasy.</param>
    /// <param name="capturedGroups">Grupy przechwycone z URL.</param>
    /// <param name="headers">Nagłówki żądania HTTP.</param>
    /// <param name="errorMessage">Komunikat błędu, jeśli transformacja się nie powiodła.</param>
    /// <returns>Przetransformowana treść żądania lub treść oryginalna / pusta.</returns>
    public static string? Transform(
        string? inputBody,
        string? bodyTemplate,
        string? transformType,
        Match? match,
        IDictionary<string, string>? capturedGroups,
        IDictionary<string, string>? headers,
        out string? errorMessage)
    {
        errorMessage = null;

        // 1. Jeśli brak szablonu (pusta kolumna Body), przekazujemy treść wejściową bez zmian
        if (string.IsNullOrWhiteSpace(bodyTemplate))
        {
            return inputBody;
        }

        // 2. Jeśli szablon to (empty), usuwamy treść żądania przed wysłaniem upstream
        if (GatewayRouteMatcher.IsEmptyValue(bodyTemplate))
        {
            return string.Empty;
        }

        // 3. Podstawienie parametrów URL Regex / grup w szablonie (np. {grupa}, $1)
        var resolvedTemplate = GatewayRouteMatcher.ApplyReplacements(bodyTemplate, match, capturedGroups);

        var engine = (transformType ?? "Fluid").Trim();

        if (engine.Equals("JUST", StringComparison.OrdinalIgnoreCase) ||
            engine.Equals("JUST.net", StringComparison.OrdinalIgnoreCase) ||
            engine.Equals("JUSTnet", StringComparison.OrdinalIgnoreCase))
        {
            return TransformWithJust(inputBody, resolvedTemplate, out errorMessage);
        }
        else
        {
            return TransformWithFluid(inputBody, resolvedTemplate, capturedGroups, headers, out errorMessage);
        }
    }

    /// <summary>
    /// Transformacja za pomocą silnika Fluid (Liquid: {{ body.JsonProperty }}).
    /// </summary>
    public static string? TransformWithFluid(
        string? inputBody,
        string templateString,
        IDictionary<string, string>? capturedGroups,
        IDictionary<string, string>? headers,
        out string? errorMessage)
    {
        errorMessage = null;

        if (!FluidParser.TryParse(templateString, out var template, out var parseError))
        {
            errorMessage = $"Błąd parsowania szablonu Fluid: {parseError}";
            return inputBody;
        }

        try
        {
            var options = new TemplateOptions();
            var context = new TemplateContext(options);

            // Parsowanie wejściowego JSON do słownika obiektów dla Fluid
            object? parsedBody = null;
            if (!string.IsNullOrWhiteSpace(inputBody))
            {
                try
                {
                    using var doc = JsonDocument.Parse(inputBody);
                    parsedBody = ConvertJsonElement(doc.RootElement);
                }
                catch
                {
                    // Jeśli wejście nie jest poprawnym JSON-em, przekazujemy jako czysty string
                    parsedBody = inputBody;
                }
            }

            // Rejestracja zmiennych dostępnych w szablonie
            if (parsedBody != null)
            {
                context.SetValue("body", parsedBody);
                context.SetValue("root", parsedBody);
            }
            else
            {
                context.SetValue("body", new Dictionary<string, object?>());
            }

            if (headers != null && headers.Count > 0)
            {
                context.SetValue("headers", headers);
            }

            if (capturedGroups != null && capturedGroups.Count > 0)
            {
                context.SetValue("groups", capturedGroups);
            }

            context.SetValue("raw_body", inputBody ?? string.Empty);

            var result = template.Render(context, NullEncoder.Default);
            return result;
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd wykonania transformacji Fluid: {ex.Message}";
            return inputBody;
        }
    }

    /// <summary>
    /// Transformacja za pomocą biblioteki JUST.net (JSON Under Simple Transformation).
    /// </summary>
    public static string? TransformWithJust(
        string? inputBody,
        string transformerJson,
        out string? errorMessage)
    {
        errorMessage = null;

        var sourceJson = !string.IsNullOrWhiteSpace(inputBody) ? inputBody.Trim() : "{}";

        try
        {
            // JUST.JsonTransformer wykonuje transformację JSON -> JSON
            var result = JUST.JsonTransformer.Transform(transformerJson, sourceJson);
            return result;
        }
        catch (Exception ex)
        {
            errorMessage = $"Błąd transformacji JUST.net: {ex.Message}";
            return inputBody;
        }
    }

    /// <summary>
    /// Konwertuje rekurencyjnie JsonElement do struktur Dictionary / List dla elastycznego wiązania w Liquid / Fluid.
    /// </summary>
    private static object? ConvertJsonElement(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in element.EnumerateObject())
                {
                    dict[property.Name] = ConvertJsonElement(property.Value);
                }
                return dict;

            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(ConvertJsonElement(item));
                }
                return list;

            case JsonValueKind.String:
                return element.GetString();

            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longVal))
                    return longVal;
                if (element.TryGetDouble(out var doubleVal))
                    return doubleVal;
                return element.GetDecimal();

            case JsonValueKind.True:
                return true;

            case JsonValueKind.False:
                return false;

            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            default:
                return null;
        }
    }
}
