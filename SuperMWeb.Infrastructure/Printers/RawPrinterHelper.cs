using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace SuperMWeb.Infrastructure.Printers;

// P/Invoke a winspool.drv para enviar bytes RAW a la impresora.
// Portado desde SuperM.Clases.Helpers.RawPrinterHelper sin cambio funcional
// salvo por el manejo de errores: aqui se retorna el codigo Win32 en vez
// de mostrar un dialogo WinForms (que era responsabilidad de UI).
[SupportedOSPlatform("windows")]
internal static class RawPrinterHelper
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private class DOCINFOA
    {
        [MarshalAs(UnmanagedType.LPStr)] public string pDocName = string.Empty;
        [MarshalAs(UnmanagedType.LPStr)] public string? pOutputFile;
        [MarshalAs(UnmanagedType.LPStr)] public string pDataType = "RAW";
    }

    [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern bool StartDocPrinter(IntPtr hPrinter, int Level, [In] DOCINFOA pDocInfo);

    [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

    public static string? BuscarImpresora(string? nombreParcial)
    {
        if (string.IsNullOrWhiteSpace(nombreParcial))
            return null;

        foreach (string printer in PrinterSettings.InstalledPrinters)
        {
            if (printer.IndexOf(nombreParcial, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return printer;
            }
        }
        return null;
    }

    // Devuelve (exito, codigoErrorWin32). El codigo es null si exito.
    public static (bool Exito, int? CodigoError) SendBytesToPrinter(string printerName, byte[] bytes)
    {
        IntPtr unmanagedPointer = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
            return SendBytesToPrinter(printerName, unmanagedPointer, bytes.Length);
        }
        finally
        {
            Marshal.FreeCoTaskMem(unmanagedPointer);
        }
    }

    private static (bool Exito, int? CodigoError) SendBytesToPrinter(string printerName, IntPtr pBytes, int dwCount)
    {
        var di = new DOCINFOA { pDocName = "Ticket de Venta", pDataType = "RAW" };
        bool success = false;

        if (OpenPrinter(printerName.Normalize(), out IntPtr hPrinter, IntPtr.Zero))
        {
            if (StartDocPrinter(hPrinter, 1, di))
            {
                if (StartPagePrinter(hPrinter))
                {
                    success = WritePrinter(hPrinter, pBytes, dwCount, out int _);
                    EndPagePrinter(hPrinter);
                }
                EndDocPrinter(hPrinter);
            }
            ClosePrinter(hPrinter);
        }

        if (!success)
        {
            int error = Marshal.GetLastWin32Error();
            return (false, error);
        }
        return (true, null);
    }
}
