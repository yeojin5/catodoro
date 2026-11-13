# Microsoft Store 에 올릴 MSIX 패키지를 만든다.
#   사용법:  powershell -ExecutionPolicy Bypass -File store\build-msix.ps1
#   결과물:  store\out\Catodoro_<버전>_x64.msix
# 필요한 것: .NET SDK, Windows SDK(makeappx.exe / makepri.exe)
# 스토어에 올리는 패키지는 직접 서명하지 않는다(스토어가 서명한다).

$ErrorActionPreference = "Stop"
$store = $PSScriptRoot
$root = Split-Path $store -Parent
$out = Join-Path $store "out"
$layout = Join-Path $out "layout"

function Find-SdkTool([string]$name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $bin = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    $found = Get-ChildItem $bin -Recurse -Filter $name -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "\\x64\\" } | Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $found) { throw "$name 을 찾지 못했습니다. Windows SDK 를 설치해 주세요." }
    return $found.FullName
}

$makeappx = Find-SdkTool "makeappx.exe"
$makepri = Find-SdkTool "makepri.exe"

# 1) 버전과 제품 ID
[xml]$proj = Get-Content (Join-Path $root "Catodoro.csproj") -Encoding UTF8
$version = ($proj.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
$version4 = "$version.0"   # MSIX 는 네 자리 버전을 쓴다 (스토어 규칙: 마지막 자리는 0)
$id = Get-Content (Join-Path $store "identity.json") -Raw -Encoding UTF8 | ConvertFrom-Json
if ($id.Publisher -eq "CN=CatodoroTest") {
    Write-Warning "identity.json 이 아직 시험용 값입니다. 이 패키지는 스토어에 올릴 수 없습니다."
}

# 2) 빌드 (실행 중인 Catodoro 가 exe 를 잡고 있으면 빌드가 실패한다)
if (Get-Process Catodoro -ErrorAction SilentlyContinue) { throw "Catodoro 가 실행 중입니다. 닫은 뒤 다시 실행해 주세요." }
dotnet build (Join-Path $root "Catodoro.csproj") -c Release -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "빌드 실패" }
$exe = Join-Path $root "bin\Release\Catodoro.exe"

# 3) 패키지에 넣을 파일 배치
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force (Join-Path $layout "Assets") | Out-Null
Copy-Item $exe $layout

$logos = Join-Path $out "logos"
Start-Process $exe -ArgumentList "--export-assets `"$logos`"" -Wait
Copy-Item (Join-Path $logos "store\*.png") (Join-Path $layout "Assets") -Exclude "StoreListingIcon-*"
Copy-Item (Join-Path $logos "store\StoreListingIcon-300.png") $out

$manifest = Get-Content (Join-Path $store "AppxManifest.xml") -Raw -Encoding UTF8
$manifest = $manifest.Replace("{{IdentityName}}", $id.IdentityName).Replace("{{Publisher}}", $id.Publisher)
$manifest = $manifest.Replace("{{PublisherDisplayName}}", $id.PublisherDisplayName).Replace("{{Version}}", $version4)
[System.IO.File]::WriteAllText((Join-Path $layout "AppxManifest.xml"), $manifest, (New-Object System.Text.UTF8Encoding($false)))

# 4) 배율별 로고를 윈도우가 고를 수 있게 색인(resources.pri)을 만든다
$priConfig = Join-Path $out "priconfig.xml"
& $makepri createconfig /cf $priConfig /dq en-US /pv 10.0.0 /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri createconfig 실패" }
[xml]$cfg = Get-Content $priConfig
$packaging = $cfg.resources.packaging        # 색인을 여러 파일로 쪼개는 설정은 뺀다
if ($packaging) { [void]$cfg.resources.RemoveChild($packaging) }
$cfg.Save($priConfig)
& $makepri new /pr $layout /cf $priConfig /mn (Join-Path $layout "AppxManifest.xml") /of (Join-Path $layout "resources.pri") /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makepri new 실패" }

# 5) 묶기
$msix = Join-Path $out "Catodoro_${version4}_x64.msix"
& $makeappx pack /d $layout /p $msix /o | Out-Null
if ($LASTEXITCODE -ne 0) { throw "makeappx pack 실패" }

Write-Host ""
Write-Host "완성: $msix ($([math]::Round((Get-Item $msix).Length / 1KB)) KB)"
