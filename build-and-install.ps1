#Requires -Version 5.1
# STS2 sts2-better-reward Mod - PowerShell 一键构建安装脚本

$MOD_NAME = "sts2-better-reward"
$PROJECT_DIR = $PSScriptRoot
$GAME_DIR = "E:\SteamLibrary\steamapps\common\Slay the Spire 2"
$MODS_DIR = Join-Path $GAME_DIR "mods"

# 颜色函数
function Write-Ok($msg) { Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Err($msg) { Write-Host "[X]  $msg" -ForegroundColor Red }
function Write-Info($msg) { Write-Host "[..] $msg" -ForegroundColor Cyan }
function Write-Warn($msg) { Write-Host "[!]  $msg" -ForegroundColor Yellow }

# 最终结果
function Show-Result($success, $details) {
    Clear-Host
    Write-Host "==========================================" -ForegroundColor White
    if ($success) {
        Write-Host "   构建 + 安装 成功！" -ForegroundColor Yellow
    } else {
        Write-Host "   构建 + 安装 失败！" -ForegroundColor Red
    }
    Write-Host "==========================================" -ForegroundColor White
    Write-Host ""
    foreach ($line in $details) {
        Write-Host $line
    }
    Write-Host ""
    Read-Host "按 Enter 键退出"
    exit $(if ($success) { 0 } else { 1 })
}

# 检查 .NET SDK
Write-Info "检查 .NET SDK..."
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    Show-Result $false @(
        "[X] .NET SDK 未安装",
        "    请先安装 .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0"
    )
}
Write-Ok ".NET SDK 已安装"

# 检测游戏目录
Write-Info "检测游戏目录..."
if (-not (Test-Path (Join-Path $GAME_DIR "SlayTheSpire2.exe"))) {
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2",
        "D:\SteamLibrary\steamapps\common\Slay the Spire 2"
    )
    $found = $false
    foreach ($c in $candidates) {
        if (Test-Path (Join-Path $c "SlayTheSpire2.exe")) {
            $GAME_DIR = $c
            $MODS_DIR = Join-Path $GAME_DIR "mods"
            $found = $true
            break
        }
    }
    if (-not $found) {
        Show-Result $false @(
            "[X] 找不到游戏目录",
            "    请修改脚本中的 GAME_DIR 变量"
        )
    }
}
Write-Ok "游戏目录: $GAME_DIR"

# 创建 mods 目录
Write-Info "检查 mods 目录..."
if (-not (Test-Path $MODS_DIR)) {
    try {
        New-Item -ItemType Directory -Path $MODS_DIR -Force | Out-Null
        Write-Ok "已创建 mods 目录"
    } catch {
        Show-Result $false @(
            "[X] 无法创建 mods 目录",
            "    错误: $($_.Exception.Message)"
        )
    }
} else {
    Write-Ok "mods 目录已存在"
}

# 清理 Godot 缓存（避免 AssemblyInfo.cs 重复）
Write-Info "清理 Godot 缓存..."
$godotCache = Join-Path $PROJECT_DIR ".godot"
if (Test-Path $godotCache) {
    Remove-Item -Recurse -Force $godotCache
    Write-Ok "已清理 .godot 缓存"
}

# 构建项目
Write-Info "构建 Mod DLL..."
Push-Location $PROJECT_DIR
try {
    # 直接运行 dotnet build 并捕获输出
    $buildOutput = dotnet build -c Release -p:GameDirWin="$GAME_DIR" 2>&1
    $buildExitCode = $LASTEXITCODE
    if ($buildExitCode -ne 0) {
        Show-Result $false @(
            "[X] 构建失败 (退出码: $buildExitCode)",
            "    输出:",
            "    $buildOutput"
        )
    }
} catch {
    Show-Result $false @(
        "[X] 构建命令执行失败",
        "    错误: $($_.Exception.Message)"
    )
}
Pop-Location
Write-Ok "构建成功"

# 检查构建产物 (Godot SDK 输出到 .godot/mono/temp/bin/Release/)
$DLL_SRC = Join-Path $PROJECT_DIR ".godot\mono\temp\bin\Release\$MOD_NAME.dll"
$JSON_SRC = Join-Path $PROJECT_DIR "$MOD_NAME.json"

Write-Info "检查构建产物..."
if (-not (Test-Path $DLL_SRC)) {
    Show-Result $false @(
        "[X] 找不到 DLL: $DLL_SRC"
    )
}
Write-Ok "DLL 文件已生成"

if (-not (Test-Path $JSON_SRC)) {
    Show-Result $false @(
        "[X] 找不到 JSON: $JSON_SRC"
    )
}
Write-Ok "JSON 文件已找到"

# 复制 DLL
Write-Info "复制 DLL 到游戏目录..."
try {
    Copy-Item -Path $DLL_SRC -Destination (Join-Path $MODS_DIR "$MOD_NAME.dll") -Force
    Write-Ok "DLL 已安装"
} catch {
    Show-Result $false @(
        "[X] DLL 复制失败",
        "    错误: $($_.Exception.Message)"
    )
}

# 复制 JSON
Write-Info "复制 JSON 到游戏目录..."
try {
    Copy-Item -Path $JSON_SRC -Destination (Join-Path $MODS_DIR "$MOD_NAME.json") -Force
    Write-Ok "JSON 已安装"
} catch {
    Show-Result $false @(
        "[X] JSON 复制失败",
        "    错误: $($_.Exception.Message)"
    )
}

# 最终校验
Write-Info "校验安装结果..."
if (-not (Test-Path (Join-Path $MODS_DIR "$MOD_NAME.dll"))) {
    Show-Result $false @("[X] 校验失败: DLL 不在目标目录")
}
if (-not (Test-Path (Join-Path $MODS_DIR "$MOD_NAME.json"))) {
    Show-Result $false @("[X] 校验失败: JSON 不在目标目录")
}
Write-Ok "文件校验通过"

# 成功结果
Show-Result $true @(
    "[OK] 构建:   成功",
    "[OK] 复制:   成功",
    "[OK] 校验:   通过",
    "",
    "安装位置:",
    "  $MODS_DIR\$MOD_NAME.dll",
    "  $MODS_DIR\$MOD_NAME.json",
    "",
    "下一步:",
    "  1. 启动 Slay the Spire 2",
    "  2. 主菜单点击 Mods",
    "  3. 找到 sts2-better-reward 并启用",
    "  4. 开始游戏，享受最大能量 +1 和精英/Boss 随机额外奖励！"
)
