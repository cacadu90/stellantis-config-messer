using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

[assembly: AssemblyVersion("0.0.0.0")]
[assembly: CompilationRelaxations(8)]
[assembly: RuntimeCompatibility(WrapNonExceptionThrows = true)]
namespace ZoneTool
{
	// Token: 0x02000002 RID: 2
	internal class Opt
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public string L(string lang)
		{
			return (lang == "fi" && !string.IsNullOrEmpty(this.Fi)) ? this.Fi : this.En;
		}

		// Token: 0x04000001 RID: 1
		public string En;

		// Token: 0x04000002 RID: 2
		public string Fi;

		// Token: 0x04000003 RID: 3
		public string Val;
	}
	// Token: 0x02000003 RID: 3
	internal class Field
	{
		// Token: 0x06000003 RID: 3 RVA: 0x00002094 File Offset: 0x00000294
		public string L(string lang)
		{
			return (lang == "fi" && !string.IsNullOrEmpty(this.Fi)) ? this.Fi : this.En;
		}

		// Token: 0x04000004 RID: 4
		public string En;

		// Token: 0x04000005 RID: 5
		public string Fi;

		// Token: 0x04000006 RID: 6
		public string Desc;

		// Token: 0x04000007 RID: 7
		public string Type;

		// Token: 0x04000008 RID: 8
		public string Mask;

		// Token: 0x04000009 RID: 9
		public int ByteStart;

		// Token: 0x0400000A RID: 10
		public List<Opt> Options = new List<Opt>();
	}
	// Token: 0x02000004 RID: 4
	internal class Zone
	{
		// Token: 0x0400000B RID: 11
		public string Id;

		// Token: 0x0400000C RID: 12
		public string Name;

		// Token: 0x0400000D RID: 13
		public List<Field> Fields = new List<Field>();
	}
	// Token: 0x02000005 RID: 5
	internal class Module
	{
		// Token: 0x0400000E RID: 14
		public string Id;

		// Token: 0x0400000F RID: 15
		public string Name;

		// Token: 0x04000010 RID: 16
		public string Tx;

		// Token: 0x04000011 RID: 17
		public string Rx;

		// Token: 0x04000012 RID: 18
		public string Key;

		// Token: 0x04000013 RID: 19
		public string Trace;

		// Token: 0x04000014 RID: 20
		public int ByteOffset;

		// Token: 0x04000015 RID: 21
		public bool Verified;

		// Token: 0x04000016 RID: 22
		public List<Zone> Zones = new List<Zone>();
	}
	// VIN pairing candidate (infotainment / radio unit on one tx:rx pair)
	internal class VinPair
	{
		public string Tx;

		public string Rx;

		public string Label;

		public string ModVin;

		public Module Mod;

		public List<string> Keys = new List<string>();
	}
	// Token: 0x02000006 RID: 6
	internal static class Data
	{
		// Token: 0x06000007 RID: 7 RVA: 0x0000210C File Offset: 0x0000030C
		private static string S(object o)
		{
			return (o == null) ? "" : o.ToString();
		}

		// Token: 0x06000008 RID: 8 RVA: 0x00002130 File Offset: 0x00000330
		private static Dictionary<string, object> D(object o)
		{
			return o as Dictionary<string, object>;
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00002148 File Offset: 0x00000348
		private static object[] A(object o)
		{
			return o as object[];
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002160 File Offset: 0x00000360
		private static object[] Arr(object o)
		{
			object[] result;
			if (o == null)
			{
				result = new object[0];
			}
			else
			{
				object[] array = o as object[];
				if (array != null)
				{
					result = array;
				}
				else if (o is Dictionary<string, object>)
				{
					result = new object[]
					{
						o
					};
				}
				else
				{
					result = new object[0];
				}
			}
			return result;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x000021C0 File Offset: 0x000003C0
		private static string Pick(Dictionary<string, object> d, params string[] keys)
		{
			foreach (string key in keys)
			{
				if (d.ContainsKey(key) && d[key] != null)
				{
					return Data.S(d[key]);
				}
			}
			return "";
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002250 File Offset: 0x00000450
		public static void Load(string path)
		{
			string input = File.ReadAllText(path, Encoding.UTF8);
			Dictionary<string, object> dictionary = Data.D(new JavaScriptSerializer
			{
				MaxJsonLength = 83886080
			}.DeserializeObject(input));
			if (dictionary.ContainsKey("seedkey"))
			{
				Dictionary<string, object> dictionary2 = Data.D(dictionary["seedkey"]);
				Data.Sec1 = (from x in Data.A(dictionary2["sec1"])
				select Convert.ToInt32(x)).ToArray<int>();
				Data.Sec2 = (from x in Data.A(dictionary2["sec2"])
				select Convert.ToInt32(x)).ToArray<int>();
			}
			Data.Modules.Clear();
			foreach (object o in Data.Arr(dictionary["modules"]))
			{
				Dictionary<string, object> dictionary3 = Data.D(o);
				if (dictionary3 != null)
				{
					Module module = new Module
					{
						Id = Data.S(dictionary3["id"]),
						Name = Data.S(dictionary3["name"]),
						Tx = Data.S(dictionary3["tx"]),
						Rx = Data.S(dictionary3["rx"]),
						Key = Data.S(dictionary3["key"]),
						Trace = (dictionary3.ContainsKey("traceability") ? Data.S(dictionary3["traceability"]) : "no"),
						ByteOffset = (dictionary3.ContainsKey("byteOffset") ? Convert.ToInt32(dictionary3["byteOffset"]) : 0),
						Verified = (dictionary3.ContainsKey("verified") && Convert.ToBoolean(dictionary3["verified"]))
					};
					if (dictionary3.ContainsKey("zones"))
					{
						foreach (object o2 in Data.Arr(dictionary3["zones"]))
						{
							Dictionary<string, object> dictionary4 = Data.D(o2);
							if (dictionary4 != null)
							{
								Zone zone = new Zone
								{
									Id = Data.S(dictionary4["id"]),
									Name = Data.Pick(dictionary4, new string[]
									{
										"name"
									})
								};
								if (dictionary4.ContainsKey("fields"))
								{
									foreach (object o3 in Data.Arr(dictionary4["fields"]))
									{
										Dictionary<string, object> dictionary5 = Data.D(o3);
										if (dictionary5 != null)
										{
											Field field = new Field
											{
												En = Data.Pick(dictionary5, new string[]
												{
													"en",
													"name"
												}),
												Fi = Data.Pick(dictionary5, new string[]
												{
													"fi"
												}),
												Desc = Data.Pick(dictionary5, new string[]
												{
													"desc"
												}),
												Type = Data.Pick(dictionary5, new string[]
												{
													"type"
												}),
												Mask = (dictionary5.ContainsKey("mask") ? Data.S(dictionary5["mask"]) : "FF"),
												ByteStart = (dictionary5.ContainsKey("byteStart") ? Convert.ToInt32(dictionary5["byteStart"]) : 0)
											};
											if (dictionary5.ContainsKey("options"))
											{
												foreach (object o4 in Data.Arr(dictionary5["options"]))
												{
													Dictionary<string, object> dictionary6 = Data.D(o4);
													if (dictionary6 != null)
													{
														field.Options.Add(new Opt
														{
															En = Data.Pick(dictionary6, new string[]
															{
																"en",
																"label"
															}),
															Fi = Data.Pick(dictionary6, new string[]
															{
																"fi"
															}),
															Val = Data.S(dictionary6["val"])
														});
													}
												}
											}
											zone.Fields.Add(field);
										}
									}
								}
								module.Zones.Add(zone);
							}
						}
					}
					Data.Modules.Add(module);
				}
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x000027BC File Offset: 0x000009BC
		public static void LoadDtc(string path)
		{
			if (File.Exists(path))
			{
				Dictionary<string, object> dictionary = Data.D(new JavaScriptSerializer
				{
					MaxJsonLength = 83886080
				}.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)));
				if (dictionary != null)
				{
					foreach (KeyValuePair<string, object> keyValuePair in dictionary)
					{
						Data.Dtc[keyValuePair.Key] = Data.S(keyValuePair.Value);
					}
				}
			}
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00002870 File Offset: 0x00000A70
		public static void LoadOverlay(string path)
		{
			if (File.Exists(path))
			{
				try
				{
					Dictionary<string, object> dictionary = Data.D(new JavaScriptSerializer
					{
						MaxJsonLength = 83886080
					}.DeserializeObject(File.ReadAllText(path, Encoding.UTF8)));
					if (dictionary != null)
					{
						if (dictionary.ContainsKey("dtc"))
						{
							Dictionary<string, object> dictionary2 = Data.D(dictionary["dtc"]);
							if (dictionary2 != null)
							{
								foreach (KeyValuePair<string, object> keyValuePair in dictionary2)
								{
									Data.Dtc[keyValuePair.Key] = Data.S(keyValuePair.Value);
								}
							}
						}
						if (dictionary.ContainsKey("zones"))
						{
							Dictionary<string, object> dictionary3 = Data.D(dictionary["zones"]);
							if (dictionary3 != null)
							{
								foreach (KeyValuePair<string, object> keyValuePair in dictionary3)
								{
									Data.ZoneHints[keyValuePair.Key] = Data.S(keyValuePair.Value);
								}
							}
						}
					}
				}
				catch
				{
				}
			}
		}

		// Token: 0x04000017 RID: 23
		public static List<Module> Modules = new List<Module>();

		// Token: 0x04000018 RID: 24
		public static Dictionary<string, string> Dtc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// Token: 0x04000019 RID: 25
		public static Dictionary<string, string> ZoneHints = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

		// Token: 0x0400001A RID: 26
		public static int[] Sec1 = new int[]
		{
			178,
			63,
			170
		};

		// Token: 0x0400001B RID: 27
		public static int[] Sec2 = new int[]
		{
			177,
			2,
			171
		};
	}
	// Token: 0x02000007 RID: 7
	internal static class Cfg
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000012 RID: 18 RVA: 0x00002AA8 File Offset: 0x00000CA8
		private static string Path_
		{
			get
			{
				return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "StellantisConfigMesser.ini");
			}
		}

		// Token: 0x06000013 RID: 19 RVA: 0x00002AD0 File Offset: 0x00000CD0
		public static void Load()
		{
			try
			{
				if (File.Exists(Cfg.Path_))
				{
					foreach (string text in File.ReadAllLines(Cfg.Path_))
					{
						string text2 = text.Trim();
						if (text2.Length != 0 && !text2.StartsWith("#") && !text2.StartsWith("["))
						{
							int num = text2.IndexOf('=');
							if (num >= 0)
							{
								string a = text2.Substring(0, num).Trim().ToLower();
								string text3 = text2.Substring(num + 1).Trim();
								if (a == "lang")
								{
									Cfg.Lang = ((text3 == "en") ? "en" : "fi");
								}
								else if (a == "transport")
								{
									Cfg.Transport = text3;
								}
								else if (a == "port")
								{
									Cfg.Port = text3;
								}
								else if (a == "pin")
								{
									Cfg.Pin = (text3 == "1" || text3.ToLower() == "true");
								}
								else if (a == "onlydecoded")
								{
									Cfg.OnlyDecoded = (text3 == "1" || text3.ToLower() == "true");
								}
							}
						}
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000014 RID: 20 RVA: 0x00002C74 File Offset: 0x00000E74
		public static void Save()
		{
			try
			{
				StringBuilder stringBuilder = new StringBuilder();
				stringBuilder.AppendLine("# Stellantis Config Messer settings");
				stringBuilder.AppendLine("[settings]");
				stringBuilder.AppendLine("lang=" + Cfg.Lang);
				stringBuilder.AppendLine("transport=" + Cfg.Transport);
				stringBuilder.AppendLine("port=" + Cfg.Port);
				stringBuilder.AppendLine("# pin=1 enables the immobiliser PIN-read button (your own car only)");
				stringBuilder.AppendLine("pin=" + (Cfg.Pin ? "1" : "0"));
				stringBuilder.AppendLine("# onlydecoded=1 hides raw-hex-only zones and the raw hex string");
				stringBuilder.AppendLine("onlydecoded=" + (Cfg.OnlyDecoded ? "1" : "0"));
				File.WriteAllText(Cfg.Path_, stringBuilder.ToString(), new UTF8Encoding(false));
			}
			catch
			{
			}
		}

		// Token: 0x0400001E RID: 30
		public static string Lang = "fi";

		// Token: 0x0400001F RID: 31
		public static string Transport = "Arduino";

		// Token: 0x04000020 RID: 32
		public static string Port = "";

		// Token: 0x04000021 RID: 33
		public static bool Pin = false;

		public static bool OnlyDecoded = true;
	}
	// Token: 0x02000008 RID: 8
	internal static class Sk
	{
		// Token: 0x06000016 RID: 22 RVA: 0x00002D6C File Offset: 0x00000F6C
		private static int Transform(int msb, int lsb, int[] sec)
		{
			int num = (msb & 255) << 8 | (lsb & 255);
			if (num > 32767)
			{
				num -= 65536;
			}
			int num2 = (int)Math.Truncate((double)num / (double)sec[0]);
			int num3 = num - num2 * sec[0];
			int num4 = num3 * sec[2] - num2 * sec[1];
			if (num4 < 0)
			{
				num4 += sec[0] * sec[2] + sec[1];
			}
			return num4;
		}

		// Token: 0x06000017 RID: 23 RVA: 0x00002DEC File Offset: 0x00000FEC
		public static string Key(string key2, byte[] chg)
		{
			int msb = Convert.ToInt32(key2.Substring(0, 2), 16);
			int lsb = Convert.ToInt32(key2.Substring(2, 2), 16);
			int num = Sk.Transform(msb, lsb, Data.Sec1) | Sk.Transform((int)chg[0], (int)chg[3], Data.Sec2);
			int num2 = Sk.Transform((int)chg[1], (int)chg[2], Data.Sec1) | Sk.Transform(num >> 8, num & 255, Data.Sec2);
			return (((long)num << 16 | (long)((ulong)num2)) & 0xFFFFFFFFL).ToString("X8");
		}
	}
	// Token: 0x02000009 RID: 9
	internal static class PinCalc
	{
		// Token: 0x06000018 RID: 24 RVA: 0x00002E80 File Offset: 0x00001080
		private static byte[] B4(long v)
		{
			return new byte[]
			{
				(byte)(v >> 24 & 255L),
				(byte)(v >> 16 & 255L),
				(byte)(v >> 8 & 255L),
				(byte)(v & 255L)
			};
		}

		// Token: 0x06000019 RID: 25 RVA: 0x00002ED4 File Offset: 0x000010D4
		public static List<string> Find(List<long[]> pairs)
		{
			List<string> list = new List<string>();
			List<string> result;
			if (pairs.Count == 0)
			{
				result = list;
			}
			else
			{
				byte[] chg = PinCalc.B4(pairs[0][0]);
				string b = ((uint)pairs[0][1]).ToString("X8");
				for (int i = 0; i <= 65535; i++)
				{
					string text = i.ToString("X4");
					if (!(Sk.Key(text, chg) != b))
					{
						bool flag = true;
						for (int j = 1; j < pairs.Count; j++)
						{
							if (Sk.Key(text, PinCalc.B4(pairs[j][0])) != ((uint)pairs[j][1]).ToString("X8"))
							{
								flag = false;
								break;
							}
						}
						if (flag)
						{
							list.Add(text);
						}
					}
				}
				result = list;
			}
			return result;
		}
	}
	// Token: 0x0200000A RID: 10
	internal static class Hex
	{
		// Token: 0x0600001A RID: 26 RVA: 0x00002FF0 File Offset: 0x000011F0
		public static byte[] ToBytes(string h)
		{
			h = (h ?? "").Trim().Replace(" ", "");
			byte[] array = new byte[h.Length / 2];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = Convert.ToByte(h.Substring(i * 2, 2), 16);
			}
			return array;
		}

		// Token: 0x0600001B RID: 27 RVA: 0x00003058 File Offset: 0x00001258
		public static string ToStr(byte[] b, int len)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < len; i++)
			{
				stringBuilder.Append(b[i].ToString("X2"));
			}
			return stringBuilder.ToString();
		}

		// Token: 0x0600001C RID: 28 RVA: 0x000030A0 File Offset: 0x000012A0
		public static long U32(byte[] b, int off)
		{
			return (long)(((ulong)b[off] << 24 | (ulong)b[off + 1] << 16 | (ulong)b[off + 2] << 8 | (ulong)b[off + 3]) & 0xFFFFFFFFUL);
		}
	}
	// Token: 0x0200000B RID: 11
	internal interface ITransport
	{
		// Token: 0x17000002 RID: 2
		// (set) Token: 0x0600001D RID: 29
		Action<string> Log { set; }

		// Token: 0x17000003 RID: 3
		// (get) Token: 0x0600001E RID: 30
		bool IsOpen { get; }

		// Token: 0x0600001F RID: 31
		string Open(string portOrNull);

		// Token: 0x06000020 RID: 32
		void Close();

		// Token: 0x06000021 RID: 33
		void SelectEcu(string tx, string rx);

		// Token: 0x06000022 RID: 34
		string Send(string cmdHex, string[] tokens, int maxMs);
	}
	// Token: 0x0200000C RID: 12
	internal interface ISniffer
	{
		// Token: 0x06000023 RID: 35
		string SniffStart(int idA, int idB);

		// Token: 0x06000024 RID: 36
		List<string> SniffCollect(int ms);

		// Token: 0x06000025 RID: 37
		void SniffStop();
	}
	// Token: 0x0200000D RID: 13
	internal class ArduinoTransport : ITransport, ISniffer
	{
		// Token: 0x17000004 RID: 4
		// (get) Token: 0x06000026 RID: 38 RVA: 0x000030D8 File Offset: 0x000012D8
		// (set) Token: 0x06000027 RID: 39 RVA: 0x000030EF File Offset: 0x000012EF
		public Action<string> Log { private get; set; }

		// Token: 0x17000005 RID: 5
		// (get) Token: 0x06000028 RID: 40 RVA: 0x000030F8 File Offset: 0x000012F8
		public bool IsOpen
		{
			get
			{
				return this.sp != null && this.sp.IsOpen;
			}
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00003124 File Offset: 0x00001324
		public string Open(string port)
		{
			string result;
			try
			{
				this.sp = new SerialPort(port, 115200);
				this.sp.ReadTimeout = 500;
				this.sp.WriteTimeout = 500;
				this.sp.Open();
				Thread.Sleep(2000);
				try
				{
					this.sp.ReadExisting();
				}
				catch
				{
				}
				result = "";
			}
			catch (Exception ex)
			{
				result = ex.Message;
			}
			return result;
		}

		// Token: 0x0600002A RID: 42 RVA: 0x000031C4 File Offset: 0x000013C4
		public void Close()
		{
			try
			{
				if (this.sp != null)
				{
					this.sp.Close();
				}
			}
			catch
			{
			}
		}

		// Token: 0x0600002B RID: 43 RVA: 0x00003208 File Offset: 0x00001408
		public void SelectEcu(string tx, string rx)
		{
			this.Send(">" + tx + ":" + rx, null, 300);
		}

		// Token: 0x0600002C RID: 44 RVA: 0x0000322C File Offset: 0x0000142C
		public string Send(string cmd, string[] tokens, int maxMs)
		{
			this.sp.Write(cmd + "\n");
			Stopwatch stopwatch = Stopwatch.StartNew();
			StringBuilder stringBuilder = new StringBuilder();
			while (stopwatch.ElapsedMilliseconds < (long)maxMs)
			{
				try
				{
					stringBuilder.Append(this.sp.ReadExisting());
				}
				catch
				{
				}
				if (tokens != null)
				{
					string text = stringBuilder.ToString().Replace(" ", "").Replace("\r", "").Replace("\n", "");
					bool flag = false;
					foreach (string value in tokens)
					{
						if (text.Contains(value))
						{
							flag = true;
							break;
						}
					}
					if (flag)
					{
						break;
					}
				}
				Thread.Sleep((tokens != null) ? 8 : 15);
			}
			string text2 = stringBuilder.ToString().Trim().Replace("\r", " ").Replace("\n", " ");
			while (text2.Contains("  "))
			{
				text2 = text2.Replace("  ", " ");
			}
			if (this.Log != null)
			{
				this.Log(cmd.PadRight(16) + " -> " + text2);
			}
			return text2.Replace(" ", "");
		}

		// Token: 0x0600002D RID: 45 RVA: 0x000033D4 File Offset: 0x000015D4
		public string SniffStart(int idA, int idB)
		{
			return this.Send("P" + idA.ToString("X3") + ":" + idB.ToString("X3"), new string[]
			{
				"SNIFFOK"
			}, 2000);
		}

		// Token: 0x0600002E RID: 46 RVA: 0x00003428 File Offset: 0x00001628
		public void SniffStop()
		{
			try
			{
				this.sp.Write("R\n");
				Thread.Sleep(150);
				this.sp.ReadExisting();
			}
			catch
			{
			}
		}

		// Token: 0x0600002F RID: 47 RVA: 0x00003478 File Offset: 0x00001678
		public List<string> SniffCollect(int ms)
		{
			List<string> list = new List<string>();
			Stopwatch stopwatch = Stopwatch.StartNew();
			StringBuilder stringBuilder = new StringBuilder();
			while (stopwatch.ElapsedMilliseconds < (long)ms)
			{
				try
				{
					stringBuilder.Append(this.sp.ReadExisting());
				}
				catch
				{
				}
				string text;
				while ((text = stringBuilder.ToString()).IndexOf('\n') >= 0)
				{
					int num = text.IndexOf('\n');
					string text2 = text.Substring(0, num).Trim();
					stringBuilder.Remove(0, num + 1);
					if (text2.StartsWith("P:"))
					{
						list.Add(text2);
						if (this.Log != null)
						{
							this.Log(text2);
						}
					}
				}
				Thread.Sleep(8);
			}
			return list;
		}

		// Token: 0x04000022 RID: 34
		private SerialPort sp;
	}
	// Token: 0x0200000E RID: 14
	internal class Elm327Transport : ITransport, ISniffer
	{
		// Token: 0x17000006 RID: 6
		// (get) Token: 0x06000031 RID: 49 RVA: 0x00003578 File Offset: 0x00001778
		// (set) Token: 0x06000032 RID: 50 RVA: 0x0000358F File Offset: 0x0000178F
		public Action<string> Log { private get; set; }

		// Token: 0x17000007 RID: 7
		// (get) Token: 0x06000033 RID: 51 RVA: 0x00003598 File Offset: 0x00001798
		public bool IsOpen
		{
			get
			{
				return this.sp != null && this.sp.IsOpen;
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x000035D4 File Offset: 0x000017D4
		public string Open(string port)
		{
			foreach (int num in new int[]
			{
				115200,
				38400,
				9600
			})
			{
				try
				{
					if (this.sp != null)
					{
						try
						{
							this.sp.Close();
						}
						catch
						{
						}
					}
					this.sp = new SerialPort(port, num)
					{
						ReadTimeout = 500,
						WriteTimeout = 500
					};
					this.sp.Open();
					Thread.Sleep(200);
					this.Cmd("ATZ", 2000);
					Thread.Sleep(200);
					string text = this.Cmd("ATI", 1000) + " " + this.Cmd("AT@1", 600);
					if (text.ToUpper().Contains("ELM") || text.ToUpper().Contains("OBD") || text.ToUpper().Contains("V1.") || text.ToUpper().Contains("V2."))
					{
						this.InitDiag();
						if (this.Log != null)
						{
							this.Log(string.Concat(new object[]
							{
								"ELM327 @ ",
								num,
								" baud: ",
								text.Trim()
							}));
						}
						return "";
					}
				}
				catch (Exception ex)
				{
					if (this.Log != null)
					{
						this.Log(string.Concat(new object[]
						{
							"ELM open ",
							num,
							": ",
							ex.Message
						}));
					}
				}
			}
			try
			{
				if (this.sp != null)
				{
					this.sp.Close();
				}
			}
			catch
			{
			}
			this.sp = null;
			return "ELM327 did not respond (ATI). Check the cable and its baud rate.";
		}

		// Token: 0x06000035 RID: 53 RVA: 0x00003860 File Offset: 0x00001A60
		private void InitDiag()
		{
			this.sniffing = false;
			foreach (string cmd in new string[]
			{
				"ATE0",
				"ATL0",
				"ATS0",
				"ATH0",
				"ATSP6",
				"ATAT1",
				"ATAL",
				"ATCAF1",
				"ATST96"
			})
			{
				this.Cmd(cmd, 800);
			}
		}

		// Token: 0x06000036 RID: 54 RVA: 0x000038F0 File Offset: 0x00001AF0
		public void Close()
		{
			try
			{
				if (this.sp != null)
				{
					this.sp.Close();
				}
			}
			catch
			{
			}
		}

		// Token: 0x06000037 RID: 55 RVA: 0x00003934 File Offset: 0x00001B34
		private static string Pad3(string id)
		{
			id = (id ?? "").Trim().ToUpper();
			return (id.Length < 3) ? id.PadLeft(3, '0') : id;
		}

		// Token: 0x06000038 RID: 56 RVA: 0x00003974 File Offset: 0x00001B74
		public void SelectEcu(string tx, string rx)
		{
			if (this.sniffing)
			{
				this.InitDiag();
			}
			this.Cmd("ATSH" + Elm327Transport.Pad3(tx), 500);
			this.Cmd("ATCRA" + Elm327Transport.Pad3(rx), 500);
			this.Cmd("ATFCSH" + Elm327Transport.Pad3(tx), 500);
			this.Cmd("ATFCSD300000", 500);
			this.Cmd("ATFCSM1", 500);
		}

		// Token: 0x06000039 RID: 57 RVA: 0x00003A0C File Offset: 0x00001C0C
		private string Cmd(string cmd, int ms)
		{
			string result;
			if (this.sp == null)
			{
				result = "";
			}
			else
			{
				try
				{
					this.sp.DiscardInBuffer();
				}
				catch
				{
				}
				this.sp.Write(cmd + "\r");
				Stopwatch stopwatch = Stopwatch.StartNew();
				StringBuilder stringBuilder = new StringBuilder();
				while (stopwatch.ElapsedMilliseconds < (long)ms)
				{
					try
					{
						stringBuilder.Append(this.sp.ReadExisting());
					}
					catch
					{
					}
					if (stringBuilder.ToString().Contains(">"))
					{
						break;
					}
					Thread.Sleep(6);
				}
				result = stringBuilder.ToString().Replace("\r", " ").Replace("\n", " ").Replace(">", "").Trim();
			}
			return result;
		}

		// Token: 0x0600003A RID: 58 RVA: 0x00003B14 File Offset: 0x00001D14
		public string Send(string cmdHex, string[] tokens, int maxMs)
		{
			string result;
			if (this.sp == null)
			{
				result = "";
			}
			else
			{
				try
				{
					this.sp.DiscardInBuffer();
				}
				catch
				{
				}
				this.sp.Write(cmdHex + "\r");
				Stopwatch stopwatch = Stopwatch.StartNew();
				StringBuilder stringBuilder = new StringBuilder();
				while (stopwatch.ElapsedMilliseconds < (long)maxMs)
				{
					try
					{
						stringBuilder.Append(this.sp.ReadExisting());
					}
					catch
					{
					}
					if (stringBuilder.ToString().Contains(">"))
					{
						break;
					}
					Thread.Sleep(6);
				}
				string text = Elm327Transport.ParseData(stringBuilder.ToString());
				if (this.Log != null)
				{
					this.Log(cmdHex.PadRight(16) + " -> " + text);
				}
				result = text;
			}
			return result;
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00003C28 File Offset: 0x00001E28
		private static string ParseData(string s)
		{
			s = (s ?? "").Replace(">", "").Replace("\n", "\r");
			StringBuilder stringBuilder = new StringBuilder();
			foreach (string text in s.Split(new char[]
			{
				'\r'
			}))
			{
				string text2 = text.Trim().ToUpper();
				if (text2.Length != 0)
				{
					if (!text2.Contains("SEARCHING") && !text2.Contains("BUS") && !text2.Contains("STOPPED") && !(text2 == "OK") && !text2.Contains("NO DATA") && !text2.Contains("UNABLE") && !text2.Contains("ERROR") && !text2.Contains("?"))
					{
						text2 = Regex.Replace(text2, "^[0-9A-F]:\\s*", "");
						text2 = text2.Replace(" ", "");
						if (text2.Length > 3)
						{
							if (Regex.IsMatch(text2, "^[0-9A-F]+$"))
							{
								stringBuilder.Append(text2);
							}
						}
					}
				}
			}
			return stringBuilder.ToString();
		}

		// Token: 0x0600003C RID: 60 RVA: 0x00003D98 File Offset: 0x00001F98
		public string SniffStart(int idA, int idB)
		{
			this.sniffing = true;
			foreach (string cmd in new string[]
			{
				"ATE0",
				"ATL0",
				"ATS0",
				"ATSP6",
				"ATH1",
				"ATD1",
				"ATAL",
				"ATCAF0"
			})
			{
				this.Cmd(cmd, 700);
			}
			int num = idA ^ idB;
			int num2 = ~num & 2047;
			this.Cmd("ATCF" + (idA & num2).ToString("X3"), 500);
			this.Cmd("ATCM" + num2.ToString("X3"), 500);
			try
			{
				this.sp.DiscardInBuffer();
				this.sp.Write("ATMA\r");
			}
			catch
			{
			}
			return "SNIFFOK";
		}

		// Token: 0x0600003D RID: 61 RVA: 0x00003EC8 File Offset: 0x000020C8
		public void SniffStop()
		{
			try
			{
				this.sp.Write("\r");
				Thread.Sleep(80);
				this.sp.DiscardInBuffer();
			}
			catch
			{
			}
			this.InitDiag();
		}

		// Token: 0x0600003E RID: 62 RVA: 0x00003F1C File Offset: 0x0000211C
		public List<string> SniffCollect(int ms)
		{
			List<string> list = new List<string>();
			Stopwatch stopwatch = Stopwatch.StartNew();
			StringBuilder stringBuilder = new StringBuilder();
			while (stopwatch.ElapsedMilliseconds < (long)ms)
			{
				try
				{
					stringBuilder.Append(this.sp.ReadExisting());
				}
				catch
				{
				}
				string text;
				while ((text = stringBuilder.ToString()).IndexOf('\r') >= 0)
				{
					int num = text.IndexOf('\r');
					string text2 = text.Substring(0, num).Trim();
					stringBuilder.Remove(0, num + 1);
					text2 = text2.Replace(">", "").Trim().ToUpper();
					if (text2.Length != 0)
					{
						string[] array = text2.Split(new char[]
						{
							' '
						}, StringSplitOptions.RemoveEmptyEntries);
						if (array.Length >= 2)
						{
							if (Regex.IsMatch(array[0], "^[0-9A-F]{3}$"))
							{
								string text3 = array[0];
								StringBuilder stringBuilder2 = new StringBuilder();
								int num2 = 1;
								if (array.Length >= 2 && array[1].Length == 1)
								{
									num2 = 2;
								}
								for (int i = num2; i < array.Length; i++)
								{
									if (Regex.IsMatch(array[i], "^[0-9A-F]{2}$"))
									{
										stringBuilder2.Append(array[i]);
									}
								}
								if (stringBuilder2.Length > 0)
								{
									string text4 = string.Concat(new object[]
									{
										"P:",
										text3,
										":",
										stringBuilder2
									});
									list.Add(text4);
									if (this.Log != null)
									{
										this.Log(text4);
									}
								}
							}
						}
					}
				}
				Thread.Sleep(8);
			}
			return list;
		}

		// Token: 0x04000024 RID: 36
		private SerialPort sp;

		// Token: 0x04000025 RID: 37
		private bool sniffing = false;
	}
	// Token: 0x0200000F RID: 15
	internal class VciTransport : ITransport
	{
		// Token: 0x06000040 RID: 64
		[DllImport("C:\\AWRoot\\drv\\VCIAccess.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_openSession")]
		private static extern int OpenSession();

		// Token: 0x06000041 RID: 65
		[DllImport("C:\\AWRoot\\drv\\VCIAccess.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_closeSession")]
		private static extern int CloseSession();

		// Token: 0x06000042 RID: 66
		[DllImport("C:\\AWRoot\\drv\\VCIAccess.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_changeComLine")]
		private static extern int ChangeComLine(int line);

		// Token: 0x06000043 RID: 67
		[DllImport("C:\\AWRoot\\drv\\VCIAccess.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_bindProtocol")]
		private static extern int BindProtocol(byte[] desc, int len);

		// Token: 0x06000044 RID: 68
		[DllImport("C:\\AWRoot\\drv\\VCIAccess.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "_writeAndRead")]
		private static extern int WriteAndRead(byte[] ecuDesc, int ecuLen, byte[] inBuf, int inLen, byte[] outBuf, int outLen, int timeout);

		// Token: 0x06000045 RID: 69
		[DllImport("kernel32.dll", CharSet = CharSet.Auto)]
		private static extern bool SetDllDirectory(string path);

		// Token: 0x17000008 RID: 8
		// (get) Token: 0x06000046 RID: 70 RVA: 0x00004144 File Offset: 0x00002344
		// (set) Token: 0x06000047 RID: 71 RVA: 0x0000415B File Offset: 0x0000235B
		public Action<string> Log { private get; set; }

		// Token: 0x06000048 RID: 72 RVA: 0x00004164 File Offset: 0x00002364
		private static string Status(int c)
		{
			string result;
			switch (c)
			{
			case -12:
				result = "VCI BUSY";
				break;
			case -11:
				result = "TIMEOUT";
				break;
			default:
				switch (c)
				{
				case -5:
					return "NO ECU RESPONSE";
				case -4:
					return "CABLE UNPLUGGED";
				case 0:
				case 1:
					return "OK";
				}
				result = "ERR " + c;
				break;
			}
			return result;
		}

		// Token: 0x17000009 RID: 9
		// (get) Token: 0x06000049 RID: 73 RVA: 0x000041E8 File Offset: 0x000023E8
		public bool IsOpen
		{
			get
			{
				return this.connected;
			}
		}

		// Token: 0x0600004A RID: 74 RVA: 0x00004200 File Offset: 0x00002400
		public string Open(string ignored)
		{
			try
			{
				VciTransport.SetDllDirectory("C:\\AWRoot\\drv");
			}
			catch
			{
			}
			string currentDirectory = Directory.GetCurrentDirectory();
			string result;
			try
			{
				Directory.SetCurrentDirectory("C:\\");
				int num;
				try
				{
					num = VciTransport.OpenSession();
				}
				catch (DllNotFoundException)
				{
					return "VCIAccess.dll ei l\u00f6ytynyt (C:\\AWRoot\\drv).";
				}
				catch (Exception ex)
				{
					return "VCI-lataus ep\u00e4onnistui: " + ex.Message;
				}
				if (this.Log != null)
				{
					this.Log("VCI _openSession -> " + num);
				}
				this.connected = (num == 0 || num == 1);
				result = (this.connected ? "" : ("VCI open: " + VciTransport.Status(num)));
			}
			finally
			{
				try
				{
					Directory.SetCurrentDirectory(currentDirectory);
				}
				catch
				{
				}
			}
			return result;
		}

		// Token: 0x0600004B RID: 75 RVA: 0x00004318 File Offset: 0x00002518
		public void Close()
		{
			if (this.connected)
			{
				string currentDirectory = Directory.GetCurrentDirectory();
				try
				{
					Directory.SetCurrentDirectory("C:\\");
					try
					{
						VciTransport.CloseSession();
					}
					catch
					{
					}
				}
				finally
				{
					try
					{
						Directory.SetCurrentDirectory(currentDirectory);
					}
					catch
					{
					}
				}
				this.connected = false;
			}
		}

		// Token: 0x0600004C RID: 76 RVA: 0x0000439C File Offset: 0x0000259C
		public void SelectEcu(string tx, string rx)
		{
			int c = VciTransport.ChangeComLine(17);
			if (this.Log != null)
			{
				this.Log("VCI ChangeComLine(17) -> " + VciTransport.Status(c));
			}
			int c2 = VciTransport.BindProtocol(VciTransport.PD_DIAG_ON_CAN, VciTransport.PD_DIAG_ON_CAN.Length);
			if (this.Log != null)
			{
				this.Log("VCI BindProtocol(DIAGONCAN) -> " + VciTransport.Status(c2));
			}
			string str = (tx.Length == 3) ? ("0" + tx) : tx;
			string str2 = (rx.Length == 3) ? ("0" + rx) : rx;
			this.desc = Hex.ToBytes(str2 + str);
		}

		// Token: 0x0600004D RID: 77 RVA: 0x00004460 File Offset: 0x00002660
		public string Send(string cmd, string[] tokens, int maxMs)
		{
			string result;
			if (this.desc == null)
			{
				result = "";
			}
			else
			{
				byte[] array = Hex.ToBytes(cmd);
				byte[] array2 = new byte[2048];
				int num = VciTransport.WriteAndRead(this.desc, this.desc.Length, array, array.Length, array2, array2.Length, maxMs);
				string text = (num > 0) ? Hex.ToStr(array2, num) : "";
				if (this.Log != null)
				{
					this.Log(cmd.PadRight(16) + " -> " + ((num > 0) ? text : ("[" + VciTransport.Status(num) + "]")));
				}
				result = text;
			}
			return result;
		}

		// Token: 0x04000027 RID: 39
		private const string DLL = "C:\\AWRoot\\drv\\VCIAccess.dll";

		// Token: 0x04000028 RID: 40
		private const int LINE_CAN_DIAG = 17;

		// Token: 0x04000029 RID: 41
		private bool connected;

		// Token: 0x0400002A RID: 42
		private byte[] desc;

		// Token: 0x0400002B RID: 43
		private static readonly byte[] PD_DIAG_ON_CAN = new byte[]
		{
			3,
			16,
			232
		};
	}
	// Token: 0x02000010 RID: 16
	internal class DemoTransport : ITransport
	{
		// Token: 0x1700000A RID: 10
		// (get) Token: 0x06000050 RID: 80 RVA: 0x00004544 File Offset: 0x00002744
		// (set) Token: 0x06000051 RID: 81 RVA: 0x0000455B File Offset: 0x0000275B
		public Action<string> Log { private get; set; }

		// Token: 0x1700000B RID: 11
		// (get) Token: 0x06000052 RID: 82 RVA: 0x00004564 File Offset: 0x00002764
		public bool IsOpen
		{
			get
			{
				return this.open;
			}
		}

		// Token: 0x06000053 RID: 83 RVA: 0x0000457C File Offset: 0x0000277C
		public string Open(string p)
		{
			this.open = true;
			return "";
		}

		// Token: 0x06000054 RID: 84 RVA: 0x0000459A File Offset: 0x0000279A
		public void Close()
		{
			this.open = false;
		}

		// Token: 0x06000055 RID: 85 RVA: 0x000045A4 File Offset: 0x000027A4
		public void SelectEcu(string tx, string rx)
		{
		}

		// Token: 0x06000056 RID: 86 RVA: 0x000045A8 File Offset: 0x000027A8
		public string Send(string cmd, string[] tokens, int maxMs)
		{
			cmd = (cmd ?? "").ToUpper();
			string result;
			if (cmd.StartsWith("1003"))
			{
				result = "5003";
			}
			else if (cmd.StartsWith("1001"))
			{
				result = "5001";
			}
			else if (cmd.StartsWith("3E"))
			{
				result = "7E00";
			}
			else if (cmd.StartsWith("2703"))
			{
				result = "670300000000";
			}
			else if (cmd.StartsWith("2704"))
			{
				result = "6704";
			}
			else if (cmd.StartsWith("2E") && cmd.Length >= 6)
			{
				result = "6E" + cmd.Substring(2, 4);
			}
			else if (cmd.StartsWith("21") && cmd.Length >= 4)
            {
                result = "61" + cmd.Substring(2, 2) + new string('0', 160);
            }
            else if (cmd.StartsWith("22") && cmd.Length >= 6)
			{
				result = "62" + cmd.Substring(2, 4) + new string('0', 160);
			}
			else if (cmd.StartsWith("19"))
			{
				result = "5902FF";
			}
			else if (cmd.StartsWith("14"))
			{
				result = "54";
			}
			else if (cmd.StartsWith("10C0"))
				{
					result = "50C0";
				}
				else if (cmd.StartsWith("1081"))
				{
					result = "5081";
				}
				else if (cmd.StartsWith("17"))
				{
					result = "5700";
				}
				else if (cmd.StartsWith("21") && cmd.Length >= 4)
				{
					result = "61" + cmd.Substring(2, 2) + new string('0', 512);
				}
				else if (cmd.StartsWith("3B") && cmd.Length >= 4)
				{
					result = "7B" + cmd.Substring(2, 2);
				}
				else
				{
					result = "";
				}
			return result;
		}

		// Token: 0x0400002D RID: 45
		private bool open = false;
	}
	// Token: 0x02000011 RID: 17
	internal static class Flags
	{
		// Token: 0x06000058 RID: 88 RVA: 0x00004718 File Offset: 0x00002918
		public static Bitmap Fi()
		{
			Bitmap bitmap = new Bitmap(28, 18);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.Clear(Color.White);
				using (SolidBrush solidBrush = new SolidBrush(Color.FromArgb(0, 53, 128)))
				{
					graphics.FillRectangle(solidBrush, 8, 0, 5, 18);
					graphics.FillRectangle(solidBrush, 0, 7, 28, 5);
				}
				graphics.DrawRectangle(Pens.Silver, 0, 0, 27, 17);
			}
			return bitmap;
		}

		// Token: 0x06000059 RID: 89 RVA: 0x000047D4 File Offset: 0x000029D4
		public static Bitmap Gb()
		{
			Bitmap bitmap = new Bitmap(28, 18);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.SmoothingMode = SmoothingMode.AntiAlias;
				graphics.Clear(Color.FromArgb(0, 33, 113));
				Pen pen = new Pen(Color.White, 5f);
				Pen pen2 = new Pen(Color.FromArgb(200, 16, 46), 2f);
				graphics.DrawLine(pen, 0, 0, 28, 18);
				graphics.DrawLine(pen, 28, 0, 0, 18);
				graphics.DrawLine(pen2, 0, 0, 28, 18);
				graphics.DrawLine(pen2, 28, 0, 0, 18);
				graphics.FillRectangle(Brushes.White, 10, 0, 8, 18);
				graphics.FillRectangle(Brushes.White, 0, 5, 28, 8);
				using (SolidBrush solidBrush = new SolidBrush(Color.FromArgb(200, 16, 46)))
				{
					graphics.FillRectangle(solidBrush, 12, 0, 4, 18);
					graphics.FillRectangle(solidBrush, 0, 7, 28, 4);
				}
				pen.Dispose();
				pen2.Dispose();
				graphics.DrawRectangle(Pens.Silver, 0, 0, 27, 17);
			}
			return bitmap;
		}

		// Token: 0x0600005A RID: 90 RVA: 0x00004950 File Offset: 0x00002B50
		public static Bitmap Dim(Bitmap src)
		{
			Bitmap bitmap = new Bitmap(src.Width, src.Height);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				ColorMatrix colorMatrix = new ColorMatrix();
				colorMatrix.Matrix33 = 0.3f;
				ImageAttributes imageAttributes = new ImageAttributes();
				imageAttributes.SetColorMatrix(colorMatrix);
				graphics.DrawImage(src, new Rectangle(0, 0, src.Width, src.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, imageAttributes);
			}
			return bitmap;
		}
	}
	// Token: 0x02000012 RID: 18
	internal class ZoneView
	{
		// Token: 0x0400002F RID: 47
		public Zone Zone;

		// Token: 0x04000030 RID: 48
		public byte[] Bytes;

		// Token: 0x04000031 RID: 49
		public string OrigHex;

		// Token: 0x04000032 RID: 50
		public List<Field> Fs = new List<Field>();

		// Token: 0x04000033 RID: 51
		public List<Control> Cs = new List<Control>();

		// Token: 0x04000034 RID: 52
		public TextBox RawBox;
	}
	// Token: 0x02000013 RID: 19
	internal static class Theme
	{
		// Token: 0x0600005C RID: 92 RVA: 0x00004A10 File Offset: 0x00002C10
		public static Font Mono(float s)
		{
			return new Font("Consolas", s);
		}

		// Token: 0x0600005D RID: 93 RVA: 0x00004A30 File Offset: 0x00002C30
		public static void Btn(Button b, Color accent, Color fore)
		{
			b.FlatStyle = FlatStyle.Flat;
			b.BackColor = accent;
			b.ForeColor = fore;
			b.FlatAppearance.BorderSize = ((accent == Theme.Card) ? 1 : 0);
			b.FlatAppearance.BorderColor = Theme.Border;
			b.FlatAppearance.MouseOverBackColor = ControlPaint.Light(accent, 0.15f);
			b.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(accent, 0.05f);
			b.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
			b.Cursor = Cursors.Hand;
			Theme.Round(b, 8);
		}

		// Token: 0x0600005E RID: 94 RVA: 0x00004ADE File Offset: 0x00002CDE
		public static void Round(Control c, int r)
		{
			c.Resize -= Theme.RoundOnResize;
			c.Resize += Theme.RoundOnResize;
			c.Tag = r;
			Theme.SetRound(c, r);
		}

		// Token: 0x0600005F RID: 95 RVA: 0x00004B1C File Offset: 0x00002D1C
		private static void RoundOnResize(object s, EventArgs e)
		{
			Control control = (Control)s;
			if (control.Tag is int)
			{
				Theme.SetRound(control, (int)control.Tag);
			}
		}

		// Token: 0x06000060 RID: 96 RVA: 0x00004B58 File Offset: 0x00002D58
		private static void SetRound(Control c, int r)
		{
			if (c.Width > r && c.Height > r)
			{
				GraphicsPath graphicsPath = new GraphicsPath();
				int width = c.Width;
				int height = c.Height;
				graphicsPath.AddArc(0, 0, r, r, 180f, 90f);
				graphicsPath.AddArc(width - r - 1, 0, r, r, 270f, 90f);
				graphicsPath.AddArc(width - r - 1, height - r - 1, r, r, 0f, 90f);
				graphicsPath.AddArc(0, height - r - 1, r, r, 90f, 90f);
				graphicsPath.CloseAllFigures();
				c.Region = new Region(graphicsPath);
			}
		}

		// Token: 0x04000035 RID: 53
		public static readonly Color Bg = Color.FromArgb(24, 34, 54);

		// Token: 0x04000036 RID: 54
		public static readonly Color Panel = Color.FromArgb(31, 43, 64);

		// Token: 0x04000037 RID: 55
		public static readonly Color Card = Color.FromArgb(42, 56, 81);

		public static readonly Color Input = Color.FromArgb(20, 29, 48);

		// Token: 0x04000038 RID: 56
		public static readonly Color Border = Color.FromArgb(58, 75, 102);

		// Token: 0x04000039 RID: 57
		public static readonly Color Text = Color.FromArgb(230, 237, 245);

		// Token: 0x0400003A RID: 58
		public static readonly Color Sub = Color.FromArgb(159, 176, 197);

		// Token: 0x0400003B RID: 59
		public static readonly Color Blue = Color.FromArgb(37, 99, 235);

		// Token: 0x0400003C RID: 60
		public static readonly Color Cyan = Color.FromArgb(56, 189, 248);

		// Token: 0x0400003D RID: 61
		public static readonly Color Green = Color.FromArgb(34, 197, 94);

		// Token: 0x0400003E RID: 62
		public static readonly Color Amber = Color.FromArgb(245, 158, 11);

		// Token: 0x0400003F RID: 63
		public static readonly Color Red = Color.FromArgb(239, 68, 68);
	}
	// Token: 0x02000014 RID: 20
	internal class DarkMenu : ProfessionalColorTable
	{
		// Token: 0x1700000C RID: 12
		// (get) Token: 0x06000062 RID: 98 RVA: 0x00004CF8 File Offset: 0x00002EF8
		public override Color MenuItemSelected
		{
			get
			{
				return Theme.Card;
			}
		}

		// Token: 0x1700000D RID: 13
		// (get) Token: 0x06000063 RID: 99 RVA: 0x00004D10 File Offset: 0x00002F10
		public override Color MenuItemBorder
		{
			get
			{
				return Theme.Border;
			}
		}

		// Token: 0x1700000E RID: 14
		// (get) Token: 0x06000064 RID: 100 RVA: 0x00004D28 File Offset: 0x00002F28
		public override Color MenuBorder
		{
			get
			{
				return Theme.Border;
			}
		}

		// Token: 0x1700000F RID: 15
		// (get) Token: 0x06000065 RID: 101 RVA: 0x00004D40 File Offset: 0x00002F40
		public override Color ToolStripDropDownBackground
		{
			get
			{
				return Theme.Panel;
			}
		}

		// Token: 0x17000010 RID: 16
		// (get) Token: 0x06000066 RID: 102 RVA: 0x00004D58 File Offset: 0x00002F58
		public override Color ImageMarginGradientBegin
		{
			get
			{
				return Theme.Panel;
			}
		}

		// Token: 0x17000011 RID: 17
		// (get) Token: 0x06000067 RID: 103 RVA: 0x00004D70 File Offset: 0x00002F70
		public override Color ImageMarginGradientMiddle
		{
			get
			{
				return Theme.Panel;
			}
		}

		// Token: 0x17000012 RID: 18
		// (get) Token: 0x06000068 RID: 104 RVA: 0x00004D88 File Offset: 0x00002F88
		public override Color ImageMarginGradientEnd
		{
			get
			{
				return Theme.Panel;
			}
		}

		// Token: 0x17000013 RID: 19
		// (get) Token: 0x06000069 RID: 105 RVA: 0x00004DA0 File Offset: 0x00002FA0
		public override Color MenuItemSelectedGradientBegin
		{
			get
			{
				return Theme.Card;
			}
		}

		// Token: 0x17000014 RID: 20
		// (get) Token: 0x0600006A RID: 106 RVA: 0x00004DB8 File Offset: 0x00002FB8
		public override Color MenuItemSelectedGradientEnd
		{
			get
			{
				return Theme.Card;
			}
		}

		// Token: 0x17000015 RID: 21
		// (get) Token: 0x0600006B RID: 107 RVA: 0x00004DD0 File Offset: 0x00002FD0
		public override Color MenuItemPressedGradientBegin
		{
			get
			{
				return Theme.Panel;
			}
		}

		// Token: 0x17000016 RID: 22
		// (get) Token: 0x0600006C RID: 108 RVA: 0x00004DE8 File Offset: 0x00002FE8
		public override Color MenuItemPressedGradientEnd
		{
			get
			{
				return Theme.Panel;
			}
		}
	}
	// Token: 0x02000015 RID: 21
	internal class MainForm : Form
	{
		// Token: 0x0600006E RID: 110 RVA: 0x00004E08 File Offset: 0x00003008
		private string T(string key)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>
			{
				{
					"auto",
					"Autodetect"
				},
				{
					"conn",
					"Yhdist\u00e4"
				},
				{
					"disc",
					"Katkaise"
				},
				{
					"noconn",
					"Ei yhteytt\u00e4"
				},
				{
					"module",
					"Ohjainlaite (ECU):"
				},
				{
					"read",
					"Lue"
				},
				{
					"scan",
					"Skannaa"
				},
				{
					"write",
					"Kirjoita"
				},
				{
					"dtcr",
					"Lue DTC"
				},
				{
					"dtcc",
					"Tyhjenn\u00e4 DTC"
				},
				{
					"connto",
					"Yhdistetty "
				},
				{
					"pin",
					"Lue PIN"
				},
				{
					"tools",
					"Ty\u00f6kalut"
				},
				{
					"restore",
					"Palauta konfiguraatio backupista\u2026"
				},
				{
					"ripper",
					"Diagbox DataRipper (rippaa selitteet)"
				}
			};
			Dictionary<string, string> dictionary2 = new Dictionary<string, string>
			{
				{
					"auto",
					"Autodetect"
				},
				{
					"conn",
					"Connect"
				},
				{
					"disc",
					"Disconnect"
				},
				{
					"noconn",
					"Not connected"
				},
				{
					"module",
					"ECU:"
				},
				{
					"read",
					"Read"
				},
				{
					"scan",
					"Scan"
				},
				{
					"write",
					"Write"
				},
				{
					"dtcr",
					"Read DTC"
				},
				{
					"dtcc",
					"Clear DTC"
				},
				{
					"connto",
					"Connected "
				},
				{
					"pin",
					"Read PIN"
				},
				{
					"tools",
					"Tools"
				},
				{
					"restore",
					"Restore config from backup\u2026"
				},
				{
					"ripper",
					"Diagbox DataRipper (rip descriptions)"
				}
			};
			Dictionary<string, string> dictionary3 = (this.lang == "fi") ? dictionary : dictionary2;
			return dictionary3.ContainsKey(key) ? dictionary3[key] : key;
		}

		// Token: 0x0600006F RID: 111 RVA: 0x00005068 File Offset: 0x00003268
		private bool Vci()
		{
			return this.cbTrans.SelectedIndex == 1;
		}

		// Token: 0x06000070 RID: 112 RVA: 0x00005088 File Offset: 0x00003288
		private bool Elm()
		{
			return this.cbTrans.SelectedIndex == 2;
		}

		// Token: 0x06000071 RID: 113 RVA: 0x000050A8 File Offset: 0x000032A8
		private bool Demo()
		{
			return this.cbTrans.SelectedIndex == 3;
		}

		// Token: 0x06000072 RID: 114 RVA: 0x0000539C File Offset: 0x0000359C
		public MainForm()
		{
			this.Text = "Stellantis Config Messer";
			base.Width = 1180;
			base.Height = 760;
			base.StartPosition = FormStartPosition.CenterScreen;
			this.Font = new Font("Segoe UI", 9f);
			try
			{
				string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
				string text2 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
				if (File.Exists(text))
				{
					using (Bitmap bitmap = new Bitmap(text))
					{
						using (Bitmap bitmap2 = new Bitmap(bitmap, 32, 32))
						{
							base.Icon = Icon.FromHandle(bitmap2.GetHicon());
						}
					}
				}
				else if (File.Exists(text2))
				{
					base.Icon = new Icon(text2);
				}
			}
			catch
			{
			}
			Panel top = new Panel
			{
				Dock = DockStyle.Top,
				Height = 44
			};
			this.cbTrans = new ComboBox
			{
				Left = 10,
				Top = 10,
				Width = 130,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			this.cbTrans.Items.Add("Arduino");
			this.cbTrans.Items.Add("Diagbox VCI");
			this.cbTrans.Items.Add("ELM327");
			this.cbTrans.Items.Add("Demo");
			this.cbTrans.SelectedIndex = 0;
			this.cbTrans.SelectedIndexChanged += delegate(object s, EventArgs e)
			{
				if (this.trans != null && this.trans.IsOpen)
				{
					this.Connect();
				}
				bool visible2 = this.cbTrans.SelectedIndex == 0 || this.cbTrans.SelectedIndex == 2;
				this.cbPort.Visible = visible2;
				this.btnAuto.Visible = visible2;
				if (this.cbTrans.SelectedItem != null)
				{
					Cfg.Transport = this.cbTrans.SelectedItem.ToString();
				}
			};
			this.cbPort = new ComboBox
			{
				Left = 146,
				Top = 10,
				Width = 100,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			this.btnAuto = new Button
			{
				Left = 252,
				Top = 9,
				Width = 80
			};
			this.btnAuto.Click += delegate(object s, EventArgs e)
			{
				this.AutoDetect();
			};
			this.btnConn = new Button
			{
				Left = 338,
				Top = 9,
				Width = 100
			};
			this.btnConn.Click += delegate(object s, EventArgs e)
			{
				this.Connect();
			};
			this.lblStatus = new Label
			{
				Left = 446,
				Top = 13,
				Width = 230,
				AutoEllipsis = true
			};
			this.btnLog = new Button
			{
				Left = 682,
				Top = 9,
				Width = 150
			};
			this.btnLog.Click += delegate(object s, EventArgs e)
			{
				this.logPanel.Visible = !this.logPanel.Visible;
				this.UpdateLogBtn();
			};
			this.chkDecoded = new CheckBox
			{
				Left = 842,
				Top = 13,
				Width = 170,
				AutoSize = false,
				Checked = Cfg.OnlyDecoded,
				ForeColor = Theme.Text,
				BackColor = Theme.Panel,
				FlatStyle = FlatStyle.Flat
			};
			this.chkDecoded.CheckedChanged += delegate(object s, EventArgs e)
			{
				Cfg.OnlyDecoded = this.chkDecoded.Checked;
				Cfg.Save();
				this.Render();
			};
			this.fiOn = Flags.Fi();
			this.fiOff = Flags.Dim(this.fiOn);
			this.gbOn = Flags.Gb();
			this.gbOff = Flags.Dim(this.gbOn);
			this.flagFi = new PictureBox
			{
				Width = 28,
				Height = 18,
				Top = 12,
				Image = this.fiOn,
				Cursor = Cursors.Hand
			};
			this.flagGb = new PictureBox
			{
				Width = 28,
				Height = 18,
				Top = 12,
				Image = this.gbOn,
				Cursor = Cursors.Hand
			};
			this.flagFi.Click += delegate(object s, EventArgs e)
			{
				this.SetLang("fi");
			};
			this.flagGb.Click += delegate(object s, EventArgs e)
			{
				this.SetLang("en");
			};
			top.Controls.AddRange(new Control[]
			{
				this.cbTrans,
				this.cbPort,
				this.btnAuto,
				this.btnConn,
				this.lblStatus,
				this.btnLog,
				this.chkDecoded,
				this.flagFi,
				this.flagGb
			});
			top.Resize += delegate(object s, EventArgs e)
			{
				this.flagGb.Left = top.Width - 40;
				this.flagFi.Left = top.Width - 74;
			};
			Panel row2 = new Panel
			{
				Dock = DockStyle.Top,
				Height = 66
			};
			this.lblMod = new Label
			{
				Left = 10,
				Top = 24,
				Width = 120
			};
			this.cbModule = new ComboBox
			{
				Left = 132,
				Top = 21,
				Width = 360,
				DropDownStyle = ComboBoxStyle.DropDownList
			};
			this.cbModule.SelectedIndexChanged += delegate(object s, EventArgs e)
			{
				this.SelectModule();
			};
			this.btnRead = new Button
			{
				Left = 500,
				Top = 20,
				Width = 72,
				Height = 26
			};
			this.btnRead.Click += delegate(object s, EventArgs e)
			{
				this.DoReadAll();
			};
			this.btnScan = new Button
			{
				Left = 576,
				Top = 20,
				Width = 80,
				Height = 26
			};
			this.btnScan.Click += delegate(object s, EventArgs e)
			{
				this.DoScan();
			};
			this.btnWrite = new Button
			{
				Left = 660,
				Top = 20,
				Width = 92,
				Height = 26,
				Enabled = false
			};
			this.btnWrite.Click += delegate(object s, EventArgs e)
			{
				this.DoWrite();
			};
			this.btnDtcR = new Button
			{
				Left = 756,
				Top = 20,
				Width = 84,
				Height = 26
			};
			this.btnDtcR.Click += delegate(object s, EventArgs e)
			{
				this.DoDtc(false);
			};
			this.btnDtcC = new Button
			{
				Left = 844,
				Top = 20,
				Width = 96,
				Height = 26
			};
			this.btnDtcC.Click += delegate(object s, EventArgs e)
			{
				this.DoDtc(true);
			};
			this.btnPin = new Button
			{
				Left = 948,
				Top = 20,
				Width = 92,
				Height = 26,
				Visible = Cfg.Pin
			};
			this.btnPin.Click += delegate(object s, EventArgs e)
			{
				this.OpenPin();
			};
			row2.Controls.AddRange(new Control[]
			{
				this.lblMod,
				this.cbModule,
				this.btnRead,
				this.btnScan,
				this.btnWrite,
				this.btnDtcR,
				this.btnDtcC,
				this.btnPin
			});
			this.logo = new PictureBox
			{
				Width = 60,
				Height = 60,
				Top = 3,
				SizeMode = PictureBoxSizeMode.Zoom,
				Anchor = (AnchorStyles.Top | AnchorStyles.Right)
			};
			try
			{
				string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
				if (File.Exists(text))
				{
					this.logo.Image = Image.FromFile(text);
				}
			}
			catch
			{
			}
			row2.Controls.Add(this.logo);
			this.logo.BringToFront();
			MenuStrip menuStrip = new MenuStrip();
			this.menuTools = new ToolStripMenuItem();
			this.menuRestore = new ToolStripMenuItem();
			this.menuRestore.Click += delegate(object s, EventArgs e)
			{
				this.RestoreBackup();
			};
			this.menuRip = new ToolStripMenuItem();
			this.menuRip.Click += delegate(object s, EventArgs e)
			{
				this.DiagboxRip();
			};
			this.menuView = new ToolStripMenuItem();
			this.menuView.Click += delegate(object s, EventArgs e)
			{
				this.ViewBackup();
			};
			this.menuExport = new ToolStripMenuItem();
			this.menuExport.Click += delegate(object s, EventArgs e) { this.ExportConfig(); };
			this.menuCompare = new ToolStripMenuItem();
			this.menuCompare.Click += delegate(object s, EventArgs e) { this.CompareBackups(); };
			this.menuProfiles = new ToolStripMenuItem();
			ToolStripMenuItem pAcc = new ToolStripMenuItem("ACC + LPA");
			pAcc.Click += delegate(object s, EventArgs e) { this.ApplyProfile("ACC", "ACC + LPA"); };
			ToolStripMenuItem pPark = new ToolStripMenuItem("Park Assist");
			pPark.Click += delegate(object s, EventArgs e) { this.ApplyProfile("PARK", "Park Assist"); };
			this.menuProfiles.DropDownItems.Add(pAcc);
			this.menuProfiles.DropDownItems.Add(pPark);
			this.menuTools.DropDownItems.Add(this.menuView);
			this.menuTools.DropDownItems.Add(this.menuExport);
			this.menuTools.DropDownItems.Add(this.menuCompare);
			this.menuTools.DropDownItems.Add(this.menuProfiles);
			this.menuTools.DropDownItems.Add(this.menuRestore);
			this.menuTools.DropDownItems.Add(this.menuRip);
			menuStrip.Items.Add(this.menuTools);
			this.pnlFields = new FlowLayoutPanel
			{
				Dock = DockStyle.Fill,
				AutoScroll = true,
				FlowDirection = FlowDirection.TopDown,
				WrapContents = false,
				BorderStyle = BorderStyle.FixedSingle
			};
			this.logPanel = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 190,
				Padding = new Padding(1, 1, 1, 1)
			};
			this.txtLog = new TextBox
			{
				Dock = DockStyle.Fill,
				Multiline = true,
				ScrollBars = ScrollBars.Vertical,
				ReadOnly = true,
				BackColor = Color.Black,
				ForeColor = Color.LightGreen,
				Font = new Font("Consolas", 8.5f)
			};
			this.logPanel.Controls.Add(this.txtLog);
			Panel panel = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 24,
				BackColor = Color.FromArgb(245, 245, 245)
			};
			LinkLabel linkLabel = new LinkLabel
			{
				Left = 8,
				Top = 4,
				AutoSize = true,
				Text = "\u2615 Support: buymeacoffee.com/plagufin"
			};
			linkLabel.LinkClicked += delegate(object s, LinkLabelLinkClickedEventArgs e)
			{
				try
				{
					Process.Start("https://www.buymeacoffee.com/plagufin");
				}
				catch
				{
				}
			};
			LinkLabel linkLabel2 = new LinkLabel
			{
				Left = 330,
				Top = 4,
				AutoSize = true,
				Text = "github.com/plagu-fin/stellantis-config-messer"
			};
			linkLabel2.LinkClicked += delegate(object s, LinkLabelLinkClickedEventArgs e)
			{
				try
				{
					Process.Start("https://github.com/plagu-fin/stellantis-config-messer");
				}
				catch
				{
				}
			};
			panel.Controls.Add(linkLabel);
			panel.Controls.Add(linkLabel2);
			Panel fieldsHost = new Panel
			{
				Dock = DockStyle.Fill,
				Padding = new Padding(1, 1, 1, 1)
			};
			this.fieldsHost = fieldsHost;
			fieldsHost.Controls.Add(this.pnlFields);
			Panel searchPanel = new Panel { Dock = DockStyle.Top, Height = 30 };
			this.lblSearch = new Label { Left = 6, Top = 7, Width = 92, Text = "Hae / Search:", ForeColor = Theme.Sub, AutoSize = false };
			this.txtSearch = new TextBox { Left = 100, Top = 4, Width = 300, BorderStyle = BorderStyle.FixedSingle };
			this.txtSearch.TextChanged += delegate(object es, EventArgs ee) { this.Render(); };
			searchPanel.Controls.Add(this.lblSearch);
			searchPanel.Controls.Add(this.txtSearch);
			fieldsHost.Controls.Add(searchPanel);
			base.Controls.Add(fieldsHost);
			base.Controls.Add(this.logPanel);
			base.Controls.Add(panel);
			base.Controls.Add(row2);
			base.Controls.Add(top);
			base.Controls.Add(menuStrip);
			base.MainMenuStrip = menuStrip;
			Cfg.Load();
			this.lang = Cfg.Lang;
			this.btnPin.Visible = Cfg.Pin;
			if (this.chkDecoded != null)
			{
				this.chkDecoded.Checked = Cfg.OnlyDecoded;
			}
			int num = this.cbTrans.Items.IndexOf(Cfg.Transport);
			if (num >= 0)
			{
				this.cbTrans.SelectedIndex = num;
			}
			bool visible = this.cbTrans.SelectedIndex == 0 || this.cbTrans.SelectedIndex == 2;
			this.cbPort.Visible = visible;
			this.btnAuto.Visible = visible;
			this.RefreshPorts();
			if (!string.IsNullOrEmpty(Cfg.Port) && this.cbPort.Items.Contains(Cfg.Port))
			{
				this.cbPort.SelectedItem = Cfg.Port;
			}
			this.ApplyTheme(menuStrip);
			this.PopulateModules(Data.Modules);
			this.SetLang(this.lang);
			base.FormClosing += delegate(object s, FormClosingEventArgs e)
			{
				Cfg.Lang = this.lang;
				if (this.cbTrans.SelectedItem != null)
				{
					Cfg.Transport = this.cbTrans.SelectedItem.ToString();
				}
				if (this.cbPort.SelectedItem != null)
				{
					Cfg.Port = this.cbPort.SelectedItem.ToString();
				}
				Cfg.Save();
			};
			base.Shown += delegate(object s, EventArgs e)
			{
				this.flagGb.Left = top.Width - 40;
				this.flagFi.Left = top.Width - 74;
				this.logo.Left = row2.Width - 80;
			};
		}

		// Token: 0x06000073 RID: 115 RVA: 0x0000615C File Offset: 0x0000435C
		private void ApplyTheme(MenuStrip menu)
		{
			this.BackColor = Theme.Bg;
			this.ThemeWalk(this);
			this.pnlFields.BackColor = Theme.Bg;
			this.pnlFields.BorderStyle = BorderStyle.None;
			if (this.fieldsHost != null)
			{
				this.fieldsHost.BackColor = Theme.Border;
			}
			this.logPanel.BackColor = Theme.Border;
			this.logPanel.Padding = new Padding(1, 1, 1, 1);
			this.txtLog.BackColor = Theme.Input;
			this.txtLog.ForeColor = Theme.Cyan;
			this.txtLog.Font = Theme.Mono(8.5f);
			this.txtLog.BorderStyle = BorderStyle.None;
			Theme.Btn(this.btnConn, Theme.Blue, Color.White);
			Theme.Btn(this.btnAuto, Theme.Card, Theme.Text);
			Theme.Btn(this.btnLog, Theme.Card, Theme.Text);
			Theme.Btn(this.btnRead, Theme.Blue, Color.White);
			Theme.Btn(this.btnScan, Theme.Blue, Color.White);
			Theme.Btn(this.btnWrite, Theme.Amber, Color.Black);
			Theme.Btn(this.btnDtcR, Theme.Card, Theme.Text);
			Theme.Btn(this.btnDtcC, Theme.Red, Color.White);
			Theme.Btn(this.btnPin, Theme.Amber, Color.Black);
			this.cbTrans.FlatStyle = (this.cbPort.FlatStyle = (this.cbModule.FlatStyle = FlatStyle.Flat));
			this.cbTrans.BackColor = (this.cbPort.BackColor = (this.cbModule.BackColor = Theme.Input));
			this.cbTrans.ForeColor = (this.cbPort.ForeColor = (this.cbModule.ForeColor = Theme.Text));
			this.cbModule.Font = Theme.Mono(9f);
			if (menu != null)
			{
				menu.BackColor = Theme.Panel;
				menu.ForeColor = Theme.Text;
				menu.Renderer = new ToolStripProfessionalRenderer(new DarkMenu());
				foreach (object obj in menu.Items)
				{
					ToolStripItem toolStripItem = (ToolStripItem)obj;
					toolStripItem.ForeColor = Theme.Text;
					ToolStripMenuItem toolStripMenuItem = toolStripItem as ToolStripMenuItem;
					if (toolStripMenuItem != null)
					{
						foreach (object obj2 in toolStripMenuItem.DropDownItems)
						{
							ToolStripItem toolStripItem2 = (ToolStripItem)obj2;
							toolStripItem2.BackColor = Theme.Panel;
							toolStripItem2.ForeColor = Theme.Text;
						}
					}
				}
			}
		}

		// Token: 0x06000074 RID: 116 RVA: 0x00006478 File Offset: 0x00004678
		private void ThemeWalk(Control c)
		{
			foreach (object obj in c.Controls)
			{
				Control control = (Control)obj;
				if (!(control is Button) && !(control is PictureBox))
				{
					if (control is LinkLabel)
					{
						LinkLabel linkLabel = (LinkLabel)control;
						linkLabel.LinkColor = Theme.Cyan;
						linkLabel.ActiveLinkColor = Theme.Amber;
						linkLabel.ForeColor = Theme.Sub;
						linkLabel.BackColor = Color.Transparent;
					}
					else if (control is TextBox)
					{
						control.BackColor = Theme.Input;
						control.ForeColor = Theme.Text;
					}
					else if (control is ComboBox)
					{
						control.BackColor = Theme.Input;
						control.ForeColor = Theme.Text;
					}
					else if (control is ListBox)
					{
						control.BackColor = Theme.Input;
						control.ForeColor = Theme.Text;
					}
					else if (control is Label)
					{
						control.ForeColor = Theme.Text;
						control.BackColor = Color.Transparent;
					}
					else if (control is FlowLayoutPanel)
					{
						control.BackColor = Theme.Bg;
					}
					else if (control is Panel)
					{
						control.BackColor = Theme.Panel;
					}
				}
				this.ThemeWalk(control);
			}
		}

		// Token: 0x06000075 RID: 117 RVA: 0x0000665C File Offset: 0x0000485C
		private void SetLang(string l)
		{
			this.lang = l;
			this.flagFi.Image = ((l == "fi") ? this.fiOn : this.fiOff);
			this.flagGb.Image = ((l == "en") ? this.gbOn : this.gbOff);
			this.btnAuto.Text = this.T("auto");
			this.btnConn.Text = ((this.trans != null && this.trans.IsOpen) ? this.T("disc") : this.T("conn"));
			this.lblMod.Text = this.T("module");
			this.btnRead.Text = this.T("read");
			this.btnScan.Text = this.T("scan");
			this.btnWrite.Text = this.T("write");
			this.btnDtcR.Text = this.T("dtcr");
			this.btnDtcC.Text = this.T("dtcc");
			this.btnPin.Text = this.T("pin");
			if (this.menuTools != null)
			{
				this.menuTools.Text = this.T("tools");
			}
			if (this.menuRestore != null)
			{
				this.menuRestore.Text = this.T("restore");
			}
			if (this.menuRip != null)
			{
				this.menuRip.Text = this.T("ripper");
				if (this.menuView != null) { this.menuView.Text = (this.lang == "fi") ? "Varmuuskopion tarkastelu" : "View backup"; }
				if (this.menuProfiles != null) { this.menuProfiles.Text = (this.lang == "fi") ? "Valmiit profiilit (kirjoita kaikki moduulit)" : "Ready profiles (write all modules)"; }
				if (this.menuExport != null) { this.menuExport.Text = (this.lang == "fi") ? "Vie luettu konfiguraatio (.txt)" : "Export read config (.txt)"; }
				if (this.menuCompare != null) { this.menuCompare.Text = (this.lang == "fi") ? "Vertaa varmuuskopioita" : "Compare backups"; }
			}
			if ((this.trans == null || !this.trans.IsOpen) && !this.busy)
			{
				this.lblStatus.Text = this.T("noconn");
				this.lblStatus.ForeColor = Theme.Red;
			}
			this.UpdateLogBtn();
			this.Render();
		}

		// Token: 0x06000076 RID: 118 RVA: 0x0000687C File Offset: 0x00004A7C
		private void UpdateLogBtn()
		{
			if (this.btnLog != null && this.logPanel != null)
			{
				this.btnLog.Text = (this.logPanel.Visible ? ((this.lang == "fi") ? "Piilota loki" : "Hide log") : ((this.lang == "fi") ? "N\u00e4yt\u00e4 loki" : "Show log"));
			}
			if (this.chkDecoded != null)
			{
				this.chkDecoded.Text = ((this.lang == "fi") ? "Vain selke\u00e4t" : "Decoded only");
			}
		}

		// Token: 0x06000077 RID: 119 RVA: 0x00007094 File Offset: 0x00005294
		private void OpenPin()
		{
			bool fi = this.lang == "fi";
			Form form = new Form
			{
				Text = (fi ? "PIN-luku (immobilisaattori)" : "PIN read (immobiliser)"),
				Width = 560,
				Height = 470,
				StartPosition = FormStartPosition.CenterParent,
				Font = this.Font,
				BackColor = Theme.Bg,
				ForeColor = Theme.Text
			};
			form.Controls.Add(new Label
			{
				Left = 10,
				Top = 8,
				Width = 525,
				Height = 62,
				Text = (fi ? "PIN luetaan OMASTA autostasi (esim. vara-avaimen koodaukseen).\n\"Nuuski autosta\" kuuntelee CAN-v\u00e4yl\u00e4\u00e4 (0x072=haaste, 0x0A8=vaste) virta-syklin aikana ja murtaa 16-bittisen PIN:in (65536 vaihtoehtoa, hetkess\u00e4). Tai liit\u00e4 parit k\u00e4sin: haaste,vaste hexeiss\u00e4, yksi per rivi." : "The PIN is read from YOUR OWN car (e.g. to code a spare key).\n\"Sniff from car\" listens on CAN (0x072=challenge, 0x0A8=response) across an ignition cycle and cracks the 16-bit PIN (65536 options, instant). Or paste pairs manually: challenge,response in hex, one per line.")
			});
			TextBox txt = new TextBox
			{
				Left = 10,
				Top = 76,
				Width = 525,
				Height = 150,
				Multiline = true,
				Font = new Font("Consolas", 9f),
				ScrollBars = ScrollBars.Vertical,
				Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
			};
			Button btnSniff = new Button
			{
				Left = 10,
				Top = 234,
				Width = 190,
				Text = (fi ? "Nuuski autosta" : "Sniff from car")
			};
			Button btn = new Button
			{
				Left = 210,
				Top = 234,
				Width = 160,
				Text = (fi ? "Laske parit (k\u00e4sin)" : "Compute (manual)")
			};
			TextBox outp = new TextBox
			{
				Left = 10,
				Top = 272,
				Width = 525,
				Height = 150,
				Multiline = true,
				ReadOnly = true,
				Font = new Font("Consolas", 10f),
				ScrollBars = ScrollBars.Vertical,
				Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
			};
			Func<List<long[]>, string> show = delegate(List<long[]> pairs)
			{
				string result;
				if (pairs.Count == 0)
				{
					result = (fi ? "Ei kelvollisia pareja." : "No valid pairs.");
				}
				else
				{
					List<string> list = PinCalc.Find(pairs);
					if (list.Count == 0)
					{
						result = (fi ? "PIN:i\u00e4 ei l\u00f6ytynyt (v\u00e4\u00e4r\u00e4t parit tai eri ECU)." : "No PIN found (wrong pairs or different ECU).");
					}
					else if (list.Count == 1)
					{
						result = "PIN: " + list[0];
					}
					else
					{
						result = (fi ? (list.Count + " ehdokasta (lis\u00e4\u00e4 pareja): ") : (list.Count + " candidates (add pairs): ")) + string.Join(", ", list);
					}
				}
				return result;
			};
			btn.Click += delegate(object s, EventArgs e)
			{
				List<long[]> list = new List<long[]>();
				foreach (string text in txt.Text.Replace("\r", "").Split(new char[]
				{
					'\n'
				}))
				{
					string[] array2 = text.Trim().Split(new char[]
					{
						',',
						';',
						' ',
						'\t'
					}, StringSplitOptions.RemoveEmptyEntries);
					if (array2.Length >= 2)
					{
						try
						{
							list.Add(new long[]
							{
								Convert.ToInt64(array2[0].Replace("0x", ""), 16),
								Convert.ToInt64(array2[1].Replace("0x", ""), 16)
							});
						}
						catch
						{
						}
					}
				}
				outp.Text = show(list);
			};
			btnSniff.Click += delegate(object s, EventArgs e)
			{
				ISniffer sniffer = this.trans as ISniffer;
				if (sniffer == null || this.trans == null || !this.trans.IsOpen)
				{
					MessageBox.Show(fi ? "Nuuskinta vaatii Arduino- tai ELM327-yhteyden (VCI ei tue kuuntelua)." : "Sniffing needs an Arduino or ELM327 connection (VCI can't listen).");
				}
				else
				{
					string text = fi ? "Luetaan PIN OMASTA autostasi.\n\n1) Auton virta POIS.\n2) Paina OK.\n3) Kierr\u00e4 virta P\u00c4\u00c4LLE (\u00e4l\u00e4 k\u00e4ynnist\u00e4) ~12 s kuluessa.\n\nToistetaan pari sykli\u00e4 yksik\u00e4sitteisyyden vuoksi." : "Reading the PIN from YOUR OWN car.\n\n1) Ignition OFF.\n2) Click OK.\n3) Turn ignition ON (do not start) within ~12 s.\n\nDo a couple of cycles for uniqueness.";
					if (MessageBox.Show(text, "PIN", MessageBoxButtons.OKCancel) == DialogResult.OK)
					{
						btnSniff.Enabled = (btn.Enabled = false);
						
						outp.Text = (fi ? "Nuuskitaan 0x072/0x0A8 \u2014 kierr\u00e4 virta p\u00e4\u00e4lle nyt" : "Sniffing 0x072/0x0A8 \u2014 turn ignition on now");
						List<string> list = new List<string>();
						try
						{
							sniffer.SniffStart(114, 168);
							for (int i = 0; i < 12; i++)
							{
								list.AddRange(sniffer.SniffCollect(1000));
								outp = outp;
								outp.Text += ".";
								Application.DoEvents();
							}
						}
						finally
						{
							sniffer.SniffStop();
						}
						byte[] array = null;
						List<byte[][]> list2 = new List<byte[][]>();
						foreach (string text2 in list)
						{
							string[] array2 = text2.Split(new char[]
							{
								':'
							});
							if (array2.Length >= 3)
							{
								int num;
								byte[] array3;
								try
								{
									num = Convert.ToInt32(array2[1], 16);
									array3 = Hex.ToBytes(array2[2]);
								}
								catch
								{
									continue;
								}
								if (num == 114)
								{
									array = array3;
								}
								else if (num == 168 && array != null)
								{
									list2.Add(new byte[][]
									{
										array,
										array3
									});
								}
							}
						}
						btnSniff.Enabled = (btn.Enabled = true);
						if (list2.Count == 0)
						{
							outp.Text = (fi ? "Ei saatu 0x072/0x0A8 -kehyksi\u00e4.\nTarkista johdotus (CAN pinnit 6/14) tai etsi oikeat ID:t CAN-kuuntelijalla (CanKuuntelija.ino)." : "No 0x072/0x0A8 frames.\nCheck wiring (CAN pins 6/14) or find the right IDs with the CAN listener (CanKuuntelija.ino).");
						}
						else
						{
							int[] array4 = new int[2];
							array4[0] = 1;
							int[] array5 = array4;
							foreach (int num2 in array5)
							{
								foreach (int num3 in array5)
								{
									List<long[]> list3 = new List<long[]>();
									foreach (byte[][] array8 in list2)
									{
										if (array8[0].Length >= num2 + 4 && array8[1].Length >= num3 + 4)
										{
											list3.Add(new long[]
											{
												Hex.U32(array8[0], num2),
												Hex.U32(array8[1], num3)
											});
										}
									}
									if (list3.Count != 0)
									{
										List<string> list4 = PinCalc.Find(list3);
										if (list4.Count == 1)
										{
											StringBuilder stringBuilder = new StringBuilder();
											foreach (long[] array9 in list3)
											{
												stringBuilder.AppendLine(((uint)array9[0]).ToString("X8") + "," + ((uint)array9[1]).ToString("X8"));
											}
											txt.Text = stringBuilder.ToString();
											outp.Text = "PIN: " + list4[0] + (fi ? string.Concat(new object[]
											{
												"\n(",
												list3.Count,
												" paria, tavusiirto ",
												num2,
												"/",
												num3,
												")"
											}) : string.Concat(new object[]
											{
												"\n(",
												list3.Count,
												" pairs, byte offset ",
												num2,
												"/",
												num3,
												")"
											}));
											return;
										}
									}
								}
							}
							outp.Text = (fi ? "Ei yksik\u00e4sitteist\u00e4 PIN:i\u00e4. Toista sykli tai tarkista raa'at kehykset:\n" : "No unique PIN. Repeat the cycle or inspect raw frames:\n") + string.Join("\n", list.Distinct<string>().Take(24));
						}
					}
				}
			};
			form.Controls.Add(txt);
			form.Controls.Add(btnSniff);
			form.Controls.Add(btn);
			form.Controls.Add(outp);
			this.ThemeWalk(form);
			txt.Font = Theme.Mono(9.5f);
			txt.ForeColor = Theme.Cyan;
			outp.Font = Theme.Mono(10f);
			outp.ForeColor = Theme.Green;
			Theme.Btn(btnSniff, Theme.Amber, Color.Black);
			Theme.Btn(btn, Theme.Blue, Color.White);
			form.ShowDialog(this);
		}

		// Token: 0x06000078 RID: 120 RVA: 0x00007454 File Offset: 0x00005654
		private void L(string s)
		{
			if (this.txtLog.InvokeRequired)
			{
				this.txtLog.BeginInvoke(new Action(delegate()
				{
					this.L(s);
				}));
			}
			else
			{
				this.txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + s + "\r\n");
			}
		}

		// Token: 0x06000079 RID: 121 RVA: 0x000074F4 File Offset: 0x000056F4
		private void RefreshPorts()
		{
			this.cbPort.Items.Clear();
			foreach (string item in from x in SerialPort.GetPortNames()
			orderby x
			select x)
			{
				this.cbPort.Items.Add(item);
			}
			if (this.cbPort.Items.Contains("COM7"))
			{
				this.cbPort.SelectedItem = "COM7";
			}
			else if (this.cbPort.Items.Count > 0)
			{
				this.cbPort.SelectedIndex = 0;
			}
		}

		// Token: 0x0600007A RID: 122 RVA: 0x000075E4 File Offset: 0x000057E4
		private void AutoDetect()
		{
			this.RefreshPorts();
			string text = null;
			string text2 = null;
			try
			{
				using (ManagementObjectSearcher managementObjectSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'"))
				{
					foreach (ManagementBaseObject managementBaseObject in managementObjectSearcher.Get())
					{
						ManagementObject managementObject = (ManagementObject)managementBaseObject;
						string text3 = (managementObject["Name"] ?? "").ToString();
						int num = text3.LastIndexOf("(COM");
						if (num >= 0)
						{
							int num2 = text3.IndexOf(')', num);
							string text4 = text3.Substring(num + 1, num2 - num - 1);
							if (text3.IndexOf("Arduino", StringComparison.OrdinalIgnoreCase) >= 0 || text3.IndexOf("CH340", StringComparison.OrdinalIgnoreCase) >= 0 || text3.IndexOf("FTDI", StringComparison.OrdinalIgnoreCase) >= 0 || text3.IndexOf("USB Serial", StringComparison.OrdinalIgnoreCase) >= 0 || text3.IndexOf("USB-SERIAL", StringComparison.OrdinalIgnoreCase) >= 0)
							{
								text = text4;
								text2 = text3;
								break;
							}
						}
					}
				}
			}
			catch (Exception ex)
			{
				this.L("Autodetect WMI: " + ex.Message);
			}
			if (text == null)
			{
				string[] portNames = SerialPort.GetPortNames();
				if (portNames.Length == 1)
				{
					text = portNames[0];
					text2 = "ainoa portti";
				}
			}
			if (text != null)
			{
				if (!this.cbPort.Items.Contains(text))
				{
					this.cbPort.Items.Add(text);
				}
				this.cbPort.SelectedItem = text;
				this.L(string.Concat(new string[]
				{
					"Autodetect: ",
					text,
					"  (",
					text2,
					")"
				}));
			}
			else
			{
				this.L((this.lang == "fi") ? "Autodetect: ei l\u00f6ytynyt Arduinoa." : "Autodetect: no Arduino found.");
			}
		}

		// Token: 0x0600007B RID: 123 RVA: 0x00007870 File Offset: 0x00005A70
		private void Connect()
		{
			try
			{
				if (this.trans != null && this.trans.IsOpen)
				{
					this.trans.Close();
					this.trans = null;
					this.Text = "Stellantis Config Messer";
					this.vin = "";
					this.lblStatus.Text = this.T("noconn");
					this.lblStatus.ForeColor = Theme.Red;
					this.btnConn.Text = this.T("conn");
					this.PopulateModules(Data.Modules);
				}
				else if (this.Demo())
				{
					this.trans = new DemoTransport
					{
						Log = new Action<string>(this.L)
					};
					this.trans.Open(null);
					this.lblStatus.Text = this.T("connto") + "Demo";
					this.lblStatus.ForeColor = Theme.Amber;
					this.btnConn.Text = this.T("disc");
					this.Text = "Stellantis Config Messer  \u2014  DEMO";
					this.PopulateModules(Data.Modules);
					this.L((this.lang == "fi") ? "Demo: kaikki 53 ECU:ta ja zonet selattavissa ilman autoa (dummy-data)." : "Demo: all 53 ECUs and zones browsable without a car (dummy data).");
				}
				else
				{
					string text;
					if (this.Vci())
					{
						VciTransport vciTransport = new VciTransport
						{
							Log = new Action<string>(this.L)
						};
						text = vciTransport.Open(null);
						this.trans = vciTransport;
					}
					else
					{
						if (this.cbPort.SelectedItem == null)
						{
							MessageBox.Show((this.lang == "fi") ? "Valitse COM-portti." : "Select a COM port.");
							return;
						}
						if (this.Elm())
						{
							Elm327Transport elm327Transport = new Elm327Transport
							{
								Log = new Action<string>(this.L)
							};
							text = elm327Transport.Open(this.cbPort.SelectedItem.ToString());
							this.trans = elm327Transport;
						}
						else
						{
							ArduinoTransport arduinoTransport = new ArduinoTransport
							{
								Log = new Action<string>(this.L)
							};
							text = arduinoTransport.Open(this.cbPort.SelectedItem.ToString());
							this.trans = arduinoTransport;
						}
					}
					if (text != "")
					{
						this.trans = null;
						MessageBox.Show(((this.lang == "fi") ? "Yhteysvirhe: " : "Connection error: ") + text);
						this.lblStatus.Text = this.T("noconn");
						this.lblStatus.ForeColor = Theme.Red;
					}
					else
					{
						this.lblStatus.Text = this.T("connto") + (this.Vci() ? "Diagbox VCI" : this.cbPort.SelectedItem.ToString());
						this.lblStatus.ForeColor = Theme.Green;
						this.btnConn.Text = this.T("disc");
						this.ReadVinToTitle();
						this.DetectEcus();
					}
				}
			}
			catch (Exception ex)
			{
				this.trans = null;
				MessageBox.Show(((this.lang == "fi") ? "Yhteysvirhe: " : "Connection error: ") + ex.Message);
			}
		}

		// Token: 0x0600007C RID: 124 RVA: 0x00007C44 File Offset: 0x00005E44
		private void ReadVinToTitle()
		{
			try
			{
				this.trans.SelectEcu("752", "652");
				string text = this.trans.Send("22F190", new string[]
				{
					"62F190",
					"7F22"
				}, 900);
				int num = text.IndexOf("62F190"); if (num < 0) { text = this.trans.Send("21B0", new string[] { "61B0", "7F21" }, 900); num = text.IndexOf("61B0"); if (num >= 0) { text = "62F190" + text.Substring(num + 4); num = 0; } }
				if (num >= 0)
				{
					string text2 = MainForm.AsciiOf(Hex.ToBytes(text.Substring(num + 6)));
					if (text2.Length >= 11)
					{
						this.vin = text2;
						this.Text = "Stellantis Config Messer  \u2014  VIN " + text2;
						this.L("VIN: " + text2);
					}
				}
			}
			catch
			{
			}
		}

		// Token: 0x0600007D RID: 125 RVA: 0x00007D20 File Offset: 0x00005F20
		private static string AsciiOf(byte[] b)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (byte b2 in b)
			{
				if (b2 >= 32 && b2 < 127)
				{
					stringBuilder.Append((char)b2);
				}
			}
			return stringBuilder.ToString().Trim();
		}

		// Token: 0x0600007E RID: 126 RVA: 0x00007D84 File Offset: 0x00005F84
		private void SelectModule()
		{
			if (this.cbModule.SelectedIndex >= 0 && this.cbModule.SelectedIndex < this.shown.Count)
			{
				this.curMod = this.shown[this.cbModule.SelectedIndex];
				this.views.Clear();
				this.Render();
				this.btnWrite.Enabled = false;
			}
		}

		// Token: 0x0600007F RID: 127 RVA: 0x00007E00 File Offset: 0x00006000
		private void PopulateModules(List<Module> mods)
		{
			this.shown = (mods ?? new List<Module>());
			this.cbModule.Items.Clear();
			foreach (Module module in this.shown)
			{
				this.cbModule.Items.Add(module.Id + " \u2014 " + module.Name);
			}
			if (this.cbModule.Items.Count > 0)
			{
				this.cbModule.SelectedIndex = 0;
			}
			else
			{
				this.curMod = null;
				this.views.Clear();
				this.Render();
				this.btnWrite.Enabled = false;
			}
		}

		// Token: 0x06000080 RID: 128 RVA: 0x000083AC File Offset: 0x000065AC
		private void DetectEcus()
		{
			if (this.trans != null && this.trans.IsOpen)
			{
				this.busy = true;
				this.SetButtons(false);
				List<Module> all = Data.Modules;
				ITransport tr = this.trans;
				new Thread(delegate()
				{
					List<Module> present = new List<Module>();
					List<VinPair> vinMiss = new List<VinPair>();
					Dictionary<string, bool> dictionary = new Dictionary<string, bool>();
					int num = 0;
					foreach (Module module in all)
					{
						num++;
						this.Status(string.Concat(new object[]
						{
							(this.lang == "fi") ? "Haetaan ECU " : "Probing ECU ",
							num,
							"/",
							all.Count,
							"  ",
							module.Id
						}));
						string key = module.Tx + ":" + module.Rx;
						bool flag;
						if (!dictionary.TryGetValue(key, out flag))
						{
							try
							{
								tr.SelectEcu(module.Tx, module.Rx);
								string text = tr.Send("3E00", null, 250);
								flag = (text.Contains("7E") || text.Contains("7F3E") || text.Contains("5003"));
								if (!flag)
								{
									text = tr.Send("1003", null, 250);
									flag = (text.Contains("5003") || text.Contains("7F10")); if (!flag && module.Id == "BSI2004") { text = tr.Send("10C0", null, 350); flag = (text.Contains("50C0") || text.Contains("7F10")); }
								}
							}
							catch
							{
								flag = false;
							}
							dictionary[key] = flag;
						}
						if (flag)
						{
							present.Add(module);
						}
					}
					try
					{
						if (this.IsValidVin(this.vin))
						{
							Dictionary<string, VinPair> seen = new Dictionary<string, VinPair>();
							foreach (Module module2 in present)
							{
								if (!this.IsInfotainment(module2))
								{
									continue;
								}
								string pk = module2.Tx + ":" + module2.Rx;
								VinPair vp;
								if (!seen.TryGetValue(pk, out vp))
								{
									string mv = this.ReadVinFrom(tr, module2.Tx, module2.Rx);
									vp = new VinPair
									{
										Tx = module2.Tx,
										Rx = module2.Rx,
										Mod = module2,
										ModVin = mv,
										Label = module2.Id + " (" + module2.Name + ")"
									};
									seen[pk] = vp;
									if (mv != null && !string.Equals(mv, this.vin, StringComparison.OrdinalIgnoreCase))
									{
										vinMiss.Add(vp);
									}
								}
								if (!string.IsNullOrEmpty(module2.Key) && !vp.Keys.Contains(module2.Key.ToUpper()))
								{
									vp.Keys.Add(module2.Key.ToUpper());
								}
							}
						}
					}
					catch
					{
					}
					try
					{
						tr.Send("1001", null, 200);
					}
					catch
					{
					}
					this.BeginInvoke(new Action(delegate()
					{
						this.busy = false;
						this.SetButtons(true);
						if (present.Count == 0)
						{
							this.PopulateModules(all);
							this.L((this.lang == "fi") ? "Ei tunnistettu ECU:ja v\u00e4yl\u00e4lt\u00e4 - n\u00e4ytet\u00e4\u00e4n kaikki." : "No ECUs detected on the bus - showing all.");
						}
						else
						{
							this.PopulateModules(present);
							this.L(((this.lang == "fi") ? "L\u00f6ytyi " : "Found ") + present.Count + ((this.lang == "fi") ? " ohjainlaitetta v\u00e4yl\u00e4lt\u00e4." : " ECUs on the bus."));
						}
						this.lblStatus.Text = ((this.trans != null && this.trans.IsOpen) ? (this.T("connto") + (this.Vci() ? "Diagbox VCI" : (this.Demo() ? "Demo" : this.cbPort.SelectedItem))) : this.T("noconn"));
						this.lblStatus.ForeColor = ((this.trans != null && this.trans.IsOpen) ? Theme.Green : Theme.Red);
						if (vinMiss.Count > 0)
						{
							List<VinPair> confirmed = new List<VinPair>();
							foreach (VinPair vp2 in vinMiss)
							{
								string msg = (this.lang == "fi") ? (vp2.Label + "\n\nTallennettu VIN:  " + vp2.ModVin + "\nAuton VIN:          " + this.vin + "\n\nVIN ei t\u00e4sm\u00e4\u00e4. Paritetaanko t\u00e4m\u00e4 yksikk\u00f6 t\u00e4h\u00e4n autoon\n(kirjoitetaan auton VIN yksik\u00f6n F190-zoneen)?") : (vp2.Label + "\n\nStored VIN:  " + vp2.ModVin + "\nCar VIN:      " + this.vin + "\n\nVIN mismatch. Pair this unit to this car\n(write the car VIN to the unit's F190 zone)?");
								if (vp2.Keys.Count == 0)
								{
									MessageBox.Show(msg + ((this.lang == "fi") ? "\n\n(Ei kirjoitusavainta t\u00e4lle yksik\u00f6lle - paritus ei ole mahdollista.)" : "\n\n(No write key for this unit - pairing is not possible.)"), (this.lang == "fi") ? "VIN ei t\u00e4sm\u00e4\u00e4" : "VIN mismatch", MessageBoxButtons.OK, MessageBoxIcon.Warning);
								}
								else if (MessageBox.Show(msg, (this.lang == "fi") ? "VIN-paritus" : "VIN pairing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
								{
									confirmed.Add(vp2);
								}
							}
							if (confirmed.Count > 0)
							{
								this.PairVinBatch(confirmed);
							}
						}
					}));
				})
				{
					IsBackground = true
				}.Start();
			}
		}

		// True for infotainment / radio units that carry a VIN (NAC, RCC, IVI, RD4/RD4.5, SMEG...).
		private bool IsInfotainment(Module m)
		{
			string text = (m.Id ?? "").ToUpper();
			string text2 = (m.Name ?? "").ToUpper();
			return text.Contains("RCC") || text.Contains("NAC") || text.Contains("TELEMAT") || text.Contains("AUTORADIO") || text.Contains("RADIO") || text.Contains("SMEG") || text.Contains("RNEG") || text.Contains("IVI") || text2.Contains("INFOTAINMENT") || text2.Contains("RADIO");
		}

		private bool IsValidVin(string v)
		{
			if (string.IsNullOrEmpty(v) || v.Length != 17)
			{
				return false;
			}
			foreach (char c in v)
			{
				if (!char.IsLetterOrDigit(c))
				{
					return false;
				}
			}
			return true;
		}

		private static string AsciiToHex(string s)
		{
			StringBuilder stringBuilder = new StringBuilder();
			foreach (char c in s)
			{
				stringBuilder.Append(((int)c & 255).ToString("X2"));
			}
			return stringBuilder.ToString();
		}

		// Read VIN (22F190) from a given tx:rx pair, as ASCII. null if no answer.
		private string ReadVinFrom(ITransport tr, string tx, string rx)
		{
			string result;
			try
			{
				tr.SelectEcu(tx, rx);
				string text = tr.Send("22F190", null, 900);
				int num = text.IndexOf("62F190"); if (num < 0) { text = this.trans.Send("21B0", new string[] { "61B0", "7F21" }, 900); num = text.IndexOf("61B0"); if (num >= 0) { text = "62F190" + text.Substring(num + 4); num = 0; } }
				if (num < 0)
				{
					result = null;
				}
				else
				{
					string text2 = MainForm.AsciiOf(Hex.ToBytes(text.Substring(num + 6)));
					result = ((text2.Length < 11) ? null : text2);
				}
			}
			catch
			{
				result = null;
			}
			return result;
		}

		// Raw F190 data hex (after 62F190) from the currently selected ECU. null if no answer.
		private string RawF190(ITransport tr)
		{
			string text = tr.Send("22F190", null, 900);
			int num = text.IndexOf("62F190"); if (num < 0) { text = this.trans.Send("21B0", new string[] { "61B0", "7F21" }, 900); num = text.IndexOf("61B0"); if (num >= 0) { text = "62F190" + text.Substring(num + 4); num = 0; } }
			if (num < 0)
			{
				return null;
			}
			return text.Substring(num + 6);
		}

		// Pair one or more infotainment units to this car by writing the car VIN into F190.
		private void PairVinBatch(List<VinPair> items)
		{
			this.busy = true;
			this.SetButtons(false);
			ITransport tr = this.trans;
			string carVin = this.vin;
			string vinHex = MainForm.AsciiToHex(carVin);
			new Thread(delegate()
			{
				int num = 0;
				foreach (VinPair vinPair in items)
				{
					try
					{
						tr.SelectEcu(vinPair.Tx, vinPair.Rx);
						try
						{
							string text = this.RawF190(tr);
							if (text != null)
							{
								List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
								list.Add(new KeyValuePair<string, string>("F190", text));
								string str = this.SaveBackupRows(vinPair.Mod, list);
								this.L(((this.lang == "fi") ? "Varmuuskopio (F190): " : "Backup (F190): ") + str);
							}
						}
						catch (Exception ex)
						{
							this.L("WARN backup: " + ex.Message);
						}
						if (!tr.Send("1003", null, 1200).Contains("5003"))
						{
							this.L((this.lang == "fi") ? ("VIRHE 1003 (" + vinPair.Label + ").") : ("ERROR 1003 (" + vinPair.Label + ")."));
						}
						else
						{
							bool flag = false;
							foreach (string text2 in vinPair.Keys)
							{
								string text3 = tr.Send("2703", null, 900);
								int num2 = text3.IndexOf("6703");
								if (num2 >= 0)
								{
									byte[] chg = Hex.ToBytes(text3.Substring(num2 + 4, 8));
									string str2 = Sk.Key(text2, chg);
									if (tr.Send("2704" + str2, null, 900).Contains("6704"))
									{
										flag = true;
										this.L("Unlock OK (key " + text2 + ").");
										break;
									}
								}
							}
							if (!flag)
							{
								tr.Send("1001", null, 300);
								this.L((this.lang == "fi") ? ("VIRHE: avausta ei saatu - v\u00e4\u00e4r\u00e4 avain? (" + vinPair.Label + ").") : ("ERROR: unlock failed - wrong key? (" + vinPair.Label + ")."));
							}
							else
							{
								string text4 = tr.Send("2EF190" + vinHex, null, 2500);
								if (!text4.Contains("6EF190"))
								{
									this.L("VIRHE/ERROR F190: " + text4);
									tr.Send("1001", null, 300);
								}
								else
								{
									string text5 = this.RawF190(tr);
									tr.Send("1001", null, 300);
									if (text5 != null && text5.Length >= vinHex.Length && text5.Substring(0, vinHex.Length).Equals(vinHex, StringComparison.OrdinalIgnoreCase))
									{
										num++;
										this.L((this.lang == "fi") ? ("VIN paritettu: " + vinPair.Label + " -> " + carVin) : ("VIN paired: " + vinPair.Label + " -> " + carVin));
									}
									else
									{
										this.L((this.lang == "fi") ? ("TARKISTA VIN (" + vinPair.Label + "): " + text5) : ("CHECK VIN (" + vinPair.Label + "): " + text5));
									}
								}
							}
						}
					}
					catch (Exception ex2)
					{
						try
						{
							tr.Send("1001", null, 300);
						}
						catch
						{
						}
						this.L("VIRHE/ERROR pair: " + ex2.Message);
					}
				}
				int done = num;
				int total = items.Count;
				this.BeginInvoke(new Action(delegate()
				{
					this.busy = false;
					this.SetButtons(true);
					MessageBox.Show((this.lang == "fi") ? ("VIN paritettu " + done + "/" + total + " yksikk\u00f6\u00f6n.") : ("VIN paired to " + done + "/" + total + " unit(s)."), (this.lang == "fi") ? "Valmis" : "Done");
				}));
			})
			{
				IsBackground = true
			}.Start();
		}

		// Token: 0x06000081 RID: 129 RVA: 0x0000842C File Offset: 0x0000662C
		private bool Need()
		{
			bool result;
			if (this.busy)
			{
				result = false;
			}
			else if (this.trans == null || !this.trans.IsOpen)
			{
				MessageBox.Show((this.lang == "fi") ? "Ei yhteytt\u00e4." : "Not connected.");
				result = false;
			}
			else if (this.curMod == null)
			{
				MessageBox.Show((this.lang == "fi") ? "Valitse moduuli." : "Select module.");
				result = false;
			}
			else
			{
				result = true;
			}
			return result;
		}

		// Token: 0x06000082 RID: 130 RVA: 0x000084D4 File Offset: 0x000066D4
		private string Tx(string cmd, int ms)
		{
			return this.trans.Send(cmd, null, ms);
		}

		// Token: 0x06000083 RID: 131 RVA: 0x000084F4 File Offset: 0x000066F4
		private bool OpenSession()
		{
			this.trans.SelectEcu(this.curMod.Tx, this.curMod.Rx);
			string openCmd = ((this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "10C0" : "1003"; string text = this.Tx(openCmd, 1200);
			bool result;
			if (!text.Contains(((this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "50C0" : "5003"))
			{
				this.L("VIRHE/ERROR 1003 -> " + text);
				result = false;
			}
			else
			{
				result = true;
			}
			return result;
		}

		// Token: 0x06000084 RID: 132 RVA: 0x00008560 File Offset: 0x00006760
		private int Idx(Field f)
		{
			return f.ByteStart + this.curMod.ByteOffset;
		}

		// Token: 0x06000085 RID: 133 RVA: 0x00008584 File Offset: 0x00006784
		private string ReadZone(string zone, int ms)
{
bool kwp = (zone.Length == 2);
string req = kwp ? ("21" + zone) : ("22" + zone);
string okPrefix = kwp ? ("61" + zone) : ("62" + zone);
string errPrefix = kwp ? "7F21" : "7F22";
string text = this.trans.Send(req, new string[] { okPrefix, errPrefix }, ms);
int num = text.IndexOf(okPrefix);
return (num < 0) ? null : text.Substring(num + 2 + zone.Length);
}

		// Token: 0x06000086 RID: 134 RVA: 0x00008A1C File Offset: 0x00006C1C
		private void DoReadAll()
		{
			if (this.Need())
			{
				this.busy = true;
				this.SetButtons(false);
				Module mod = this.curMod;
				new Thread(delegate()
				{
					List<ZoneView> list = new List<ZoneView>();
					try
					{
						this.trans.SelectEcu(mod.Tx, mod.Rx);
						if (!this.Tx((mod.Id == "BSI2004") ? "10C0" : "1003", 1200).Contains((mod.Id == "BSI2004") ? "50C0" : "5003"))
						{
							this.L("VIRHE/ERROR 1003.");
						}
						else
						{
							int num = 0;
							foreach (Zone zone in mod.Zones)
							{
								num++;
								this.Status(string.Concat(new object[]
								{
									(this.lang == "fi") ? "Luetaan " : "Reading ",
									num,
									"/",
									mod.Zones.Count,
									"  ",
									zone.Id
								}));
								ZoneView zoneView = this.ReadInto(zone, 700);
								if (zoneView != null)
								{
									list.Add(zoneView);
								}
							}
							this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
						}
					}
					catch (Exception ex)
					{
						this.L("VIRHE/ERROR read all: " + ex.Message);
					}
					this.BeginInvoke(new Action(delegate()
					{
						this.views = list;
						this.Render();
						this.btnWrite.Enabled = (this.views.Count > 0);
						this.busy = false;
						this.SetButtons(true);
						this.lblStatus.Text = ((this.trans != null && this.trans.IsOpen) ? (this.T("connto") + (this.Vci() ? "Diagbox VCI" : this.cbPort.SelectedItem)) : this.T("noconn"));
						this.lblStatus.ForeColor = ((this.trans != null && this.trans.IsOpen) ? Theme.Green : Theme.Red);
						this.L(string.Concat(new object[]
						{
							(this.lang == "fi") ? "Luettu kaikki: " : "Read all: ",
							list.Count,
							"/",
							mod.Zones.Count,
							(this.lang == "fi") ? " zonea." : " zones."
						}));
					}));
				})
				{
					IsBackground = true
				}.Start();
			}
		}

		// Token: 0x06000087 RID: 135 RVA: 0x00008F98 File Offset: 0x00007198
		private void DoScan()
		{
			if (this.Need())
			{
				this.busy = true;
				this.SetButtons(false);
				Module mod = this.curMod;
				new Thread(delegate()
				{
					List<ZoneView> list = new List<ZoneView>();
					try
					{
						this.trans.SelectEcu(mod.Tx, mod.Rx);
						if (!this.Tx((mod.Id == "BSI2004") ? "10C0" : "1003", 1200).Contains((mod.Id == "BSI2004") ? "50C0" : "5003"))
						{
							this.L("VIRHE/ERROR 1003.");
						}
						else
						{
							bool isKwpScan = (mod.Id == "BSI2004"); int[] array = isKwpScan ? new int[] { 0x80 } : new int[] { 8448, 8704, 8960 }; int stepCount = isKwpScan ? 128 : 256;
							int num = array.Length * stepCount;
							int num2 = 0;
							foreach (int num3 in array)
							{
								for (int j = 0; j < stepCount; j++)
								{
									num2++;
									string zid = isKwpScan ? (num3 + j).ToString("X2") : (num3 + j).ToString("X4");
									if (num2 % 16 == 0)
									{
										this.Status(string.Concat(new object[]
										{
											(this.lang == "fi") ? "Skannataan " : "Scanning ",
											num2,
											"/",
											num,
											"  ",
											zid
										}));
									}
									string text = this.ReadZone(zid, 500);
									if (text != null)
									{
										Zone zone = mod.Zones.FirstOrDefault((Zone zz) => zz.Id == zid);
										if (zone == null)
										{
											zone = new Zone
											{
												Id = zid,
												Name = "(discovered)"
											};
										}
										list.Add(new ZoneView
										{
											Zone = zone,
											OrigHex = text,
											Bytes = Hex.ToBytes(text)
										});
									}
								}
							}
							this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
						}
					}
					catch (Exception ex)
					{
						this.L("VIRHE/ERROR scan: " + ex.Message);
					}
					this.BeginInvoke(new Action(delegate()
					{
						this.views = list;
						this.Render();
						this.btnWrite.Enabled = (this.views.Count > 0);
						this.busy = false;
						this.SetButtons(true);
						this.lblStatus.Text = ((this.trans != null && this.trans.IsOpen) ? (this.T("connto") + (this.Vci() ? "Diagbox VCI" : this.cbPort.SelectedItem)) : this.T("noconn"));
						this.lblStatus.ForeColor = ((this.trans != null && this.trans.IsOpen) ? Theme.Green : Theme.Red);
						this.L(((this.lang == "fi") ? "Skannaus valmis: " : "Scan done: ") + list.Count + ((this.lang == "fi") ? " zonea l\u00f6ytyi." : " zones found."));
					}));
				})
				{
					IsBackground = true
				}.Start();
			}
		}

		// Token: 0x06000088 RID: 136 RVA: 0x00008FFC File Offset: 0x000071FC
		private ZoneView ReadInto(Zone z, int ms)
		{
			string text = this.ReadZone(z.Id, ms);
			ZoneView result;
			if (text == null)
			{
				result = null;
			}
			else
			{
				result = new ZoneView
				{
					Zone = z,
					OrigHex = text,
					Bytes = Hex.ToBytes(text)
				};
			}
			return result;
		}

		// Token: 0x06000089 RID: 137 RVA: 0x00009068 File Offset: 0x00007268
		private void SetButtons(bool en)
		{
			if (base.InvokeRequired)
			{
				base.BeginInvoke(new Action(delegate()
				{
					this.SetButtons(en);
				}));
			}
			else
			{
				this.btnRead.Enabled = en;
				this.btnScan.Enabled = en;
				this.btnDtcR.Enabled = en;
				this.btnDtcC.Enabled = en;
				this.btnConn.Enabled = en;
				this.cbModule.Enabled = en;
				this.cbTrans.Enabled = en;
				if (this.menuRestore != null)
				{
					this.menuRestore.Enabled = en;
				}
			}
		}

		// Token: 0x0600008A RID: 138 RVA: 0x00009174 File Offset: 0x00007374
		private void Status(string s)
		{
			if (base.InvokeRequired)
			{
				base.BeginInvoke(new Action(delegate()
				{
					this.Status(s);
				}));
			}
			else
			{
				this.lblStatus.Text = s;
				this.lblStatus.ForeColor = Theme.Cyan;
			}
		}

		// Token: 0x0600008B RID: 139 RVA: 0x000092A8 File Offset: 0x000074A8
		private Control BuildControl(ZoneView v, Field f, int left, int width)
		{
			int num = Convert.ToInt32(f.Mask, 16);
			int num2 = f.ByteStart + this.curMod.ByteOffset;
			int num3 = (int)v.Bytes[num2] & num;
			Control control;
			if (f.Type == "enum" && f.Options.Count >= 2)
			{
				if (f.Options.Count == 2)
				{
					CheckBox ck = new CheckBox
					{
						Top = 3,
						Width = width,
						Height = 26,
						Appearance = Appearance.Button,
						TextAlign = ContentAlignment.MiddleCenter,
						FlatStyle = FlatStyle.Flat,
						Cursor = Cursors.Hand
					};
					ck.FlatAppearance.BorderColor = Theme.Border;
					int num4 = Convert.ToInt32(f.Options[1].Val, 16) & num;
					ck.Checked = (num3 == num4);
					Field fc = f;
					Action paint = delegate()
					{
						ck.Text = (ck.Checked ? fc.Options[1] : fc.Options[0]).L(this.lang);
						ck.BackColor = (ck.Checked ? Theme.Green : Theme.Input);
						ck.ForeColor = (ck.Checked ? Color.Black : Theme.Text);
					};
					paint();
					ck.CheckedChanged += delegate(object s, EventArgs e)
					{
						paint();
					};
					Theme.Round(ck, 6);
					control = ck;
				}
				else
				{
					ComboBox comboBox = new ComboBox
					{
						Top = 4,
						Width = width,
						DropDownStyle = ComboBoxStyle.DropDownList,
						FlatStyle = FlatStyle.Flat,
						BackColor = Theme.Input,
						ForeColor = Theme.Text
					};
					int selectedIndex = 0;
					for (int i = 0; i < f.Options.Count; i++)
					{
						comboBox.Items.Add(f.Options[i].L(this.lang));
						if ((Convert.ToInt32(f.Options[i].Val, 16) & num) == num3)
						{
							selectedIndex = i;
						}
					}
					comboBox.SelectedIndex = selectedIndex;
					control = comboBox;
				}
			}
			else
			{
				control = new TextBox
				{
					Top = 5,
					Width = Math.Min(width, 140),
					Font = Theme.Mono(9.5f),
					Text = num3.ToString("X2"),
					BackColor = Theme.Input,
					ForeColor = Theme.Cyan,
					BorderStyle = BorderStyle.FixedSingle
				};
			}
			control.Left = left;
			return control;
		}

		// Token: 0x0600008C RID: 140 RVA: 0x00009588 File Offset: 0x00007788
		private Panel NewRow()
		{
			return new Panel
			{
				Width = 1040,
				Height = 34,
				Margin = new Padding(2, 1, 2, 3),
				BackColor = Theme.Card
			};
		}

		// Token: 0x0600008D RID: 141 RVA: 0x00009614 File Offset: 0x00007814
		private void Render()
		{
			if (this.pnlFields != null)
			{
				this.pnlFields.SuspendLayout();
				this.pnlFields.Controls.Clear();
				if (this.views == null || this.views.Count == 0)
				{
					bool fi = this.lang == "fi";
					Label intro1 = new Label
					{
						AutoSize = false,
						Width = 1040,
						Height = 40,
						Margin = new Padding(2, 12, 2, 0),
						Padding = new Padding(16, 9, 0, 0),
						BackColor = Theme.Card,
						ForeColor = Theme.Cyan,
						Font = new Font(FontFamily.GenericSansSerif, 13f, FontStyle.Bold),
						Text = fi ? "Stellantis Config Messer \u2013 n\u00e4in se toimii" : "Stellantis Config Messer \u2013 how it works"
					};
					Label intro2 = new Label
					{
						AutoSize = false,
						Width = 1040,
						Height = 340,
						Margin = new Padding(2, 0, 2, 12),
						Padding = new Padding(18, 8, 14, 12),
						BackColor = Theme.Card,
						ForeColor = Theme.Text,
						Font = new Font(FontFamily.GenericSansSerif, 10f, FontStyle.Regular),
						Text = this.IntroText(fi)
					};
					this.pnlFields.Controls.Add(intro1);
					this.pnlFields.Controls.Add(intro2);
				}
				string text = this.HintFor(this.curMod);
				if (text != null)
				{
					Label label = new Label
					{
						AutoSize = false,
						Width = 1040,
						Height = 36,
						Margin = new Padding(2, 4, 2, 4),
						ForeColor = Theme.Amber,
						BackColor = Theme.Card,
						Padding = new Padding(8, 4, 0, 0),
						AutoEllipsis = true,
						Text = ((this.lang == "fi") ? "Diagbox-telekoodaus (viite): " : "Diagbox telecoding (ref): ") + text
					};
					ToolTip toolTip = new ToolTip();
					toolTip.SetToolTip(label, (text.Length > 1200) ? (text.Substring(0, 1200) + "\u2026") : text);
					this.pnlFields.Controls.Add(label);
				}
				int hidden = 0;
				using (List<ZoneView>.Enumerator enumerator = this.views.GetEnumerator())
				{
					while (enumerator.MoveNext())
					{
						ZoneView v = enumerator.Current;
						v.Fs.Clear();
						v.Cs.Clear();
						v.RawBox = null;
						List<Field> list = (from f in v.Zone.Fields
						where f.ByteStart + this.curMod.ByteOffset < v.Bytes.Length
						select f).ToList<Field>();
							string q = (this.txtSearch != null) ? this.txtSearch.Text.Trim().ToLower() : "";
							if (q.Length > 0)
							{
								bool zoneMatch = v.Zone.Name.ToLower().Contains(q) || v.Zone.Id.ToLower().Contains(q);
								if (!zoneMatch) { list = (from f in list where f.L(this.lang).ToLower().Contains(q) select f).ToList<Field>(); if (list.Count == 0) { continue; } }
							}
						if (v.Zone.Id == "F190")
						{
							Panel panel = this.NewRow();
							panel.Controls.Add(new Label
							{
								Left = 10,
								Top = 8,
								Width = 150,
								ForeColor = Theme.Text,
								Text = v.Zone.Id + "  VIN:"
							});
							panel.Controls.Add(new TextBox
							{
								Left = 170,
								Top = 5,
								Width = 340,
								ReadOnly = true,
								Font = Theme.Mono(10f),
								BackColor = Theme.Input,
								ForeColor = Theme.Cyan,
								BorderStyle = BorderStyle.FixedSingle,
								Text = MainForm.AsciiOf(v.Bytes)
							});
							this.pnlFields.Controls.Add(panel);
						}
						else if (list.Count >= 2)
						{
							Label value = new Label
							{
								AutoSize = false,
								Width = 1040,
								Height = 26,
								Margin = new Padding(2, 8, 2, 2),
								Font = new Font("Segoe UI", 9f, FontStyle.Bold),
								ForeColor = Theme.Cyan,
								BackColor = Theme.Panel,
								Padding = new Padding(8, 4, 0, 0),
								Text = (Cfg.OnlyDecoded ? (v.Zone.Id + "   " + v.Zone.Name) : string.Concat(new string[]
								{
									v.Zone.Id,
									"   ",
									v.Zone.Name,
									"      [",
									v.OrigHex,
									"]"
								})),
								AutoEllipsis = true
							};
							this.pnlFields.Controls.Add(value);
							foreach (Field field in list)
							{
								Panel panel2 = this.NewRow();
								Label label2 = new Label
								{
									Left = 26,
									Top = 8,
									Width = 410,
									ForeColor = Theme.Text,
									Text = field.L(this.lang),
									AutoEllipsis = true
								};
								if (!string.IsNullOrEmpty(field.Desc))
								{
									ToolTip toolTip = new ToolTip();
									toolTip.SetToolTip(label2, field.Desc);
								}
								panel2.Controls.Add(label2);
								Control control = this.BuildControl(v, field, 450, 400);
								panel2.Controls.Add(control);
								this.pnlFields.Controls.Add(panel2);
								v.Fs.Add(field);
								v.Cs.Add(control);
							}
						}
						else if (list.Count == 1)
						{
							Field field = list[0];
							Panel panel2 = this.NewRow();
							Label label2 = new Label
							{
								Left = 10,
								Top = 8,
								Width = 430,
								ForeColor = Theme.Text,
								Text = v.Zone.Id + "  " + field.L(this.lang),
								AutoEllipsis = true
							};
							ToolTip toolTip = new ToolTip();
							toolTip.SetToolTip(label2, (string.IsNullOrEmpty(field.Desc) ? "" : (field.Desc + "\n")) + "raw: " + v.OrigHex);
							panel2.Controls.Add(label2);
							Control control = this.BuildControl(v, field, 450, 400);
							panel2.Controls.Add(control);
							this.pnlFields.Controls.Add(panel2);
							v.Fs.Add(field);
							v.Cs.Add(control);
						}
						else if (Cfg.OnlyDecoded)
						{
							hidden++;
						}
						else
						{
							Panel panel2 = this.NewRow();
							panel2.Controls.Add(new Label
							{
								Left = 10,
								Top = 8,
								Width = 430,
								ForeColor = Theme.Sub,
								Text = v.Zone.Id + "  " + v.Zone.Name + "  (raw)",
								AutoEllipsis = true
							});
							TextBox textBox = new TextBox
							{
								Left = 450,
								Top = 5,
								Width = 520,
								Font = Theme.Mono(9.5f),
								BackColor = Theme.Input,
								ForeColor = Theme.Cyan,
								BorderStyle = BorderStyle.FixedSingle,
								Text = v.OrigHex
							};
							panel2.Controls.Add(textBox);
							this.pnlFields.Controls.Add(panel2);
							v.RawBox = textBox;
						}
					}
				}
				if (Cfg.OnlyDecoded && hidden > 0)
				{
					Label hideNote = new Label
					{
						AutoSize = false,
						Width = 1040,
						Height = 24,
						Margin = new Padding(2, 6, 2, 8),
						ForeColor = Theme.Sub,
						BackColor = Theme.Panel,
						Padding = new Padding(8, 4, 0, 0),
						AutoEllipsis = true,
						Text = ((this.lang == "fi") ? (hidden + " raakazonea piilotettu \u2013 poista 'Vain selke\u00e4t' n\u00e4hd\u00e4ksesi ne.") : (hidden + " raw-only zones hidden \u2013 uncheck 'Decoded only' to see them."))
					};
					this.pnlFields.Controls.Add(hideNote);
				}
				this.pnlFields.ResumeLayout();
			}
		}

		private string IntroText(bool fi)
		{
			if (fi)
			{
				return "\u2022  Valitse yhteystapa (Arduino / Diagbox VCI / ELM327) ja portti, paina Yhdist\u00e4.\n" +
					"    Tai valitse Demo ja selaa kaikkia ohjainlaitteita ja zoneja ilman autoa.\n\n" +
					"\u2022  Valitse ohjainlaite (ECU) yl\u00e4reunan pudotusvalikosta.\n\n" +
					"\u2022  Lue  \u2013  hakee ECU:n kaikki konfiguraatiozonet. Kentt\u00e4 n\u00e4kyy kytkimen\u00e4, pudotusvalikkona\n" +
					"    tai hex-kentt\u00e4n\u00e4; raaka hex n\u00e4kyy aina my\u00f6s dekoodaamattomille zoneille.\n\n" +
					"\u2022  Muuta arvot ja paina Kirjoita. Varmuuskopio tehd\u00e4\u00e4n automaattisesti ennen jokaista\n" +
					"    kirjoitusta (backup\\<VIN>\\<ECU>). Ei-varmistetuista ECU:ista tulee varoitus.\n\n" +
					"\u2022  Lue DTC / Tyhjenn\u00e4 DTC k\u00e4sittelee vikakoodit.  Skannaa etsii piilotetut zonet.\n\n" +
					"\u2022  Ty\u00f6kalut-valikko:  palauta konfiguraatio backupista  tai  aja Diagbox DataRipper.\n\n" +
					"\u26a0  K\u00e4yt\u00e4 omalla vastuulla, vain omaan autoosi, vakaalla j\u00e4nnitteell\u00e4.";
			}
			return "\u2022  Pick an interface (Arduino / Diagbox VCI / ELM327) and port, then press Connect.\n" +
				"    Or pick Demo to browse every ECU and zone without a car.\n\n" +
				"\u2022  Select an ECU from the drop-down at the top.\n\n" +
				"\u2022  Read  \u2013  fetches all of the ECU's config zones. A field shows as a toggle, a drop-down\n" +
				"    or a hex box; the raw hex is always shown, even for zones with no decoded fields.\n\n" +
				"\u2022  Change the values and press Write. A backup is taken automatically before every\n" +
				"    write (backup\\<VIN>\\<ECU>). Non-verified ECUs show a warning first.\n\n" +
				"\u2022  Read DTC / Clear DTC handle fault codes.  Scan searches for hidden zones.\n\n" +
				"\u2022  Tools menu:  restore configuration from a backup  or  run the Diagbox DataRipper.\n\n" +
				"\u26a0  Use entirely at your own risk, on a car you own, with a stable power supply.";
		}

		// Token: 0x0600008E RID: 142 RVA: 0x00009E74 File Offset: 0x00008074
		private string BuildNewHex(ZoneView v)
		{
			string result;
			if (v.RawBox != null)
			{
				result = v.RawBox.Text.Trim().ToUpper().Replace(" ", "");
			}
			else
			{
				byte[] array = (byte[])v.Bytes.Clone();
				for (int i = 0; i < v.Fs.Count; i++)
				{
					Field field = v.Fs[i];
					int num = Convert.ToInt32(field.Mask, 16);
					Control control = v.Cs[i];
					int num2;
					if (control is CheckBox)
					{
						CheckBox checkBox = (CheckBox)control;
						num2 = (Convert.ToInt32(checkBox.Checked ? field.Options[1].Val : field.Options[0].Val, 16) & num);
					}
					else if (control is ComboBox)
					{
						ComboBox comboBox = (ComboBox)control;
						num2 = (Convert.ToInt32(field.Options[comboBox.SelectedIndex].Val, 16) & num);
					}
					else
					{
						num2 = (Convert.ToInt32(((TextBox)control).Text.Trim(), 16) & num);
					}
					int num3 = field.ByteStart + this.curMod.ByteOffset;
					if (num3 < array.Length)
					{
						array[num3] = (byte)(((int)array[num3] & ~num) | num2);
					}
				}
				result = Hex.ToStr(array, array.Length);
			}
			return result;
		}

		// Token: 0x0600008F RID: 143 RVA: 0x0000A014 File Offset: 0x00008214
		private string SafeVin()
		{
			string result;
			if (string.IsNullOrEmpty(this.vin))
			{
				result = "no_vin";
			}
			else
			{
				StringBuilder stringBuilder = new StringBuilder();
				foreach (char c in this.vin)
				{
					stringBuilder.Append(char.IsLetterOrDigit(c) ? c : '_');
				}
				result = stringBuilder.ToString();
			}
			return result;
		}

		// Token: 0x06000090 RID: 144 RVA: 0x0000A090 File Offset: 0x00008290
		private string SaveBackupRows(Module mod, List<KeyValuePair<string, string>> rows)
		{
			string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup", this.SafeVin(), mod.Id);
			Directory.CreateDirectory(text);
			string text2 = Path.Combine(text, DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + ".csv");
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.AppendLine(string.Concat(new string[]
			{
				"ECU;",
				mod.Id,
				" (",
				mod.Name,
				")"
			}));
			stringBuilder.AppendLine("VIN;" + this.vin);
			stringBuilder.AppendLine(string.Concat(new string[]
			{
				"Pair;>",
				mod.Tx,
				":",
				mod.Rx,
				";Key;",
				mod.Key
			}));
			stringBuilder.AppendLine("Timestamp;" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			stringBuilder.AppendLine("Zone;Hex");
			foreach (KeyValuePair<string, string> keyValuePair in rows)
			{
				stringBuilder.AppendLine(keyValuePair.Key + ";" + keyValuePair.Value);
			}
			File.WriteAllText(text2, stringBuilder.ToString(), new UTF8Encoding(false));
			return text2;
		}

		// Token: 0x06000091 RID: 145 RVA: 0x0000A248 File Offset: 0x00008448
		private string SaveBackup()
		{
			List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
			foreach (ZoneView zoneView in this.views)
			{
				list.Add(new KeyValuePair<string, string>(zoneView.Zone.Id, zoneView.OrigHex));
			}
			return this.SaveBackupRows(this.curMod, list);
		}

		// Token: 0x06000092 RID: 146 RVA: 0x0000A2D0 File Offset: 0x000084D0
		private void DoWrite()
		{
			if (this.Need())
			{
				if (this.views.Count == 0)
				{
					MessageBox.Show((this.lang == "fi") ? "Lue ensin." : "Read first.");
				}
				else
				{
					List<KeyValuePair<ZoneView, string>> list = new List<KeyValuePair<ZoneView, string>>();
					try
					{
						foreach (ZoneView zoneView in this.views)
						{
							string text = this.BuildNewHex(zoneView);
							if (!text.Equals(zoneView.OrigHex, StringComparison.OrdinalIgnoreCase))
							{
								list.Add(new KeyValuePair<ZoneView, string>(zoneView, text));
							}
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show(((this.lang == "fi") ? "Virheellinen sy\u00f6te: " : "Invalid input: ") + ex.Message);
						return;
					}
					if (list.Count == 0)
					{
						MessageBox.Show((this.lang == "fi") ? "Ei muutoksia." : "No changes.");
					}
					else
					{
						StringBuilder stringBuilder = new StringBuilder();
						if (!this.curMod.Verified)
						{
							stringBuilder.AppendLine((this.lang == "fi") ? "! T\u00e4m\u00e4n ECU:n avainta EI ole varmistettu autolla - kirjoitus voi ep\u00e4onnistua (avaus hyl\u00e4t\u00e4\u00e4n). Lukeminen on turvallista.\n" : "! This ECU's key is NOT car-verified - write may fail (unlock rejected). Reading is safe.\n");
						}
						foreach (KeyValuePair<ZoneView, string> keyValuePair in list)
						{
							stringBuilder.AppendLine(string.Concat(new string[]
							{
								keyValuePair.Key.Zone.Id,
								":  ",
								keyValuePair.Key.OrigHex,
								"  ->  ",
								keyValuePair.Value
							}));
						}
						if (MessageBox.Show(((this.lang == "fi") ? "Kirjoitetaanko n\u00e4m\u00e4 zonet?\n\n" : "Write these zones?\n\n") + stringBuilder, (this.lang == "fi") ? "Vahvista" : "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes)
						{
							try
							{
								string str = this.SaveBackup();
								this.L(((this.lang == "fi") ? "Varmuuskopio: " : "Backup: ") + str);
							}
							catch (Exception ex)
							{
								MessageBox.Show(((this.lang == "fi") ? "Varmuuskopio ep\u00e4onnistui: " : "Backup failed: ") + ex.Message + ((this.lang == "fi") ? "\n\nKirjoitusta EI tehty." : "\n\nWrite aborted."));
								return;
							}
							try
							{
								if (this.OpenSession())
								{
									string text2 = this.Tx("2703", 800);
									int num = text2.IndexOf("6703");
									if (num < 0)
									{
										this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
										this.L("VIRHE/ERROR: no seed.");
									}
									else
									{
										byte[] chg = Hex.ToBytes(text2.Substring(num + 4, 8));
										string str2 = Sk.Key(this.curMod.Key, chg);
										if (!this.Tx("2704" + str2, 800).Contains("6704"))
										{
											this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
											this.L("VIRHE/ERROR: unlock rejected (wrong key?).");
										}
										else
										{
											int num2 = 0;
											foreach (KeyValuePair<ZoneView, string> keyValuePair in list)
											{
												ZoneView zoneView = keyValuePair.Key;
												string text = keyValuePair.Value;
												string id = zoneView.Zone.Id;
												string text3 = this.ReadZone(id, 800);
												if (text3 == null || !text3.Equals(zoneView.OrigHex, StringComparison.OrdinalIgnoreCase))
												{
													this.L(string.Concat(new string[]
													{
														"GUARD ",
														id,
														": ",
														text3,
														" != ",
														zoneView.OrigHex,
														" - ohitettu."
													}));
												}
												else if (!this.Tx((((id).Length == 2) ? "3B" : "2E") + id + text, 2000).Contains((((id).Length == 2) ? "7B" : "6E") + id))
												{
													this.L("VIRHE " + id + ": kirjoitus hyl\u00e4tty.");
												}
												else
												{
													string text4 = this.ReadZone(id, 800);
													bool flag = text4 != null && text4.Equals(text, StringComparison.OrdinalIgnoreCase);
													if (this.curMod.Trace == "yes" || this.curMod.Trace == "try")
													{
														string text5 = this.Tx("2E2901FD000000010101", 2000);
														if (text5.Contains("6E2901"))
														{
															this.L("2901 " + id + " OK.");
														}
														else if (this.curMod.Trace == "yes")
														{
															this.L(string.Concat(new string[]
															{
																"WARN 2901 ",
																id,
																" (",
																text5,
																")."
															}));
														}
													}
													if (flag)
													{
														zoneView.OrigHex = text;
														zoneView.Bytes = Hex.ToBytes(text);
														num2++;
														this.L("OK " + id + " -> " + text);
													}
													else
													{
														this.L("TARKISTA " + id + ": " + text4);
													}
												}
											}
											this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
											this.Render();
											MessageBox.Show(string.Concat(new object[]
											{
												(this.lang == "fi") ? "Kirjoitettu " : "Written ",
												num2,
												"/",
												list.Count,
												(this.lang == "fi") ? " zonea." : " zones."
											}));
										}
									}
								}
							}
							catch (Exception ex)
							{
								try
								{
									this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
								}
								catch
								{
								}
								this.L("VIRHE/ERROR write: " + ex.Message);
							}
						}
					}
				}
			}
		}

		// Token: 0x06000093 RID: 147 RVA: 0x0000B620 File Offset: 0x00009820
		private void RestoreBackup()
		{
			if (!this.busy)
			{
				if (this.trans == null || !this.trans.IsOpen)
				{
					MessageBox.Show((this.lang == "fi") ? "Ei yhteytt\u00e4." : "Not connected.");
				}
				else
				{
					string fileName;
					using (OpenFileDialog openFileDialog = new OpenFileDialog())
					{
						string text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup");
						if (Directory.Exists(text))
						{
							openFileDialog.InitialDirectory = text;
						}
						openFileDialog.Filter = "Backup CSV|*.csv|All files|*.*";
						if (openFileDialog.ShowDialog(this) != DialogResult.OK)
						{
							return;
						}
						fileName = openFileDialog.FileName;
					}
					string ecuId = null;
					List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
					try
					{
						foreach (string text2 in File.ReadAllLines(fileName))
						{
							string text3 = text2.Trim();
							if (text3.Length != 0)
							{
								string[] array2 = text3.Split(new char[]
								{
									';'
								});
								if (array2.Length >= 2 && (array2[0].Equals("ECU", StringComparison.OrdinalIgnoreCase) || array2[0].Equals("Module", StringComparison.OrdinalIgnoreCase)))
								{
									ecuId = array2[1].Split(new char[]
									{
										' '
									})[0].Trim();
								}
								else if (array2.Length == 2 && Regex.IsMatch(array2[0].Trim(), "^[0-9A-Fa-f]{3,4}$") && Regex.IsMatch(array2[1].Trim(), "^[0-9A-Fa-f]+$"))
								{
									rows.Add(new KeyValuePair<string, string>(array2[0].Trim().ToUpper(), array2[1].Trim().ToUpper()));
								}
							}
						}
					}
					catch (Exception ex)
					{
						MessageBox.Show("CSV: " + ex.Message);
						return;
					}
					if (ecuId == null || rows.Count == 0)
					{
						MessageBox.Show((this.lang == "fi") ? "Backup-tiedostoa ei tunnistettu." : "Not a recognised backup file.");
					}
					else
					{
						Module mod = Data.Modules.FirstOrDefault((Module m) => m.Id.Equals(ecuId, StringComparison.OrdinalIgnoreCase));
						if (mod == null)
						{
							MessageBox.Show(((this.lang == "fi") ? "Tuntematon ECU: " : "Unknown ECU: ") + ecuId);
						}
						else
						{
							int num = this.shown.IndexOf(mod);
							if (num < 0)
							{
								this.PopulateModules(Data.Modules);
								num = this.shown.IndexOf(mod);
							}
							if (num >= 0)
							{
								this.cbModule.SelectedIndex = num;
							}
							this.curMod = mod;
							this.busy = true;
							this.SetButtons(false);
							new Thread(delegate()
							{
								int num2 = 0;
								int total = 0;
								try
								{
									this.trans.SelectEcu(mod.Tx, mod.Rx);
									if (!this.Tx((mod.Id == "BSI2004") ? "10C0" : "1003", 1200).Contains((mod.Id == "BSI2004") ? "50C0" : "5003"))
									{
										this.L("VIRHE/ERROR 1003.");
									}
									else
									{
										List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
										List<KeyValuePair<string, string>> list2 = new List<KeyValuePair<string, string>>();
										int num3 = 0;
										foreach (KeyValuePair<string, string> item in rows)
										{
											num3++;
											this.Status(string.Concat(new object[]
											{
												(this.lang == "fi") ? "Luetaan " : "Reading ",
												num3,
												"/",
												rows.Count,
												"  ",
												item.Key
											}));
											string text4 = this.ReadZone(item.Key, 800);
											if (text4 != null)
											{
												list.Add(new KeyValuePair<string, string>(item.Key, text4));
												if (!text4.Equals(item.Value, StringComparison.OrdinalIgnoreCase))
												{
													list2.Add(item);
												}
											}
										}
										try
										{
											string str = this.SaveBackupRows(mod, list);
											this.L(((this.lang == "fi") ? "Turvakopio nyky-tilasta: " : "Safety backup of current state: ") + str);
										}
										catch
										{
										}
										if (list2.Count == 0)
										{
											this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
											this.BeginInvoke(new Action(delegate()
											{
												MessageBox.Show((this.lang == "fi") ? "Kaikki zonet jo backupin mukaiset." : "All zones already match the backup.");
											}));
										}
										else
										{
											total = list2.Count;
											bool proceed = false;
											StringBuilder prev = new StringBuilder();
											if (!mod.Verified)
											{
												prev.AppendLine((this.lang == "fi") ? "! ECU:n avainta ei varmistettu - kirjoitus voi ep\u00e4onnistua.\n" : "! ECU key not car-verified - write may fail.\n");
											}
											foreach (KeyValuePair<string, string> keyValuePair in list2)
											{
												string text5 = "";
												foreach (KeyValuePair<string, string> keyValuePair2 in list)
												{
													if (keyValuePair2.Key == keyValuePair.Key)
													{
														text5 = keyValuePair2.Value;
														break;
													}
												}
												prev.AppendLine(string.Concat(new string[]
												{
													keyValuePair.Key,
													":  ",
													text5,
													"  ->  ",
													keyValuePair.Value
												}));
											}
											this.Invoke(new Action(delegate()
											{
												proceed = (MessageBox.Show(((this.lang == "fi") ? string.Concat(new object[]
												{
													"Palautetaanko ",
													total,
													" zonea ECU:hun ",
													mod.Id,
													"?\n\n"
												}) : string.Concat(new object[]
												{
													"Restore ",
													total,
													" zones to ",
													mod.Id,
													"?\n\n"
												})) + prev, (this.lang == "fi") ? "Palauta" : "Restore", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.Yes);
											}));
											if (!proceed)
											{
												this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
											}
											else
											{
												string text6 = this.Tx("2703", 800);
												int num4 = text6.IndexOf("6703");
												if (num4 < 0)
												{
													this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
													this.L("VIRHE/ERROR: no seed.");
												}
												else
												{
													byte[] chg = Hex.ToBytes(text6.Substring(num4 + 4, 8));
													string str2 = Sk.Key(mod.Key, chg);
													if (!this.Tx("2704" + str2, 800).Contains("6704"))
													{
														this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
														this.L("VIRHE/ERROR: unlock rejected (wrong/unknown key?).");
													}
													else
													{
														foreach (KeyValuePair<string, string> keyValuePair in list2)
														{
															if (!this.Tx((((keyValuePair.Key).Length == 2) ? "3B" : "2E") + keyValuePair.Key + keyValuePair.Value, 2000).Contains((((keyValuePair.Key).Length == 2) ? "7B" : "6E") + keyValuePair.Key))
															{
																this.L("VIRHE " + keyValuePair.Key + ": write rejected.");
															}
															else
															{
																string text7 = this.ReadZone(keyValuePair.Key, 800);
																if (text7 != null && text7.Equals(keyValuePair.Value, StringComparison.OrdinalIgnoreCase))
																{
																	num2++;
																	this.L("OK " + keyValuePair.Key + " -> " + keyValuePair.Value);
																}
																else
																{
																	this.L("TARKISTA " + keyValuePair.Key + ": " + text7);
																}
																if (mod.Trace == "yes" || mod.Trace == "try")
																{
																	if (this.Tx("2E2901FD000000010101", 2000).Contains("6E2901"))
																	{
																		this.L("2901 " + keyValuePair.Key + " OK.");
																	}
																}
															}
														}
														this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
													}
												}
											}
										}
									}
								}
								catch (Exception ex2)
								{
									try
									{
										this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
									}
									catch
									{
									}
									this.L("VIRHE/ERROR restore: " + ex2.Message);
								}
								finally
								{
									int okF = num2;
									int totF = total;
									this.BeginInvoke(new Action(delegate()
									{
										this.busy = false;
										this.SetButtons(true);
										this.lblStatus.Text = ((this.trans != null && this.trans.IsOpen) ? (this.T("connto") + (this.Vci() ? "Diagbox VCI" : this.cbPort.SelectedItem)) : this.T("noconn"));
										this.lblStatus.ForeColor = ((this.trans != null && this.trans.IsOpen) ? Theme.Green : Theme.Red);
										this.views = new List<ZoneView>();
										this.Render();
										this.btnWrite.Enabled = false;
										if (totF > 0)
										{
											MessageBox.Show(string.Concat(new object[]
											{
												(this.lang == "fi") ? "Palautettu " : "Restored ",
												okF,
												"/",
												totF,
												(this.lang == "fi") ? " zonea." : " zones."
											}));
										}
									}));
								}
							})
							{
								IsBackground = true
							}.Start();
						}
					}
				}
			}
		}

		// Token: 0x06000094 RID: 148 RVA: 0x0000BA04 File Offset: 0x00009C04
		private void DiagboxRip()
		{
			string text = "C:\\AWRoot";
			string path = Path.Combine(text, "dtrd\\comm\\XMLTLCD");
			if (!Directory.Exists(text))
			{
				MessageBox.Show((this.lang == "fi") ? "Ei Diagbox-asennusta (C:\\AWRoot puuttuu)." : "No Diagbox installation (C:\\AWRoot not found).");
			}
			else if (!Directory.Exists(path))
			{
				MessageBox.Show((this.lang == "fi") ? "Diagbox l\u00f6ytyi, mutta telekoodausdataa ei (XMLTLCD puuttuu)." : "Diagbox found, but no telecoding data (XMLTLCD missing).");
			}
			else
			{
				try
				{
					Dictionary<string, string> dictionary = new Dictionary<string, string>();
					int num = 0;
					int num2 = 0;
					foreach (string path2 in Directory.GetFiles(path, "A51_*.xml"))
					{
						num++;
						string key = Path.GetFileNameWithoutExtension(path2).Substring(4);
						List<string> list = new List<string>();
						foreach (object obj in Regex.Matches(File.ReadAllText(path2), "TlcdParameter[^>]*Name=\"([^\"]+)\""))
						{
							Match match = (Match)obj;
							string value = match.Groups[1].Value;
							if (value.Length > 0 && !list.Contains(value))
							{
								list.Add(value);
							}
						}
						num2 += list.Count;
						if (list.Count > 0)
						{
							dictionary[key] = string.Join("; ", list.GetRange(0, Math.Min(list.Count, 150)));
						}
					}
					StringBuilder stringBuilder = new StringBuilder();
					stringBuilder.Append("{\"zones\":{");
					bool flag = true;
					foreach (KeyValuePair<string, string> keyValuePair in dictionary)
					{
						if (!flag)
						{
							stringBuilder.Append(",");
						}
						flag = false;
						stringBuilder.Append(string.Concat(new string[]
						{
							"\"",
							MainForm.JsEsc(keyValuePair.Key),
							"\":\"",
							MainForm.JsEsc(keyValuePair.Value),
							"\""
						}));
					}
					stringBuilder.Append("}}");
					string path3 = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagbox_overlay.json");
					File.WriteAllText(path3, stringBuilder.ToString(), new UTF8Encoding(false));
					Data.LoadOverlay(path3);
					this.L(string.Concat(new object[]
					{
						(this.lang == "fi") ? "Diagbox-rippaus: " : "Diagbox rip: ",
						num,
						(this.lang == "fi") ? " ECU, " : " ECUs, ",
						num2,
						" params -> diagbox_overlay.json"
					}));
					MessageBox.Show((this.lang == "fi") ? string.Concat(new object[]
					{
						"Rippaus valmis.\n",
						num,
						" ECU-tiedostoa, ",
						num2,
						" telekoodausparametria luettu Diagboxista.\n\nDTC-selitteet (5717 koodia) tulevat jo avoimesta dtc.json:sta.\nHUOM: ripattu data on paikallista (Diagbox-omaa) - ei julkaista."
					}) : string.Concat(new object[]
					{
						"Rip done.\n",
						num,
						" ECU files, ",
						num2,
						" telecoding params read from Diagbox.\n\nDTC descriptions (5717 codes) already come from open dtc.json.\nNote: ripped data is local (Diagbox-proprietary) - not published."
					}), "Diagbox DataRipper", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
					this.Render();
				}
				catch (Exception ex)
				{
					MessageBox.Show("Rip: " + ex.Message);
				}
			}
		}

		// Token: 0x06000095 RID: 149 RVA: 0x0000BE54 File Offset: 0x0000A054
		private static string JsEsc(string s)
		{
			return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ").Replace("\t", " ");
		}

		// Token: 0x06000096 RID: 150 RVA: 0x0000BEBC File Offset: 0x0000A0BC
		private string HintFor(Module m)
		{
			string result;
			if (m == null || Data.ZoneHints.Count == 0)
			{
				result = null;
			}
			else
			{
				string text = m.Id.ToUpper();
				string text2 = (m.Name ?? "").ToUpper();
				foreach (KeyValuePair<string, string> keyValuePair in Data.ZoneHints)
				{
					string text3 = keyValuePair.Key.ToUpper();
					if (text3.Length >= 2 && (text == text3 || text.Contains(text3) || text3.Contains(text) || text2.Contains(text3)))
					{
						return keyValuePair.Value;
					}
				}
				result = null;
			}
			return result;
		}

		// Token: 0x06000097 RID: 151 RVA: 0x0000BFB4 File Offset: 0x0000A1B4
		private List<string> DtcDecode(string r)
		{
			List<string> list = new List<string>();
			r = (r ?? "").Replace(" ", "");
			int kwpIdx = r.IndexOf("57");
if (r.IndexOf("5902") < 0 && kwpIdx >= 0 && r.Length >= kwpIdx + 4)
{
int count = 0;
try { count = Convert.ToInt32(r.Substring(kwpIdx + 2, 2), 16); } catch { }
string kwpData = r.Substring(kwpIdx + 4);
for (int ci = 0; ci < count && (ci * 6 + 6) <= kwpData.Length; ci++)
{
int ki = ci * 6;
string rawCode = kwpData.Substring(ki, 4);
string stByte = kwpData.Substring(ki + 4, 2);
string shortCode = rawCode.Substring(1);
string desc = "";
if (Data.Dtc != null)
{
if (!Data.Dtc.TryGetValue(shortCode, out desc))
Data.Dtc.TryGetValue(rawCode, out desc);
}
list.Add(rawCode + " (" + shortCode + ") [" + stByte + "]" + (string.IsNullOrEmpty(desc) ? "" : (" - " + desc)));
}
return list;
}
int num = r.IndexOf("5902");
			List<string> result;
			if (num < 0)
			{
				result = list;
			}
			else
			{
				string text = r.Substring(num + 4);
				if (text.Length < 2)
				{
					result = list;
				}
				else
				{
					text = text.Substring(2);
					int num2 = 0;
					while (num2 + 8 <= text.Length)
					{
						string text2 = text.Substring(num2, 6);
						string text3 = text.Substring(num2 + 6, 2);
						int num3;
						try
						{
							num3 = Convert.ToInt32(text2.Substring(0, 2), 16);
						}
						catch
						{
							break;
						}
						string text4 = "PCBU"[num3 >> 6 & 3].ToString() + (num3 >> 4 & 3).ToString() + (num3 & 15).ToString("X") + text2.Substring(2, 2);
						string text5;
						if (!Data.Dtc.TryGetValue(text4, out text5))
						{
							text5 = "";
						}
						list.Add(string.Concat(new string[]
						{
							text4,
							(text5 != "") ? ("  " + text5) : "",
							"   [FTB=",
							text2.Substring(4, 2),
							" status=",
							text3,
							"]"
						}));
						num2 += 8;
					}
					result = list;
				}
			}
			return result;
		}

		// Token: 0x06000098 RID: 152 RVA: 0x0000C178 File Offset: 0x0000A378
		private void ViewBackup()
{
    string fileName;
    using (OpenFileDialog ofd = new OpenFileDialog())
    {
        string bdir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup");
        if (Directory.Exists(bdir)) { ofd.InitialDirectory = bdir; }
        ofd.Filter = "Backup CSV|*.csv|All files|*.*";
        if (ofd.ShowDialog(this) != DialogResult.OK) { return; }
        fileName = ofd.FileName;
    }
    string ecuId = null; string vin = ""; string ts = "";
    List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
    try
    {
        foreach (string ln in File.ReadAllLines(fileName))
        {
            string t = ln.Trim();
            if (t.Length == 0) { continue; }
            string[] a = t.Split(new char[] { ';' });
            if (a.Length >= 2 && (a[0].Equals("ECU", StringComparison.OrdinalIgnoreCase) || a[0].Equals("Module", StringComparison.OrdinalIgnoreCase))) { ecuId = a[1].Split(new char[] { ' ' })[0].Trim(); }
            else if (a.Length >= 2 && a[0].Equals("VIN", StringComparison.OrdinalIgnoreCase)) { vin = a[1].Trim(); }
            else if (a.Length >= 2 && a[0].Equals("Timestamp", StringComparison.OrdinalIgnoreCase)) { ts = a[1].Trim(); }
            else if (a.Length == 2 && Regex.IsMatch(a[0].Trim(), "^[0-9A-Fa-f]{2,4}$") && Regex.IsMatch(a[1].Trim(), "^[0-9A-Fa-f]+$")) { rows.Add(new KeyValuePair<string, string>(a[0].Trim().ToUpper(), a[1].Trim().ToUpper())); }
        }
    }
    catch (Exception ex) { MessageBox.Show("CSV: " + ex.Message); return; }
    if (ecuId == null || rows.Count == 0) { MessageBox.Show((this.lang == "fi") ? "Backup-tiedostoa ei tunnistettu." : "Not a recognised backup file."); return; }
    Module mod = Data.Modules.FirstOrDefault((Module m) => m.Id.Equals(ecuId, StringComparison.OrdinalIgnoreCase));
    bool fi = this.lang == "fi";
    int off = (mod != null) ? mod.ByteOffset : 0;
    List<string> lines = new List<string>();
    lines.Add("ECU: " + ecuId + ((mod != null) ? ("  (" + mod.Name + ")") : (fi ? "  (tuntematon)" : "  (unknown)")));
    if (vin != "") { lines.Add("VIN: " + vin); }
    if (ts != "") { lines.Add((fi ? "Aika: " : "Time: ") + ts); }
    lines.Add("");
    foreach (KeyValuePair<string, string> row in rows)
    {
        string zid = row.Key;
        byte[] bytes = Hex.ToBytes(row.Value);
        Zone zone = (mod != null) ? mod.Zones.FirstOrDefault((Zone z) => z.Id.Equals(zid, StringComparison.OrdinalIgnoreCase)) : null;
        List<Field> flds = new List<Field>();
        if (zone != null) { foreach (Field f in zone.Fields) { if (f.ByteStart + off < bytes.Length) { flds.Add(f); } } }
        if (flds.Count > 0)
        {
            lines.Add(zid + "  " + zone.Name);
            foreach (Field f in flds)
            {
                int mask = Convert.ToInt32(f.Mask, 16);
                int val = (int)bytes[f.ByteStart + off] & mask;
                string disp = "0x" + val.ToString("X2");
                foreach (Opt o in f.Options) { if ((Convert.ToInt32(o.Val, 16) & mask) == val) { disp = o.L(this.lang); break; } }
                lines.Add("    " + f.L(this.lang) + ": " + disp);
            }
        }
        else { lines.Add(zid + ((zone != null) ? ("  " + zone.Name) : (fi ? "  (tuntematon zone)" : "  (unknown zone)")) + "   [raw " + row.Value + "]"); }
    }
    Form form = new Form { Text = (fi ? "Varmuuskopion tarkastelu - " : "Backup viewer - ") + Path.GetFileName(fileName), Width = 740, Height = 580, StartPosition = FormStartPosition.CenterParent, Font = this.Font, BackColor = Theme.Bg, ForeColor = Theme.Text };
    ListBox lb = new ListBox { Dock = DockStyle.Fill, Font = Theme.Mono(9.5f), BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
    foreach (string s in lines) { lb.Items.Add(s); }
    Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.Bg };
    Button save = new Button { Left = 8, Top = 9, Width = 220, Height = 28, Text = (fi ? "Tallenna .txt" : "Save .txt") };
    Theme.Btn(save, Theme.Card, Theme.Text);
    save.Click += delegate(object s2, EventArgs e2)
    {
        using (SaveFileDialog sfd = new SaveFileDialog())
        {
            sfd.Filter = "Text|*.txt";
            sfd.FileName = ecuId + "_" + ((vin != "") ? vin : "config") + ".txt";
            if (sfd.ShowDialog(form) == DialogResult.OK) { try { File.WriteAllText(sfd.FileName, string.Join("\r\n", lines.ToArray()), new UTF8Encoding(false)); } catch (Exception ex2) { MessageBox.Show(ex2.Message); } }
        }
    };
    bar.Controls.Add(save);
    form.Controls.Add(lb);
    form.Controls.Add(bar);
    form.ShowDialog(this);
}
		private class ProfOp { public string Zone; public string Set; public int At = -1; public string Val; }
		private class ProfMod { public string Name; public string Tx; public string Rx; public string Key; public string Trace; public List<ProfOp> Ops = new List<ProfOp>(); }

		private List<ProfMod> Profile(string name)
		{
			List<ProfMod> l = new List<ProfMod>();
			if (name == "ACC")
			{
				l.Add(new ProfMod { Name = "BSI", Tx = "752", Rx = "652", Key = "B4E0", Trace = "yes", Ops = { new ProfOp { Zone = "2348", At = 0, Val = "05" } } });
				l.Add(new ProfMod { Name = "ARTIV radar", Tx = "6B6", Rx = "696", Key = "EFCA", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 1, Val = "F7" } } });
				l.Add(new ProfMod { Name = "CVM camera", Tx = "74A", Rx = "64A", Key = "E2E5", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 2, Val = "8D" } } });
				l.Add(new ProfMod { Name = "VCU", Tx = "6A2", Rx = "682", Key = "0107", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 3, Val = "6F" } } });
				l.Add(new ProfMod { Name = "ESP", Tx = "6AD", Rx = "68D", Key = "BC93", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 33, Val = "EF" } } });
			}
			else if (name == "PARK")
			{
				l.Add(new ProfMod { Name = "BSI", Tx = "752", Rx = "652", Key = "B4E0", Trace = "yes", Ops = { new ProfOp { Zone = "2300", At = 0, Val = "37" }, new ProfOp { Zone = "2470", At = 0, Val = "01" } } });
				l.Add(new ProfMod { Name = "AAS park assist", Tx = "75D", Rx = "65D", Key = "B6F0", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 0, Val = "13" } } });
				l.Add(new ProfMod { Name = "ESP", Tx = "6AD", Rx = "68D", Key = "BC93", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 34, Val = "9F" } } });
				l.Add(new ProfMod { Name = "DAE steering", Tx = "6B5", Rx = "695", Key = "2105", Trace = "try", Ops = { new ProfOp { Zone = "2101", At = 1, Val = "CE" } } });
				l.Add(new ProfMod { Name = "RCC infotainment", Tx = "764", Rx = "664", Key = "F107", Trace = "try", Ops = { new ProfOp { Zone = "210C", Set = "A010" }, new ProfOp { Zone = "2133", Set = "0E0100000E04" } } });
			}
			return l;
		}

		private string ReadZoneRaw(ITransport tr, string zone)
		{
			bool kwp = (zone.Length == 2); string r = tr.Send((kwp ? "21" : "22") + zone, new string[] { (kwp ? "61" : "62") + zone, kwp ? "7F21" : "7F22" }, 900);
			int i = r.IndexOf((kwp ? "61" : "62") + zone);
			if (i < 0) { return null; }
			return r.Substring(i + 2 + zone.Length);
		}

		private void ApplyProfile(string name, string label)
		{
			if (this.busy) { return; }
			if (this.trans == null || !this.trans.IsOpen) { MessageBox.Show((this.lang == "fi") ? "Ei yhteytt\u00e4." : "Not connected."); return; }
			bool fi = this.lang == "fi";
			List<ProfMod> prof = this.Profile(name);
			StringBuilder sb = new StringBuilder();
			sb.AppendLine((fi ? "Profiili: " : "Profile: ") + label);
			sb.AppendLine(fi ? "Kirjoitetaan N\u00c4IHIN moduuleihin kerralla:" : "Writes to ALL these modules at once:");
			sb.AppendLine("");
			foreach (ProfMod m in prof)
			{
				sb.AppendLine(m.Name + "  (" + m.Tx + ":" + m.Rx + ")");
				foreach (ProfOp op in m.Ops)
				{
					sb.AppendLine("    " + op.Zone + ((op.Set != null) ? (" = " + op.Set) : ("  byte" + op.At + " = " + op.Val)));
				}
			}
			sb.AppendLine("");
			sb.AppendLine(fi ? "Varmuuskopio otetaan ennen jokaista kirjoitusta. Varmista ett\u00e4 kyseess\u00e4 on OIKEA auto (e-2008). Jatketaanko?" : "A backup is taken before each write. Make sure this is the RIGHT car (e-2008). Continue?");
			if (MessageBox.Show(sb.ToString(), (fi ? "Kirjoita profiili" : "Write profile"), MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { return; }
			this.busy = true;
			this.SetButtons(false);
			ITransport tr = this.trans;
			new Thread(delegate()
			{
				int okMods = 0; int okWrites = 0; int total = 0;
				foreach (ProfMod m in prof)
				{
					try
					{
						tr.SelectEcu(m.Tx, m.Rx);
						try
						{
							List<KeyValuePair<string, string>> brows = new List<KeyValuePair<string, string>>();
							foreach (ProfOp op in m.Ops) { string cur0 = this.ReadZoneRaw(tr, op.Zone); if (cur0 != null) { brows.Add(new KeyValuePair<string, string>(op.Zone, cur0)); } }
							if (brows.Count > 0) { Module bm = new Module { Id = m.Name.Replace(" ", "_"), Name = m.Name, Tx = m.Tx, Rx = m.Rx, Key = m.Key, Trace = m.Trace, ByteOffset = 0 }; string bp = this.SaveBackupRows(bm, brows); this.L(((fi) ? "Varmuuskopio: " : "Backup: ") + bp); }
						}
						catch (Exception be) { this.L("WARN backup " + m.Name + ": " + be.Message); }
						if (!tr.Send("1003", null, 1200).Contains("5003")) { this.L("VIRHE/ERROR 1003 (" + m.Name + ") - ohitettu."); continue; }
						string sr = tr.Send("2703", null, 900); int si = sr.IndexOf("6703");
						if (si < 0) { tr.Send("1001", null, 300); this.L("VIRHE/ERROR: no seed (" + m.Name + ")."); continue; }
						string keyAns = Sk.Key(m.Key, Hex.ToBytes(sr.Substring(si + 4, 8)));
						if (!tr.Send("2704" + keyAns, null, 900).Contains("6704")) { tr.Send("1001", null, 300); this.L("VIRHE/ERROR: unlock rejected (" + m.Name + ") - ohitettu."); continue; }
						foreach (ProfOp op in m.Ops)
						{
							total++;
							string cur = this.ReadZoneRaw(tr, op.Zone);
							if (cur == null) { this.L("VIRHE " + m.Name + " " + op.Zone + ": luku ep\u00e4onnistui."); continue; }
							string newHex;
							if (op.Set != null) { newHex = op.Set.ToUpper(); }
							else { byte[] b = Hex.ToBytes(cur); if (op.At < b.Length) { b[op.At] = Convert.ToByte(op.Val, 16); } else { this.L("VIRHE " + m.Name + " " + op.Zone + ": byte" + op.At + " ei mahdu."); continue; } newHex = Hex.ToStr(b, b.Length); }
							if (newHex.Equals(cur, StringComparison.OrdinalIgnoreCase)) { this.L("OK " + m.Name + " " + op.Zone + ": jo asetettu."); okWrites++; continue; }
							string w = tr.Send("2E" + op.Zone + newHex, null, 2000);
							if (!w.Contains("6E" + op.Zone)) { this.L("VIRHE " + m.Name + " " + op.Zone + ": kirjoitus hyl\u00e4tty (" + w + ")."); continue; }
							string re = this.ReadZoneRaw(tr, op.Zone);
							if (re != null && re.StartsWith(newHex, StringComparison.OrdinalIgnoreCase)) { okWrites++; this.L("OK " + m.Name + " " + op.Zone + " -> " + newHex); } else { this.L("TARKISTA " + m.Name + " " + op.Zone + ": " + re); }
							if (m.Trace == "yes" || m.Trace == "try") { string ts = tr.Send("2E2901FD000000010101", null, 2000); if (ts.Contains("6E2901")) { this.L("2901 " + m.Name + " OK."); } else if (m.Trace == "yes") { this.L("WARN 2901 " + m.Name + " (" + ts + ")."); } }
						}
						tr.Send("1001", null, 300);
						okMods++;
					}
					catch (Exception ex)
					{
						try { tr.Send("1001", null, 300); } catch { }
						this.L("VIRHE/ERROR " + m.Name + ": " + ex.Message);
					}
				}
				int fm = okMods; int fw = okWrites; int ft = total;
				this.BeginInvoke(new Action(delegate()
				{
					this.busy = false;
					this.SetButtons(true);
					MessageBox.Show((fi ? ("Profiili kirjoitettu.\nModuuleja: " + fm + "/" + prof.Count + "\nZoneja: " + fw + "/" + ft + "\n\nTee virtasykli (sytytys pois ~30 s) ja aja Tyhjenn\u00e4 DTC jokaiselle moduulille.") : ("Profile written.\nModules: " + fm + "/" + prof.Count + "\nZones: " + fw + "/" + ft + "\n\nDo a power cycle and clear DTCs on each module.")), (fi ? "Valmis" : "Done"));
				}));
			})
			{ IsBackground = true }.Start();
		}
		private Dictionary<string, string> ParseBackup(string file, out string ecuId)
		{
			ecuId = null;
			Dictionary<string, string> d = new Dictionary<string, string>();
			foreach (string ln in File.ReadAllLines(file))
			{
				string t = ln.Trim();
				if (t.Length == 0) { continue; }
				string[] a = t.Split(new char[] { ';' });
				if (a.Length >= 2 && (a[0].Equals("ECU", StringComparison.OrdinalIgnoreCase) || a[0].Equals("Module", StringComparison.OrdinalIgnoreCase))) { ecuId = a[1].Split(new char[] { ' ' })[0].Trim(); }
				else if (a.Length == 2 && Regex.IsMatch(a[0].Trim(), "^[0-9A-Fa-f]{2,4}$") && Regex.IsMatch(a[1].Trim(), "^[0-9A-Fa-f]+$")) { d[a[0].Trim().ToUpper()] = a[1].Trim().ToUpper(); }
			}
			return d;
		}

		private string DecodeField(Module mod, Field f, byte[] bytes)
		{
			int off = (mod != null) ? mod.ByteOffset : 0;
			if (f.ByteStart + off >= bytes.Length) { return null; }
			int mask = Convert.ToInt32(f.Mask, 16);
			int val = (int)bytes[f.ByteStart + off] & mask;
			foreach (Opt o in f.Options) { if ((Convert.ToInt32(o.Val, 16) & mask) == val) { return o.L(this.lang); } }
			return "0x" + val.ToString("X2");
		}

		private void ShowReport(string title, List<string> lines, string saveName)
		{
			bool fi = this.lang == "fi";
			Form form = new Form { Text = title, Width = 760, Height = 600, StartPosition = FormStartPosition.CenterParent, Font = this.Font, BackColor = Theme.Bg, ForeColor = Theme.Text };
			ListBox lb = new ListBox { Dock = DockStyle.Fill, Font = Theme.Mono(9.5f), BackColor = Theme.Panel, ForeColor = Theme.Text, BorderStyle = BorderStyle.None, HorizontalScrollbar = true };
			foreach (string s in lines) { lb.Items.Add(s); }
			Panel bar = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Theme.Bg };
			Button save = new Button { Left = 8, Top = 9, Width = 220, Height = 28, Text = (fi ? "Tallenna .txt" : "Save .txt") };
			Theme.Btn(save, Theme.Card, Theme.Text);
			save.Click += delegate(object s2, EventArgs e2)
			{
				using (SaveFileDialog sfd = new SaveFileDialog())
				{
					sfd.Filter = "Text|*.txt"; sfd.FileName = saveName;
					if (sfd.ShowDialog(form) == DialogResult.OK) { try { File.WriteAllText(sfd.FileName, string.Join("\r\n", lines.ToArray()), new UTF8Encoding(false)); } catch (Exception ex) { MessageBox.Show(ex.Message); } }
				}
			};
			bar.Controls.Add(save); form.Controls.Add(lb); form.Controls.Add(bar); form.ShowDialog(this);
		}

		private void ExportConfig()
		{
			bool fi = this.lang == "fi";
			if (this.views == null || this.views.Count == 0) { MessageBox.Show(fi ? "Lue ensin." : "Read first."); return; }
			int off = (this.curMod != null) ? this.curMod.ByteOffset : 0;
			List<string> lines = new List<string>();
			lines.Add("ECU: " + ((this.curMod != null) ? (this.curMod.Id + "  (" + this.curMod.Name + ")") : ""));
			if (!string.IsNullOrEmpty(this.vin)) { lines.Add("VIN: " + this.vin); }
			lines.Add((fi ? "Aika: " : "Time: ") + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			lines.Add("");
			foreach (ZoneView v in this.views)
			{
				List<Field> flds = new List<Field>();
				foreach (Field f in v.Zone.Fields) { if (f.ByteStart + off < v.Bytes.Length) { flds.Add(f); } }
				if (flds.Count > 0)
				{
					lines.Add(v.Zone.Id + "  " + v.Zone.Name);
					foreach (Field f in flds) { lines.Add("    " + f.L(this.lang) + ": " + this.DecodeField(this.curMod, f, v.Bytes)); }
				}
				else { lines.Add(v.Zone.Id + "  " + v.Zone.Name + "   [raw " + v.OrigHex + "]"); }
			}
			this.ShowReport((fi ? "Luettu konfiguraatio" : "Read configuration"), lines, ((this.curMod != null) ? this.curMod.Id : "config") + "_" + (string.IsNullOrEmpty(this.vin) ? "config" : this.vin) + ".txt");
		}

		private void CompareBackups()
		{
			bool fi = this.lang == "fi";
			string fa; string fb;
			using (OpenFileDialog o1 = new OpenFileDialog())
			{
				string bdir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backup"); if (Directory.Exists(bdir)) { o1.InitialDirectory = bdir; }
				o1.Title = fi ? "Valitse 1. varmuuskopio (vanha)" : "Select 1st backup (old)"; o1.Filter = "Backup CSV|*.csv|All files|*.*";
				if (o1.ShowDialog(this) != DialogResult.OK) { return; }
				fa = o1.FileName;
			}
			using (OpenFileDialog o2 = new OpenFileDialog())
			{
				o2.InitialDirectory = Path.GetDirectoryName(fa); o2.Title = fi ? "Valitse 2. varmuuskopio (uusi)" : "Select 2nd backup (new)"; o2.Filter = "Backup CSV|*.csv|All files|*.*";
				if (o2.ShowDialog(this) != DialogResult.OK) { return; }
				fb = o2.FileName;
			}
			string ia; string ib;
			Dictionary<string, string> da; Dictionary<string, string> db;
			try { da = this.ParseBackup(fa, out ia); db = this.ParseBackup(fb, out ib); }
			catch (Exception ex) { MessageBox.Show("CSV: " + ex.Message); return; }
			Module mod = Data.Modules.FirstOrDefault((Module m) => m.Id.Equals(ia, StringComparison.OrdinalIgnoreCase));
			List<string> lines = new List<string>();
			lines.Add((fi ? "Vertailu" : "Compare") + ": " + Path.GetFileName(fa) + "  ->  " + Path.GetFileName(fb));
			lines.Add("ECU: " + ia + ((ib != ia) ? (" / " + ib) : "") + ((mod != null) ? ("  (" + mod.Name + ")") : ""));
			lines.Add("");
			SortedSet<string> zones = new SortedSet<string>();
			foreach (string k in da.Keys) { zones.Add(k); }
			foreach (string k in db.Keys) { zones.Add(k); }
			int diffs = 0;
			foreach (string z in zones)
			{
				string va = da.ContainsKey(z) ? da[z] : null;
				string vb = db.ContainsKey(z) ? db[z] : null;
				if (va == vb) { continue; }
				diffs++;
				Zone zdef = (mod != null) ? mod.Zones.FirstOrDefault((Zone zz) => zz.Id.Equals(z, StringComparison.OrdinalIgnoreCase)) : null;
				lines.Add(z + ((zdef != null) ? ("  " + zdef.Name) : "") + ":  " + (va ?? "-") + "  ->  " + (vb ?? "-"));
				if (zdef != null && va != null && vb != null)
				{
					byte[] ba = Hex.ToBytes(va); byte[] bb = Hex.ToBytes(vb);
					foreach (Field f in zdef.Fields)
					{
						string la = this.DecodeField(mod, f, ba); string lb2 = this.DecodeField(mod, f, bb);
						if (la != null && lb2 != null && la != lb2) { lines.Add("      " + f.L(this.lang) + ": " + la + "  ->  " + lb2); }
					}
				}
			}
			if (diffs == 0) { lines.Add(fi ? "Ei eroja." : "No differences."); }
			this.ShowReport((fi ? "Vertaa varmuuskopioita" : "Compare backups"), lines, "compare_" + ia + ".txt");
		}
		private void ShowDtc(string title, List<string> items, string raw)
		{
			Form form = new Form
			{
				Text = title,
				Width = 620,
				Height = 440,
				StartPosition = FormStartPosition.CenterParent,
				Font = this.Font,
				BackColor = Theme.Bg,
				ForeColor = Theme.Text
			};
			ListBox listBox = new ListBox
			{
				Dock = DockStyle.Fill,
				Font = Theme.Mono(9.5f),
				BackColor = Theme.Panel,
				ForeColor = Theme.Text,
				BorderStyle = BorderStyle.None
			};
			if (items.Count == 0)
			{
				listBox.Items.Add((this.lang == "fi") ? "Ei vikakoodeja (puhdas)." : "No DTCs (clean).");
			}
			else
			{
				foreach (string item in items)
				{
					listBox.Items.Add(item);
				}
			}
			Panel panel = new Panel
			{
				Dock = DockStyle.Bottom,
				Height = 50,
				BackColor = Theme.Bg
			};
			panel.Controls.Add(new TextBox
			{
				Left = 8,
				Top = 14,
				Width = 585,
				ReadOnly = true,
				Font = Theme.Mono(8f),
				BackColor = Theme.Card,
				ForeColor = Theme.Sub,
				BorderStyle = BorderStyle.FixedSingle,
				Text = "raw: " + raw,
				Anchor = (AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right)
			});
			form.Controls.Add(listBox);
			form.Controls.Add(panel);
			form.ShowDialog(this);
		}

		// Token: 0x06000099 RID: 153 RVA: 0x0000C398 File Offset: 0x0000A598
		private void DoDtc(bool clear)
		{
			if (this.Need())
			{
				try
				{
					if (this.OpenSession())
					{
						if (clear)
						{
							string text = this.Tx(((this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "14FF00" : "14FFFFFF", 1500);
							this.L(text.Contains("54") ? "DTC cleared (54)." : ("Clear: " + text));
							Thread.Sleep(300);
						}
						string text2 = this.Tx(((this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "17FF00" : "19020C", 1500);
						this.Tx((this.curMod != null && (this.curMod.Id == "BSI2004" || this.curMod.Id == "CMB2004" || this.curMod.Id == "EMF2004" || this.curMod.Id == "RD4_2004" || this.curMod.Id == "CLIM_2004" || this.curMod.Id == "AAS_2004" || this.curMod.Id == "BSM_2004")) ? "1081" : "1001", 300);
						this.L("DTC (" + this.curMod.Id + "): " + text2);
						this.ShowDtc(((this.lang == "fi") ? "Vikakoodit: " : "DTCs: ") + this.curMod.Id, this.DtcDecode(text2), text2);
					}
				}
				catch (Exception ex)
				{
					this.L("VIRHE/ERROR DTC: " + ex.Message);
				}
			}
		}

		// Token: 0x04000040 RID: 64
		private ComboBox cbTrans;

		// Token: 0x04000041 RID: 65
		private ComboBox cbPort;

		// Token: 0x04000042 RID: 66
		private ComboBox cbModule;

		// Token: 0x04000043 RID: 67
		private FlowLayoutPanel pnlFields;

		// Token: 0x04000044 RID: 68
		private TextBox txtLog;

		// Token: 0x04000045 RID: 69
		private Button btnAuto;

		// Token: 0x04000046 RID: 70
		private Button btnConn;

		// Token: 0x04000047 RID: 71
		private Button btnRead;

		// Token: 0x04000048 RID: 72
		private Button btnScan;

		// Token: 0x04000049 RID: 73
		private Button btnWrite;

		// Token: 0x0400004A RID: 74
		private Button btnDtcR;

		// Token: 0x0400004B RID: 75
		private Button btnDtcC;

		// Token: 0x0400004C RID: 76
		private Button btnPin;

		// Token: 0x0400004D RID: 77
		private Button btnLog;

		// Token: 0x0400004E RID: 78
		private Panel logPanel;

		private Panel fieldsHost;

			private TextBox txtSearch;

			private Label lblSearch;

		private CheckBox chkDecoded;

		// Token: 0x0400004F RID: 79
		private Label lblStatus;

		// Token: 0x04000050 RID: 80
		private Label lblMod;

		// Token: 0x04000051 RID: 81
		private PictureBox flagFi;

		// Token: 0x04000052 RID: 82
		private PictureBox flagGb;

		// Token: 0x04000053 RID: 83
		private PictureBox logo;

		// Token: 0x04000054 RID: 84
		private Bitmap fiOn;

		// Token: 0x04000055 RID: 85
		private Bitmap fiOff;

		// Token: 0x04000056 RID: 86
		private Bitmap gbOn;

		// Token: 0x04000057 RID: 87
		private Bitmap gbOff;

		// Token: 0x04000058 RID: 88
		private ITransport trans;

		// Token: 0x04000059 RID: 89
		private Module curMod;

		// Token: 0x0400005A RID: 90
		private List<ZoneView> views = new List<ZoneView>();

		// Token: 0x0400005B RID: 91
		private List<Module> shown = new List<Module>();

		// Token: 0x0400005C RID: 92
		private string lang = "fi";

		// Token: 0x0400005D RID: 93
		private bool busy = false;

		// Token: 0x0400005E RID: 94
		private string vin = "";

		// Token: 0x0400005F RID: 95
		private ToolStripMenuItem menuTools;

		// Token: 0x04000060 RID: 96
		private ToolStripMenuItem menuRestore;

		// Token: 0x04000061 RID: 97
		private ToolStripMenuItem menuRip;

			private ToolStripMenuItem menuView;

			private ToolStripMenuItem menuProfiles;

			private ToolStripMenuItem menuExport;

			private ToolStripMenuItem menuCompare;
	}
	// Token: 0x02000016 RID: 22
	internal static class Program
	{
		// Token: 0x060000AD RID: 173 RVA: 0x0000C4C8 File Offset: 0x0000A6C8
		[STAThread]
		private static void Main()
		{
			Application.EnableVisualStyles(); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException); Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e) { MessageBox.Show(e.Exception.ToString(), "Virhe"); };
			Application.SetCompatibleTextRenderingDefault(false);
			try
			{
				Data.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zones.json"));
			}
			catch (Exception ex)
			{
				MessageBox.Show("zones.json: " + ex.Message, "Stellantis Config Messer", MessageBoxButtons.OK, MessageBoxIcon.Hand);
				return;
			}
			try
			{
				Data.LoadDtc(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dtc.json"));
			}
			catch
			{
			}
			try
			{
				Data.LoadOverlay(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "diagbox_overlay.json"));
			}
			catch
			{
			}
			Application.Run(new MainForm());
		}
	}
}
