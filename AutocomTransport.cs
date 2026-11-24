using System;
using System.Collections.Generic;
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
        public static AutocomTransport SharedInstance { get; set; } public string ActivePort { get; private set; }
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

        private void WLog(string m) { if (Log != null) Log("[DS150E] " + m); }
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
                byte[] rx = (byte[])Array.CreateInstance(typeof(byte), 256); uint r = 0;
                WinUsb_ReadPipe(hUsb, 0x81, rx, 256, out r, IntPtr.Zero);
                if (r <= 2) return (byte[])Array.CreateInstance(typeof(byte), 0);
                byte[] p = (byte[])Array.CreateInstance(typeof(byte), (int)(r - 2));
                Array.Copy(rx, 2, p, 0, p.Length); return p;
            }
            return (byte[])Array.CreateInstance(typeof(byte), 0);
        }        public string ProbeHardwareInfo()
        {
            if (!IsOpen) return "Ei yhteytta";
            WriteRaw(new byte[] { 0x01, 0x00 }); byte[] fw = ReadRaw(80);
            FirmwareInfo = (fw.Length > 0) ? BitConverter.ToString(fw) : "USB-valmiustila (STM32 heraa 12V OBD-virralla)";
            WriteRaw(new byte[] { 0x04, 0x10 }); byte[] vRaw = ReadRaw(80);
            ObdVoltage = (vRaw.Length >= 2) ? (((vRaw[0] << 8) | vRaw[1]) / 100.0) : 0.0;
            string voltStr = (ObdVoltage > 0.5) ? (ObdVoltage.ToString("0.00", CultureInfo.InvariantCulture) + " V" + (ObdVoltage < 12.0 ? " [VAROITUS: Matala jannite!]" : " [OK]")) : "0.00 V (ei kytketty auton OBD-pistokkeeseen)";
            string summary = "Laite: " + FirmwareInfo + " | OBD Pin 16 jannite: " + voltStr;
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
                    ReadRaw(60); WLog("Avattu " + ActivePort); ProbeHardwareInfo(); return ActivePort;
                }
            }
            WLog("DS150E WinUSB-laitetta ei loytynyt."); return null;
        }
        public void SelectEcu(string tx, string rx)
        {
            curTx = (tx ?? "7E0").Trim(); curRx = (rx ?? "7E8").Trim();
            isKLine = curTx.StartsWith("KLINE", StringComparison.OrdinalIgnoreCase) || curTx.Length <= 2;
            WLog("SelectEcu TX=" + curTx + " RX=" + curRx + (isKLine ? " [K-Line]" : " [CAN]"));
            if (!IsOpen) return;
            if (isKLine) { WriteRaw(new byte[] { 0x02, 0x07, 0x0F }); ReadRaw(60); }
            else { ushort t = 0x7E0, r = 0x7E8; ushort.TryParse(curTx.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out t); ushort.TryParse(curRx.Replace("0x", ""), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out r); WriteRaw(new byte[] { 0x03, (byte)(t >> 8), (byte)(t & 0xFF), (byte)(r >> 8), (byte)(r & 0xFF) }); ReadRaw(60); }
        }
        private static string ToHex(byte[] b, int off, int cnt) { StringBuilder sb = new StringBuilder(cnt * 2); for (int i = off; i < off + cnt && i < b.Length; i++) sb.AppendFormat("{0:X2}", b[i]); return sb.ToString(); }
        public string Send(string cmdHex, string[] tokens, int maxMs)
        {
            if (!IsOpen) return "";
            string c = (cmdHex ?? "").Replace(" ", "").Replace("-", ""); if (c.Length % 2 != 0) c = "0" + c;
            byte[] pay = (byte[])Array.CreateInstance(typeof(byte), c.Length / 2);
            for (int i = 0; i < pay.Length; i++) pay[i] = byte.Parse(c.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            WLog(">> " + c);
            byte[] frm = (byte[])Array.CreateInstance(typeof(byte), isKLine ? (pay.Length + 2) : 10);
            if (isKLine) { frm[0] = 0x06; frm[1] = (byte)pay.Length; Array.Copy(pay, 0, frm, 2, pay.Length); }
            else { frm[0] = 0x07; frm[1] = (byte)(pay.Length & 0x0F); Array.Copy(pay, 0, frm, 2, Math.Min(pay.Length, 7)); }
            WriteRaw(frm); byte[] resp = ReadRaw(Math.Min(Math.Max(maxMs, 80), 500));
            if (resp.Length == 0) { WLog("<< (ei vastausta)"); return ""; }
            if (!isKLine)
            {
                byte pci = (byte)(resp[0] & 0xF0);
                if (pci == 0x00 && resp.Length > 1) { string h = ToHex(resp, 1, Math.Min((int)(resp[0] & 0x0F), resp.Length - 1)); WLog("<< [SF] " + h); return h; }
                if (pci == 0x10 && resp.Length >= 3)
                {
                    int tot = ((resp[0] & 0x0F) << 8) | resp[1]; List<byte> full = new List<byte>();
                    for (int i = 2; i < resp.Length && full.Count < tot; i++) full.Add(resp[i]);
                    WriteRaw(new byte[] { 0x07, 0x03, 0x30, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
                    int g = 0; while (full.Count < tot && g++ < 32) { byte[] cf = ReadRaw(60); if (cf.Length < 2) break; if ((cf[0] & 0xF0) == 0x20) for (int i = 1; i < cf.Length && full.Count < tot; i++) full.Add(cf[i]); }
                    string h = ToHex(full.ToArray(), 0, full.Count); WLog("<< [MF " + full.Count + "B] " + h); return h;
                }
            }
            string raw = ToHex(resp, 0, resp.Length); WLog("<< " + raw); return raw;
        }
        public string ScanPeugeot406D8()
        {
            if (!IsOpen) Open("");
            if (!IsOpen) return "Delphi DS150E ei ole kytketty USB-porttiin.";
            var sb = new StringBuilder("=== 1997 PEUGEOT 406 D8 (ES9J4 V6) — DS150E MONIPINNISKANNAUS ===\r\n" + ProbeHardwareInfo() + "\r\n");
            int[] pins = new int[] { 7, 11, 12, 13 };
            string[] names = new string[] { "Pin 7: Moottori Bosch MP 7.0 (ES9J4)", "Pin 11: Automaattivaihteisto ZF 4HP20", "Pin 12: ABS Bosch 5.3", "Pin 13: Turvatyynyt / Airbag" };
            for (int i = 0; i < pins.Length; i++)
            {
                WriteRaw(new byte[] { 0x02, (byte)pins[i], 0x0F }); byte[] ack = ReadRaw(90);
                WriteRaw(new byte[] { 0x06, 0x02, 0x21, 0x80 }); byte[] ecu = ReadRaw(140);
                string rStr = (ecu.Length > 0) ? ("VASTASI (" + BitConverter.ToString(ecu) + ")") : ("Ei vastausta (MUX=" + (ack.Length > 0 ? BitConverter.ToString(ack) : "OK") + ")");
                sb.AppendLine(names[i] + " -> " + rStr); WLog(names[i] + " -> " + rStr);
            }
            return sb.ToString();
        }
        public void Close() { try { if (hUsb != IntPtr.Zero) { WinUsb_Free(hUsb); hUsb = IntPtr.Zero; } } catch { } try { if (hDev != null && !hDev.IsInvalid) { hDev.Close(); hDev.Dispose(); } } catch { } hDev = null; }
        public void Dispose() { Close(); }
    }
}