using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;

namespace ZoneTool
{
    class Opt { public string En; public string Fi; public string Val; public string L(string lang) { return (lang == "fi" && !string.IsNullOrEmpty(Fi)) ? Fi : En; } }
    class Field { public string En; public string Fi; public string Desc; public string Type; public string Mask; public int ByteStart; public List<Opt> Options = new List<Opt>(); public string L(string lang) { return (lang == "fi" && !string.IsNullOrEmpty(Fi)) ? Fi : En; } }
    class Zone { public string Id; public string Name; public List<Field> Fields = new List<Field>(); }
    class Module { public string Id; public string Name; public string Tx; public string Rx; public string Key; public string Trace; public int ByteOffset; public List<Zone> Zones = new List<Zone>(); }

    static class Data
    {
        public static List<Module> Modules = new List<Module>();
        public static int[] Sec1 = { 0xB2, 0x3F, 0xAA };
        public static int[] Sec2 = { 0xB1, 0x02, 0xAB };
        static string S(object o) { return o == null ? "" : o.ToString(); }
        static Dictionary<string, object> D(object o) { return o as Dictionary<string, object>; }
        static object[] A(object o) { return o as object[]; }
        static string Pick(Dictionary<string, object> d, params string[] keys) { foreach (var k in keys) if (d.ContainsKey(k) && d[k] != null) return S(d[k]); return ""; }
        public static void Load(string path)
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            var js = new JavaScriptSerializer(); js.MaxJsonLength = 80 * 1024 * 1024;
            var root = D(js.DeserializeObject(json));
            if (root.ContainsKey("seedkey")) { var sk = D(root["seedkey"]); Sec1 = A(sk["sec1"]).Select(x => Convert.ToInt32(x)).ToArray(); Sec2 = A(sk["sec2"]).Select(x => Convert.ToInt32(x)).ToArray(); }
            Modules.Clear();
            foreach (var mo in A(root["modules"]))
            {
                var md = D(mo);
                var m = new Module { Id = S(md["id"]), Name = S(md["name"]), Tx = S(md["tx"]), Rx = S(md["rx"]), Key = S(md["key"]), Trace = md.ContainsKey("traceability") ? S(md["traceability"]) : "no", ByteOffset = md.ContainsKey("byteOffset") ? Convert.ToInt32(md["byteOffset"]) : 0 };
                foreach (var zo in A(md["zones"]))
                {
                    var zd = D(zo); if (S(zd["id"]) == "2100") continue; var z = new Zone { Id = S(zd["id"]), Name = Pick(zd, "name") };
                    if (zd.ContainsKey("fields") && A(zd["fields"]) != null)
                        foreach (var fo in A(zd["fields"]))
                        {
                            var fd = D(fo);
                            var f = new Field { En = Pick(fd, "en", "name"), Fi = Pick(fd, "fi"), Desc = Pick(fd, "desc"), Type = Pick(fd, "type"), Mask = fd.ContainsKey("mask") ? S(fd["mask"]) : "FF", ByteStart = fd.ContainsKey("byteStart") ? Convert.ToInt32(fd["byteStart"]) : 0 };
                            if (fd.ContainsKey("options") && A(fd["options"]) != null)
                                foreach (var oo in A(fd["options"])) { var od = D(oo); f.Options.Add(new Opt { En = Pick(od, "en", "label"), Fi = Pick(od, "fi"), Val = S(od["val"]) }); }
                            z.Fields.Add(f);
                        }
                    m.Zones.Add(z);
                }
                Modules.Add(m);
            }
        }
    }

    static class Sk
    {
        static int Transform(int msb, int lsb, int[] sec) { int data = ((msb & 0xFF) << 8) | (lsb & 0xFF); if (data > 32767) data -= 65536; int quot = (int)Math.Truncate((double)data / sec[0]); int rem = data - quot * sec[0]; int r = rem * sec[2] - quot * sec[1]; if (r < 0) r += sec[0] * sec[2] + sec[1]; return r; }
        public static string Key(string key2, byte[] chg) { int pinHi = Convert.ToInt32(key2.Substring(0, 2), 16); int pinLo = Convert.ToInt32(key2.Substring(2, 2), 16); int m = Transform(pinHi, pinLo, Data.Sec1) | Transform(chg[0], chg[3], Data.Sec2); int l = Transform(chg[1], chg[2], Data.Sec1) | Transform((m >> 8), (m & 0xFF), Data.Sec2); long res = (((long)m << 16) | (uint)l) & 0xFFFFFFFFL; return res.ToString("X8"); }
    }

    static class Hex
    {
        public static byte[] ToBytes(string h) { h = (h ?? "").Trim().Replace(" ", ""); var b = new byte[h.Length / 2]; for (int i = 0; i < b.Length; i++) b[i] = Convert.ToByte(h.Substring(i * 2, 2), 16); return b; }
        public static string ToStr(byte[] b, int len) { var sb = new StringBuilder(); for (int i = 0; i < len; i++) sb.Append(b[i].ToString("X2")); return sb.ToString(); }
    }

    // ---------- Transport-abstraktio ----------
    interface ITransport
    {
        Action<string> Log { set; }
        bool IsOpen { get; }
        string Open(string portOrNull);   // "" = ok, muu = virhe
        void Close();
        void SelectEcu(string tx, string rx);
        string Send(string cmdHex, string[] tokens, int maxMs); // palauttaa pakatun hex-vastauksen
    }

    class ArduinoTransport : ITransport
    {
        SerialPort sp; public Action<string> Log { private get; set; }
        public bool IsOpen { get { return sp != null && sp.IsOpen; } }
        public string Open(string port) { try { sp = new SerialPort(port, 115200); sp.ReadTimeout = 500; sp.WriteTimeout = 500; sp.Open(); Thread.Sleep(2000); try { sp.ReadExisting(); } catch { } return ""; } catch (Exception e) { return e.Message; } }
        public void Close() { try { if (sp != null) sp.Close(); } catch { } }
        public void SelectEcu(string tx, string rx) { Send(">" + tx + ":" + rx, null, 300); }
        public string Send(string cmd, string[] tokens, int maxMs)
        {
            sp.Write(cmd + "\n"); var sw = System.Diagnostics.Stopwatch.StartNew(); var sb = new StringBuilder();
            while (sw.ElapsedMilliseconds < maxMs)
            {
                try { sb.Append(sp.ReadExisting()); } catch { }
                if (tokens != null) { string packed0 = sb.ToString().Replace(" ", "").Replace("\r", "").Replace("\n", ""); bool hit = false; foreach (var t in tokens) if (packed0.Contains(t)) { hit = true; break; } if (hit) break; }
                Thread.Sleep(tokens != null ? 8 : 15);
            }
            string raw = sb.ToString().Trim().Replace("\r", " ").Replace("\n", " "); while (raw.Contains("  ")) raw = raw.Replace("  ", " ");
            if (Log != null) Log(cmd.PadRight(16) + " -> " + raw);
            return raw.Replace(" ", "");
        }
    }

    class VciTransport : ITransport
    {
        const string DLL = @"C:\AWRoot\drv\VCIAccess.dll";
        [DllImport(DLL, EntryPoint = "_openSession", CallingConvention = CallingConvention.Cdecl)] static extern int OpenSession();
        [DllImport(DLL, EntryPoint = "_closeSession", CallingConvention = CallingConvention.Cdecl)] static extern int CloseSession();
        [DllImport(DLL, EntryPoint = "_changeComLine", CallingConvention = CallingConvention.Cdecl)] static extern int ChangeComLine(int line);
        [DllImport(DLL, EntryPoint = "_bindProtocol", CallingConvention = CallingConvention.Cdecl)] static extern int BindProtocol(byte[] desc, int len);
        [DllImport(DLL, EntryPoint = "_writeAndRead", CallingConvention = CallingConvention.Cdecl)] static extern int WriteAndRead(byte[] ecuDesc, int ecuLen, byte[] inBuf, int inLen, byte[] outBuf, int outLen, int timeout);
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)] static extern bool SetDllDirectory(string path);

        public Action<string> Log { private get; set; }
        bool connected; byte[] desc;
        const int LINE_CAN_DIAG = 17;
        static readonly byte[] PD_DIAG_ON_CAN = { 0x03, 0x10, 0xE8 };

        static string Status(int c)
        {
            switch (c) { case 0: case 1: return "OK"; case -4: return "CABLE UNPLUGGED"; case -5: return "NO ECU RESPONSE"; case -11: return "TIMEOUT"; case -12: return "VCI BUSY"; default: return "ERR " + c; }
        }
        public bool IsOpen { get { return connected; } }

        public string Open(string ignored)
        {
            try { SetDllDirectory(@"C:\AWRoot\drv"); } catch { }
            string cwd = Directory.GetCurrentDirectory();
            try
            {
                Directory.SetCurrentDirectory(@"C:\");
                int r;
                try { r = OpenSession(); }
                catch (DllNotFoundException) { return "VCIAccess.dll ei l\u00f6ytynyt (C:\\AWRoot\\drv)."; }
                catch (Exception e) { return "VCI-lataus ep\u00e4onnistui: " + e.Message; }
                if (Log != null) Log("VCI _openSession -> " + r);
                connected = (r == 0 || r == 1);
                return connected ? "" : ("VCI open: " + Status(r));
            }
            finally { try { Directory.SetCurrentDirectory(cwd); } catch { } }
        }

        public void Close()
        {
            if (!connected) return;
            string cwd = Directory.GetCurrentDirectory();
            try { Directory.SetCurrentDirectory(@"C:\"); try { CloseSession(); } catch { } } finally { try { Directory.SetCurrentDirectory(cwd); } catch { } }
            connected = false;
        }

        public void SelectEcu(string tx, string rx)
        {
            int cl = ChangeComLine(LINE_CAN_DIAG); if (Log != null) Log("VCI ChangeComLine(17) -> " + Status(cl));
            int bp = BindProtocol(PD_DIAG_ON_CAN, PD_DIAG_ON_CAN.Length); if (Log != null) Log("VCI BindProtocol(DIAGONCAN) -> " + Status(bp));
            string t = tx.Length == 3 ? "0" + tx : tx; string r = rx.Length == 3 ? "0" + rx : rx;
            desc = Hex.ToBytes(r + t); // RX_H + TX_H
        }

        public string Send(string cmd, string[] tokens, int maxMs)
        {
            if (desc == null) return "";
            byte[] inb = Hex.ToBytes(cmd); byte[] outb = new byte[2048];
            int r = WriteAndRead(desc, desc.Length, inb, inb.Length, outb, outb.Length, maxMs);
            string res = r > 0 ? Hex.ToStr(outb, r) : "";
            if (Log != null) Log(cmd.PadRight(16) + " -> " + (r > 0 ? res : ("[" + Status(r) + "]")));
            return res;
        }
    }

    static class Flags
    {
        public static Bitmap Fi() { var bmp = new Bitmap(28, 18); using (var g = Graphics.FromImage(bmp)) { g.Clear(Color.White); using (var bl = new SolidBrush(Color.FromArgb(0, 53, 128))) { g.FillRectangle(bl, 8, 0, 5, 18); g.FillRectangle(bl, 0, 7, 28, 5); } g.DrawRectangle(Pens.Silver, 0, 0, 27, 17); } return bmp; }
        public static Bitmap Gb() { var bmp = new Bitmap(28, 18); using (var g = Graphics.FromImage(bmp)) { g.SmoothingMode = SmoothingMode.AntiAlias; g.Clear(Color.FromArgb(0, 33, 113)); var white = new Pen(Color.White, 5); var red = new Pen(Color.FromArgb(200, 16, 46), 2); g.DrawLine(white, 0, 0, 28, 18); g.DrawLine(white, 28, 0, 0, 18); g.DrawLine(red, 0, 0, 28, 18); g.DrawLine(red, 28, 0, 0, 18); g.FillRectangle(Brushes.White, 10, 0, 8, 18); g.FillRectangle(Brushes.White, 0, 5, 28, 8); using (var rb = new SolidBrush(Color.FromArgb(200, 16, 46))) { g.FillRectangle(rb, 12, 0, 4, 18); g.FillRectangle(rb, 0, 7, 28, 4); } white.Dispose(); red.Dispose(); g.DrawRectangle(Pens.Silver, 0, 0, 27, 17); } return bmp; }
    }

    class ZoneView { public Zone Zone; public byte[] Bytes; public string OrigHex; public List<Field> Fs = new List<Field>(); public List<Control> Cs = new List<Control>(); public TextBox RawBox; }

    class MainForm : Form
    {
        ComboBox cbTrans, cbPort, cbModule, cbZone; FlowLayoutPanel pnlFields; TextBox txtLog;
        Button btnAuto, btnConn, btnRead, btnReadAll, btnWrite, btnDtcR, btnDtcC, btnLog; Panel logPanel;
        Label lblStatus, lblMod, lblZone; PictureBox flagFi, flagGb;
        ITransport trans; Module curMod;
        List<ZoneView> views = new List<ZoneView>();
        string lang = "fi"; bool busy = false;

        string T(string key)
        {
            var fi = new Dictionary<string, string> { {"auto","Autodetect"},{"conn","Yhdist\u00e4"},{"disc","Katkaise"},{"noconn","Ei yhteytt\u00e4"},{"module","Moduuli:"},{"zone","Zone:"},{"read","Lue"},{"readall","Lue kaikki"},{"write","Kirjoita"},{"dtcr","Lue DTC"},{"dtcc","Tyhjenn\u00e4 DTC"},{"connto","Yhdistetty "} };
            var en = new Dictionary<string, string> { {"auto","Autodetect"},{"conn","Connect"},{"disc","Disconnect"},{"noconn","Not connected"},{"module","Module:"},{"zone","Zone:"},{"read","Read"},{"readall","Read all"},{"write","Write"},{"dtcr","Read DTC"},{"dtcc","Clear DTC"},{"connto","Connected "} };
            var d = lang == "fi" ? fi : en; return d.ContainsKey(key) ? d[key] : key;
        }
        bool Vci() { return cbTrans.SelectedIndex == 1; }

        public MainForm()
        {
            Text = "Stellantis Config Messer"; Width = 1120; Height = 760; StartPosition = FormStartPosition.CenterScreen; Font = new Font("Segoe UI", 9);

            var top = new Panel { Dock = DockStyle.Top, Height = 44 };
            cbTrans = new ComboBox { Left = 10, Top = 10, Width = 130, DropDownStyle = ComboBoxStyle.DropDownList };
            cbTrans.Items.Add("Arduino"); cbTrans.Items.Add("Diagbox VCI"); cbTrans.SelectedIndex = 0;
            cbTrans.SelectedIndexChanged += (s, e) => { if (trans != null && trans.IsOpen) Connect(); bool ard = !Vci(); cbPort.Visible = ard; btnAuto.Visible = ard; };
            cbPort = new ComboBox { Left = 146, Top = 10, Width = 100, DropDownStyle = ComboBoxStyle.DropDownList };
            btnAuto = new Button { Left = 252, Top = 9, Width = 80 }; btnAuto.Click += (s, e) => AutoDetect();
            btnConn = new Button { Left = 338, Top = 9, Width = 100 }; btnConn.Click += (s, e) => Connect();
            lblStatus = new Label { Left = 446, Top = 13, Width = 230, AutoEllipsis = true };
            btnLog = new Button { Left = 682, Top = 9, Width = 150 }; btnLog.Click += (s, e) => { logPanel.Visible = !logPanel.Visible; UpdateLogBtn(); };
            flagFi = new PictureBox { Width = 28, Height = 18, Top = 12, Image = Flags.Fi(), Cursor = Cursors.Hand };
            flagGb = new PictureBox { Width = 28, Height = 18, Top = 12, Image = Flags.Gb(), Cursor = Cursors.Hand };
            flagFi.Click += (s, e) => SetLang("fi"); flagGb.Click += (s, e) => SetLang("en");
            top.Controls.AddRange(new Control[] { cbTrans, cbPort, btnAuto, btnConn, lblStatus, btnLog, flagFi, flagGb });
            top.Resize += (s, e) => { flagGb.Left = top.Width - 40; flagFi.Left = top.Width - 74; };

            var row2 = new Panel { Dock = DockStyle.Top, Height = 40 };
            lblMod = new Label { Left = 10, Top = 11, Width = 60 };
            cbModule = new ComboBox { Left = 72, Top = 8, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList }; cbModule.SelectedIndexChanged += (s, e) => FillZones();
            lblZone = new Label { Left = 322, Top = 11, Width = 44 };
            cbZone = new ComboBox { Left = 366, Top = 8, Width = 230, DropDownStyle = ComboBoxStyle.DropDownList };
            btnRead = new Button { Left = 604, Top = 7, Width = 70 }; btnRead.Click += (s, e) => DoReadSingle();
            btnReadAll = new Button { Left = 678, Top = 7, Width = 100 }; btnReadAll.Click += (s, e) => DoReadAll();
            btnWrite = new Button { Left = 782, Top = 7, Width = 100, Enabled = false }; btnWrite.Click += (s, e) => DoWrite();
            btnDtcR = new Button { Left = 886, Top = 7, Width = 80 }; btnDtcR.Click += (s, e) => DoDtc(false);
            btnDtcC = new Button { Left = 970, Top = 7, Width = 95 }; btnDtcC.Click += (s, e) => DoDtc(true);
            row2.Controls.AddRange(new Control[] { lblMod, cbModule, lblZone, cbZone, btnRead, btnReadAll, btnWrite, btnDtcR, btnDtcC });

            pnlFields = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BorderStyle = BorderStyle.FixedSingle };
            logPanel = new Panel { Dock = DockStyle.Bottom, Height = 190 };
            txtLog = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical, ReadOnly = true, BackColor = Color.Black, ForeColor = Color.LightGreen, Font = new Font("Consolas", 8.5f) };
            logPanel.Controls.Add(txtLog);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 24, BackColor = Color.FromArgb(245, 245, 245) };
            var bmc = new LinkLabel { Left = 8, Top = 4, AutoSize = true, Text = "☕ Support: buymeacoffee.com/plagufin" };
            bmc.LinkClicked += (s, e) => { try { System.Diagnostics.Process.Start("https://www.buymeacoffee.com/plagufin"); } catch { } };
            var ghl = new LinkLabel { Left = 330, Top = 4, AutoSize = true, Text = "github.com/plagu-fin/stellantis-config-messer" };
            ghl.LinkClicked += (s, e) => { try { System.Diagnostics.Process.Start("https://github.com/plagu-fin/stellantis-config-messer"); } catch { } };
            footer.Controls.Add(bmc); footer.Controls.Add(ghl);

            Controls.Add(pnlFields); Controls.Add(logPanel); Controls.Add(footer); Controls.Add(row2); Controls.Add(top);
            RefreshPorts();
            foreach (var m in Data.Modules) cbModule.Items.Add(m.Id + " \u2014 " + m.Name);
            if (cbModule.Items.Count > 0) cbModule.SelectedIndex = 0;
            SetLang("fi");
            this.Shown += (s, e) => { flagGb.Left = top.Width - 40; flagFi.Left = top.Width - 74; };
        }

        void SetLang(string l)
        {
            lang = l;
            flagFi.BorderStyle = l == "fi" ? BorderStyle.Fixed3D : BorderStyle.None;
            flagGb.BorderStyle = l == "en" ? BorderStyle.Fixed3D : BorderStyle.None;
            btnAuto.Text = T("auto"); btnConn.Text = (trans != null && trans.IsOpen) ? T("disc") : T("conn");
            lblMod.Text = T("module"); lblZone.Text = T("zone"); btnRead.Text = T("read"); btnReadAll.Text = T("readall"); btnWrite.Text = T("write"); btnDtcR.Text = T("dtcr"); btnDtcC.Text = T("dtcc");
            if ((trans == null || !trans.IsOpen) && !busy) { lblStatus.Text = T("noconn"); lblStatus.ForeColor = Color.Firebrick; }
            UpdateLogBtn(); Render();
        }

        void UpdateLogBtn() { if (btnLog == null || logPanel == null) return; btnLog.Text = logPanel.Visible ? (lang == "fi" ? "Piilota loki" : "Hide log") : (lang == "fi" ? "N\u00e4yt\u00e4 loki" : "Show log"); }
        void L(string s) { if (txtLog.InvokeRequired) { txtLog.BeginInvoke(new Action(() => L(s))); return; } txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n"); }

        void RefreshPorts() { cbPort.Items.Clear(); foreach (var p in SerialPort.GetPortNames().OrderBy(x => x)) cbPort.Items.Add(p); if (cbPort.Items.Contains("COM7")) cbPort.SelectedItem = "COM7"; else if (cbPort.Items.Count > 0) cbPort.SelectedIndex = 0; }

        void AutoDetect()
        {
            RefreshPorts(); string found = null, foundName = null;
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = (mo["Name"] ?? "").ToString(); int a = name.LastIndexOf("(COM"); if (a < 0) continue;
                        int b = name.IndexOf(')', a); string com = name.Substring(a + 1, b - a - 1);
                        if (name.IndexOf("Arduino", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("CH340", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("FTDI", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("USB Serial", StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("USB-SERIAL", StringComparison.OrdinalIgnoreCase) >= 0) { found = com; foundName = name; break; }
                    }
            }
            catch (Exception ex) { L("Autodetect WMI: " + ex.Message); }
            if (found == null) { var ports = SerialPort.GetPortNames(); if (ports.Length == 1) { found = ports[0]; foundName = "ainoa portti"; } }
            if (found != null) { if (!cbPort.Items.Contains(found)) cbPort.Items.Add(found); cbPort.SelectedItem = found; L("Autodetect: " + found + "  (" + foundName + ")"); }
            else L(lang == "fi" ? "Autodetect: ei l\u00f6ytynyt Arduinoa." : "Autodetect: no Arduino found.");
        }

        void Connect()
        {
            try
            {
                if (trans != null && trans.IsOpen) { trans.Close(); trans = null; this.Text = "Stellantis Config Messer"; lblStatus.Text = T("noconn"); lblStatus.ForeColor = Color.Firebrick; btnConn.Text = T("conn"); return; }
                string err;
                if (Vci()) { var v = new VciTransport { Log = L }; err = v.Open(null); trans = v; }
                else { if (cbPort.SelectedItem == null) { MessageBox.Show(lang == "fi" ? "Valitse COM-portti." : "Select a COM port."); return; } var a = new ArduinoTransport { Log = L }; err = a.Open(cbPort.SelectedItem.ToString()); trans = a; }
                if (err != "") { trans = null; MessageBox.Show((lang == "fi" ? "Yhteysvirhe: " : "Connection error: ") + err); lblStatus.Text = T("noconn"); lblStatus.ForeColor = Color.Firebrick; return; }
                lblStatus.Text = T("connto") + (Vci() ? "Diagbox VCI" : cbPort.SelectedItem.ToString()); lblStatus.ForeColor = Color.Green; btnConn.Text = T("disc");
                ReadVinToTitle();
            }
            catch (Exception ex) { trans = null; MessageBox.Show((lang == "fi" ? "Yhteysvirhe: " : "Connection error: ") + ex.Message); }
        }

        void ReadVinToTitle()
        {
            try
            {
                trans.SelectEcu("752", "652");
                string r = trans.Send("22F190", new[] { "62F190", "7F22" }, 900);
                int i = r.IndexOf("62F190"); if (i < 0) return;
                string vin = AsciiOf(Hex.ToBytes(r.Substring(i + 6)));
                if (vin.Length >= 11) { this.Text = "Stellantis Config Messer  —  VIN " + vin; L("VIN: " + vin); }
            }
            catch { }
        }
        static string AsciiOf(byte[] b) { var sb = new StringBuilder(); foreach (var ch in b) if (ch >= 32 && ch < 127) sb.Append((char)ch); return sb.ToString().Trim(); }

        void FillZones() { cbZone.Items.Clear(); curMod = Data.Modules[cbModule.SelectedIndex]; foreach (var z in curMod.Zones) cbZone.Items.Add(z.Id + "  " + z.Name); if (cbZone.Items.Count > 0) cbZone.SelectedIndex = 0; views.Clear(); Render(); btnWrite.Enabled = false; }

        bool Need() { if (busy) return false; if (trans == null || !trans.IsOpen) { MessageBox.Show(lang == "fi" ? "Ei yhteytt\u00e4." : "Not connected."); return false; } if (curMod == null) { MessageBox.Show(lang == "fi" ? "Valitse moduuli." : "Select module."); return false; } return true; }
        string Tx(string cmd, int ms) { return trans.Send(cmd, null, ms); }
        bool OpenSession() { trans.SelectEcu(curMod.Tx, curMod.Rx); string r = Tx("1003", 1200); if (!r.Contains("5003")) { L("VIRHE/ERROR 1003 -> " + r); return false; } return true; }
        int Idx(Field f) { return f.ByteStart + curMod.ByteOffset; }
        string ReadZone(string zone, int ms) { string r = trans.Send("22" + zone, new[] { "62" + zone, "7F22" }, ms); int i = r.IndexOf("62" + zone); if (i < 0) return null; return r.Substring(i + 2 + zone.Length); }

        void DoReadSingle()
        {
            if (!Need()) return; if (cbZone.SelectedIndex < 0) return; var z = curMod.Zones[cbZone.SelectedIndex];
            try { if (!OpenSession()) return; var v = ReadInto(z, 800); Tx("1001", 300); views = v == null ? new List<ZoneView>() : new List<ZoneView> { v }; Render(); btnWrite.Enabled = views.Count > 0; }
            catch (Exception ex) { L("VIRHE/ERROR read: " + ex.Message); }
        }

        void DoReadAll()
        {
            if (!Need()) return; busy = true; SetButtons(false); var mod = curMod;
            var th = new Thread(() =>
            {
                var list = new List<ZoneView>();
                try
                {
                    trans.SelectEcu(mod.Tx, mod.Rx);
                    if (!Tx("1003", 1200).Contains("5003")) L("VIRHE/ERROR 1003.");
                    else { int n = 0; foreach (var z in mod.Zones) { n++; Status((lang == "fi" ? "Luetaan " : "Reading ") + n + "/" + mod.Zones.Count + "  " + z.Id); var v = ReadInto(z, 700); if (v != null) list.Add(v); } Tx("1001", 300); }
                }
                catch (Exception ex) { L("VIRHE/ERROR read all: " + ex.Message); }
                this.BeginInvoke(new Action(() =>
                {
                    views = list; Render(); btnWrite.Enabled = views.Count > 0; busy = false; SetButtons(true);
                    lblStatus.Text = (trans != null && trans.IsOpen) ? (T("connto") + (Vci() ? "Diagbox VCI" : cbPort.SelectedItem)) : T("noconn"); lblStatus.ForeColor = (trans != null && trans.IsOpen) ? Color.Green : Color.Firebrick;
                    L((lang == "fi" ? "Luettu kaikki: " : "Read all: ") + list.Count + "/" + mod.Zones.Count + " zonea.");
                }));
            });
            th.IsBackground = true; th.Start();
        }

        ZoneView ReadInto(Zone z, int ms) { string hex = ReadZone(z.Id, ms); if (hex == null) return null; return new ZoneView { Zone = z, OrigHex = hex, Bytes = Hex.ToBytes(hex) }; }
        void SetButtons(bool en) { if (InvokeRequired) { BeginInvoke(new Action(() => SetButtons(en))); return; } btnRead.Enabled = en; btnReadAll.Enabled = en; btnDtcR.Enabled = en; btnDtcC.Enabled = en; btnConn.Enabled = en; cbModule.Enabled = en; cbTrans.Enabled = en; }
        void Status(string s) { if (InvokeRequired) { BeginInvoke(new Action(() => Status(s))); return; } lblStatus.Text = s; lblStatus.ForeColor = Color.DarkBlue; }

        Control BuildControl(ZoneView v, Field f, int left, int width)
        {
            int maskByte = Convert.ToInt32(f.Mask, 16); int ix = f.ByteStart + curMod.ByteOffset; int cur = v.Bytes[ix] & maskByte; Control ctrl;
            if (f.Type == "enum" && f.Options.Count >= 2)
            {
                if (f.Options.Count == 2)
                {
                    var ck = new CheckBox { Top = 3, Width = width, Height = 26, Appearance = Appearance.Button, TextAlign = ContentAlignment.MiddleCenter };
                    int onVal = Convert.ToInt32(f.Options[1].Val, 16) & maskByte; ck.Checked = (cur == onVal); ck.Text = (ck.Checked ? f.Options[1] : f.Options[0]).L(lang);
                    var fc = f; ck.CheckedChanged += (s, e) => { ck.Text = (ck.Checked ? fc.Options[1] : fc.Options[0]).L(lang); }; ctrl = ck;
                }
                else { var cb = new ComboBox { Top = 4, Width = width, DropDownStyle = ComboBoxStyle.DropDownList }; int sel = 0; for (int i = 0; i < f.Options.Count; i++) { cb.Items.Add(f.Options[i].L(lang)); if ((Convert.ToInt32(f.Options[i].Val, 16) & maskByte) == cur) sel = i; } cb.SelectedIndex = sel; ctrl = cb; }
            }
            else { ctrl = new TextBox { Top = 5, Width = Math.Min(width, 140), Font = new Font("Consolas", 9), Text = cur.ToString("X2") }; }
            ctrl.Left = left; return ctrl;
        }

        Panel NewRow() { return new Panel { Width = 1040, Height = 34, Margin = new Padding(2, 2, 2, 5) }; }

        void Render()
        {
            if (pnlFields == null) return;
            pnlFields.SuspendLayout(); pnlFields.Controls.Clear();
            foreach (var v in views)
            {
                v.Fs.Clear(); v.Cs.Clear(); v.RawBox = null;
                var fields = v.Zone.Fields.Where(f => (f.ByteStart + curMod.ByteOffset) < v.Bytes.Length).ToList();

                if (v.Zone.Id == "F190")
                {
                    var rv = NewRow(); rv.Controls.Add(new Label { Left = 10, Top = 8, Width = 150, Text = v.Zone.Id + "  VIN:" }); rv.Controls.Add(new TextBox { Left = 170, Top = 5, Width = 340, ReadOnly = true, Font = new Font("Consolas", 10), Text = AsciiOf(v.Bytes) }); pnlFields.Controls.Add(rv); continue;
                }

                if (fields.Count >= 2)
                {
                    var head = new Label { AutoSize = false, Width = 1040, Height = 24, Margin = new Padding(2, 8, 2, 2), Font = new Font("Segoe UI", 9, FontStyle.Bold), ForeColor = Color.White, BackColor = Color.FromArgb(70, 90, 120), Padding = new Padding(6, 3, 0, 0), Text = v.Zone.Id + "   " + v.Zone.Name + "      [" + v.OrigHex + "]", AutoEllipsis = true };
                    pnlFields.Controls.Add(head);
                    foreach (var f in fields)
                    {
                        var row = NewRow();
                        var lbl = new Label { Left = 26, Top = 8, Width = 410, Text = f.L(lang), AutoEllipsis = true };
                        if (!string.IsNullOrEmpty(f.Desc)) { var tt = new ToolTip(); tt.SetToolTip(lbl, f.Desc); }
                        row.Controls.Add(lbl); var ctrl = BuildControl(v, f, 450, 400); row.Controls.Add(ctrl);
                        pnlFields.Controls.Add(row); v.Fs.Add(f); v.Cs.Add(ctrl);
                    }
                }
                else if (fields.Count == 1)
                {
                    var f = fields[0]; var row = NewRow();
                    var lbl = new Label { Left = 10, Top = 8, Width = 430, Text = v.Zone.Id + "  " + f.L(lang), AutoEllipsis = true };
                    var tt = new ToolTip(); tt.SetToolTip(lbl, (string.IsNullOrEmpty(f.Desc) ? "" : f.Desc + "\n") + "raw: " + v.OrigHex);
                    row.Controls.Add(lbl); var ctrl = BuildControl(v, f, 450, 400); row.Controls.Add(ctrl);
                    pnlFields.Controls.Add(row); v.Fs.Add(f); v.Cs.Add(ctrl);
                }
                else
                {
                    var row = NewRow();
                    row.Controls.Add(new Label { Left = 10, Top = 8, Width = 430, Text = v.Zone.Id + "  " + v.Zone.Name + "  (raw)", AutoEllipsis = true });
                    var tx = new TextBox { Left = 450, Top = 5, Width = 520, Font = new Font("Consolas", 9), Text = v.OrigHex };
                    row.Controls.Add(tx); pnlFields.Controls.Add(row); v.RawBox = tx;
                }
            }
            pnlFields.ResumeLayout();
        }

        string BuildNewHex(ZoneView v)
        {
            if (v.RawBox != null) return v.RawBox.Text.Trim().ToUpper().Replace(" ", "");
            byte[] b = (byte[])v.Bytes.Clone();
            for (int i = 0; i < v.Fs.Count; i++)
            {
                var f = v.Fs[i]; int mask = Convert.ToInt32(f.Mask, 16); int val; var c = v.Cs[i];
                if (c is CheckBox) { var ck = (CheckBox)c; val = Convert.ToInt32((ck.Checked ? f.Options[1].Val : f.Options[0].Val), 16) & mask; }
                else if (c is ComboBox) { var cb = (ComboBox)c; val = Convert.ToInt32(f.Options[cb.SelectedIndex].Val, 16) & mask; }
                else { val = Convert.ToInt32(((TextBox)c).Text.Trim(), 16) & mask; }
                int ix = f.ByteStart + curMod.ByteOffset; if (ix >= b.Length) continue; b[ix] = (byte)((b[ix] & ~mask) | val);
            }
            return Hex.ToStr(b, b.Length);
        }

        string SaveBackup()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup", curMod.Id);
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".csv");
            var sb = new StringBuilder();
            sb.AppendLine("Module;" + curMod.Id + " (" + curMod.Name + ")");
            sb.AppendLine("Pair;>" + curMod.Tx + ":" + curMod.Rx + ";Key;" + curMod.Key);
            sb.AppendLine("Timestamp;" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("Zone;Hex");
            foreach (var v in views) sb.AppendLine(v.Zone.Id + ";" + v.OrigHex);
            File.WriteAllText(file, sb.ToString(), new UTF8Encoding(false));
            return file;
        }

        void DoWrite()
        {
            if (!Need()) return; if (views.Count == 0) { MessageBox.Show(lang == "fi" ? "Lue ensin." : "Read first."); return; }
            var changed = new List<KeyValuePair<ZoneView, string>>();
            try { foreach (var v in views) { string nw = BuildNewHex(v); if (!nw.Equals(v.OrigHex, StringComparison.OrdinalIgnoreCase)) changed.Add(new KeyValuePair<ZoneView, string>(v, nw)); } }
            catch (Exception ex) { MessageBox.Show((lang == "fi" ? "Virheellinen sy\u00f6te: " : "Invalid input: ") + ex.Message); return; }
            if (changed.Count == 0) { MessageBox.Show(lang == "fi" ? "Ei muutoksia." : "No changes."); return; }
            var sb = new StringBuilder(); foreach (var c in changed) sb.AppendLine(c.Key.Zone.Id + ":  " + c.Key.OrigHex + "  ->  " + c.Value);
            if (MessageBox.Show((lang == "fi" ? "Kirjoitetaanko n\u00e4m\u00e4 zonet?\n\n" : "Write these zones?\n\n") + sb, (lang == "fi" ? "Vahvista" : "Confirm"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            try { string bk = SaveBackup(); L((lang == "fi" ? "Varmuuskopio: " : "Backup: ") + bk); }
            catch (Exception ex) { MessageBox.Show((lang == "fi" ? "Varmuuskopio ep\u00e4onnistui: " : "Backup failed: ") + ex.Message + (lang == "fi" ? "\n\nKirjoitusta EI tehty." : "\n\nWrite aborted.")); return; }
            try
            {
                if (!OpenSession()) return;
                string seedr = Tx("2703", 800); int si = seedr.IndexOf("6703"); if (si < 0) { Tx("1001", 300); L("VIRHE/ERROR: no seed."); return; }
                byte[] chg = Hex.ToBytes(seedr.Substring(si + 4, 8)); string key = Sk.Key(curMod.Key, chg);
                if (!Tx("2704" + key, 800).Contains("6704")) { Tx("1001", 300); L("VIRHE/ERROR: unlock rejected (wrong key?)."); return; }
                int okCount = 0;
                foreach (var c in changed)
                {
                    var v = c.Key; string nw = c.Value; string zid = v.Zone.Id;
                    string now = ReadZone(zid, 800);
                    if (now == null || !now.Equals(v.OrigHex, StringComparison.OrdinalIgnoreCase)) { L("GUARD " + zid + ": " + now + " != " + v.OrigHex + " - ohitettu."); continue; }
                    if (!Tx("2E" + zid + nw, 2000).Contains("6E" + zid)) { L("VIRHE " + zid + ": kirjoitus hyl\u00e4tty."); continue; }
                    string chk = ReadZone(zid, 800); bool ok = chk != null && chk.Equals(nw, StringComparison.OrdinalIgnoreCase);
                    if (curMod.Trace == "yes" || curMod.Trace == "try") { string tr = Tx("2E2901FD000000010101", 2000); if (tr.Contains("6E2901")) L("2901 " + zid + " OK."); else if (curMod.Trace == "yes") L("WARN 2901 " + zid + " (" + tr + ")."); }
                    if (ok) { v.OrigHex = nw; v.Bytes = Hex.ToBytes(nw); okCount++; L("OK " + zid + " -> " + nw); } else L("TARKISTA " + zid + ": " + chk);
                }
                Tx("1001", 300); Render();
                MessageBox.Show((lang == "fi" ? "Kirjoitettu " : "Written ") + okCount + "/" + changed.Count + (lang == "fi" ? " zonea." : " zones."));
            }
            catch (Exception ex) { try { Tx("1001", 300); } catch { } L("VIRHE/ERROR write: " + ex.Message); }
        }

        List<string> DtcDecode(string r)
        {
            var res = new List<string>(); r = (r ?? "").Replace(" ", "");
            int i = r.IndexOf("5902"); if (i < 0) return res;
            string d = r.Substring(i + 4); if (d.Length < 2) return res; d = d.Substring(2);
            for (int p = 0; p + 8 <= d.Length; p += 8)
            {
                string dtc = d.Substring(p, 6); string st = d.Substring(p + 6, 2);
                int b1; try { b1 = Convert.ToInt32(dtc.Substring(0, 2), 16); } catch { break; }
                char letter = "PCBU"[(b1 >> 6) & 3];
                string code = letter.ToString() + ((b1 >> 4) & 3).ToString() + (b1 & 0xF).ToString("X") + dtc.Substring(2, 2);
                res.Add(code + "   FTB=" + dtc.Substring(4, 2) + "  status=" + st + "   (raw " + dtc + ")");
            }
            return res;
        }

        void ShowDtc(string title, List<string> items, string raw)
        {
            var f = new Form { Text = title, Width = 540, Height = 420, StartPosition = FormStartPosition.CenterParent, Font = this.Font };
            var lst = new ListBox { Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };
            if (items.Count == 0) lst.Items.Add(lang == "fi" ? "Ei vikakoodeja (puhdas)." : "No DTCs (clean).");
            else foreach (var s in items) lst.Items.Add(s);
            var bot = new Panel { Dock = DockStyle.Bottom, Height = 50 };
            bot.Controls.Add(new TextBox { Left = 8, Top = 14, Width = 505, ReadOnly = true, Font = new Font("Consolas", 8), Text = "raw: " + raw, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top });
            f.Controls.Add(lst); f.Controls.Add(bot); f.ShowDialog(this);
        }

        void DoDtc(bool clear)
        {
            if (!Need()) return;
            try { if (!OpenSession()) return; if (clear) { string c = Tx("14FFFFFF", 1500); L(c.Contains("54") ? "DTC cleared (54)." : "Clear: " + c); Thread.Sleep(300); } string r = Tx("19020C", 1500); Tx("1001", 300); L("DTC (" + curMod.Id + "): " + r); ShowDtc((lang == "fi" ? "Vikakoodit: " : "DTCs: ") + curMod.Id, DtcDecode(r), r); }
            catch (Exception ex) { L("VIRHE/ERROR DTC: " + ex.Message); }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try { Data.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zones.json")); }
            catch (Exception ex) { MessageBox.Show("zones.json: " + ex.Message, "Stellantis Config Messer", MessageBoxButtons.OK, MessageBoxIcon.Error); return; }
            Application.Run(new MainForm());
        }
    }
}
