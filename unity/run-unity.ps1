<#
.SYNOPSIS
  Run (or watch) a Unity batchmode job to completion in a SINGLE tool call.

.DESCRIPTION
  Replaces the poll-the-logFile pattern that briefs 003-005 prescribed.

  The old pattern existed for a real reason: a first `Library/` rebuild can
  run 10-40 minutes, well past any tool's own call timeout, so the brief
  told the Generateur to start Unity detached and re-check the log every
  30-60 s. Each re-check is a separate API request that re-sends the whole
  accumulated context, and context grows monotonically within an agent --
  so the polling loop is paid at the agent's *current* context size, over
  and over. One measured Generateur spent 586 tool calls on `wc -l` of a
  single log file.

  This script waits inside one PowerShell process instead, and returns
  exactly once. Two supported regimes, neither of which polls:

    short jobs  - call it in the foreground with -TimeoutSec under the
                  tool's own ceiling. One call, one return.
    long jobs   - call it with the Bash tool's run_in_background, which
                  re-invokes the agent when the process exits. Also one
                  call; the completion signal is a notification, not a poll.

  It never streams the log into the transcript. The full log stays on disk
  where Unity wrote it; stdout gets a bounded summary.

.PARAMETER LogFile
  Absolute path Unity writes with -logFile. Required, and required to be
  absolute: a relative -logFile has produced empty/truncated logs on this
  machine (brief 003, Success Condition 3). Need not exist yet.

.PARAMETER UnityArgLine
  The Unity argument line, passed to the process verbatim. A single string
  rather than an array on purpose: every Unity flag starts with '-', so an
  array parameter would have PowerShell trying to bind '-batchmode' as a
  parameter of this script. Pass -logFile inside it too -- this script does
  not inject it, so Unity receives exactly what the brief specifies.

.PARAMETER UnityExe
  Unity executable. Defaults to the declared 6000.0.43f1 install. Also the
  test seam: tests point it at a stand-in so the suite never needs Unity.

.PARAMETER AttachPid
  Watch an already-running process instead of launching one. Returns
  immediately if that process has already exited.

.PARAMETER TimeoutSec
  Wall-clock ceiling. On expiry the process tree is killed (taskkill /T /F)
  and the script exits 124. Default 540 s, just under the 600 s tool ceiling.

.PARAMETER TestResults
  Optional NUnit XML from -testResults. When present and parseable, its
  <test-run> totals go in the summary, so the agent does not open the XML.

.PARAMETER MaxErrorLines
  Cap on distinct error lines echoed into the summary. Default 10.

.OUTPUTS
  Exit code: 0 on Unity exit 0; Unity's own code when it exits non-zero;
  124 on timeout; 125 on a precondition failure (bad args, missing exe,
  unwatchable pid). 124/125 are this script's, not Unity's -- Unity does
  not use them.
#>
[CmdletBinding(DefaultParameterSetName = "Launch")]
param(
    [Parameter(Mandatory = $true)]
    [string]$LogFile,

    [Parameter(ParameterSetName = "Launch")]
    [string]$UnityArgLine = "",

    [Parameter(ParameterSetName = "Launch")]
    [string]$UnityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe",

    [Parameter(ParameterSetName = "Attach", Mandatory = $true)]
    [int]$AttachPid,

    [int]$TimeoutSec = 540,
    [string]$TestResults = "",
    [int]$MaxErrorLines = 10
)

$ErrorActionPreference = "Stop"

# Reserved by this script; Unity does not use them.
$EXIT_TIMEOUT = 124
$EXIT_PRECONDITION = 125

