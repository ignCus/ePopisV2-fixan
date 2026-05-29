param(
    [string]$ConfigPath = "",
    [switch]$Fix,
    [int]$ContextLines = 5
)

function Resolve-ConfigPath {
    param($candidate)
    if ($candidate -and (Test-Path $candidate)) { return (Resolve-Path $candidate).ProviderPath }

    # Try pointer file in ProgramData
    $pointer = Join-Path $env:ProgramData "ePopis\Config\admin_config.json"
    if (Test-Path $pointer) {
        try {
            $json = Get-Content $pointer -Raw | ConvertFrom-Json
            if ($json.GlavniFolderPath) {
                $cfg = Join-Path $json.GlavniFolderPath "Config"
                if (Test-Path $cfg) { return $cfg }
            }
        } catch { }
    }

    # Try registry HKCU\SOFTWARE\OktagonBet -> GlavniFolder
    try {
        $reg = Get-ItemProperty -Path "HKCU:\SOFTWARE\OktagonBet" -ErrorAction SilentlyContinue
        if ($reg -and $reg.GlavniFolder) {
            $cfg = Join-Path $reg.GlavniFolder "Config"
            if (Test-Path $cfg) { return $cfg }
        }
    } catch { }

    # Try current working directory Config
    $cwdcfg = Join-Path (Get-Location) "Config"
    if (Test-Path $cwdcfg) { return $cwdcfg }

    return $null
}

function Read-LastMatches {
    param($path, $pattern, $context=3)
    if (-not (Test-Path $path)) { return }
    $all = Get-Content $path -Raw -ErrorAction SilentlyContinue
    if (-not $all) { return }
    $lines = $all -split "\r?\n"
    $matches = Select-String -InputObject $lines -Pattern $pattern -AllMatches
    foreach ($m in $matches) {
        $lineNumber = $m.LineNumber
        $start = [Math]::Max(1, $lineNumber - $context)
        $end = [Math]::Min($lines.Count, $lineNumber + $context)
        "---- Match at line $lineNumber ----"
        for ($i = $start; $i -le $end; $i++) {
            $prefix = if ($i -eq $lineNumber) { '=>' } else { '   ' }
            "{0,4}: {1}{2}" -f $i, $prefix, $lines[$i-1]
        }
    }
}

function Parse-DecimalFromString($s) {
    if (-not $s) { return 0 }
    # extract first number (allow negative, decimals, ignore thousand separators)
    $m = [regex]::Match($s, "-?\d+[\d\s,\.]*")
    if (-not $m.Success) { return 0 }
    $num = $m.Value -replace '[\s,]', ''
    try { return [decimal]::Parse($num, [System.Globalization.CultureInfo]::InvariantCulture) } catch { return 0 }
}

# Resolve config path
$cfgPath = Resolve-ConfigPath -candidate $ConfigPath
if (-not $cfgPath) {
    Write-Host "Could not auto-detect Config folder. Provide path with -ConfigPath 'C:\path\to\Config'" -ForegroundColor Yellow
    exit 1
}
Write-Host "Using Config folder: $cfgPath" -ForegroundColor Cyan

# Define file paths
$files = @{
    debug = Join-Path $cfgPath 'debug_log.txt'
    sank_journal = Join-Path $cfgPath 'sank_journal.txt'
    endshift_journal = Join-Path $cfgPath 'endshift_journal.txt'
    prva_smena = Join-Path $cfgPath 'prva_smena_sank.txt'
    sank_ukupno = Join-Path $cfgPath 'sank_ukupno.txt'
    prenos_sanka = Join-Path $cfgPath 'prenos_sanka.txt'
    sank_total = Join-Path $cfgPath 'sank_total.txt'
    prva_smena_podaci = Join-Path $cfgPath 'prva_smena_podaci.txt'
    zadnja_inkasacija = Join-Path $cfgPath 'zadnja_inkasacija.txt'
}

Write-Host "Files status:" -ForegroundColor Green
foreach ($k in $files.Keys) {
    $p = $files[$k]
    if (Test-Path $p) { Write-Host "  $k -> Exists: $p (LastWrite: $(Get-Item $p).LastWriteTime)" } else { Write-Host "  $k -> MISSING: $p" -ForegroundColor Yellow }
}

# Show last part of debug log and search for endshift markers
if (Test-Path $files.debug) {
    Write-Host "`n--- Last 200 lines of debug_log.txt ---" -ForegroundColor Cyan
    Get-Content $files.debug -Tail 200 | ForEach-Object { Write-Host $_ }

    Write-Host "`n--- Context matches for 'ZAVRSAVANJE SMENE' or 'btnZavrsiSmenu' ---" -ForegroundColor Cyan
    Read-LastMatches -path $files.debug -pattern 'ZAVRSAVANJE SMENE|btnZavrsiSmenu' -context $ContextLines
} else {
    Write-Host "No debug_log.txt to inspect." -ForegroundColor Yellow
}

