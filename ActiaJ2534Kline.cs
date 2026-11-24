using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ZoneTool
{
    public class ActiaJ2534Kline
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruOpenDelegate(IntPtr pName, out uint pDeviceID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruCloseDelegate(uint DeviceID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruConnectDelegate(uint DeviceID, uint ProtocolID, uint Flags, uint BaudRate, out uint pChannelID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruDisconnectDelegate(uint ChannelID);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruIoctlDelegate(uint ChannelID, uint IoctlID, IntPtr pInput, IntPtr pOutput);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruReadMsgsDelegate(uint ChannelID, IntPtr pMsg, ref uint pNumMsgs, uint Timeout);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int PassThruWriteMsgsDelegate(uint ChannelID, IntPtr pMsg, ref uint pNumMsgs, uint Timeout);

        [StructLayout(LayoutKind.Sequential)]
        private struct SByteArray
        {
            public uint NumOfBytes;
            public IntPtr BytePtr;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PassThruMsg
        {
            public uint ProtocolID;
            public uint RxStatus;
            public uint TxFlags;
            public uint Timestamp;
            public uint DataSize;
            public uint ExtraDataIndex;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4128)]
            public byte[] Data;
        }

        public static string Probe406D8ViaJ2534()
        {
            var sb = new StringBuilder("=== ACTIA J2534 PASSTHRU & 1997 406 D8 SKANNERI ===\r\n");
            var dlls = new List<string>();

            foreach (string regPath in new[] { @"SOFTWARE\WOW6432Node\PassThruSupport.04.04", @"SOFTWARE\PassThruSupport.04.04" })
            {
                try
                {
                    using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(regPath))
                    {
                        if (baseKey != null)
                        {
                            foreach (string sub in baseKey.GetSubKeyNames())
                            {
                                using (RegistryKey sk = baseKey.OpenSubKey(sub))
                                {
                                    string fn = sk != null ? sk.GetValue("FunctionLibrary") as string : null;
                                    string nm = sk != null ? sk.GetValue("Name") as string : sub;
                                    sb.AppendLine(string.Format("Rekisteri [{0}]: {1}", nm, fn ?? "(tyhja)"));
                                    if (!string.IsNullOrEmpty(fn) && File.Exists(fn) && !dlls.Contains(fn)) dlls.Add(fn);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) { sb.AppendLine("Reg virhe: " + ex.Message); }
            }

            if (File.Exists(@"C:\AWRoot\drv\PCMToJ2534.ini"))
            {
                sb.AppendLine("\r\n--- PCMToJ2534.ini ---");
                sb.AppendLine(File.ReadAllText(@"C:\AWRoot\drv\PCMToJ2534.ini"));
            }

            foreach (string candidate in new[] { @"C:\AWRoot\drv\PCMToJ2534.dll", @"C:\AWRoot\drv\actia_j2534.dll", @"C:\AWRoot\drv\com_j2534.dll" })
            {
                if (File.Exists(candidate) && !dlls.Contains(candidate)) dlls.Add(candidate);
            }

            if (dlls.Count == 0)
            {
                sb.AppendLine("Yhtaan J2534 DLL-tiedostoa ei loytynyt.");
                return sb.ToString();
            }

            foreach (string dll in dlls)
            {
                sb.AppendLine("\r\nTestataan DLL: " + dll);
                IntPtr hMod = LoadLibrary(dll);
                if (hMod == IntPtr.Zero) { sb.AppendLine("LoadLibrary epaonnistui: " + Marshal.GetLastWin32Error()); continue; }

                IntPtr pOpen = GetProcAddress(hMod, "PassThruOpen");
                IntPtr pClose = GetProcAddress(hMod, "PassThruClose");
                IntPtr pConn = GetProcAddress(hMod, "PassThruConnect");
                IntPtr pDisc = GetProcAddress(hMod, "PassThruDisconnect");
                IntPtr pIoctl = GetProcAddress(hMod, "PassThruIoctl");

                if (pOpen == IntPtr.Zero || pConn == IntPtr.Zero) { sb.AppendLine("PassThru-funktiot puuttuvat."); continue; }

                var fnOpen = (PassThruOpenDelegate)Marshal.GetDelegateForFunctionPointer(pOpen, typeof(PassThruOpenDelegate));
                var fnClose = pClose != IntPtr.Zero ? (PassThruCloseDelegate)Marshal.GetDelegateForFunctionPointer(pClose, typeof(PassThruCloseDelegate)) : null;
                var fnConn = (PassThruConnectDelegate)Marshal.GetDelegateForFunctionPointer(pConn, typeof(PassThruConnectDelegate));
                var fnDisc = pDisc != IntPtr.Zero ? (PassThruDisconnectDelegate)Marshal.GetDelegateForFunctionPointer(pDisc, typeof(PassThruDisconnectDelegate)) : null;
                var fnIoctl = pIoctl != IntPtr.Zero ? (PassThruIoctlDelegate)Marshal.GetDelegateForFunctionPointer(pIoctl, typeof(PassThruIoctlDelegate)) : null;

                uint devId = 0;
                int rcOpen = fnOpen(IntPtr.Zero, out devId);
                sb.AppendLine(string.Format("PassThruOpen -> rc={0}, devId={1}", rcOpen, devId));
                if (rcOpen != 0) continue;

                // ProtocolID: 3 = ISO9141, 4 = ISO14230 (KWP2000)
                foreach (uint protoId in new uint[] { 3, 4 })
                {
                    uint chId = 0;
                    int rcConn = fnConn(devId, protoId, 0, 10400, out chId);
                    sb.AppendLine(string.Format("PassThruConnect(proto={0}, 10400bps) -> rc={1}, chId={2}", protoId, rcConn, chId));
                    if (rcConn == 0 && fnIoctl != null)
                    {
                        foreach (byte ecuAddr in new byte[] { 0x10, 0x13, 0x33, 0x01, 0x28, 0x20, 0x68 })
                        {
                            IntPtr inBuf = Marshal.AllocHGlobal(1);
                            IntPtr outBuf = Marshal.AllocHGlobal(16);
                            IntPtr inArr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SByteArray)));
                            IntPtr outArr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(SByteArray)));
                            try
                            {
                                Marshal.WriteByte(inBuf, ecuAddr);
                                var sIn = new SByteArray { NumOfBytes = 1, BytePtr = inBuf };
                                var sOut = new SByteArray { NumOfBytes = 16, BytePtr = outBuf };
                                Marshal.StructureToPtr(sIn, inArr, false);
                                Marshal.StructureToPtr(sOut, outArr, false);

                                // FIVE_BAUD_INIT = 0x04
                                int rcInit = fnIoctl(chId, 0x04, inArr, outArr);
                                if (rcInit == 0)
                                {
                                    var resOut = (SByteArray)Marshal.PtrToStructure(outArr, typeof(SByteArray));
                                    byte[] kw = new byte[Math.Min((int)resOut.NumOfBytes, 16)];
                                    if (kw.Length > 0) Marshal.Copy(outBuf, kw, 0, kw.Length);
                                    sb.AppendLine(string.Format("   >>> 406 D8 VASTASI! Proto={0} | Osoite 0x{1:X2} | KeyBytes: {2}", protoId, ecuAddr, BitConverter.ToString(kw)));
                                }
                            }
                            finally
                            {
                                Marshal.FreeHGlobal(inBuf); Marshal.FreeHGlobal(outBuf);
                                Marshal.FreeHGlobal(inArr); Marshal.FreeHGlobal(outArr);
                            }
                        }
                        if (fnDisc != null) fnDisc(chId);
                    }
                }
                if (fnClose != null) fnClose(devId);
            }

            string result = sb.ToString();
            try { string ld = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"); Directory.CreateDirectory(ld); File.AppendAllText(Path.Combine(ld, "VCI_Log_" + DateTime.Now.ToString("yyyyMMdd") + ".txt"), result + Environment.NewLine); } catch { }
            return result;
        }
    }
}