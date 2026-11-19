# Stellantis Config Messer

A native Windows app for reading and writing (telecoding) configuration **zones** on
PSA/Stellantis ECUs (Peugeot, Citroën, DS, Opel on the PSA platform), Diagbox-style —
with human-readable field names, toggles and drop-downs instead of raw hex.

It talks to the car over the diagnostic CAN bus either through a cheap
**Arduino + MCP2515** running the [ludwig-v/arduino-psa-diag](https://github.com/ludwig-v/arduino-psa-diag)
firmware, **or** through a **Diagbox / Evolution XS VCI** interface (via the `VCIAccess.dll`
that ships with Diagbox).

> ☕ If this saved you a trip to the dealer: **https://www.buymeacoffee.com/plagufin**

---

## What it does

- **Browse & edit config zones** of many ECUs (BSI, ESP/ABS, instrument cluster,
  VCU, parking-assist, steering, radar, camera, …) with decoded fields:
  - on/off settings → **toggle buttons**
  - multi-value settings → **drop-downs**
  - everything else → guarded **hex** field
- **Read all zones** of a module in one click (fast, non-blocking).
- **Automatic seed→key unlock** (ludwig-v / prototux algorithm) — no need to know the key.
- **Read-before-write guard**, verify-after-write, and **Secured Traceability (2901)**
  stamping in the same session.
- **Automatic backup** of the current configuration before every write
  (`backup\<module>\<timestamp>.csv`).
- **DTC read / clear** with decoded fault codes (Bxxxx / Uxxxx + FTB + status).
- **Two transports**: Arduino (serial) or Diagbox VCI — pick from the top bar.
- Reads and shows the **VIN** in the title bar.
- English / Finnish UI.

## Requirements

- Windows with **.NET Framework 4.x** (already present on modern Windows) — the app is
  a single native `.exe`, no install.
- **Transport**, one of:
  - Arduino Uno + MCP2515 CAN shield flashed with
    [arduino-psa-diag](https://github.com/ludwig-v/arduino-psa-diag) (115200 baud), **or**
  - a Diagbox / Evolution XS VCI with `C:\AWRoot\drv\VCIAccess.dll` (installed by Diagbox).
    The app must be built/run as **x86** to load the 32-bit DLL (see below).
- An OBD adapter wiring the correct CAN-DIAG pins (3/8 on older PSA, 6/14 on newer).

## Build

No IDE needed — compile with the C# compiler that ships with .NET Framework:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /platform:x86 /target:winexe ^
  /out:StellantisConfigMesser.exe ^
  /reference:System.Windows.Forms.dll /reference:System.Drawing.dll ^
  /reference:System.Web.Extensions.dll /reference:System.Management.dll ZoneTool.cs
```

`/platform:x86` is required so the app can P/Invoke the 32-bit Diagbox `VCIAccess.dll`.
(The Arduino transport works regardless.)

## Zone map (`zones.json`)

The app is data-driven: it reads `zones.json` describing modules, keys, zones and fields.
Generate it with:

```
powershell -ExecutionPolicy Bypass -File build-zones.ps1
```

`build-zones.ps1` is **self-contained**: it downloads every field map from
[Barracuda09/PyPSADiag](https://github.com/Barracuda09/PyPSADiag) — including the BSI2010
map — and converts them to the app's schema. **No Diagbox required.** (Pre-built releases
ship a ready-made `zones.json`.)

## ⚠️ Disclaimer

Telecoding ECUs can misconfigure or, in rare cases, brick modules, and can enable features
your car's hardware does not support. **Use entirely at your own risk**, on a vehicle you
own, with a stable power supply. Always keep the automatic backups. The authors take no
responsibility for any damage. This project is not affiliated with Stellantis/PSA.

## Credits

This project stands on the shoulders of the PSA reverse-engineering community:

- **[Barracuda09/PyPSADiag](https://github.com/Barracuda09/PyPSADiag)** — zone/field JSON
  maps, VCI transport design, and overall reference. (GPL)
- **[ludwig-v/arduino-psa-diag](https://github.com/ludwig-v/arduino-psa-diag)** — the Arduino
  diagnostic firmware and ECU zone/key documentation.
- **[ludwig-v/psa-seedkey-algorithm](https://github.com/ludwig-v/psa-seedkey-algorithm)** —
  seed/key algorithm and ECU key list.
- **[prototux/PSA-RE](https://github.com/prototux/PSA-RE)** — the UDS security (seed/key)
  algorithm source and constants.
- **[halloworld007/PyPSADiag-VCI](https://github.com/halloworld007/PyPSADiag-VCI)** — the
  Evolution XS / Diagbox VCI (`VCIAccess.dll`) access approach reused by PyPSADiag.
- **Diagbox / Lexia (Stellantis)** — the `VCIAccess.dll` driver used by the VCI transport is
  part of Diagbox and is **not** distributed here.

## License

GPL-3.0 — see [LICENSE](LICENSE). (Derived in part from GPL-licensed PyPSADiag.)
