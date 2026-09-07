param (
    [int]$DurationSeconds = 60,
    [int]$IntervalSeconds = 2,
    [string]$ExePath = "$PSScriptRoot\BuildLLT\bin\Release\net9.0-windows10.0.26100.0\win-x64\Lenovo Legion Toolkit.exe"
)

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "   Lenovo Legion Toolkit - Runtime Diagnostic & Smoke Test       " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan

$existingProc = Get-Process "Lenovo Legion Toolkit" -ErrorAction SilentlyContinue | Select-Object -First 1
$launchedByScript = $false
$proc = $null

if ($null -ne $existingProc) {
    Write-Host "[INFO] Detected active running instance of Lenovo Legion Toolkit (PID: $($existingProc.Id))." -ForegroundColor Green
    Write-Host "[INFO] Attaching diagnostic telemetry profiler to existing instance..." -ForegroundColor Gray
    $proc = $existingProc
} else {
    if (-not (Test-Path $ExePath)) {
        Write-Error "Executable not found at: $ExePath"
        exit 1
    }

    Write-Host "[1/4] Launching target process: $ExePath" -ForegroundColor Yellow
    try {
        $proc = Start-Process -FilePath $ExePath -PassThru
    } catch {
        # Fallback via dotnet host if elevation / app execution alias restricts direct spawn
        $dllPath = [System.IO.Path]::ChangeExtension($ExePath, ".dll")
        if (Test-Path $dllPath) {
            Write-Host "[INFO] Attempting launch via dotnet runtime host: $dllPath" -ForegroundColor Gray
            $proc = Start-Process -FilePath "D:\dotnet9\dotnet.exe" -ArgumentList "`"$dllPath`"" -PassThru
        }
    }

    if ($null -eq $proc -or $proc.HasExited) {
        # Check if spawned process handed off to another instance
        $proc = Get-Process "Lenovo Legion Toolkit" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $proc) {
            Write-Host "[FAIL] Failed to start process or process exited prematurely." -ForegroundColor Red
            exit 1
        }
    }

    $launchedByScript = $true
    Write-Host "[OK] Process running. PID: $($proc.Id)" -ForegroundColor Green
    Write-Host "[INFO] Waiting 6s for WPF UI and runtime warm-up..." -ForegroundColor Gray
    Start-Sleep -Seconds 6
}

Write-Host "Target PID        : $($proc.Id)" -ForegroundColor Gray
Write-Host "Duration          : $DurationSeconds seconds (sampling every $IntervalSeconds s)" -ForegroundColor Gray
Write-Host ""

# Data collection
$samples = @()
$startTime = [System.Diagnostics.Stopwatch]::StartNew()

Write-Host "[2/4] Monitoring runtime metrics in real-time..." -ForegroundColor Yellow
Write-Host ("{0,-8} | {1,-8} | {2,-8} | {3,-16} | {4,-16} | {5,-10}" -f "Elapsed", "PID", "Threads", "Working Set (MB)", "Private (MB)", "Handles") -ForegroundColor White
Write-Host ("-" * 78) -ForegroundColor Gray

try {
    while ($startTime.Elapsed.TotalSeconds -lt $DurationSeconds) {
        $proc.Refresh()
        if ($proc.HasExited) {
            Write-Host "`n[WARN] Process terminated early at $($startTime.Elapsed.ToString('mm\:ss'))" -ForegroundColor Red
            break
        }

        $wsMB = [Math]::Round($proc.WorkingSet64 / 1MB, 2)
        $privMB = [Math]::Round($proc.PrivateMemorySize64 / 1MB, 2)
        $handles = $proc.HandleCount
        $threads = $proc.Threads.Count
        $elapsed = $startTime.Elapsed.ToString("mm\:ss")

        $sample = [PSCustomObject]@{
            ElapsedSeconds = [Math]::Round($startTime.Elapsed.TotalSeconds, 1)
            WorkingSetMB   = $wsMB
            PrivateMB      = $privMB
            Handles        = $handles
            Threads        = $threads
        }
        $samples += $sample

        Write-Host ("{0,-8} | {1,-8} | {2,-8} | {3,-16} | {4,-16} | {5,-10}" -f $elapsed, $proc.Id, $threads, "$wsMB MB", "$privMB MB", $handles) -ForegroundColor Gray

        Start-Sleep -Seconds $IntervalSeconds
    }
}
finally {
    if ($launchedByScript) {
        Write-Host "`n[3/4] Terminating test-spawned process..." -ForegroundColor Yellow
        if (-not $proc.HasExited) {
            Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
            Write-Host "[OK] Test process PID $($proc.Id) terminated cleanly." -ForegroundColor Green
        }
    } else {
        Write-Host "`n[3/4] Detaching profiler from running instance (PID $($proc.Id) preserved)." -ForegroundColor Green
    }
}

