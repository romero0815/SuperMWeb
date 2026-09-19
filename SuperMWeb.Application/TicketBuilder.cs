using System.Text;
using SuperMWeb.Domain;

namespace SuperMWeb.Application;

// Genera los bytes ESC/POS de los tickets. Portado tal cual desde
// SuperM.Clases.Utils.Impresion sin cambio de contenido. Funciones puras:
// no habla con la impresora, solo devuelve byte[] listo para enviar.
public static class TicketBuilder
{
    // Ancho fisico usado para el centrado en todos los tickets del desktop.
    private const int AnchoCentrado = 45;

    public static byte[] ConstruirFactura(IEnumerable<LineaTicket> productos, decimal total, string nombreNegocio)
    {
        ArgumentNullException.ThrowIfNull(productos);
        ArgumentNullException.ThrowIfNull(nombreNegocio);

        List<byte> bytes = new List<byte>();

        // Nombre del negocio en doble altura
        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x10 });
        bytes.AddRange(Encoding.ASCII.GetBytes(CentrarTexto(nombreNegocio.ToUpper()) + "\n"));

        // Volver a tamano normal
        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 });

        // Fecha
        bytes.AddRange(Encoding.ASCII.GetBytes(CentrarTexto("Fecha: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm tt")) + "\n"));

        bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("Producto       Cant   V.Unit    Subtotal\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

        // Ajuste de interlineado (un poco mas grande)
        bytes.AddRange(new byte[] { 0x1B, 0x33, 28 }); // ESC 3 n -> n=28 puntos

        foreach (var item in productos)
        {
            string nombreCompleto = item.Nombre;
            int maxLong = 13;

            string nombreLinea1 = nombreCompleto.Length > maxLong
                ? nombreCompleto[..maxLong]
                : nombreCompleto.PadRight(maxLong);

            string cantidad = item.Cantidad.ToString().PadLeft(5);
            string precio = item.PrecioUnitario.ToString("C0").PadLeft(10);
            string subtotal = (item.Cantidad * item.PrecioUnitario).ToString("C0").PadLeft(12);

            bytes.AddRange(Encoding.ASCII.GetBytes($"{nombreLinea1}{cantidad}{precio}{subtotal}\n"));

            if (nombreCompleto.Length > maxLong)
            {
                string nombreLinea2 = nombreCompleto[maxLong..];
                bytes.AddRange(Encoding.ASCII.GetBytes(nombreLinea2 + "\n"));
            }

            bytes.AddRange(Encoding.ASCII.GetBytes("\n"));
        }

        bytes.AddRange(new byte[] { 0x1B, 0x32 });

        bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes($"TOTAL: {total:C0}".PadLeft(40) + "\n\n"));

        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x10 });
        bytes.AddRange(Encoding.ASCII.GetBytes(CentrarTexto("¡Gracias por su compra!") + "\n"));

        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 });
        bytes.AddRange(Encoding.ASCII.GetBytes("\n\n\n\n\n\n\n\n"));

        // Corte automatico
        bytes.AddRange(new byte[] { 0x1B, 0x69 });

        return bytes.ToArray();
    }

    public static byte[] ConstruirEtiquetaPrecio(string nombre, decimal precio)
    {
        ArgumentNullException.ThrowIfNull(nombre);

        List<byte> bytes = new List<byte>();

        bytes.AddRange(new byte[] { 0x1B, 0x32 });

        bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

        // Activar negrita
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x01 });

        // Tamano mas grande
        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x10 });

        bytes.AddRange(Encoding.ASCII.GetBytes(CentrarTexto(nombre.ToUpper()) + "\n\n"));
        bytes.AddRange(Encoding.ASCII.GetBytes(CentrarTexto($"PRECIO: {precio:C0}") + "\n\n"));

        // Desactivar negrita
        bytes.AddRange(new byte[] { 0x1B, 0x45, 0x00 });

        bytes.AddRange(new byte[] { 0x1B, 0x32 });
        bytes.AddRange(Encoding.ASCII.GetBytes("------------------------------------------\n"));

        bytes.AddRange(new byte[] { 0x1B, 0x21, 0x00 });
        bytes.AddRange(Encoding.ASCII.GetBytes("\n\n\n\n"));

        // Cortar papel
        bytes.AddRange(new byte[] { 0x1B, 0x69 });

        return bytes.ToArray();
    }

    // Pulso al conector DK de la impresora para abrir el cajon monedero
    // (ESC p m t1 t2). m=0 dispara el pin 2 y m=1 el pin 5; t1/t2 son el
    // tiempo encendido/apagado en unidades de 2 ms (50 ms / 500 ms).
    public static byte[] ConstruirAperturaCajon(byte pin = 0)
    {
        if (pin > 1)
            throw new ArgumentOutOfRangeException(nameof(pin), "El pin del cajon debe ser 0 (pin 2) o 1 (pin 5).");

        return new byte[] { 0x1B, 0x70, pin, 0x19, 0xFA };
    }

    private static string CentrarTexto(string texto, int ancho = AnchoCentrado)
    {
        int espacios = (ancho - texto.Length) / 2;
        return new string(' ', espacios > 0 ? espacios : 0) + texto;
    }
}
