$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$Python = Join-Path $Root ".venv\Scripts\python.exe"
$Uv = Get-Command uv -ErrorAction SilentlyContinue

if (-not (Test-Path $Python)) {
    if ($Uv) {
        & uv venv --python 3.12 --seed (Join-Path $Root ".venv")
    } else {
        python -m venv (Join-Path $Root ".venv")
    }
}

if ($Uv) {
    & uv pip install -e ".[dev]" --python $Python
} else {
    & $Python -m pip install -e ".[dev]"
}

& $Python -m PyInstaller (Join-Path $Root "packaging\atten.spec") --noconfirm --clean
