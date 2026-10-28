$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
$root = Split-Path $PSScriptRoot
$destination = Join-Path $root 'EpiRead/wwwroot/images/books'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$catalog = [ordered]@{}

function Add-Cover([string]$title, [System.IO.FileInfo]$cover) {
    if (-not $cover -or [string]::IsNullOrWhiteSpace($title)) { return }
    $hash = (Get-FileHash -LiteralPath $cover.FullName -Algorithm SHA256).Hash.ToLowerInvariant().Substring(0, 16)
    $name = "$hash$($cover.Extension.ToLowerInvariant())"
    Copy-Item -LiteralPath $cover.FullName -Destination (Join-Path $destination $name) -Force
    $catalog[$title.Trim()] = "/images/books/$name"
}

foreach ($epub in Get-ChildItem (Join-Path $root 'TestData/Books') -Recurse -Filter episode1.epub) {
    $zip = [IO.Compression.ZipFile]::OpenRead($epub.FullName)
    try {
        $entry = $zip.Entries | Where-Object { $_.FullName -like '*.opf' } | Select-Object -First 1
        if (-not $entry) { continue }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { [xml]$metadata = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $title = $metadata.SelectSingleNode("//*[local-name()='metadata']/*[local-name()='title']").InnerText
        $cover = Get-ChildItem -LiteralPath $epub.DirectoryName -Filter *.jpg | Select-Object -First 1
        Add-Cover $title $cover
        Add-Cover ($title -split ' / ', 2)[0] $cover
        if ($title -eq 'The Story of the Typewriter, 1873-1923') { Add-Cover 'The Story of the Typewriter' $cover }
        if ($title -eq 'The Prophet') { Add-Cover 'The Prophet by Kahlil Gibran' $cover }
    } finally { $zip.Dispose() }
}

foreach ($readme in Get-ChildItem (Join-Path $root 'TestData/TestBooks') -Recurse -Filter README.txt) {
    $titleLine = Get-Content -LiteralPath $readme.FullName | Where-Object { $_ -match '^Title\t' } | Select-Object -First 1
    $cover = Get-ChildItem -LiteralPath $readme.DirectoryName -Filter *.jpg | Select-Object -First 1
    Add-Cover ($titleLine -split "`t", 2)[1] $cover
}
$catalog | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'catalog.json') -Encoding utf8
Write-Output "Bundled covers for $($catalog.Count) book titles."
