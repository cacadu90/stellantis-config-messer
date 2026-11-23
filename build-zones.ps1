# Builds zones.json for Stellantis Config Messer.
# Self-contained: auto-discovers EVERY ECU map from Barracuda09/PyPSADiag (GPL), reads each
# map's tx_id/rx_id/keys straight from the JSON, and converts the zones to the app schema.
# No Diagbox needed. ASCII-safe (umlauts from code points).
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$AE = [char]0x00E4; $OE = [char]0x00F6
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $dir 'maps_src'; New-Item -ItemType Directory -Force $src | Out-Null
$H = @{ 'User-Agent' = 'zt' }
$RAW = 'https://raw.githubusercontent.com/Barracuda09/PyPSADiag/main/'

# --- enumerate + download every json map ---
Write-Host "Enumerating PyPSADiag maps ..."
$tree = Invoke-RestMethod "https://api.github.com/repos/Barracuda09/PyPSADiag/git/trees/main?recursive=1" -Headers $H
$mapPaths = $tree.tree | Where-Object { $_.path -match '^json/.+\.json$' } | Select-Object -ExpandProperty path
Write-Host ("Found {0} maps, downloading ..." -f $mapPaths.Count)
foreach ($p in $mapPaths) {
  $local = Join-Path $src ($p -replace '[\\/]', '_')
  if (Test-Path $local) { continue }   # cached; delete maps_src to force refresh
  try { Invoke-WebRequest ($RAW + $p) -Headers $H -OutFile $local -UseBasicParsing } catch { Write-Host "  skip $p ($($_.Exception.Message))" }
}

function MaskHex([string]$binMask) { return ('{0:X2}' -f [Convert]::ToInt32($binMask, 2)) }
function Shift([int]$maskByte) { $s = 0; $m = $maskByte; if ($m -eq 0) { return 0 }; while (($m -band 1) -eq 0) { $s++; $m = $m -shr 1 }; return $s }
function FiLabel([string]$en) {
  switch -regex ($en) {
    'Type of traction control' { 'Traktionhallinnan tyyppi (grip/ASR+)'; break }
    'Lane Positioning Assist.*PUSH'  { 'Kaistakeskitys (LPA) - nappi'; break }
    'Lane Positioning Assist.*LED'   { 'Kaistakeskitys (LPA) - LED'; break }
    'Lane Positioning Assist'   { 'Kaistakeskitys (LPA)'; break }
    'Lane Keeping Assist.*PUSH'      { 'Kaistavahti (LKA) - nappi'; break }
    'Lane Keeping Assist.*LED'       { 'Kaistavahti (LKA) - LED'; break }
    'Lane Keeping Assist'       { 'Kaistavahti (LKA)'; break }
    'descent control|HADC'      { "Alam${AE}kihallinta (HADC)"; break }
    'Adaptive Cruise|ACC Type'  { 'Mukautuva vakkari (ACC)'; break }
    'hill start'                { "M${AE}kil${AE}ht${OE}avustin"; break }
    default { '' }
  }
}
$ON = "P${AE}${AE}ll${AE}"