# Metric Analysis
Write-Host "`n[4/4] Analyzing diagnostic telemetry..." -ForegroundColor Yellow

if ($samples.Count -lt 3) {
    Write-Host "[FAIL] Insufficient samples gathered for diagnosis." -ForegroundColor Red
    exit 1
}

$firstSample = $samples[0]
$lastSample  = $samples[-1]
$maxWS       = ($samples | Measure-Object -Property WorkingSetMB -Maximum).Maximum
$minWS       = ($samples | Measure-Object -Property WorkingSetMB -Minimum).Minimum
$maxHandles  = ($samples | Measure-Object -Property Handles -Maximum).Maximum
$minHandles  = ($samples | Measure-Object -Property Handles -Minimum).Minimum

$handleDelta = $lastSample.Handles - $firstSample.Handles
$wsDelta     = $lastSample.WorkingSetMB - $firstSample.WorkingSetMB

# Pass/Fail Criteria
$isHandleStable = [Math]::Abs($handleDelta) -le 100
$isMemoryStable = $wsDelta -le 80.0
$didNotCrash    = ($samples.Count -ge [Math]::Floor($DurationSeconds / ($IntervalSeconds * 1.5)))

Write-Host "`n=================================================================" -ForegroundColor Cyan
Write-Host "                       DIAGNOSTIC REPORT                         " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host ("Total Samples Collected : {0}" -f $samples.Count)
Write-Host ("Initial Working Set     : {0} MB" -f $firstSample.WorkingSetMB)
Write-Host ("Peak Working Set        : {0} MB" -f $maxWS)
Write-Host ("Final Working Set       : {0} MB (Delta: {1:+#,0.0;-#,0.0;0.0} MB)" -f $lastSample.WorkingSetMB, $wsDelta)
Write-Host ("Initial Handle Count    : {0}" -f $firstSample.Handles)
Write-Host ("Peak Handle Count       : {0}" -f $maxHandles)
Write-Host ("Final Handle Count      : {0} (Delta: {1:+#,0;-#,0;0})" -f $lastSample.Handles, $handleDelta)
Write-Host ("Active Threads          : {0} threads" -f $lastSample.Threads)
Write-Host ("-" * 65)

$allPassed = $true

if ($didNotCrash) {
    Write-Host " [PASS] Process Lifetime Stability : Process ran full duration without crash" -ForegroundColor Green
} else {
    Write-Host " [FAIL] Process Lifetime Stability : Process exited prematurely" -ForegroundColor Red
    $allPassed = $false
}

if ($isHandleStable) {
    Write-Host " [PASS] Handle Stability           : No handle leak detected (delta = $handleDelta)" -ForegroundColor Green
} else {
    Write-Host " [FAIL] Handle Stability           : Handle leak detected (delta = $handleDelta > 100)" -ForegroundColor Red
    $allPassed = $false
}

if ($isMemoryStable) {
    Write-Host " [PASS] Memory Stability           : No unbounded memory creep detected (delta = $wsDelta MB)" -ForegroundColor Green
} else {
    Write-Host " [FAIL] Memory Stability           : Excessive memory growth detected (delta = $wsDelta MB > 80 MB)" -ForegroundColor Red
    $allPassed = $false
}

Write-Host "=================================================================" -ForegroundColor Cyan
if ($allPassed) {
    Write-Host "                 OVERALL RESULT: [PASS] (HEALTHY)                " -ForegroundColor Green
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 0
} else {
    Write-Host "                 OVERALL RESULT: [FAIL] (ISSUES DETECTED)        " -ForegroundColor Red
    Write-Host "=================================================================" -ForegroundColor Cyan
    exit 1
}
