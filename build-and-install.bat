@echo off
chcp 65001 >nul
:: STS2 开局能量+1 Mod - Windows 一键构建安装脚本

setlocal EnableDelayedExpansion

set "MOD_NAME=StartingEnergyMod"
set "PROJECT_DIR=%~dp0"
set "GAME_DIR=E:\SteamLibrary\steamapps\common\Slay the Spire 2"
set "MODS_DIR=%GAME_DIR%\mods"
set "BUILD_OK=0"
set "COPY_DLL_OK=0"
set "COPY_JSON_OK=0"

echo === STS2 %MOD_NAME% 构建安装脚本 ===
echo.

:: 检查 .NET SDK
where dotnet >nul 2>nul
if errorlevel 1 (
    echo [X] .NET SDK 未安装
    echo     请先安装 .NET 9 SDK: https://dotnet.microsoft.com/download/dotnet/9.0
    call :ShowResult 0
    exit /b 1
)
echo [OK] .NET SDK 已安装

:: 检测游戏目录
if not exist "%GAME_DIR%\SlayTheSpire2.exe" (
    if exist "C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe" (
        set "GAME_DIR=C:\Program Files (x86)\Steam\steamapps\common\Slay the Spire 2"
    ) else if exist "D:\SteamLibrary\steamapps\common\Slay the Spire 2\SlayTheSpire2.exe" (
        set "GAME_DIR=D:\SteamLibrary\steamapps\common\Slay the Spire 2"
    ) else (
        echo [X] 找不到游戏目录
        call :ShowResult 0
        exit /b 1
    )
)
echo [OK] 游戏目录: %GAME_DIR%

:: 创建 mods 目录
if not exist "%MODS_DIR%" (
    mkdir "%MODS_DIR%" >nul 2>nul
    if not exist "%MODS_DIR%" (
        echo [X] 无法创建 mods 目录
        call :ShowResult 0
        exit /b 1
    )
    echo [OK] 已创建 mods 目录
) else (
    echo [OK] mods 目录已存在
)

:: 构建项目
echo.
echo [..] 正在构建 Mod DLL...
cd /d "%PROJECT_DIR%"
dotnet build -c Release -p:GameDir="%GAME_DIR%" >"%TEMP%\sts2_build.log" 2>&1

if errorlevel 1 (
    echo [X] 构建失败
    echo     错误日志:
    type "%TEMP%\sts2_build.log"
    call :ShowResult 0
    exit /b 1
)
echo [OK] 构建成功
set "BUILD_OK=1"

:: 检查构建产物
set "DLL_SRC=%PROJECT_DIR%bin\Release\net9.0\%MOD_NAME%.dll"
set "JSON_SRC=%PROJECT_DIR%%MOD_NAME%.json"

if not exist "%DLL_SRC%" (
    echo [X] 找不到构建产物: %DLL_SRC%
    call :ShowResult 0
    exit /b 1
)
echo [OK] DLL 文件已生成

if not exist "%JSON_SRC%" (
    echo [X] 找不到元数据文件: %JSON_SRC%
    call :ShowResult 0
    exit /b 1
)
echo [OK] JSON 文件已找到

:: 复制 DLL
echo.
echo [..] 正在复制 DLL 到游戏目录...
copy /Y "%DLL_SRC%" "%MODS_DIR%\%MOD_NAME%.dll" >nul 2>nul
if errorlevel 1 (
    echo [X] DLL 复制失败
    call :ShowResult 0
    exit /b 1
)
echo [OK] DLL 已安装
set "COPY_DLL_OK=1"

:: 复制 JSON
echo [..] 正在复制 JSON 到游戏目录...
copy /Y "%JSON_SRC%" "%MODS_DIR%\%MOD_NAME%.json" >nul 2>nul
if errorlevel 1 (
    echo [X] JSON 复制失败
    call :ShowResult 0
    exit /b 1
)
echo [OK] JSON 已安装
set "COPY_JSON_OK=1"

:: 最终校验
echo.
echo [..] 校验安装结果...
if not exist "%MODS_DIR%\%MOD_NAME%.dll" (
    echo [X] 校验失败: DLL 不在目标目录
    call :ShowResult 0
    exit /b 1
)
if not exist "%MODS_DIR%\%MOD_NAME%.json" (
    echo [X] 校验失败: JSON 不在目标目录
    call :ShowResult 0
    exit /b 1
)
echo [OK] 文件校验通过

call :ShowResult 1
exit /b 0

:: ==========================================
:: 显示最终结果子程序
:: 参数: %1 = 1成功, 0失败
:: ==========================================
:ShowResult
cls
echo ==========================================
if "%1"=="1" (
    color 0E
    echo    构建 + 安装 成功！
    echo ==========================================
    echo.
    echo [OK] 构建:   成功
    echo [OK] 复制:   成功
    echo [OK] 校验:   通过
    echo.
    echo 安装位置:
    echo   %MODS_DIR%\%MOD_NAME%.dll
    echo   %MODS_DIR%\%MOD_NAME%.json
    echo.
    echo 下一步:
    echo   1. 启动 Slay the Spire 2
    echo   2. 主菜单点击 Mods
    echo   3. 找到 开局能量+1 并启用
    echo   4. 开始游戏，享受 4 点初始能量！
) else (
    color 0C
    echo    构建 + 安装 失败！
    echo ==========================================
    echo.
    echo 请检查上方错误信息，常见问题:
    echo   - .NET 9 SDK 未安装
    echo   - 游戏目录路径错误
    echo   - 文件被占用，关闭游戏后重试
)
echo.
color 07
pause
exit /b 0
