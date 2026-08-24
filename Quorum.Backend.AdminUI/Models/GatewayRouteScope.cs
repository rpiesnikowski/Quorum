using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Quorum.Backend.AdminUI.Models;

/// <summary>
/// Tabela mapująca wiele zakresów (Scopes) do reguły API Gateway.
/// </summary>
[Table("GatewayRouteScopes")]
public class GatewayRouteScope
{
    [Key]
    public int Id { get; set; }

    /// <summary>
    /// Identyfikator reguły API Gateway
    /// </summary>
    [Required]
    public int GatewayRouteId { get; set; }

    /// <summary>
    /// Relacja do reguły GatewayRoute
    /// </summary>
    [ForeignKey(nameof(GatewayRouteId))]
    public virtual GatewayRoute? GatewayRoute { get; set; }

    /// <summary>
    /// Nazwa zakresu (np. api1, api.read, orders.write, openid)
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string Scope { get; set; } = string.Empty;
}
