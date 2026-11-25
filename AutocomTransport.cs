using System;
using System.Collections.Generic;
using System.Globalization;
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
        private string curTx = "7E0", curRx = "7E8";
        private bool isKLine = false;
        public Action<string> Log { get; set; }
        public static AutocomTransport SharedInstance { get; set; }
        public bool IsOpen { get { return (hUsb != IntPtr.Zero); } }
        public string ActivePort { get; private set; }
        public string FirmwareInfo { get; private set; }
        public double ObdVoltage { get; private set; }

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

        private void WLog(string m) { if (Log != null) Log("[ADP186] " + m); }
        private string FindPath()
        {
            Guid g = G_USB; IntPtr h = SetupDiGetClassDevs(ref g, IntPtr.Zero, IntPtr.Zero, 0x12);
            if (h == new IntPtr(-1)) return null;
            try
            {
                IF_DATA id = new IF_DATA { cbSize = Marshal.SizeOf(typeof(IF_DATA)) };
                for (uint i = 0; SetupDiEnumDeviceInterfaces(h, IntPtr.Zero, ref g, i, ref id); i++)
                {
                    IF_DET det = new IF_DET { cbSize = (IntPtr.Size == 8) ? 8 : 6 }; uint req = 0;
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
        private int WriteRaw(byte[] d) { if (hUsb != IntPtr.Zero) { uint w = 0; WinUsb_WritePipe(hUsb, 0x02, d, (uint)d.Length, out w, IntPtr.Zero); return (int)w; } return 0; }
        private byte[] ReadRaw(int ms)
        {
            Thread.Sleep(ms);
            if (hUsb != IntPtr.Zero)
            {
                byte[] rx = (byte[])Array.CreateInstance(typeof(byte), 512); uint r = 0;
                WinUsb_ReadPipe(hUsb, 0x81, rx, 512, out r, IntPtr.Zero);
                if (r <= 2) return (byte[])Array.CreateInstance(typeof(byte), 0);
                byte[] p = (byte[])Array.CreateInstance(typeof(byte), (int)(r - 2));
                Array.Copy(rx, 2, p, 0, p.Length); return p;
            }
            return (byte[])Array.CreateInstance(typeof(byte), 0);
        }        public string SendStarCmd(string starCmd, int waitMs)
        {
            if (!IsOpen) return "";
            string line = starCmd.EndsWith("\r") ? starCmd : (starCmd + "\r");
            WriteRaw(Encoding.ASCII.GetBytes(line));
            byte[] resp = ReadRaw(waitMs);
            if (resp.Length == 0) return "";
            string ascii = Encoding.ASCII.GetString(resp).Trim('\r', '\n', '\0', ' ');
            WLog(starCmd + " -> " + (ascii.Length > 0 ? ascii : BitConverter.ToString(resp)));
            return ascii;
        }
        public string ProbeHardwareInfo()
        {
            if (!IsOpen) return "Ei yhteytta";
            string ping = SendStarCmd("*999", 80);
            string id200 = SendStarCmd("*200", 80);
            string id201 = SendStarCmd("*201", 80);
            string v203 = SendStarCmd("*203", 80);
            FirmwareInfo = !string.IsNullOrEmpty(id200) ? id200 : (!string.IsNullOrEmpty(ping) ? ping : "ADP186 valmiustila (vaatii 12V OBD-virran)");
            double v = 0.0;
            if (!string.IsNullOrEmpty(v203)) double.TryParse(v203.Replace("V", "").Replace(",", ".").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out v);
            ObdVoltage = v;
            string voltStr = (ObdVoltage > 0.5) ? (ObdVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" + (ObdVoltage < 12.0 ? " [VAROITUS: Matala jannite!]" : " [OK]")) : "0.00 V (ei kytketty auton OBD-pistokkeeseen)";
            string summary = "Laite (ADP186): " + FirmwareInfo + " | OBD Pin 16 jannite: " + voltStr;
            WLog(summary); return summary;
        }
        public string Open(string portOrNull)
        {
            Close(); string path = FindPath();
            if (!string.IsNullOrEmpty(path))
            {
                hDev = CreateFile(path, 0xC0000000, 3, IntPtr.Zero, 3, 0x40000000, IntPtr.Zero);
                if (hDev != null && !hDev.IsInvalid && WinUsb_Initialize(hDev, out hUsb))
                {
                    ActivePort = "WinUSB (VID_0403&PID_D6DA)"; SharedInstance = this;
                    uint tmo = 350; WinUsb_SetPipePolicy(hUsb, 0x81, 3, 4, ref tmo); WinUsb_SetPipePolicy(hUsb, 0x02, 3, 4, ref tmo);
                    Ctrl(0x00, 0x0000); Ctrl(0x03, 0x001A); Ctrl(0x04, 0x0008); Ctrl(0x01, 0x0303);
                    ReadRaw(60); WLog("Avattu " + ActivePort + " [ADP186 ASCII *protokolla]"); ProbeHardwareInfo(); return ActivePort;
                }
            }
            WLog("ADP186 WinUSB-laitetta ei loytynyt."); return null;
        }
        public void SelectEcu(string tx, string rx)
        {
            curTx = (tx ?? "7E0").Trim(); curRx = (rx ?? "7E8").Trim();
            isKLine = curTx.StartsWith("KLINE", StringComparison.OrdinalIgnoreCase) || curTx.Length <= 2;
            WLog("SelectEcu TX=" + curTx + " RX=" + curRx + (isKLine ? " [K-Line]" : " [CAN]"));
            if (IsOpen) SendStarCmd("*333_1", 60);
        }
        public string Send(string cmdHex, string[] tokens, int maxMs)
        {
            if (!IsOpen) return "";
            string c = (cmdHex ?? "").Replace(" ", "").Replace("-", "").ToUpperInvariant();
            WLog(">> " + c);
            string ans = SendStarCmd("*600" + c, Math.Min(Math.Max(maxMs, 100), 600));
            if (string.IsNullOrEmpty(ans)) { WLog("<< (ei vastausta)"); return ""; }
            WLog("<< " + ans); return ans;
        }
        public string ScanPeugeot406D8()
        {
            if (!IsOpen) Open("");
            if (!IsOpen) return "ADP186 VCI ei ole kytketty USB-porttiin.";
            var sb = new StringBuilder("=== 1997 PEUGEOT 406 D8 (ES9J4 V6) — ADP186 / ADP186 SKANNAUS ===\r\n" + ProbeHardwareInfo() + "\r\n");
            string[] ecus = new string[] { "Pin 7: Bosch MP 7.0 (9psamp72s)", "Pin 11: Automaattivaihteisto ZF 4HP20", "Pin 12: ABS Teves Mk20 (9psamk20es9)", "Pin 13: Airbag AC4 (9psasrsac4s)" };
            string[] cmds = new string[] { "*605E07", "*605E0B", "*605E0C", "*605E0D" };
            for (int i = 0; i < ecus.Length; i++)
            {
                string r = SendStarCmd(cmds[i], 140);
                string rStr = !string.IsNullOrEmpty(r) ? ("VASTASI: " + r) : "Ei vastausta (odottaa 12V OBD-virtaa)";
                sb.AppendLine(ecus[i] + " -> " + rStr); WLog(ecus[i] + " -> " + rStr);
            }
            return sb.ToString();
        }
        public void Close() { try { if (hUsb != IntPtr.Zero) { WinUsb_Free(hUsb); hUsb = IntPtr.Zero; } } catch { } try { if (hDev != null && !hDev.IsInvalid) { hDev.Close(); hDev.Dispose(); } } catch { } hDev = null; }
        public void Dispose() { Close(); }
    }
}