# Local static server for the codex: http://localhost:8099/
# Opening index.html by double click (file://) does not load the neighbouring db.js,
# so the page has to be served over http.
# POST /save?name=<file>.json writes the request body to data\extract\<file>.json
# (used by in-browser extraction helpers; never exposed beyond localhost).
$root = $PSScriptRoot
$prefix = 'http://localhost:8099/'
$l = New-Object System.Net.HttpListener
$l.Prefixes.Add($prefix)
$l.Start()
Write-Host "serving $root at $prefix"
$types = @{ '.html' = 'text/html; charset=utf-8'; '.js' = 'text/javascript; charset=utf-8'; '.json' = 'application/json';
            '.png' = 'image/png'; '.jpg' = 'image/jpeg'; '.css' = 'text/css'; '.txt' = 'text/plain; charset=utf-8' }
$enc = New-Object System.Text.UTF8Encoding($false)
while ($l.IsListening) {
  try {
    $ctx = $l.GetContext()
    $req = $ctx.Request; $res = $ctx.Response
    $res.Headers.Add('Access-Control-Allow-Origin', '*')
    $p = [Uri]::UnescapeDataString($req.Url.AbsolutePath)
    if ($req.HttpMethod -eq 'OPTIONS') { $res.Headers.Add('Access-Control-Allow-Headers', '*'); $res.StatusCode = 204; $res.Close(); continue }
    if ($req.HttpMethod -eq 'POST' -and $p -eq '/save') {
      $name = $req.QueryString['name']
      if ($name -match '^[A-Za-z0-9._-]+\.json$') {
        $dir = Join-Path $root 'data\extract'
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
        $body = (New-Object System.IO.StreamReader($req.InputStream, [System.Text.Encoding]::UTF8)).ReadToEnd()
        [IO.File]::WriteAllText((Join-Path $dir $name), $body, $enc)
        $res.StatusCode = 200
      } else { $res.StatusCode = 400 }
      $res.Close(); continue
    }
    if ($p -eq '/') { $p = '/index.html' }
    $f = Join-Path $root ($p.TrimStart('/') -replace '/', '\')
    if (Test-Path $f -PathType Leaf) {
      $ext = [IO.Path]::GetExtension($f).ToLower()
      $res.ContentType = if ($types.ContainsKey($ext)) { $types[$ext] } else { 'application/octet-stream' }
      $b = [IO.File]::ReadAllBytes($f)
      $res.ContentLength64 = $b.Length
      $res.OutputStream.Write($b, 0, $b.Length)
    } else { $res.StatusCode = 404 }
    $res.Close()
  } catch { }
}
