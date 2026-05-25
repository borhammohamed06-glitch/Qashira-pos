using System.Runtime.InteropServices;
using System.Text;

namespace Qashira.App.Services;

internal static class RawPrinterWriter
{
    public static bool WriteAscii(string printerName, string documentName, string content)
    {
        if (string.IsNullOrWhiteSpace(printerName) || string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var bytes = Encoding.ASCII.GetBytes(content);
        return WriteBytes(printerName, documentName, bytes);
    }

    private static bool WriteBytes(string printerName, string documentName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out var printerHandle, IntPtr.Zero))
        {
            return false;
        }

        try
        {
            var docInfo = new DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (StartDocPrinter(printerHandle, 1, ref docInfo) == 0)
            {
                return false;
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    return false;
                }

                try
                {
                    return WritePrinter(printerHandle, bytes, bytes.Length, out var bytesWritten)
                        && bytesWritten == bytes.Length;
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string DocumentName;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? OutputFile;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string DataType;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out IntPtr printerHandle, IntPtr printerDefaults);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr printerHandle, int level, ref DocInfo docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr printerHandle, byte[] data, int dataLength, out int bytesWritten);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr printerHandle);
}
