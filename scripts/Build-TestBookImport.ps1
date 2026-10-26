param(
    [string]$Source = (Join-Path $PSScriptRoot '..\TestData\TestBooks'),
    [string]$Output = (Join-Path $PSScriptRoot '..\TestData\EpiRead-TestBooks-Import.zip')
)

$ErrorActionPreference = 'Stop'
$sourcePath = (Resolve-Path -LiteralPath $Source).Path
$outputPath = [IO.Path]::GetFullPath($Output)
$staging = Join-Path ([IO.Path]::GetTempPath()) ("epiread-import-" + [guid]::NewGuid())

function Parse-Metadata([string]$path) {
    $values = @{}
    foreach ($line in Get-Content -LiteralPath $path) {
        $parts = $line -split "`t", 2
        if ($parts.Count -eq 2) { $values[$parts[0]] = $parts[1] }
    }
    return $values
}

try {
    New-Item -ItemType Directory -Path $staging | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $staging 'covers') | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $staging 'documents') | Out-Null

    $books = @()
    $categoryMaps = @()
    $documents = @()
    $now = [DateTime]::UtcNow.ToString('o')
    $index = 0

    foreach ($readme in Get-ChildItem -LiteralPath $sourcePath -Recurse -Filter README.txt | Sort-Object FullName) {
        $index++
        $metadata = Parse-Metadata $readme.FullName
        $folder = $readme.Directory
        $epub = Get-ChildItem -LiteralPath $folder.FullName -Filter *.epub | Select-Object -First 1
        $cover = Get-ChildItem -LiteralPath $folder.FullName -File |
            Where-Object { $_.Extension -match '^\.(jpg|jpeg|png|webp)$' } | Select-Object -First 1
        if (-not $epub -or -not $cover) { throw "Missing EPUB or cover in $($folder.FullName)" }

        $relative = [IO.Path]::GetRelativePath($sourcePath, $folder.FullName)
        $category = ($relative -split '[\\/]')[1]
        $safeStem = ('book-{0:D2}' -f $index)
        $coverEntry = "covers/$safeStem$($cover.Extension.ToLowerInvariant())"
        $documentEntry = "documents/$safeStem.epub"
        Copy-Item -LiteralPath $cover.FullName -Destination (Join-Path $staging $coverEntry)
        Copy-Item -LiteralPath $epub.FullName -Destination (Join-Path $staging $documentEntry)

        $releaseDate = [DateTime]::Parse($metadata['Release Date'], [Globalization.CultureInfo]::InvariantCulture)
        $languageCode = if ($metadata['Language'] -eq 'Chinese') { 'ZH' } else { 'EN' }
        $books += [pscustomobject]@{
            Title = $metadata['Title']; AuthorName = $metadata['Author']; Description = $metadata['Desc']
            PublicationYear = $releaseDate.Year; LanguageCode = $languageCode
            CoverImagePath = $coverEntry; CoverImageLink = ''; Status = 'Available'
            UserId = ''; UserEmail = ''; CreatedAt = $now; UpdatedAt = $now
        }
        $categoryMaps += [pscustomobject]@{
            BookTitle = $metadata['Title']; BookAuthorName = $metadata['Author']
            BookCategoryId = 0; BookCategoryName = $category
        }
        $documents += [pscustomobject]@{
            BookTitle = $metadata['Title']; BookAuthorName = $metadata['Author']; FormatType = 'EPUB'
            DocumentPath = $documentEntry; DocumentLink = ''; CreatedAt = $now; UpdatedAt = $now
        }
    }

    $csvFiles = @{
        'Books.csv' = $books
        'BookCategoryMaps.csv' = $categoryMaps
        'BookDocuments.csv' = $documents
    }
    $hashes = @{}
    foreach ($entry in $csvFiles.GetEnumerator()) {
        $path = Join-Path $staging $entry.Key
        $entry.Value | ConvertTo-Csv -NoTypeInformation | Set-Content -LiteralPath $path -Encoding utf8
        $hashes[$entry.Key] = [Convert]::ToBase64String(
            [Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($path)))
    }

    $timestamp = [DateTime]::UtcNow.ToString('o')
    $combined = @($hashes['Books.csv'], $hashes['BookCategoryMaps.csv'],
        $hashes['BookDocuments.csv'], $timestamp) -join '|'
    $integrity = [Convert]::ToBase64String(
        [Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($combined)))
    $manifest = [ordered]@{
        Version = '1.0'; ExportTimestamp = $timestamp; ExportType = 'full'
        BookCount = $books.Count; FileHashes = $hashes; IntegrityKey = $integrity
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $staging '.epiread_manifest.json') -Encoding utf8

    if (Test-Path -LiteralPath $outputPath) { Remove-Item -LiteralPath $outputPath -Force }
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $outputPath -CompressionLevel Optimal
    Write-Output "Created $outputPath with $($books.Count) books."
}
finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
