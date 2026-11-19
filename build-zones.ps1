# Builds zones.json for Stellantis Config Messer.
# Self-contained: downloads the field maps from Barracuda09/PyPSADiag (GPL) and converts
# them to the app schema. No Diagbox needed. ASCII-safe (umlauts from code points).
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$AE = [char]0x00E4; $OE = [char]0x00F6
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $dir 'maps_src'; New-Item -ItemType Directory -Force $src | Out-Null

# --- download PyPSADiag maps ---
$dl = [ordered]@{
  'ESP90.json'          = 'ABRASR/ESP90.json'
  'COMBINE_UDS_EV.json' = 'COMBINE/COMBINE_UDS_EV.json'
  'E_VCU.json'          = 'VCU/E_VCU.json'
  'AAS_UDS_G6.json'     = 'AAS/AAS_UDS_G6.json'
  'DAE_UDS.json'        = 'DIRECTN/DAE_UDS.json'
  'RADAR_AV4.json'      = 'ARTIV/RADAR_AV4.json'
  'CVM3.json'           = 'CVM/CVM3.json'
  'BSI2010.json'        = 'BMF/BSI2010.json'
}
$base = 'https://raw.githubusercontent.com/Barracuda09/PyPSADiag/main/json/'
foreach ($k in $dl.Keys) { $out = Join-Path $src $k; Write-Host "Downloading $k ..."; Invoke-WebRequest ($base + $dl[$k]) -Headers @{ 'User-Agent' = 'zt' } -OutFile $out -UseBasicParsing }

function MaskHex([string]$binMask) { return ('{0:X2}' -f [Convert]::ToInt32($binMask, 2)) }
function Shift([int]$maskByte) { $s = 0; $m = $maskByte; if ($m -eq 0) { return 0 }; while (($m -band 1) -eq 0) { $s++; $m = $m -shr 1 }; return $s }
function FiLabel([string]$en) {
  switch -regex ($en) {
    'Type of traction control' { 'Traktionhallinnan tyyppi (grip/ASR+)'; break }
    'Lane Positioning Assist'   { 'Kaistakeskitys (LPA)'; break }
    'Lane Keeping Assist'       { 'Kaistavahti (LKA)'; break }
    'descent control|HADC'      { "Alam${AE}kihallinta (HADC)"; break }
    'Adaptive Cruise|ACC Type'  { 'Mukautuva vakkari (ACC)'; break }
    'hill start'                { "M${AE}kil${AE}ht${OE}avustin"; break }
    default { '' }
  }
}
$ON = "P${AE}${AE}ll${AE}"

function ConvField($fd) {
  $out = @()
  $binMask = "$($fd.mask)"
  if ($binMask.Length -ne 8) { return $out }
  $maskByte = [Convert]::ToInt32($binMask, 2)
  $byte = [int]$fd.byte
  $en = "$($fd.name)"
  $fi = FiLabel $en
  $hasParams = ($fd.PSObject.Properties.Name -contains 'params') -and ($fd.params)
  if ($hasParams) {
    $opts = @()
    foreach ($p in $fd.params) {
      if ($p.PSObject.Properties.Name -contains 'mask') { $v = MaskHex "$($p.mask)" }
      else { $sh = Shift $maskByte; $v = ('{0:X2}' -f ((([Convert]::ToInt32("$($p.value)", 16)) -shl $sh) -band $maskByte)) }
      $opts += [ordered]@{ en = "$($p.name)"; fi = ''; val = $v }
    }
    $out += [ordered]@{ en = $en; fi = $fi; byteStart = $byte; mask = ('{0:X2}' -f $maskByte); type = 'enum'; options = $opts }
  }
  elseif ("$($fd.form_type)" -eq 'checkbox') {
    $mh = '{0:X2}' -f $maskByte; $al = "$($fd.available_logic)"
    if ($al -eq 'active_low') { $opts = @([ordered]@{en='Off';fi='Pois';val=$mh}, [ordered]@{en='On';fi=$ON;val='00'}) }
    else { $opts = @([ordered]@{en='Off';fi='Pois';val='00'}, [ordered]@{en='On';fi=$ON;val=$mh}) }
    $out += [ordered]@{ en = $en; fi = $fi; byteStart = $byte; mask = $mh; type = 'enum'; options = $opts }
  }
  else { $out += [ordered]@{ en = $en; fi = $fi; byteStart = $byte; mask = ('{0:X2}' -f $maskByte); type = 'bits'; options = @() } }
  return $out
}

function ConvModule($jsonPath) {
  $j = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $zonesOut = @()
  foreach ($zp in ($j.zones.PSObject.Properties)) {
    $zid = $zp.Name; $z = $zp.Value
    $fieldsOut = @()
    $metaKeys = @('name','type','form_type','id','tab','zoneLength')
    if ($z.PSObject.Properties.Name -contains 'mask') {
      # BMF/BSI style: the zone object IS a single field-def (its params are the options)
      $fieldsOut += ConvField $z
    }
    elseif (($z.PSObject.Properties.Name -contains 'params') -and ($z.params -is [Array])) {
      foreach ($fd in $z.params) { $fieldsOut += ConvField $fd }
    } else {
      $keys = @($z.PSObject.Properties.Name | Where-Object { $_ -notin $metaKeys })
      $final = @()
      foreach ($k in $keys) { if ($k -match '-38$') { $final += $k } elseif (-not ($keys -contains ($k + '-38'))) { $final += $k } }
      foreach ($k in $final) { $fieldsOut += ConvField ($z.$k) }
    }
    $zonesOut += [ordered]@{ id = $zid; name = "$($z.name)"; fields = $fieldsOut }
  }
  return $zonesOut
}

