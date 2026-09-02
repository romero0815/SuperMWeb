using SuperMWeb.Domain;

namespace SuperMWeb.Domain.Tests;

// Caracteriza el calculo de Subtotal en las entidades del Domain.
// La regla original (desktop y BD) es: Subtotal = Cantidad * PrecioUnitario,
// sin redondeo aplicativo (la BD lo guarda como decimal(18,2) computed PERSISTED).
public class SubtotalTests
{
    [Theory]
    [InlineData(1, 1000, 1000)]
    [InlineData(3, 2500, 7500)]
    [InlineData(10, 999, 9990)]
    [InlineData(0, 5000, 0)]
    [InlineData(1, 0, 0)]
    [InlineData(7, 1234.56, 8641.92)]     // precio con decimales, aunque el POS los oculte
    [InlineData(100, 15000, 1500000)]     // canasta grande
    public void DetalleFactura_Subtotal_es_cantidad_por_precio(int cantidad, decimal precio, decimal esperado)
    {
        var d = new DetalleFactura { Cantidad = cantidad, PrecioUnitario = precio };
        Assert.Equal(esperado, d.Subtotal);
    }

    [Theory]
    [InlineData(1, 1000, 1000)]
    [InlineData(3, 2500, 7500)]
    [InlineData(0, 5000, 0)]
    [InlineData(7, 1234.56, 8641.92)]
    public void DetalleCompra_Subtotal_es_cantidad_por_precio(int cantidad, decimal precio, decimal esperado)
    {
        var d = new DetalleCompra { Cantidad = cantidad, PrecioUnitario = precio };
        Assert.Equal(esperado, d.Subtotal);
    }

    [Theory]
    [InlineData(1, 1000, 1000)]
    [InlineData(3, 2500, 7500)]
    [InlineData(0, 5000, 0)]
    public void LineaTicket_Subtotal_es_cantidad_por_precio(int cantidad, decimal precio, decimal esperado)
    {
        var l = new LineaTicket { Cantidad = cantidad, PrecioUnitario = precio, Nombre = "X" };
        Assert.Equal(esperado, l.Subtotal);
    }
}