# Convert one field-def object (has an 8-char binary mask) to the app schema.
function ConvField($fd) {
  $out = @()
  if (-not ($fd.PSObject.Properties.Name -contains 'mask')) { return $out }
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

# Convert one zone (any of the 3 PyPSADiag layouts) to a field list. Undecodable -> empty (raw).
function ConvZone($z) {
  $fields = @()
  if ($z.PSObject.Properties.Name -contains 'mask') {
    $fields += ConvField $z                                   # BMF: the zone IS one field
  }
  elseif (($z.PSObject.Properties.Name -contains 'params') -and ($z.params -is [Array])) {
    $looksFields = $false
    foreach ($p in $z.params) { if ($p -is [psobject] -and (($p.PSObject.Properties.Name -contains 'mask') -or ($p.PSObject.Properties.Name -contains 'byte'))) { $looksFields = $true; break } }
    if ($looksFields) { foreach ($fd in $z.params) { $fields += ConvField $fd } }  # COMBINE: params = fields
  }
  else {
    # ESP style: object-valued properties that are themselves field-defs (have a mask). Prefer -38 variants.
    $keys = @($z.PSObject.Properties | Where-Object { $_.Value -is [psobject] -and ($_.Value.PSObject.Properties.Name -contains 'mask') } | Select-Object -ExpandProperty Name)
    $final = @(); foreach ($k in $keys) { if ($k -match '-38$') { $final += $k } elseif (-not ($keys -contains ($k + '-38'))) { $final += $k } }
    foreach ($k in $final) { $fields += ConvField ($z.$k) }
  }
  return $fields
}

function ConvModule($jsonPath) {
  $j = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $zonesOut = @()
  if (-not ($j.PSObject.Properties.Name -contains 'zones') -or $null -eq $j.zones) { return $zonesOut }
  foreach ($zp in ($j.zones.PSObject.Properties)) {
    $z = $zp.Value
    $zonesOut += [ordered]@{ id = $zp.Name; name = "$($z.name)"; fields = (ConvZone $z) }
  }
  return $zonesOut
}

# Read tx/rx/key/name from a map JSON. key: prefer key3 (matches 27 03/04), else key1, else string.
function ModMeta($jsonPath) {
  $j = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $key = ''
  if ($j.PSObject.Properties.Name -contains 'keys' -and $null -ne $j.keys) {
    $k = $j.keys
    if ($k -is [string]) { $key = $k }
    elseif ($k.PSObject.Properties.Name -contains 'key3') { $key = "$($k.key3)" }
    elseif ($k.PSObject.Properties.Name -contains 'key1') { $key = "$($k.key1)" }
    else { $first = @($k.PSObject.Properties)[0]; if ($first) { $key = "$($first.Value)" } }
  }
  return @{ tx = "$($j.tx_id)"; rx = "$($j.rx_id)"; key = $key.ToUpper(); name = "$($j.name)" }
}

# --- car-VERIFIED ECUs: trusted tx/rx/key/byteOffset/traceability, keyed by json path ---
$override = @{
  'json/BMF/BSI2010.json'        = @{ id='BSI';    name='BSI2010 (body control)';              key='B4E0'; off=0; trace='yes'; filt='bsi' }
  'json/ABRASR/ESP90.json'       = @{ id='ESP';    name='ESP90 (ABS/ESP)';                     key='BC93'; off=1; trace='no' }
  'json/COMBINE/COMBINE_UDS_EV.json' = @{ id='COMBINE'; name='Instrument cluster (EV)';         key='A13C'; off=0; trace='yes' }
  'json/VCU/E_VCU.json'          = @{ id='VCU';    name='VCU (electric)';                      key='0107'; off=0; trace='try' }
  'json/AAS/AAS_UDS_G6.json'     = @{ id='AAS';    name='AAS (park assist G6)';                key='B6F0'; off=0; trace='try' }
  'json/DIRECTN/DAE_UDS.json'    = @{ id='DAE';    name='DAE (power steering)';                key='2105'; off=0; trace='try' }
  'json/ARTIV/RADAR_AV4.json'    = @{ id='RADAR';  name='Front radar (AV4)';                   key='EFCA'; off=0; trace='try' }
  'json/CVM/CVM3.json'           = @{ id='CVM';    name='Camera (CVM3)';                       key='E2E5'; off=0; trace='try' }
  'json/TELEMAT/RCC.json'        = @{ id='RCC';    name='RCC/NAC (infotainment)';              key='F107'; off=0; trace='try'; filt='rcc' }
}
$orderVerified = @('json/BMF/BSI2010.json','json/ABRASR/ESP90.json','json/COMBINE/COMBINE_UDS_EV.json','json/VCU/E_VCU.json','json/AAS/AAS_UDS_G6.json','json/DIRECTN/DAE_UDS.json','json/ARTIV/RADAR_AV4.json','json/CVM/CVM3.json','json/TELEMAT/RCC.json')

# Human family names for auto-added ECUs
$fam = @{
  AAS='Park assist'; ABRASR='ABS/ESP'; AFFICHEUR='Display'; AFFICHEUR_LIN='Display (LIN)'; AFFICHEUR_LVDS_BD='Display (LVDS)';
  AIRBAG='Airbag'; AMPLHIFI='Hi-Fi amplifier'; ARTIV='Front radar'; AUTORADIO='Radio'; AVAS='Acoustic alert (EV)'; AVM='Around-view';
  BCM='Body actuators'; BECB='On-board charger'; BMF='BSI body'; BML='Battery mgmt'; BOITEVIT='Gearbox'; COMBINE='Instrument cluster';
  CSR='Comms'; CVM='Camera'; DIRECTN='Power steering'; FMUX='Facia panel'; HDC='Comfort'; INJ='Engine (petrol/diesel)'; MATT='Trailer';
  MDS='Blind spot/radar'; TELEMAT='Infotainment'; VCU='Electric VCU'; VTH='Climate'
}

function BuildMod($path, $id, $name, $key, $off, $trace, $filt, $verified) {
  $local = Join-Path $src ($path -replace '[\\/]', '_')
  if (-not (Test-Path $local)) { return $null }
  $meta = ModMeta $local
  if ("$($meta.tx)" -eq '' -or "$($meta.rx)" -eq '') { return $null }
  if ($key -eq $null) { $key = $meta.key }
  if ("$key" -notmatch '^[0-9A-Fa-f]{4}$') { $key = '' }   # drop unknown/redacted keys (****) -> read-only
  $zones = ConvModule $local
  if ($zones.Count -eq 0) { return $null }
  if ($filt -eq 'bsi') { $zones = @($zones | Where-Object { -not ($_.id -like '01*' -or $_.id -like '02*' -or $_.id -like '1*' -or $_.id -like 'F*' -or $_.id -eq '2100') }) }
  if ($filt -eq 'rcc') { $zones = @($zones | Where-Object { $_.id -like '2*' }) }
  $fc = 0; foreach ($z in $zones) { $fc += $z.fields.Count }
  Write-Host ("{0,-22} tx={1,-4} key={2,-6} zones={3,-4} fields={4}  {5}" -f $id, $meta.tx, $key, $zones.Count, $fc, $(if($verified){'[verified]'}else{''}))
  return [ordered]@{ id = $id; name = $name; tx = $meta.tx; rx = $meta.rx; key = $key; traceability = $trace; byteOffset = $off; verified = $verified; zones = $zones }
}

$modulesOut = @()
# 1) verified ECUs first, in order
foreach ($p in $orderVerified) {
  $o = $override[$p]
  $m = BuildMod $p $o.id $o.name $o.key $o.off $o.trace $o.filt $true
  if ($m) { $modulesOut += $m }
}
# 2) everything else, auto
foreach ($p in ($mapPaths | Sort-Object)) {
  if ($override.ContainsKey($p)) { continue }
  $parts = $p -split '/'
  $family = $parts[1]
  $stem = [IO.Path]::GetFileNameWithoutExtension($parts[-1])
  $id = ($family + '_' + $stem) -replace '[^A-Za-z0-9_]', '_'
  $desc = if ($fam.ContainsKey($family)) { $fam[$family] } else { $family }
  $name = $desc + ' (' + $stem + ')'
  $m = BuildMod $p $id $name $null 0 'try' '' $false
  if ($m) { $modulesOut += $m }
}

# --- DTC descriptions (open, pre-ripped from the Diagbox FDB by PyPSADiag) -> dtc.json ---
Write-Host "Downloading DTC descriptions ..."
$dtcAll = [ordered]@{}
try {
  $dtcList = (Invoke-RestMethod "https://api.github.com/repos/Barracuda09/PyPSADiag/contents/data/dtc" -Headers $H) | Where-Object { $_.name -like '*.json' }
  foreach ($f in $dtcList) {
    try {
      $j = (Invoke-WebRequest $f.download_url -Headers $H -UseBasicParsing).Content | ConvertFrom-Json
      foreach ($p in $j.PSObject.Properties) {
        if ($p.Name -eq '_comment') { continue }
        $nm = "$($p.Value.name)"
        if ($nm -ne '' -and -not $dtcAll.Contains($p.Name)) { $dtcAll[$p.Name] = $nm }
      }
    } catch { }
  }
} catch { Write-Host "DTC download failed: $($_.Exception.Message)" }
[System.IO.File]::WriteAllText((Join-Path $dir 'dtc.json'), ($dtcAll | ConvertTo-Json -Depth 3), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("DTC codes: {0}" -f $dtcAll.Count)

$root = [ordered]@{
  _info = 'Built by build-zones.ps1 from PyPSADiag maps. byteOffset added to byteStart at runtime (ESP=1). verified=true -> car-tested tx/rx/key; false -> auto from map (read OK, write-key best-effort).'
  seedkey = [ordered]@{ sec1=@(178,63,170); sec2=@(177,2,171) }
  modules = $modulesOut
}
$json = $root | ConvertTo-Json -Depth 20
$outPath = Join-Path $dir 'zones.json'
[System.IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("`nModules: {0}   Wrote: {1} ({2} kt)" -f $modulesOut.Count, $outPath, [math]::Round((Get-Item $outPath).Length/1kb))