$modTable = @(
  @{ id='BSI';     name='BSI2010 (body control)';              tx='752'; rx='652'; key='B4E0'; trace='yes'; off=0; src='BSI2010.json'; hide=$true },
  @{ id='ESP';     name='ESP90 (ABS/ESP)';                     tx='6AD'; rx='68D'; key='BC93'; trace='no';  off=1; src='ESP90.json' },
  @{ id='COMBINE'; name='Instrument cluster (COMBINE_UDS_EV)'; tx='75F'; rx='65F'; key='A13C'; trace='yes'; off=0; src='COMBINE_UDS_EV.json' },
  @{ id='VCU';     name="VCU (E_VCU, electric)";               tx='6A2'; rx='682'; key='0107'; trace='try'; off=0; src='E_VCU.json' },
  @{ id='AAS';     name='AAS (park assist G6)';                tx='75D'; rx='65D'; key='B6F0'; trace='try'; off=0; src='AAS_UDS_G6.json' },
  @{ id='DAE';     name='DAE (power steering)';                tx='6B5'; rx='695'; key='2105'; trace='try'; off=0; src='DAE_UDS.json' },
  @{ id='RADAR';   name='Front radar (RADAR_AV4)';             tx='6B6'; rx='696'; key='EFCA'; trace='try'; off=0; src='RADAR_AV4.json' },
  @{ id='CVM';     name='Camera (CVM3)';                       tx='74A'; rx='64A'; key='E2E5'; trace='try'; off=0; src='CVM3.json' },
  @{ id='RCC';     name='RCC (infotainment)';                  tx='764'; rx='664'; key='F107'; trace='try'; off=0; src='' }
)

$modulesOut = @()
foreach ($mt in $modTable) {
  $zones = @()
  if ($mt.src -ne '') { $zones = ConvModule (Join-Path $src $mt.src) }
  elseif ($mt.id -eq 'RCC') {
    # RCC/NAC: no open PyPSADiag map. If a local Diagbox NAC export (nac-tulkinta.csv) is
    # present, build full RCC from it (LOCAL only, proprietary). Otherwise minimal fallback.
    $nac = Join-Path $dir '..\nac-tulkinta.csv'
    if (Test-Path $nac) {
      foreach ($g in (Import-Csv $nac -Encoding UTF8 | Group-Object Zone)) {
        $flds = @()
        foreach ($r in $g.Group) {
          $byte = [Convert]::ToInt32($r.Tavu, 16); $maskHex = ("$($r.Maski)").ToUpper(); $en = "$($r.Kuvaus)"; $vaih = "$($r.Vaihtoehdot)"
          if ($vaih.Trim() -ne '') {
            $opts = @()
            foreach ($o in ($vaih -split '\s*;\s*')) { if ($o -match '^\s*([0-9A-Fa-f]+)\s*=\s*(.+?)\s*$') { $opts += [ordered]@{ en = $Matches[2].Trim(); fi = ''; val = ('{0:X2}' -f [Convert]::ToInt32($Matches[1], 16)) } } }
            if ($opts.Count -ge 2) { $flds += [ordered]@{ en = $en; fi = ''; byteStart = $byte; mask = $maskHex; type = 'enum'; options = $opts } }
            else { $flds += [ordered]@{ en = $en; fi = ''; byteStart = $byte; mask = $maskHex; type = 'bits'; options = @() } }
          }
          elseif ("$($r.Tila)" -match '(?i)numero|number') { $flds += [ordered]@{ en = $en; fi = ''; byteStart = $byte; mask = $maskHex; type = 'bits'; options = @() } }
          else { $mb = [Convert]::ToInt32($maskHex, 16); $flds += [ordered]@{ en = $en; fi = ''; byteStart = $byte; mask = $maskHex; type = 'enum'; options = @([ordered]@{en='Off';fi='Pois';val='00'}, [ordered]@{en='On';fi=$ON;val=('{0:X2}' -f $mb)}) } }
        }
        $zones += [ordered]@{ id = $g.Name; name = "RCC $($g.Name)"; fields = $flds }
      }
    } else {
      $zones = @(
        [ordered]@{ id='210C'; name='Park/grip menu'; fields=@([ordered]@{en='210C raw (A010=park menu)';fi='';byteStart=0;mask='FF';type='raw';options=@()}) },
        [ordered]@{ id='2133'; name='2133 (raw)'; fields=@() }
      )
    }
  }
  if ($mt.hide) { $zones = @($zones | Where-Object { -not ($_.id -like '01*' -or $_.id -like '02*' -or $_.id -like '1*' -or $_.id -like 'F*' -or $_.id -eq '2100') }) }
  $modulesOut += [ordered]@{ id=$mt.id; name=$mt.name; tx=$mt.tx; rx=$mt.rx; key=$mt.key; traceability=$mt.trace; byteOffset=$mt.off; zones=$zones }
  $fc = 0; foreach($z in $zones){ $fc += $z.fields.Count }
  Write-Host ("{0,-8} zones={1,-4} fields={2}" -f $mt.id, $zones.Count, $fc)
}

$root = [ordered]@{
  _info = 'Built by build-zones.ps1 from PyPSADiag maps. byteOffset is added to byteStart at runtime (ESP=1).'
  seedkey = [ordered]@{ sec1=@(178,63,170); sec2=@(177,2,171) }
  modules = $modulesOut
}
$json = $root | ConvertTo-Json -Depth 20
$outPath = Join-Path $dir 'zones.json'
[System.IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("Wrote: {0} ({1} kt)" -f $outPath, [math]::Round((Get-Item $outPath).Length/1kb))
