using SuperMWeb.Application;
using SuperMWeb.Domain;

namespace SuperMWeb.Domain.Tests;

// Caracterizacion de VentaCalculadora contra el comportamiento observable
// del desktop (VentasForm/ComprasForm).
public class VentaCalculadoraTests
{
    private static LineaTicket L(int id, string nombre, int cantidad, decimal precio) =>
        new() { IdProducto = id, Nombre = nombre, Cantidad = cantidad, PrecioUnitario = precio };

    // ---------- CalcularTotal ----------

    [Fact]
    public void CalcularTotal_lista_vacia_es_cero()
    {
        Assert.Equal(0m, VentaCalculadora.CalcularTotal(Array.Empty<LineaTicket>()));
    }

    [Fact]
    public void CalcularTotal_suma_los_subtotales()
    {
        var items = new[]
        {
            L(1, "Coca", 2, 3500m),   // 7000
            L(2, "Pan",  5, 800m),    // 4000
            L(3, "Leche",1, 4200m),   // 4200
        };
        Assert.Equal(15200m, VentaCalculadora.CalcularTotal(items));
    }

    [Fact]
    public void CalcularTotal_null_lanza()
    {
        Assert.Throws<ArgumentNullException>(() => VentaCalculadora.CalcularTotal(null!));
    }

    // ---------- CalcularCambio ----------

    [Theory]
    [InlineData(10000, 7500, 2500)]
    [InlineData(7500, 7500, 0)]
    [InlineData(50000, 12345, 37655)]
    public void CalcularCambio_recibido_mayor_o_igual_devuelve_diferencia(decimal recibido, decimal total, decimal esperado)
    {
        Assert.Equal(esperado, VentaCalculadora.CalcularCambio(recibido, total));
    }

    [Theory]
    [InlineData(5000, 7500)]
    [InlineData(0, 7500)]
    [InlineData(-100, 7500)] // caso patologico, replica el comportamiento del desktop (clamp a 0)
    public void CalcularCambio_recibido_menor_es_cero(decimal recibido, decimal total)
    {
        Assert.Equal(0m, VentaCalculadora.CalcularCambio(recibido, total));
    }

    // ---------- AgruparDetalle ----------

    [Fact]
    public void AgruparDetalle_lista_vacia_devuelve_vacia()
    {
        Assert.Empty(VentaCalculadora.AgruparDetalle(Array.Empty<LineaTicket>()));
    }

    [Fact]
    public void AgruparDetalle_producto_repetido_suma_cantidades()
    {
        var items = new[]
        {
            L(1, "Coca", 1, 3500m),
            L(1, "Coca", 2, 3500m),
            L(1, "Coca", 1, 3500m),
        };
        var r = VentaCalculadora.AgruparDetalle(items);
        Assert.Single(r);
        Assert.Equal(4, r[0].Cantidad);
        Assert.Equal(3500m, r[0].PrecioUnitario);
        Assert.Equal(14000m, r[0].Subtotal);
    }

    [Fact]
    public void AgruparDetalle_mismo_id_distinto_precio_produce_dos_lineas()
    {
        // El GroupBy del desktop agrupa por IdProducto/Nombre/PrecioUnitario:
        // si un producto se agrega dos veces con precios distintos (edge case),
        // el desktop produce dos lineas separadas. Este test lo fija.
        var items = new[]
        {
            L(1, "Coca", 2, 3500m),
            L(1, "Coca", 1, 4000m),
        };
        var r = VentaCalculadora.AgruparDetalle(items);
        Assert.Equal(2, r.Count);
        Assert.Contains(r, x => x.PrecioUnitario == 3500m && x.Cantidad == 2);
        Assert.Contains(r, x => x.PrecioUnitario == 4000m && x.Cantidad == 1);
    }

    [Fact]
    public void AgruparDetalle_productos_distintos_los_mantiene_separados()
    {
        var items = new[]
        {
            L(1, "Coca", 1, 3500m),
            L(2, "Pan",  3, 800m),
            L(1, "Coca", 2, 3500m),
        };
        var r = VentaCalculadora.AgruparDetalle(items);
        Assert.Equal(2, r.Count);
        Assert.Contains(r, x => x.IdProducto == 1 && x.Cantidad == 3);
        Assert.Contains(r, x => x.IdProducto == 2 && x.Cantidad == 3);
    }

    [Fact]
    public void AgruparDetalle_null_lanza()
    {
        Assert.Throws<ArgumentNullException>(() => VentaCalculadora.AgruparDetalle(null!));
    }
}
