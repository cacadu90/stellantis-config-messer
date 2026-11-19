# Koostaa zones.json PyPSADiag-kartoista (maps_src\) + BSI-varmuuskopiosta.
# ASCII-turvallinen: aakkoset koodipisteista, jotta skriptin tiedostokoodaus ei vaikuta.
$ErrorActionPreference = 'Stop'
$AE = [char]0x00E4   # a-umlaut
$OE = [char]0x00F6   # o-umlaut
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$src = Join-Path $dir 'maps_src'
$bsiCsv = Join-Path $dir '..\varmuuskopio\BSI_20261107_151057.csv'

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
$ON = "P${AE}${AE}ll${AE}"   # "Paalla"

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
    $mh = '{0:X2}' -f $maskByte
    $al = "$($fd.available_logic)"
    if ($al -eq 'active_low') { $opts = @([ordered]@{en='Off';fi='Pois';val=$mh}, [ordered]@{en='On';fi=$ON;val='00'}) }
    else { $opts = @([ordered]@{en='Off';fi='Pois';val='00'}, [ordered]@{en='On';fi=$ON;val=$mh}) }
    $out += [ordered]@{ en = $en; fi = $fi; byteStart = $byte; mask = $mh; type = 'enum'; options = $opts }
  }
  else {
    $out += [ordered]@{ en = $en; fi = $fi; byteStart = $byte; mask = ('{0:X2}' -f $maskByte); type = 'bits'; options = @() }
  }
  return $out
}

function ConvModule($jsonPath) {
  $j = Get-Content $jsonPath -Raw -Encoding UTF8 | ConvertFrom-Json
  $zonesOut = @()
  foreach ($zp in ($j.zones.PSObject.Properties)) {
    $zid = $zp.Name; $z = $zp.Value
    $fieldsOut = @()
    $metaKeys = @('name','type','form_type','id','tab','zoneLength')
    if (($z.PSObject.Properties.Name -contains 'params') -and ($z.params -is [Array])) {
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
  @{ id='BSI';     name='BSI2010_EV (korinohjain)';            tx='752'; rx='652'; key='B4E0'; trace='yes'; off=0; src='' },
  @{ id='ESP';     name='ESP90 (ABS/ESP)';                     tx='6AD'; rx='68D'; key='BC93'; trace='no';  off=1; src='ESP90.json' },
  @{ id='COMBINE'; name='Mittaristo (COMBINE_UDS_EV)';         tx='75F'; rx='65F'; key='A13C'; trace='yes'; off=0; src='COMBINE_UDS_EV.json' },
  @{ id='VCU';     name="VCU (E_VCU, s${AE}hk${OE})";           tx='6A2'; rx='682'; key='0107'; trace='try'; off=0; src='E_VCU.json' },
  @{ id='AAS';     name="AAS (pys${AE}k${OE}intiavustin G6)";   tx='75D'; rx='65D'; key='B6F0'; trace='try'; off=0; src='AAS_UDS_G6.json' },
  @{ id='DAE';     name='DAE (ohjaustehostin)';                tx='6B5'; rx='695'; key='2105'; trace='try'; off=0; src='DAE_UDS.json' },
  @{ id='RADAR';   name='Etututka (RADAR_AV4)';                tx='6B6'; rx='696'; key='EFCA'; trace='try'; off=0; src='RADAR_AV4.json' },
  @{ id='CVM';     name='Kamera (CVM3)';                       tx='74A'; rx='64A'; key='E2E5'; trace='try'; off=0; src='CVM3.json' },
  @{ id='RCC';     name='RCC (infotainment)';                  tx='764'; rx='664'; key='F107'; trace='try'; off=0; src='' }
)

$modulesOut = @()
foreach ($mt in $modTable) {
  $zones = @()
  if ($mt.src -ne '') { $zones = ConvModule (Join-Path $src $mt.src) }
  elseif ($mt.id -eq 'BSI') {
    foreach ($row in (Import-Csv $bsiCsv -Encoding UTF8)) {
      $zid = $row.Zone; $desc = $row.Kuvaus; $arvo = "$($row.Arvo)".Trim()
      if ($zid -like '01*' -or $zid -like '02*' -or $zid -like '1*' -or $zid -like 'F*' -or $zid -eq '2100') { continue }   # piilota sisaiset/kalib/ident-zonet (VIN silti otsikkopalkissa)
      if ($arvo -eq '00' -or $arvo -eq '01') {
        $flds = @([ordered]@{ en=$desc; fi=''; byteStart=0; mask='FF'; type='enum'; options=@([ordered]@{en='No';fi='Ei';val='00'},[ordered]@{en='Yes';fi='On';val='01'}) })
      } else {
        $flds = @([ordered]@{ en=$desc; fi=''; byteStart=0; mask='FF'; type='bits'; options=@() })
      }
      $zones += [ordered]@{ id=$zid; name=$desc; fields=$flds }
    }
  }
  elseif ($mt.id -eq 'RCC') {
    $zones = @(
      [ordered]@{ id='210C'; name='Park/grip -valikko'; fields=@([ordered]@{en='210C raw (A010=park valikko)';fi='';byteStart=0;mask='FF';type='raw';options=@()}) },
      [ordered]@{ id='2133'; name='2133 (raaka)'; fields=@() }
    )
  }
  $modulesOut += [ordered]@{ id=$mt.id; name=$mt.name; tx=$mt.tx; rx=$mt.rx; key=$mt.key; traceability=$mt.trace; byteOffset=$mt.off; zones=$zones }
  $fc = 0; foreach($z in $zones){ $fc += $z.fields.Count }
  Write-Host ("{0,-8} zonet={1,-3} kenttia={2}" -f $mt.id, $zones.Count, $fc)
}

$root = [ordered]@{
  _info = 'Koottu build-zones.ps1. byteOffset lisataan byteStartiin ajossa (ESP=1).'
  seedkey = [ordered]@{ sec1=@(178,63,170); sec2=@(177,2,171) }
  modules = $modulesOut
}
$json = $root | ConvertTo-Json -Depth 12
$outPath = Join-Path $dir 'zones.json'
[System.IO.File]::WriteAllText($outPath, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("Kirjoitettu: {0} ({1} kt)" -f $outPath, [math]::Round((Get-Item $outPath).Length/1kb))
