$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$resources = Join-Path $root 'resources'
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path $csc)) {
    throw "找不到 .NET Framework C# 编译器：$csc"
}
New-Item -ItemType Directory -Force -Path $dist | Out-Null
New-Item -ItemType Directory -Force -Path $resources | Out-Null

# 生成程序图标（如果还没有）
$icon = Join-Path $resources '课件捕手.ico'
if (-not (Test-Path $icon)) {
    $iconGen = Join-Path $root 'tools\IconGen.cs'
    if (Test-Path $iconGen) {
        $iconExe = Join-Path $root 'tools\IconGen.exe'
        & $csc /nologo /target:exe /platform:anycpu /codepage:65001 `
            ("/out:" + $iconExe) `
            /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll' `
            /reference:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll' `
            (Join-Path $src 'IconFactory.cs') `
            (Join-Path $src 'NativeMethods.cs') `
            $iconGen
        if ($LASTEXITCODE -ne 0) { throw "图标生成器编译失败，退出码 $LASTEXITCODE" }
        & $iconExe $icon
        if ($LASTEXITCODE -ne 0) { throw "图标生成失败，退出码 $LASTEXITCODE" }
    }
}

$sources = Get-ChildItem -Path $src -Filter '*.cs' | ForEach-Object { $_.FullName }
$refs = @(
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Core.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Drawing.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Windows.Forms.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xml.dll',
    'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\Microsoft.CSharp.dll'
)

$cscArgs = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/codepage:65001',
    ('/out:' + (Join-Path $dist '课件捕手.exe'))
)
if (Test-Path $icon) {
    $cscArgs += '/win32icon:' + $icon
    # 同时作为托管资源嵌入，供 IconFactory 在托盘/窗口标题栏按 DPI 选尺寸。
    $cscArgs += '/resource:' + $icon + ',CourseCapture.ico'
}
foreach ($r in $refs) { $cscArgs += '/reference:' + $r }
$cscArgs += $sources

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }
Write-Host ("已生成：" + (Join-Path $dist '课件捕手.exe'))
