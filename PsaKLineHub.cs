using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using StellantisConfigMesser.Diagnostics;

namespace ZoneTool
{
    internal class PsaKLineEcuSpec { public string family { get; set; } public string variant { get; set; } public string table { get; set; } public int obdPin { get; set; } public string specifMp { get; set; } public List<string> initPairs { get; set; } public List<string> sampleFrames { get; set; } }
    internal class PsaKLineRoot { public string source { get; set; } public int totalVariants { get; set; } public List<PsaKLineEcuSpec> ecus { get; set; } }

    internal class PsaKLineForm : Form
    {
        private PsaKLineRoot db;
        private TextBox txtFilter, txtEcuDetail, txtRawHex, txtDecodedOut;
        private ListView lvEcus, lvActuators;

        private VciTransport vci; public PsaKLineForm(string lang, object tr = null) { this.vci = tr as VciTransport; InitVciForm(lang); } private void InitVciForm(string lang)
        {
            bool fi = (lang == "fi");
            this.Text = fi ? "PSA K-Line & Bosch MP 7.0 Diagnostiikka (675 ECU) - v1.9.7" : "PSA K-Line & Bosch MP 7.0 Diagnostics (675 ECUs) - v1.9.7";
            this.Width = 1060; this.Height = 680; this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = Color.FromArgb(20, 26, 36); this.ForeColor = Color.White; this.Font = new Font("Segoe UI", 9.5f);
            string err = "JSON puuttuu"; try { string raw = AppDomain.CurrentDomain.GetData("PSA_JSON") as string; if (string.IsNullOrEmpty(raw)) { string p = Path.Combine(Environment.CurrentDirectory, "PsaKLineDatabase.json"); if (File.Exists(p)) raw = File.ReadAllText(p, Encoding.UTF8); } if (!string.IsNullOrEmpty(raw)) { raw = raw.Trim(); this.db = new JavaScriptSerializer { MaxJsonLength = 67108864 }.Deserialize<PsaKLineRoot>(raw); } } catch (Exception ex) { err = ex.Message; }

            TabControl tabs = new TabControl { Dock = DockStyle.Fill };
            TabPage tabDb = new TabPage(fi ? "1. PSA K-Line Tietokanta (675 ECU)" : "1. PSA K-Line Database (675 ECUs)") { BackColor = Color.FromArgb(20, 26, 36) };
            Panel pnlTopDb = new Panel { Dock = DockStyle.Top, Height = 42 };
            this.txtFilter = new TextBox { Left = 170, Top = 8, Width = 280, BackColor = Color.FromArgb(30, 38, 52), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle };
            pnlTopDb.Controls.Add(new Label { Left = 10, Top = 11, AutoSize = true, Text = fi ? "Hae ECU / Perhe:" : "Search ECU / Family:" });
            pnlTopDb.Controls.Add(this.txtFilter);
            pnlTopDb.Controls.Add(new Label { Left = 465, Top = 11, AutoSize = true, ForeColor = Color.LightGreen, Text = (db != null ? db.totalVariants + " varianttia / 122 perhetta" : err) });

            SplitContainer sp = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical, SplitterDistance = 600 };
            this.lvEcus = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BackColor = Color.FromArgb(25, 32, 44), ForeColor = Color.White };
            this.lvEcus.Columns.Add("Perhe", 85); this.lvEcus.Columns.Add("Variantti", 120); this.lvEcus.Columns.Add("Taulu", 130); this.lvEcus.Columns.Add("OBD Pin", 70); this.lvEcus.Columns.Add("Specif MP", 170);
            this.txtEcuDetail = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 20, 28), ForeColor = Color.FromArgb(140, 230, 170), Font = new Font("Consolas", 9.5f) };
            sp.Panel1.Controls.Add(this.lvEcus); sp.Panel2.Controls.Add(this.txtEcuDetail);
            tabDb.Controls.Add(sp); tabDb.Controls.Add(pnlTopDb);

            TabPage tabMp7 = new TabPage("2. Bosch MP 7.0 (ES9J4 V6) Live & DTC") { BackColor = Color.FromArgb(20, 26, 36) };
            Panel pnlHex = new Panel { Dock = DockStyle.Top, Height = 64 };
            this.txtRawHex = new TextBox { Left = 10, Top = 28, Width = 620, BackColor = Color.FromArgb(30, 38, 52), ForeColor = Color.White, Font = new Font("Consolas", 10f), Text = "61 01 0C 80" };
            Button btnVSend = new Button { Left = 520, Top = 27, Width = 140, Height = 26, Text = "Laheta VCI:lle", BackColor = Color.FromArgb(35, 130, 80), FlatStyle = FlatStyle.Flat }; btnVSend.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); if (!this.vci.IsOpen) { this.vci.Open(""); this.vci.VciKlineInit(7, 0x33, false); } string rx = this.vci.VciKlineTxRx(this.txtRawHex.Text, 1200); this.txtDecodedOut.Text = "TX -> " + this.txtRawHex.Text + "\r\nRX <- " + rx; }; pnlHex.Controls.Add(btnVSend); CheckBox chkKeep = new CheckBox { Left = 670, Top = 60, AutoSize = true, ForeColor = Color.LightGreen, Text = "3E Pito (2.5s)" }; CheckBox chkPoll = new CheckBox { Left = 780, Top = 60, AutoSize = true, ForeColor = Color.Cyan, Text = "Live (0.5s)" }; Button btnDump = new Button { Left = 875, Top = 58, Width = 115, Height = 24, Text = "Dump 21 80..FF", BackColor = Color.FromArgb(85, 65, 130), FlatStyle = FlatStyle.Flat }; System.Windows.Forms.Timer tKeep = new System.Windows.Forms.Timer { Interval = 2500 }; tKeep.Tick += delegate { if (chkKeep.Checked && this.vci != null && this.vci.IsOpen) this.vci.VciKlineTxRx("3E", 400); }; chkKeep.CheckedChanged += delegate { tKeep.Enabled = chkKeep.Checked; }; System.Windows.Forms.Timer tPoll = new System.Windows.Forms.Timer { Interval = 500 }; tPoll.Tick += delegate { if (chkPoll.Checked && this.vci != null && this.vci.IsOpen) { string rx = this.vci.VciKlineTxRx(this.txtRawHex.Text, 450); this.txtDecodedOut.Text = "[" + DateTime.Now.ToString("HH:mm:ss.f") + "] TX -> " + this.txtRawHex.Text + "\r\nRX <- " + rx; } }; chkPoll.CheckedChanged += delegate { tPoll.Enabled = chkPoll.Checked; }; btnDump.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); var sb = new StringBuilder("=== KWP2000 K-LINE DUMP (21 80..21 FF) ===\r\n"); for (int id = 0x80; id <= 0xFF; id++) { string cmd = "21 " + id.ToString("X2"); string rx = this.vci.VciKlineTxRx(cmd, 250); if (!rx.StartsWith("[") && !rx.StartsWith("7F") && !rx.StartsWith("ERR")) sb.AppendLine(cmd + " -> " + rx); } this.txtDecodedOut.Text = sb.ToString(); }; pnlHex.Controls.AddRange(new Control[] { chkKeep, chkPoll, btnDump }); Button btnDec = new Button { Left = 640, Top = 27, Width = 140, Height = 26, Text = fi ? "Tulkitse Hex" : "Decode Hex", BackColor = Color.FromArgb(45, 110, 175), FlatStyle = FlatStyle.Flat };
            Button btnPins = new Button { Left = 790, Top = 27, Width = 160, Height = 26, Text = "406 D8 OBD-pinnit", BackColor = Color.FromArgb(40, 58, 84), FlatStyle = FlatStyle.Flat };
            pnlHex.Controls.AddRange(new Control[] { new Label { Left = 10, Top = 6, AutoSize = true, Text = "KWP2000 Hex (esim. 61 01 0C 80 tai 58 02 03 51 20 01 15 10):" }, this.txtRawHex, btnDec, btnPins });
            this.txtDecodedOut = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.FromArgb(15, 20, 28), ForeColor = Color.FromArgb(220, 235, 255), Font = new Font("Consolas", 10f) };
            tabMp7.Controls.Add(this.txtDecodedOut); tabMp7.Controls.Add(pnlHex);

            TabPage tabAct = new TabPage("3. MP 7.0 Toimilaitetestit (0x30)") { BackColor = Color.FromArgb(20, 26, 36) };
            this.lvActuators = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true, GridLines = true, BackColor = Color.FromArgb(25, 32, 44), ForeColor = Color.White };
            this.lvActuators.Columns.Add("PP2000 ID", 130); this.lvActuators.Columns.Add("Toimilaite", 250); this.lvActuators.Columns.Add("Local ID", 75); this.lvActuators.Columns.Add("Start Hex", 145); this.lvActuators.Columns.Add("Stop Hex", 135); this.lvActuators.Columns.Add("Edellytykset", 250);
            foreach (var a in Mp7Decoder.ActuatorTests) { var it = new ListViewItem(a.Pp2000Id); it.SubItems.Add(a.Name); it.SubItems.Add("0x" + a.LocalId.ToString("X2")); it.SubItems.Add(BitConverter.ToString(a.KwpRequest).Replace("-", " ")); it.SubItems.Add(BitConverter.ToString(Mp7Decoder.BuildStopActuatorCommand(a.LocalId)).Replace("-", " ")); it.SubItems.Add(a.Notes); this.lvActuators.Items.Add(it); }
            tabAct.Controls.Add(this.lvActuators);

            tabs.TabPages.AddRange(new[] { tabDb, tabMp7, tabAct });
            Panel pv = new Panel { Dock = DockStyle.Top, Height = 36, BackColor = Color.FromArgb(28, 38, 54) }; Label lV = new Label { Left = 748, Top = 10, AutoSize = true, ForeColor = Color.Silver, Text = "VCI: Valmis" }; Button bO = new Button { Left = 8, Top = 5, Width = 135, Height = 26, Text = "1. Avaa Actia VCI", BackColor = Color.FromArgb(35, 115, 75), FlatStyle = FlatStyle.Flat }; Button b7 = new Button { Left = 150, Top = 5, Width = 165, Height = 26, Text = "2. Alusta Pin 7", BackColor = Color.FromArgb(45, 110, 175), FlatStyle = FlatStyle.Flat }; Button bSc = new Button { Left = 322, Top = 5, Width = 195, Height = 26, Text = "3. Skannaa PSA Pinnit", BackColor = Color.FromArgb(135, 85, 35), FlatStyle = FlatStyle.Flat }; Button bAuto = new Button { Left = 525, Top = 5, Width = 215, Height = 26, Text = "AUTO-TUNNISTA (CAN + K-Line)", BackColor = Color.FromArgb(145, 55, 135), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) }; bAuto.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); var sb = new StringBuilder("=== AUTOMAATTINEN PSA-SUKUPOLVEN TUNNISTUS ===\r\n"); this.vci.Open(""); this.vci.SelectEcu("752", "652"); string canVin = this.vci.VciKlineTxRx("22F190", 800); if (!string.IsNullOrEmpty(canVin) && !canVin.StartsWith("ERR") && !canVin.Contains("TIMEOUT") && !canVin.Contains("NO ECU")) { sb.AppendLine("TULOS: UUDEMPI CAN/UDS-SUKUPOLVI (~2004-2024)!"); sb.AppendLine("BSI CAN (752/652) vastasi -> " + canVin); sb.AppendLine("Toiminta: Kayta paaikkunan 53 CAN-ECU:ta."); } else { sb.AppendLine("CAN-vayla (Pin 6/14) ei vastannut -> Siirrytaan K-Line / VAN-tunnistukseen..."); foreach (int pin in new[] { 7, 15, 11, 12, 13, 14, 20 }) { string fRes = this.vci.VciKlineInit(pin, 0x33, true); if (fRes.Contains("-> OK")) { sb.AppendLine("Pin " + pin + " [Fast Init KWP2000 ~2001-2005] -> VASTASI! ID: " + this.vci.VciKlineTxRx("21 80", 800)); continue; } string sRes = this.vci.VciKlineInit(pin, 0x33, false); sb.AppendLine("Pin " + pin + " [5-Baud K-Line ~1996-2002] -> " + sRes); if (sRes.Contains("-> OK")) sb.AppendLine("   ECU ID: " + this.vci.VciKlineTxRx("21 80", 800)); } } this.txtDecodedOut.Text = sb.ToString(); lV.Text = "Auto-tunnistus valmis!"; }; lV = new Label { Left = 748, Top = 9, AutoSize = true, ForeColor = Color.LightGreen, Text = "VCI valmis (Pin 7 / 11 / 12 / 15 / 20)" }; bO.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); string r = this.vci.Open(""); lV.Text = this.vci.IsOpen ? "VCI: Yhdistetty (VCIAccess.dll OK)" : r; }; b7.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); lV.Text = this.vci.VciKlineInit(7, 0x33, false); }; bSc.Text = "3. Etsi Proto & 406 D8"; bSc.Click += delegate { if (this.vci == null) this.vci = new VciTransport(); this.txtDecodedOut.Text = ActiaJ2534Kline.Probe406D8ViaJ2534(); lV.Text = "Protokollahaku valmis!"; }; Action oldScan = delegate { if (this.vci == null) this.vci = new VciTransport(); var sb = new StringBuilder("=== KOKO PSA-KALUSTON K-LINE SKANNAUS ===\r\n"); foreach (int pin in new[] { 7, 11, 12, 13, 14, 15, 20 }) { string r = this.vci.VciKlineInit(pin, 0x33, false); sb.AppendLine("Pin " + pin + " -> " + r); if (r.Contains("-> OK")) sb.AppendLine("   ECU ID: " + this.vci.VciKlineTxRx("21 80", 800)); } this.txtDecodedOut.Text = sb.ToString(); lV.Text = "PSA Skannaus valmis (7,11,12,13,14,15,20)"; }; pv.Controls.AddRange(new Control[] { bO, b7, bSc, bAuto, lV }); this.Controls.Add(tabs); this.Controls.Add(pv);

            this.txtFilter.TextChanged += delegate { FilterList(this.txtFilter.Text); };
            this.lvEcus.SelectedIndexChanged += delegate { var sel = this.lvEcus.SelectedItems.Cast<ListViewItem>().FirstOrDefault(); if (sel == null) return; var e = sel.Tag as PsaKLineEcuSpec; if (e == null) return; var sb = new StringBuilder(); sb.AppendLine("Perhe: " + e.family + " | Variantti: " + e.variant + " | Taulu: " + e.table + "\r\nOBD Pin: " + e.obdPin + " (Actia Ligne" + e.obdPin + ") | Specif MP: " + e.specifMp + "\r\n\r\nKattelyparit:"); if (e.initPairs != null) foreach (var p in e.initPairs) sb.AppendLine("  " + p); sb.AppendLine("\r\nEsimerkkikehykset:"); if (e.sampleFrames != null) foreach (var f in e.sampleFrames) sb.AppendLine("  " + f); this.txtEcuDetail.Text = sb.ToString(); };
            btnPins.Click += delegate { var sb = new StringBuilder("=== PEUGEOT 406 D8 (1997) OBD-16 PINNIT ===\r\n"); foreach (var p in Mp7Decoder.VehicleEcuPins) sb.AppendLine(string.Format("Pin {0,-2} | {1,-34} | {2} baud | Addr 0x{3:X2}", p.ObdPin, p.SystemName, p.BaudRate, p.InitAddress)); this.txtDecodedOut.Text = sb.ToString(); };
            btnDec.Click += delegate { try { byte[] b = this.txtRawHex.Text.Split(new[] { ' ', '-', ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => Convert.ToByte(x, 16)).ToArray(); var sb = new StringBuilder("Raakakehys: " + BitConverter.ToString(b).Replace("-", " ") + "\r\n"); var lp = Mp7Decoder.DecodeParameter(b); if (lp != null && lp.NameFi != null) { sb.AppendLine(lp.NameFi + ": " + lp.ValueFormatted); } else { foreach (var d in Mp7Decoder.DecodeFaultCodes(b)) sb.AppendLine(d.HexCode + " (" + d.StatusFi + "): " + d.DescriptionFi); } this.txtDecodedOut.Text = sb.ToString(); } catch (Exception ex) { this.txtDecodedOut.Text = ex.Message; } };
            FilterList(""); btnPins.PerformClick();
        }

        private void FilterList(string q)
        {
            if (this.db == null || this.db.ecus == null) return;
            q = (q ?? "").Trim().ToLowerInvariant();
            this.lvEcus.BeginUpdate(); this.lvEcus.Items.Clear();
            foreach (var e in this.db.ecus) {
                if (q.Length > 0 && !((e.family + " " + e.variant + " " + e.table + " pin " + e.obdPin + " " + e.specifMp).ToLowerInvariant().Contains(q))) continue;
                var it = new ListViewItem(e.family ?? ""); it.SubItems.Add(e.variant ?? ""); it.SubItems.Add(e.table ?? ""); it.SubItems.Add("Pin " + e.obdPin); it.SubItems.Add(e.specifMp ?? ""); it.Tag = e; this.lvEcus.Items.Add(it);
            }
            this.lvEcus.EndUpdate();
        }
    }
}