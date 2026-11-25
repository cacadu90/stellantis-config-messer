using System;
using System.Collections.Generic;

namespace StellantisConfigMesser.Diagnostics
{
    public static class Mp7Decoder
    {
                public class EcuPinInfo { public string SystemName; public string Pp2000Code; public int ObdPin; public int BaudRate; public byte InitAddress; }
        public class ActuatorTest { public string Pp2000Id; public string Name; public byte LocalId; public byte[] KwpRequest; public string Notes; }
        public static readonly List<EcuPinInfo> VehicleEcuPins = new List<EcuPinInfo> {
            new EcuPinInfo { SystemName = "Moottori Bosch MP 7.0 (ES9J4 V6)", Pp2000Code = "INJ / MP7.0", ObdPin = 6,  BaudRate = 10400, InitAddress = 0x11 },
            new EcuPinInfo { SystemName = "ABS Bosch 5 / 5.3",               Pp2000Code = "ABR / ABR5",  ObdPin = 7,  BaudRate = 10400, InitAddress = 0x28 },
            new EcuPinInfo { SystemName = "Turvatyynyt / Airbag (AKF)",      Pp2000Code = "SAC / AKF",   ObdPin = 8,  BaudRate = 9600,  InitAddress = 0x2C },
            new EcuPinInfo { SystemName = "Automaattivaihteisto ZF 4HP20",   Pp2000Code = "BVA / 4HP20", ObdPin = 11, BaudRate = 10400, InitAddress = 0x18 }
        };
        public static readonly List<ActuatorTest> ActuatorTests = new List<ActuatorTest> {
            new ActuatorTest { Pp2000Id = "ACTALLUMAGE1", Name = "Sytytyspuola 1 (sylinterit 1 & 4)", LocalId = 0x01, KwpRequest = new byte[] { 0x30, 0x01, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTALLUMAGE2", Name = "Sytytyspuola 2 (sylinterit 2 & 5)", LocalId = 0x02, KwpRequest = new byte[] { 0x30, 0x02, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTALLUMAGE3", Name = "Sytytyspuola 3 (sylinterit 3 & 6)", LocalId = 0x03, KwpRequest = new byte[] { 0x30, 0x03, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTINJ_1_5",   Name = "Polttoainesuuttimet 1 & 5",         LocalId = 0x11, KwpRequest = new byte[] { 0x30, 0x11, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTINJ_2_6",   Name = "Polttoainesuuttimet 2 & 6",         LocalId = 0x12, KwpRequest = new byte[] { 0x30, 0x12, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTINJ_3_4",   Name = "Polttoainesuuttimet 3 & 4",         LocalId = 0x13, KwpRequest = new byte[] { 0x30, 0x13, 0x07, 0x01 }, Notes = "Moottori seis, virta paalla" },
            new ActuatorTest { Pp2000Id = "ACTPOMPE",     Name = "Polttoainepumpun rele",             LocalId = 0x20, KwpRequest = new byte[] { 0x30, 0x20, 0x07, 0x01 }, Notes = "Aktivoi pumpun releen" },
            new ActuatorTest { Pp2000Id = "ACTRALENTI",   Name = "Tyhjakayntisaadin (askelmoottori)", LocalId = 0x21, KwpRequest = new byte[] { 0x30, 0x21, 0x07, 0x01 }, Notes = "Ajaa tyhjakayntiventtiilia" },
            new ActuatorTest { Pp2000Id = "ACTPURGE",     Name = "Aktiivihiilisuodattimen venttiili", LocalId = 0x22, KwpRequest = new byte[] { 0x30, 0x22, 0x07, 0x01 }, Notes = "EVAP-huuhteluventtiili" },
            new ActuatorTest { Pp2000Id = "ACTCHAUFSOND", Name = "Lambda-antureiden lammitys",        LocalId = 0x25, KwpRequest = new byte[] { 0x30, 0x25, 0x07, 0x01 }, Notes = "Etu- ja takalambdan lammitys" }
        };
        public static byte[] BuildStopActuatorCommand(byte localId) { return new byte[] { 0x30, localId, 0x00 }; }
        public class LiveParameter
        {
            public byte LocalId { get; set; }
            public string NameFi { get; set; }
            public string ValueFormatted { get; set; }
            public double RawNumeric { get; set; }
            public string Unit { get; set; }
        }

        public class FaultCode
        {
            public ushort Code { get; set; }
            public string HexCode { get { return string.Format("0x{0:X4}", Code); } }
            public string StatusFi { get; set; }
            public string DescriptionFi { get; set; }
        }

        public static LiveParameter DecodeParameter(byte[] response)
        {
            if (response == null || response.Length < 3 || response[0] != 0x61)
                return null;

            byte pid = response[1];
            byte b1 = response[2];
            byte b2 = response.Length > 3 ? response[3] : (byte)0;
            ushort word = (ushort)((b1 << 8) | b2);

            switch (pid)
            {
                case 0x01: case 0x4A: case 0x4E: case 0x4F:
                    double rpm = (response.Length >= 4 && word > 255) ? word * 0.25 : b1 * 40.0;
                    return Make(pid, "Moottorin kierrosluku", string.Format("{0:0} r/min", rpm), rpm, "r/min");
                case 0x19: return Make(pid, "Jäähdytysnesteen lämpötila", string.Format("{0:0} °C", b1 - 40.0), b1 - 40.0, "°C");
                case 0x1C: return Make(pid, "Imuilman lämpötila", string.Format("{0:0} °C", b1 - 40.0), b1 - 40.0, "°C");
                case 0x24: return Make(pid, "Akun jännite", string.Format("{0:0.0} V", b1 * 0.1), b1 * 0.1, "V");
                case 0x37:
                    double inj = (response.Length >= 4 ? word : b1) * 0.004;
                    if (inj < 0.2) inj = b1 * 0.1;
                    return Make(pid, "Ruiskutusaika", string.Format("{0:0.00} ms", inj), inj, "ms");
                case 0x3B: return Make(pid, "Sytytysennakko", string.Format("{0:0.0} °", (b1 - 128) * 0.5), (b1 - 128) * 0.5, "°");
                case 0x5D: return Make(pid, "Kaasuläpän asento", string.Format("{0:0.0} %", b1 * 100.0 / 255.0), b1 * 100.0 / 255.0, "%");
                case 0x5E: return Make(pid, "Imusarjan paine", string.Format("{0:0} mbar", b1 * 4.0), b1 * 4.0, "mbar");
                case 0x61: return Make(pid, "Moottorin kuormitus", string.Format("{0:0.00} ms", b1 * 0.05), b1 * 0.05, "ms");
                case 0x67: return Make(pid, "Lambda-anturi 1 (etummainen)", string.Format("{0:0.000} V", b1 * 0.005), b1 * 0.005, "V");
                case 0x68: return Make(pid, "Lambda-anturi 2 (etummainen)", string.Format("{0:0.000} V", b1 * 0.005), b1 * 0.005, "V");
                case 0x6C: return Make(pid, "Lambda-anturi 1 (takimmainen)", string.Format("{0:0.000} V", b1 * 0.005), b1 * 0.005, "V");
                case 0x70: return Make(pid, "Lambda-anturi 2 (takimmainen)", string.Format("{0:0.000} V", b1 * 0.005), b1 * 0.005, "V");
                case 0x74: return Make(pid, "Ajoneuvon nopeus", string.Format("{0} km/h", b1), b1, "km/h");
                case 0x7A: return Make(pid, "Tyhjäkäyntisäätimen suhde (RCO)", string.Format("{0:0.0} %", b1 * 100.0 / 255.0), b1 * 100.0 / 255.0, "%");
                case 0x7E: return Make(pid, "Ajonestolaite (ADC)", (b1 & 1) == 0 ? "Lukitus avattu (OK)" : "Lukittu", b1, "");
                case 0x83: return Make(pid, "Lyhytaikainen seoskorjaus", string.Format("{0:+0.0;-0.0;0.0} %", (b1 - 128) * 100.0 / 128.0), (b1 - 128) * 100.0 / 128.0, "%");
                case 0x88: return Make(pid, "Pitkäaikainen seoskorjaus", string.Format("{0:+0.0;-0.0;0.0} %", (b1 - 128) * 100.0 / 128.0), (b1 - 128) * 100.0 / 128.0, "%");
                case 0x8D: return Make(pid, "Ilmastoinnin tila", (b1 & 1) != 0 ? "Kytketty" : "Pois päältä", b1, "");
                case 0x8E: return Make(pid, "Jäähdytyspuhaltimen tila", string.Format("0x{0:X2}", b1), b1, "");
                case 0x95: return Make(pid, "Nakutusanturi 1 (syl. 1–3)", string.Format("{0:0.00} V", b1 * 0.02), b1 * 0.02, "V");
                case 0x98: return Make(pid, "Nakutusanturi 2 (syl. 4–6)", string.Format("{0:0.00} V", b1 * 0.02), b1 * 0.02, "V");
                case 0x9B: return Make(pid, "Aktiivihiilisuodattimen venttiili", string.Format("{0:0.0} %", b1 * 100.0 / 255.0), b1 * 100.0 / 255.0, "%");
                default: return Make(pid, string.Format("Tuntematon MP 7.0 -parametri (0x{0:X2})", pid), BitConverter.ToString(response, 2), b1, "raw");
            }
        }
        public static List<FaultCode> DecodeFaultCodes(byte[] response)
        {
            var list = new List<FaultCode>();
            if (response == null || response.Length < 2) return list;

            if (response[0] == 0x58)
            {
                int count = response[1];
                for (int i = 0, idx = 2; i < count && idx + 2 < response.Length; i++, idx += 3)
                {
                    ushort code = (ushort)((response[idx] << 8) | response[idx + 1]);
                    byte st = response[idx + 2];
                    list.Add(new FaultCode {
                        Code = code,
                        StatusFi = (st & 0x20) != 0 ? "Jatkuva vika" : ((st & 0x40) != 0 ? "Satunnainen vika" : string.Format("Tila 0x{0:X2}", st)),
                        DescriptionFi = LookupMp7FaultDescription(code)
                    });
                }
            }
            else if (response.Length >= 4)
            {
                for (int i = 2; i + 1 < response.Length; i += 2)
                {
                    ushort code = (ushort)((response[i] << 8) | response[i + 1]);
                    if (code == 0x0000 || code == 0xFFFF) continue;
                    list.Add(new FaultCode { Code = code, StatusFi = "Tallennettu", DescriptionFi = LookupMp7FaultDescription(code) });
                }
            }
            return list;
        }

        public static string LookupMp7FaultDescription(ushort code)
        {
            switch (code)
            {
                case 0x0105: return "Imusarjan paineanturin (MAP) virtapiiri";
                case 0x0110: return "Imuilman lämpötila-anturin virtapiiri";
                case 0x0115: return "Jäähdytysnesteen lämpötila-anturin virtapiiri";
                case 0x0120: return "Kaasuläpän asentoanturin virtapiiri";
                case 0x0130: return "Etummainen lambda-anturi 1 (syl. 1–3) virtapiiri";
                case 0x0135: return "Etummaisen lambda-anturin 1 lämmitysvastus";
                case 0x0136: return "Takimmainen lambda-anturi 1 virtapiiri";
                case 0x0141: return "Takimmaisen lambda-anturin 1 lämmitysvastus";
                case 0x0150: return "Etummainen lambda-anturi 2 (syl. 4–6) virtapiiri";
                case 0x0155: return "Etummaisen lambda-anturin 2 lämmitysvastus";
                case 0x0170: return "Seoksen sopeutus (sylinteriryhmä 1)";
                case 0x0173: return "Seoksen sopeutus (sylinteriryhmä 2)";
                case 0x0201: return "Suihkutusventtiili 1 virtapiiri";
                case 0x0202: return "Suihkutusventtiili 2 virtapiiri";
                case 0x0203: return "Suihkutusventtiili 3 virtapiiri";
                case 0x0204: return "Suihkutusventtiili 4 virtapiiri";
                case 0x0205: return "Suihkutusventtiili 5 virtapiiri";
                case 0x0206: return "Suihkutusventtiili 6 virtapiiri";
                case 0x0300: return "Satunnaisia sytytyskatkoksia useassa sylinterissä";
                case 0x0301: return "Sytytyskatkoksia sylinterissä 1";
                case 0x0302: return "Sytytyskatkoksia sylinterissä 2";
                case 0x0303: return "Sytytyskatkoksia sylinterissä 3";
                case 0x0304: return "Sytytyskatkoksia sylinterissä 4";
                case 0x0305: return "Sytytyskatkoksia sylinterissä 5";
                case 0x0306: return "Sytytyskatkoksia sylinterissä 6";
                case 0x0325: return "Nakutusanturi 1 virtapiiri";
                case 0x0330: return "Nakutusanturi 2 virtapiiri";
                case 0x0335: return "Kampiakselin asentotunnistimen (RPM) virtapiiri";
                case 0x0340: return "Nokka-akselin asentotunnistimen virtapiiri";
                case 0x0351: return "Sytytyspuola A (sylinterit 1/4) ensiö/toisiopiiri";
                case 0x0352: return "Sytytyspuola B (sylinterit 2/5) ensiö/toisiopiiri";
                case 0x0353: return "Sytytyspuola C (sylinterit 3/6) ensiö/toisiopiiri";
                case 0x0443: return "Aktiivihiilisuodattimen tyhjennysventtiilin virtapiiri";
                case 0x0500: return "Ajoneuvon nopeusanturin signaali";
                case 0x0505: return "Tyhjäkäynnin säätöjärjestelmä (askelmoottori / RCO)";
                case 0x0560: return "Järjestelmän syöttöjännite (akun jännite)";
                case 0x0601: return "Ohjainlaitteen (ECU) sisäinen muistivirhe";
                case 0x1517: return "Ajonestolaitteen (ADC) lukitus / tunnistusvirhe";
                case 0x1600: return "Moottorinohjainlaitteen (MP 7.0) sisäinen vika";
                default: return string.Format("Bosch MP 7.0 vikakoodi 0x{0:X4}", code);
            }
        }

        private static LiveParameter Make(byte id, string name, string formatted, double raw, string unit)
        {
            return new LiveParameter { LocalId = id, NameFi = name, ValueFormatted = formatted, RawNumeric = raw, Unit = unit };
        }
    }
}
