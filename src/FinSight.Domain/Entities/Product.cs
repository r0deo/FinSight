using System.ComponentModel.DataAnnotations;

namespace FinSight.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }

    public Guid BusinessId { get; set; }

    [Required]
    [MaxLength(60)]
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string? Size { get; set; }

    public decimal SellingPrice { get; set; }

    public decimal UnitCost { get; set; }

    public bool IsActive { get; set; }
}