# Analyze sank_journal for added/delta entries
if (Test-Path $files.sank_journal) {
    Write-Host "`n--- Analyzing sank_journal.txt ---" -ForegroundColor Cyan
    $lines = Get-Content $files.sank_journal | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $sum = 0.0
    $entries = @()
    foreach ($l in $lines) {
        # try split by '|' (journal entries are pipe-separated)
        $parts = $l -split '\|'
        if ($parts.Length -ge 3) {
            $token = $parts[1].Trim().ToLower()
            if ($token -in @('added','delta')) {
                $val = Parse-DecimalFromString($parts[2])
                $sum += $val
                $entries += [PSCustomObject]@{ Raw=$l; Type=$token; Value=$val; Time=$parts[0] }
            }
        }
    }
    Write-Host "Found $($entries.Count) added/delta entries; sum = $sum" -ForegroundColor Green
    if ($entries.Count -gt 0) { $entries | Select-Object -First 30 | Format-Table -AutoSize }
} else { Write-Host "No sank_journal.txt" -ForegroundColor Yellow }

# Analyze endshift_journal for duplicates
if (Test-Path $files.endshift_journal) {
    Write-Host "`n--- Analyzing endshift_journal.txt ---" -ForegroundColor Cyan
    $lines = Get-Content $files.endshift_journal | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $groups = @{}
    foreach ($l in $lines) {
        # format: key|status|timestamp|details
        $p = $l -split '\|',4
        $key = $p[0]
        $status = if ($p.Length -ge 2) { $p[1] } else { '' }
        if (-not $groups.ContainsKey($key)) { $groups[$key] = @() }
        $groups[$key] += @{ Status=$status; Line=$l }
    }
    foreach ($k in $groups.Keys) {
        $count = $groups[$k].Count
        $statuses = ($groups[$k] | ForEach-Object { $_.Status }) -join ','
        if ($count -gt 1) { Write-Host "DUPLICATE KEY: $k -> Count=$count Statuses=[$statuses]" -ForegroundColor Yellow } else { Write-Host "Key: $k -> Count=$count Statuses=[$statuses]" }
    }
} else { Write-Host "No endshift_journal.txt (newer builds)." -ForegroundColor Yellow }

# Compare computed sank total vs current
if ($null -ne $sum) {
    if (Test-Path $files.sank_ukupno) {
        $cur = Parse-DecimalFromString((Get-Content $files.sank_ukupno -Raw))
        Write-Host "`nCurrent sank_ukupno.txt value = $cur" -ForegroundColor Cyan
        Write-Host "Computed from sank_journal added/delta sum = $sum" -ForegroundColor Cyan
        $diff = $sum - $cur
        if ($diff -eq 0) { Write-Host "sank_ukupno matches journal sum." -ForegroundColor Green }
        else { Write-Host "Difference (journal - current) = $diff" -ForegroundColor Yellow }

        if ($Fix) {
            # backup and write
            $bak = "$($files.sank_ukupno).bak.$((Get-Date).ToString('yyyyMMddHHmmss'))"
            Copy-Item -Path $files.sank_ukupno -Destination $bak -ErrorAction SilentlyContinue
            try {
                Set-Content -Path $files.sank_ukupno -Value ($sum.ToString([System.Globalization.CultureInfo]::InvariantCulture)) -Encoding UTF8
                Write-Host "Wrote computed sum $sum to sank_ukupno.txt and backed up original to $bak" -ForegroundColor Green
            } catch {
                Write-Host "Failed to write sank_ukupno.txt: $_" -ForegroundColor Red
            }
        } else {
            Write-Host "To fix sank_ukupno to the computed sum run this script again with -Fix switch (creates backup)." -ForegroundColor Yellow
        }
    } else {
        Write-Host "sank_ukupno.txt missing. If you want to create it with computed sum, run with -Fix." -ForegroundColor Yellow
        if ($Fix) {
            try {
                Set-Content -Path $files.sank_ukupno -Value ($sum.ToString([System.Globalization.CultureInfo]::InvariantCulture)) -Encoding UTF8
                Write-Host "Created sank_ukupno.txt with value $sum" -ForegroundColor Green
            } catch { Write-Host "Failed to create sank_ukupno.txt: $_" -ForegroundColor Red }
        }
    }
}

Write-Host "`nAnalysis complete. Make a backup of the entire Config folder before performing manual edits." -ForegroundColor Cyan
Write-Host "Suggested next steps: inspect debug lines around endshift time, verify prva_smena_sank.txt timestamps, and if journal shows duplicates use the -Fix option to correct sank_ukupno." -ForegroundColor White
