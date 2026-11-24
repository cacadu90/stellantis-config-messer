using System;
using System.Globalization;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace ZoneTool
{
    public class AutocomTransport : ITransport, IDisposable
    {
        private static readonly Guid G_USB = new Guid("A5DCBF10-6530-11D2-901F-00C04FB951ED");
        private SafeFileHandle hDev;
        private IntPtr hUsb = IntPtr.Zero;
        private SerialPort sp;
        private string curTx = "7E0", curRx = "7E8";
        private bool isKLine = false;

        public Action<string> Log { get; set; }
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

        private void WLog(string m) { if (Log != null) Log("[DS150E] " + m); }

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

        private void Ctrl(byte req, ushort val) { uint x = 0; WinUsb_ControlTransfer(hUsb, new SETUP_PKT { ReqType = 0x40, Req = req, Val = val }, null, 0, out x, IntPtr.Zero); }

        private int WriteRaw(byte[] d)
        {
            if (hUsb != IntPtr.Zero) { uint w = 0; WinUsb_WritePipe(hUsb, 0x02, d, (uint)d.Length, out w, IntPtr.Zero); return (int)w; }
            if (sp != null && sp.IsOpen) { sp.Write(d, 0, d.Length); return d.Length; }
            return 0;
        }

        private byte[] ReadRaw(int ms)
        {
            Thread.Sleep(ms);
            if (hUsb != IntPtr.Zero)
            {
                byte[] rx = (byte[])Array.CreateInstance(typeof(byte), 256);
                uint r = 0; WinUsb_ReadPipe(hUsb, 0x81, rx, 256, out r, IntPtr.Zero);
                if (r <= 2) return (byte[])Array.CreateInstance(typeof(byte), 0);
                byte[] p = (byte[])Array.CreateInstance(typeof(byte), (int)(r - 2));
                Array.Copy(rx, 2, p, 0, p.Length); return p;
            }
            if (sp != null && sp.IsOpen && sp.BytesToRead > 0)
            {
                byte[] b = (byte[])Array.CreateInstance(typeof(byte), sp.BytesToRead);
                int n = sp.Read(b, 0, b.Length);
                byte[] p = (byte[])Array.CreateInstance(typeof(byte), n);
                Array.Copy(b, 0, p, 0, n); return p;
            }
            return (byte[])Array.CreateInstance(typeof(byte), 0);
        }

        public string Open(string portOrNull)
        {
            Close();
            string path = FindPath();
            if (!string.IsNullOrEmpty(path))
            {
                hDev = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
                if (hDev != null && !hDev.IsInvalid && WinUsb_Initialize(hDev, out hUsb))
                {
                    ActivePort = "WinUSB (VID_0403&PID_D6DA)";
                    uint tmo = 350; WinUsb_SetPipePolicy(hUsb, 0x81, 3, 4, ref tmo); WinUsb_SetPipePolicy(hUsb, 0x02, 3, 4, ref tmo);
                    Ctrl(0x00, 0x0000); Ctrl(0x03, 0x001A); Ctrl(0x04, 0x0008); Ctrl(0x01, 0x0303);
                    ReadRaw(60); WriteRaw(new byte[] { 0x01, 0x00 }); ReadRaw(80);
                    WLog("Avattu " + ActivePort);
                    return ActivePort;
                }
            }
            foreach (string p in (string.IsNullOrEmpty(portOrNull) ? SerialPort.GetPortNames() : new string[] { portOrNull }))
            {
                try { sp = new SerialPort(p, 115200) { ReadTimeout = 400, WriteTimeout = 400, DtrEnable = true, RtsEnable = true }; sp.Open(); ActivePort = p; WLog("Avattu " + p); return p; } catch { }
            }
            WLog("DS150E-laitetta ei loytynyt."); return null;
        }

        public string OpenAndProbe(string pref) { string r = Open(pref); return IsOpen ? ("Yhteys: " + r) : "Ei yhteytta"; }

        public void SelectEcu(string tx, string rx)
        {
            curTx = (tx ?? "7E0").Trim(); curRx = (rx ?? "7E8").Trim();
            isKLine = curTx.StartsWith("KLINE", StringComparison.OrdinalIgnoreCase) || curTx.Length <= 2;
            WLog("SelectEcu TX=" + curTx + " RX=" + curRx + (isKLine ? " [K-Line]" : " [CAN]"));
            if (!IsOpen) return;
            if (isKLine) { WriteRaw(new byte[] { 0x02, 0x07, 0x0F }); ReadRaw(60); }
            else
            {
                ushort t = 0x7E0, r = 0x7E8;
                ushort.TryParse(curTx.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out t);
                ushort.TryParse(curRx.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r);
                WriteRaw(new byte[] { 0x03, (byte)(t >> 8), (byte)(t & 0xFF), (byte)(r >> 8), (byte)(r & 0xFF) }); ReadRaw(60);
            }
        }

        public string Send(string cmdHex, string[] tokens, int maxMs)
        {
            if (!IsOpen) return "";
            string c = (cmdHex ?? "").Replace(" ", "").Replace("-", "");
            if (c.Length % 2 != 0) c = "0" + c;
            byte[] pay = (byte[])Array.CreateInstance(typeof(byte), c.Length / 2);
            for (int i = 0; i < pay.Length; i++) pay[i] = byte.Parse(c.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            WLog(">> " + c);
            byte[] frm = (byte[])Array.CreateInstance(typeof(byte), isKLine ? (pay.Length + 2) : 10);
            if (isKLine) { frm[0] = 0x06; frm[1] = (byte)pay.Length; Array.Copy(pay, 0, frm, 2, pay.Length); }
            else { frm[0] = 0x07; frm[1] = (byte)(pay.Length & 0x0F); Array.Copy(pay, 0, frm, 2, Math.Min(pay.Length, 7)); }
            WriteRaw(frm);
            byte[] resp = ReadRaw(Math.Min(Math.Max(maxMs, 80), 500));
            if (resp.Length == 0) { WLog("<< (ei vastausta)"); return ""; }
            int off = (!isKLine && (resp[0] & 0xF0) == 0x00 && resp.Length > 1) ? 1 : 0;
            StringBuilder sb = new StringBuilder();
            for (int i = off; i < resp.Length; i++) sb.AppendFormat("{0:X2}", resp[i]);
            WLog("<< " + sb.ToString());
            return sb.ToString();
        }

        public string ScanKlineDs150(int pin) { SelectEcu("KLINE", "10"); return "K-Line Pin " + pin + " valmis."; }
        public void Close() { try { if (hUsb != IntPtr.Zero) { WinUsb_Free(hUsb); hUsb = IntPtr.Zero; } } catch { } try { if (hDev != null && !hDev.IsInvalid) { hDev.Close(); hDev.Dispose(); } } catch { } hDev = null; try { if (sp != null) { if (sp.IsOpen) sp.Close(); sp.Dispose(); } } catch { } sp = null; }
        public void Dispose() { Close(); }
    }
}