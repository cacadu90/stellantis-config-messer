using System;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ZoneTool
{
    public class AutocomTransport : IDisposable
    {
        private static readonly Guid G_USB = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        private SafeFileHandle hDev;
        private IntPtr hUsb = IntPtr.Zero;
        private SerialPort sp;
        public bool IsOpen { get { return (hUsb != IntPtr.Zero) || (sp != null && sp.IsOpen); } }
        public string ActivePort { get; private set; }

        [StructLayout(LayoutKind.Sequential)] struct IF_DATA { public int cbSize; public Guid Guid; public int Flags; public IntPtr Res; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] struct IF_DET { public int cbSize; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)] public string Path; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)] struct SETUP_PKT { public byte ReqType; public byte Req; public ushort Val; public ushort Idx; public ushort Len; }

        [DllImport("setupapi.dll", CharSet = CharSet.Auto)] static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr h, uint f);
        [DllImport("setupapi.dll")] static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, uint i, ref IF_DATA id);
        [DllImport("setupapi.dll", CharSet = CharSet.Auto)] static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref IF_DATA id, ref IF_DET det, uint sz, out uint req, IntPtr d);
        [DllImport("setupapi.dll")] static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern SafeFileHandle CreateFile(string n, uint a, uint sh, IntPtr sa, uint cd, uint fl, IntPtr t);
        [DllImport("winusb.dll")] static extern bool WinUsb_Initialize(SafeFileHandle h, out IntPtr u);
        [DllImport("winusb.dll")] static extern bool WinUsb_Free(IntPtr u);
        [DllImport("winusb.dll")] static extern bool WinUsb_ControlTransfer(IntPtr u, SETUP_PKT p, byte[] b, uint l, out uint x, IntPtr o);
        [DllImport("winusb.dll")] static extern bool WinUsb_WritePipe(IntPtr u, byte pid, byte[] b, uint l, out uint x, IntPtr o);
        [DllImport("winusb.dll")] static extern bool WinUsb_ReadPipe(IntPtr u, byte pid, byte[] b, uint l, out uint x, IntPtr o);
        [DllImport("winusb.dll")] static extern bool WinUsb_SetPipePolicy(IntPtr u, byte pid, uint pt, uint vl, ref uint v);

        private string FindPath()
        {
            Guid g = G_USB;
            IntPtr h = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (h == new IntPtr(-1)) return null;
            try
            {
                IF_DATA id = new IF_DATA { cbSize = Marshal.SizeOf(typeof(IF_DATA)) };
                for (uint i = 0; SetupDiEnumDeviceInterfaces(h, IntPtr.Zero, ref g, i, ref id); i++)
                {
                    IF_DET det = new IF_DET { cbSize = (IntPtr.Size == 8) ? 8 : (4 + Marshal.SystemDefaultCharSize) };
                    uint req = 0;
                    if (SetupDiGetDeviceInterfaceDetail(h, ref id, ref det, (uint)Marshal.SizeOf(det), out req, IntPtr.Zero))
                    {
                        string p = det.Path ?? "";
                        if (p.IndexOf("vid_0403", StringComparison.OrdinalIgnoreCase) >= 0 && p.IndexOf("pid_d6da", StringComparison.OrdinalIgnoreCase) >= 0) return p;
                    }
                }
            }
            finally { SetupDiDestroyDeviceInfoList(h); }
            return null;
        }

        private void Ctrl(byte req, ushort val)
        {
            uint x = 0;
            WinUsb_ControlTransfer(hUsb, new SETUP_PKT { ReqType = 0x40, Req = req, Val = val, Idx = 0, Len = 0 }, null, 0, out x, IntPtr.Zero);
        }

        public string OpenAndProbe(string pref)
        {
            Close();
            var sb = new StringBuilder("=== AUTOCOM / DELPHI DS150E (WinUSB + COM) ===\r\n");
            string path = FindPath();
            if (!string.IsNullOrEmpty(path))
            {
                hDev = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
                if (hDev != null && !hDev.IsInvalid && WinUsb_Initialize(hDev, out hUsb))
                {
                    ActivePort = "WinUSB (VID_0403&PID_D6DA)";
                    uint tmo = 400;
                    WinUsb_SetPipePolicy(hUsb, 0x81, 3, 4, ref tmo);
                    WinUsb_SetPipePolicy(hUsb, 0x02, 3, 4, ref tmo);
                    Ctrl(0x00, 0x0000); Ctrl(0x03, 0x001A); Ctrl(0x04, 0x0008); Ctrl(0x01, 0x0303);
                    Thread.Sleep(100);
                    byte[] rx = (byte[])Array.CreateInstance(typeof(byte), 64);
                    uint w = 0, r = 0;
                    WinUsb_ReadPipe(hUsb, 0x81, rx, 64, out r, IntPtr.Zero);
                    byte[] cmd = new byte[] { 0x01, 0x00 };
                    bool ok = WinUsb_WritePipe(hUsb, 0x02, cmd, 2, out w, IntPtr.Zero);
                    Thread.Sleep(150);
                    WinUsb_ReadPipe(hUsb, 0x81, rx, 64, out r, IntPtr.Zero);
                    sb.AppendLine("WinUSB avattu: " + ActivePort);
                    sb.AppendLine("FTDI 115200 bps OK (WritePipe=" + ok + ", lahetetty=" + w + " tavua)");
                    sb.AppendLine("FTDI/DS150E vastaus (" + r + " tavua): " + (r > 0 ? BitConverter.ToString(rx, 0, (int)r) : "0 tavua"));
                    return sb.ToString();
                }
            }
            foreach (string p in (string.IsNullOrEmpty(pref) ? SerialPort.GetPortNames() : new string[] { pref }))
            {
                try { sp = new SerialPort(p, 115200) { ReadTimeout = 500, WriteTimeout = 500, DtrEnable = true, RtsEnable = true }; sp.Open(); ActivePort = p; sb.AppendLine("COM avattu: " + p); return sb.ToString(); } catch { }
            }
            return sb.AppendLine("Autocom-laitetta ei loytynyt.").ToString();
        }

        public string ScanKlineDs150(int pin)
        {
            if (!IsOpen) { string m = OpenAndProbe(ActivePort); if (!IsOpen) return m; }
            var sb = new StringBuilder("=== DS150E K-LINE TESTI (" + ActivePort + ", Pin " + pin + ") ===\r\n");
            if (hUsb != IntPtr.Zero)
            {
                byte[] cmd = new byte[] { 0x02, (byte)pin, 0x0F };
                uint w = 0, r = 0;
                WinUsb_WritePipe(hUsb, 0x02, cmd, 3, out w, IntPtr.Zero);
                Thread.Sleep(120);
                byte[] rx = (byte[])Array.CreateInstance(typeof(byte), 64);
                WinUsb_ReadPipe(hUsb, 0x81, rx, 64, out r, IntPtr.Zero);
                sb.AppendLine("MUX Pin " + pin + " lahetetty (" + w + " tavua) -> vastaus: " + (r > 0 ? BitConverter.ToString(rx, 0, (int)r) : "odottaa 12V OBD-virtaa"));
            }
            return sb.ToString();
        }

        public void Close()
        {
            try { if (hUsb != IntPtr.Zero) { WinUsb_Free(hUsb); hUsb = IntPtr.Zero; } } catch { }
            try { if (hDev != null && !hDev.IsInvalid) { hDev.Close(); hDev.Dispose(); } } catch { }
            hDev = null;
            try { if (sp != null) { if (sp.IsOpen) sp.Close(); sp.Dispose(); } } catch { }
            sp = null;
        }
        public void Dispose() { Close(); }
    }
}