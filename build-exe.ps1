# Builds dist\KeepersCodex.exe: one file with the whole codex inside.
#   1. stage the site (fonts made local) into build\site
#   2. zip it into build\site.zip
#   3. compile app\Program.cs with the csc.exe that ships with .NET Framework 4.x,
#      embedding site.zip, the icon, the manifest and the WebView2 SDK (build\sdk, NuGet Microsoft.Web.WebView2)
#   4. run the new exe in self-test mode; only a passing build is copied to dist\
# Run it as a file:  powershell -ExecutionPolicy Bypass -File build-exe.ps1
# ASCII-only source: Windows PowerShell 5.1 reads .ps1 as ANSI unless it has a BOM.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not $root) { throw 'run build-exe.ps1 as a script file (powershell -File build-exe.ps1)' }
$b = "$root\build"; $site = "$b\site"; $dist = "$root\dist"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

# 1. stage
if (Test-Path $site) { Get-ChildItem $site -Recurse -File | ForEach-Object { $_.Delete() } }
New-Item -ItemType Directory -Force -Path $site, "$site\fonts", "$site\img\art", "$site\img\map", $dist | Out-Null
$html = [IO.File]::ReadAllText("$root\index.html")
$html = [regex]::Replace($html, '<link rel="preconnect" href="https://fonts\.gstatic\.com" crossorigin>\r?\n?', '')
# the local fonts must be the ones index.html asks for
$m = [regex]::Match($html, '<link rel="stylesheet" href="(https://fonts\.googleapis\.com/[^"]*)">')
if (-not $m.Success) { throw 'Google Fonts link not found in index.html' }
$srcFile = "$root\app\fonts\source.txt"
if (-not (Test-Path $srcFile) -or [IO.File]::ReadAllText($srcFile).Trim() -ne $m.Groups[1].Value) {
  throw ('app\fonts was not made from ' + $m.Groups[1].Value + ' - download the fonts again and update app\fonts\source.txt')
}
$html = $html.Remove($m.Index, $m.Length).Insert($m.Index, '<link rel="stylesheet" href="fonts/fonts.css">')
if ($html -match 'fonts\.googleapis') { throw 'more than one Google Fonts link in index.html' }
[IO.File]::WriteAllText("$site\index.html", $html, (New-Object System.Text.UTF8Encoding($false)))
foreach ($f in @('db.js', 'ru.js', 'guides.js', 'atlas.png')) { Copy-Item "$root\$f" "$site\$f" -Force }
Copy-Item "$root\img\art\*" "$site\img\art\" -Force
Copy-Item "$root\img\map\*" "$site\img\map\" -Force
Copy-Item "$root\app\licenses" "$site\licenses" -Recurse -Force
Copy-Item "$root\app\fonts\fonts.css", "$root\app\fonts\*.woff2" "$site\fonts\" -Force
foreach ($ref in [regex]::Matches([IO.File]::ReadAllText("$root\app\fonts\fonts.css"), 'url\(([^)]+)\)')) {
  if (-not (Test-Path ("$site\fonts\" + $ref.Groups[1].Value))) { throw ('fonts.css refers to a missing file: ' + $ref.Groups[1].Value) }
}
$n = (Get-ChildItem $site -Recurse -File).Count
"staged: $n files, {0:N1} MB" -f (((Get-ChildItem $site -Recurse -File | Measure-Object Length -Sum).Sum) / 1MB)

# 2. zip (paths with forward slashes, as the app looks them up)
$zipPath = "$b\site.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
$zip = [IO.Compression.ZipFile]::Open($zipPath, [IO.Compression.ZipArchiveMode]::Create)
try {
  foreach ($f in (Get-ChildItem $site -Recurse -File)) {
    $rel = $f.FullName.Substring($site.Length + 1).Replace('\', '/')
    $lvl = if ($f.Extension -in @('.png', '.jpg', '.woff2')) { [IO.Compression.CompressionLevel]::NoCompression } else { [IO.Compression.CompressionLevel]::Optimal }
    [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip, $f.FullName, $rel, $lvl)
  }
} finally { $zip.Dispose() }
"site.zip: {0:N1} MB" -f ((Get-Item $zipPath).Length / 1MB)

# 3. compile into build\ first; dist\ only gets a build that passed the self-test
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$fw = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319'
$exe = "$b\KeepersCodex.exe"
$cargs = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/codepage:65001',
  "/out:$exe", "/win32icon:$root\app\app.ico", "/win32manifest:$root\app\app.manifest",
  "/reference:$b\sdk\Microsoft.Web.WebView2.Core.dll", "/reference:$b\sdk\Microsoft.Web.WebView2.WinForms.dll",
  "/reference:$fw\System.Windows.Forms.dll", "/reference:$fw\System.Drawing.dll",
  "/reference:$fw\System.IO.Compression.dll", "/reference:$fw\System.Net.dll",
  "/resource:$zipPath,site.zip", "/resource:$root\app\app.ico,app.ico",
  "/resource:$b\sdk\Microsoft.Web.WebView2.Core.dll,wv2core",
  "/resource:$b\sdk\Microsoft.Web.WebView2.WinForms.dll,wv2winforms",
  "/resource:$b\sdk\WebView2Loader.dll,wv2loader",
  "$root\app\Program.cs")
& $csc @cargs
if ($LASTEXITCODE -ne 0) { throw "csc failed with $LASTEXITCODE" }
"exe: {0:N1} MB" -f ((Get-Item $exe).Length / 1MB)

# 4. self-test in its own WebView2 profile: loads the page, checks every tab in both languages,
#    writes JSON plus a screenshot, exits. A crash, a fallback or a page error fails the build.
$out = "$b\selftest.json"
if (Test-Path $out) { Remove-Item $out -Force }
$stProfile = Join-Path $env:LOCALAPPDATA 'KeepersCodex\WebView2-selftest'
$p = Start-Process $exe -ArgumentList "--selftest=$out" -WindowStyle Hidden -PassThru
if (-not $p.WaitForExit(90000)) { $p.Kill(); throw 'self-test timed out' }
if (-not (Test-Path $out)) { throw 'self-test: no output (the app crashed)' }
$raw = [IO.File]::ReadAllText($out)
'selftest: ' + $raw
if ($raw -like 'FALLBACK:*') { throw ('self-test: WebView2 did not start - ' + $raw) }
# ExecuteScriptAsync JSON-encodes the string that JSON.stringify returned, so two parses
$st = $raw | ConvertFrom-Json | ConvertFrom-Json
$errs = @($st.errors | Where-Object { $_ })
if (-not $st.ok -or $errs.Count -gt 0) {
  $why = if ($st.error) { $st.error } else { $errs -join '; ' }
  throw ('self-test failed: ' + $why)
}
Copy-Item $exe "$dist\KeepersCodex.exe" -Force
"passed -> $dist\KeepersCodex.exe"