function Write-Summary {
    param(
        [string]$Outcome,
        [int]$ExitCode,
        [double]$ElapsedSec,
        [string]$Log,
        [string]$Xml,
        [int]$MaxErrors,
        [string]$Note = "",
        [int]$WatchedPid = 0
    )
    # One bounded block, emitted once. Never the log body.
    Write-Output "=== unity-run summary ==="
    Write-Output ("outcome    : {0}" -f $Outcome)
    Write-Output ("exit_code  : {0}" -f $ExitCode)
    Write-Output ("elapsed_s  : {0:N1}" -f $ElapsedSec)
    Write-Output ("watched_pid: {0}" -f $WatchedPid)
    Write-Output ("log_file   : {0}" -f $Log)

    if (-not (Test-Path -LiteralPath $Log)) {
        # A real signal, not a script bug: Unity ran but wrote no log. The
        # usual cause on this machine is a relative -logFile path.
        Write-Output "log_status : ABSENT (Unity wrote no log at that path)"
    }
    else {
        $item = Get-Item -LiteralPath $Log
        $lineCount = 0
        $compileErrors = @()
        $otherErrors = @()
        # Stream the file; never hold it all in memory, never echo it.
        foreach ($line in [System.IO.File]::ReadLines($item.FullName)) {
            $lineCount++
            if ($line -match 'error CS\d+') { $compileErrors += $line }
            elseif ($line -match '^\s*(Fatal|Unhandled Exception|Aborting)') { $otherErrors += $line }
        }
        Write-Output ("log_status : {0} bytes, {1} lines" -f $item.Length, $lineCount)
        Write-Output ("compile_errors : {0}" -f $compileErrors.Count)

        $shown = @($compileErrors) + @($otherErrors) | Select-Object -Unique | Select-Object -First $MaxErrors
        foreach ($line in $shown) {
            $trimmed = $line.Trim()
            if ($trimmed.Length -gt 200) { $trimmed = $trimmed.Substring(0, 200) + " ...[truncated]" }
            Write-Output ("  ! {0}" -f $trimmed)
        }
        $total = $compileErrors.Count + $otherErrors.Count
        if ($total -gt $shown.Count) {
            Write-Output ("  ... {0} more error line(s) in the log; read the file, not this summary." -f ($total - $shown.Count))
        }
    }

    if ($Xml -and (Test-Path -LiteralPath $Xml)) {
        try {
            $run = ([xml](Get-Content -LiteralPath $Xml -Raw)).SelectSingleNode("//test-run")
            if ($run) {
                Write-Output ("tests      : total={0} passed={1} failed={2} skipped={3}" -f `
                        $run.total, $run.passed, $run.failed, $run.skipped)
            }
        }
        catch {
            # Corrupt/partial XML is itself worth reporting, not worth crashing on.
            Write-Output ("tests      : UNPARSEABLE ({0})" -f $Xml)
        }
    }
    elseif ($Xml) {
        Write-Output ("tests      : results XML absent at {0}" -f $Xml)
    }

    if ($Note) { Write-Output ("note       : {0}" -f $Note) }
    Write-Output "=== end unity-run summary ==="
}

function Stop-Tree {
    param([int]$TreePid)
    # /T takes descendants too -- a killed Unity that leaves child compilers
    # behind is exactly the orphan case this wrapper must not create.
    & taskkill.exe /PID $TreePid /T /F 2>&1 | Out-Null
    # taskkill returns before the handle is fully released; give the OS a
    # brief, bounded moment so the caller's "is it gone?" check is truthful.
    for ($i = 0; $i -lt 20; $i++) {
        if (-not (Get-Process -Id $TreePid -ErrorAction SilentlyContinue)) { return $true }
        Start-Sleep -Milliseconds 100
    }
    return $false
}

function Fail-Precondition {
    # Not Write-Error: with $ErrorActionPreference = "Stop" that terminates
    # the script before `exit` runs, and the caller sees PowerShell's own 1
    # instead of this script's documented 125.
    param([string]$Message)
    [Console]::Error.WriteLine("run-unity: $Message")
    exit $EXIT_PRECONDITION
}

if (-not [System.IO.Path]::IsPathRooted($LogFile)) {
    Fail-Precondition "LogFile must be an absolute path (a relative -logFile has produced empty logs on this machine). Got: $LogFile"
}
if ($TimeoutSec -le 0) {
    Fail-Precondition "TimeoutSec must be positive. Got: $TimeoutSec"
}

$started = Get-Date
$process = $null

if ($PSCmdlet.ParameterSetName -eq "Attach") {
    $process = Get-Process -Id $AttachPid -ErrorAction SilentlyContinue
    if (-not $process) {
        # Already finished. Report what is on disk and return -- do not wait
        # on a pid that will never exit, and do not pretend it failed.
        Write-Summary -Outcome "already_exited" -ExitCode 0 `
            -ElapsedSec 0 -Log $LogFile -Xml $TestResults -MaxErrors $MaxErrorLines `
            -Note "pid $AttachPid was not running; nothing to wait for."
        exit 0
    }
}
else {
    if (-not (Test-Path -LiteralPath $UnityExe)) {
        Fail-Precondition "Unity executable not found at declared path: $UnityExe"
    }
    # System.Diagnostics.Process rather than Start-Process -PassThru: the
    # latter's returned object leaves .ExitCode null often enough that the
    # only safe reading of it would be "unknown", and an unknown exit code
    # silently defaulted to 0 is precisely how a failed Unity run would sail
    # past the gate. This path reports the real code.
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $UnityExe
        $psi.Arguments = $UnityArgLine
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true
        # stdout/stderr are deliberately NOT redirected: Unity's output goes
        # to -logFile, and redirecting a pipe nobody drains deadlocks on a
        # full buffer -- a hang that would look exactly like the timeout this
        # wrapper is supposed to diagnose.
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $psi
        [void]$process.Start()
    }
    catch {
        Fail-Precondition ("Failed to start Unity: {0}" -f $_.Exception.Message)
    }
}

$watchedPid = $process.Id

# The whole point: ONE wait, in this process. No loop, no log tailing, no
# re-entry into the agent until this returns.
$exitedInTime = $process.WaitForExit($TimeoutSec * 1000)
$elapsed = ((Get-Date) - $started).TotalSeconds

if (-not $exitedInTime) {
    $killed = Stop-Tree -TreePid $watchedPid
    Write-Summary -Outcome "timeout" -ExitCode $EXIT_TIMEOUT -WatchedPid $watchedPid `
        -ElapsedSec $elapsed -Log $LogFile -Xml $TestResults -MaxErrors $MaxErrorLines `
        -Note ("exceeded TimeoutSec={0}; process tree {1} (pid {2}). Re-run with a larger -TimeoutSec, or in the background." -f `
            $TimeoutSec, $(if ($killed) { "killed" } else { "KILL UNCONFIRMED" }), $watchedPid)
    exit $EXIT_TIMEOUT
}

# Flush any async plumbing before reading ExitCode.
$process.WaitForExit()

$code = $null
try { $code = $process.ExitCode } catch { $code = $null }

if ($null -eq $code) {
    # Only reachable on the -AttachPid path: a process this script did not
    # start may not expose its exit code. Say so rather than defaulting to
    # 0 -- an unreadable code reported as success is how a failed Unity run
    # would pass the gate.
    Write-Summary -Outcome "exited_code_unreadable" -ExitCode -1 -WatchedPid $watchedPid `
        -ElapsedSec $elapsed -Log $LogFile -Xml $TestResults -MaxErrors $MaxErrorLines `
        -Note "process exited but its exit code is not readable from here (attached, not launched). Judge by the log and results XML, not by this script's status."
    exit 0
}

Write-Summary -Outcome $(if ($code -eq 0) { "success" } else { "unity_failed" }) `
    -ExitCode $code -WatchedPid $watchedPid -ElapsedSec $elapsed -Log $LogFile -Xml $TestResults `
    -MaxErrors $MaxErrorLines

exit $code
