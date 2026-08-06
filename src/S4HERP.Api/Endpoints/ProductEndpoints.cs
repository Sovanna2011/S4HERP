using Microsoft.EntityFrameworkCore;
using S4HERP.Api.Data;
using S4HERP.Api.Models;

namespace S4HERP.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/products").WithTags("Products");

        group.MapGet("/", async (AppDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().OrderBy(p => p.Sku).ToListAsync(ct));

        group.MapGet("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
            await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
                is { } product
                    ? Results.Ok(product)
                    : Results.NotFound());

        group.MapPost("/", async (Product product, AppDbContext db, CancellationToken ct) =>
        {
            db.Products.Add(product);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/products/{product.Id}", product);
        });

        group.MapPut("/{id:int}", async (int id, Product input, AppDbContext db, CancellationToken ct) =>
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (product is null)
            {
                return Results.NotFound();
            }

            product.Sku = input.Sku;
            product.Name = input.Name;
            product.UnitPrice = input.UnitPrice;
            product.QuantityOnHand = input.QuantityOnHand;
            await db.SaveChangesAsync(ct);
            return Results.Ok(product);
        });

        group.MapDelete("/{id:int}", async (int id, AppDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            return deleted > 0 ? Results.NoContent() : Results.NotFound();
        });

        return routes;
    }
}
