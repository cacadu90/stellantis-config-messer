# Stellantis Config Messer (`v2.6.0-beta`)

> **⚠️ HUOMIO / TESTIVERSIO (PRE-RELEASE):**
> `v2.6.0-beta` on kehitysversio, jota **ei ole vielä testattu oikeassa autossa**.
> Älä käytä kriittisiin ECU-kirjoitusoperaatioihin ennen kuin ensimmäiset autotestilokit on varmistettu.

---

## Mikä tämä on?
Kevyt, suoraan Windowsin omalla .NET C# -kääntäjällä käännettävä diagnostiikka- ja konfigurointityökalu:
* **Uudemmat PSA / Stellantis -ajoneuvot (UDS / ISO-TP / CAN):** BSI-, ECU- ja näyttömoduulien (esim. NAC / RCC / SMEG) vyöhyke- ja konfigurointimuutokset (`ZoneTool`).
* **Vanhemmat PSA K-Line -ajoneuvot (esim. 1997 Peugeot 406 D8 ES9J4 V6):** Suora monipinniskannaus (Pin 7 Bosch MP 7.0, Pin 11 ZF 4HP20, Pin 12 ABS Teves Mk20, Pin 13 Airbag AC4).

---

## Tuetut liitäntäkaapelit (Transport-kerros)
1. **Arduino / MCP2515 CAN-adapteri** (vakio UDS/ISO-TP)
2. **Actia XS Evolution / Diagbox VCI** (suora `ACTIA_VCI.dll`-rajapinta)
3. **Delphi DS150E / Autocom CDP+ (`ADP186`)** *(Experimental / `v2.6.0-beta`)*:
   * Suora WinUSB-yhteys (`VID_0403&PID_D6DA`) ilman erillisiä VCP-ajureita.
   * Käyttää aitoa ADP186 ASCII-tähtikomentokieltä (`*999`, `*200`, `*201`, `*203`, `*333_1`, `*600`, `*605E`).
   * *Huom: DS150E:n sisäinen STM32-prosessori vaatii 12 V käyttövirran auton OBD2-pistokkeen pinnistä 16.*

---

## Käynnistys
1. Pura `StellantisConfigMesser-v2.6.0-beta-TESTIVERSIO.zip` haluamaasi kansioon.
2. Käynnistä **`Kaynnista_v2.6.0.bat`** (kääntää C#-lähdekoodit automaattisesti muistiin eikä vaadi Visual Studiota).
3. Avaa **`Näytä loki`**, valitse liitäntäkaapeli ja paina **`Yhdistä`**.

---

## Kehitystila
* [x] WinUSB-suorayhteys (`VID_0403&PID_D6DA`) ja laitehallinnan tarkistus
* [x] ADP186 `*`-komentokielen toteutus (`AutocomTransport.cs`)
* [ ] **Live-autotesti (1997 Peugeot 406 D8 & UDS-kohdeajoneuvo) — työn alla**