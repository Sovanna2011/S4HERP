using System.ComponentModel.DataAnnotations;

namespace S4HERP.Api.Models;

public class Product
{
    public int Id { get; set; }

    [MaxLength(64)]
    public required string Sku { get; set; }

    [MaxLength(256)]
    public required string Name { get; set; }

    public decimal UnitPrice { get; set; }

    public int QuantityOnHand { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
