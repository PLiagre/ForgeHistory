# Minimal launcher for unity/game_unity/ — wraps the one supported way to
# open the ported project (see unity/README.md, brief 003-port-unity-game,
# Success Condition 7). This is NOT a replacement for VictoriaProject's
# automation/queue/lock machinery (automation/demo.py, automation/run_queue.py,
# cursor_tasks/, runtime_bridge/) — that layer was deliberately not ported;
# see docs/adr/0004-bulk-port-victoriaproject-unity-game.md.

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$unityExe = "C:\Program Files\Unity\Hub\Editor\6000.0.43f1\Editor\Unity.exe"
$projectPath = Join-Path $repoRoot "unity\game_unity"
$openFile = Join-Path $repoRoot "unity\game_unity\Assets\Scenes\Main.unity"

if (-not (Test-Path $unityExe)) {
    throw "Unity 6000.0.43f1 editor not found at declared path: $unityExe"
}
if (-not (Test-Path $projectPath)) {
    throw "unity/game_unity not found at: $projectPath (has brief 003's port run yet?)"
}

& $unityExe -projectPath $projectPath -openfile $openFile
