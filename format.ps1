param(
    [string]$Root = $PSScriptRoot
)

$files = Get-ChildItem -Path $Root -Filter "*.cs" -Recurse |
    Where-Object { $_.Name -notmatch "AssemblyAttributes|AssemblyInfo" }

function Add-Braces
{
    param([string[]]$lines)

    $result = [System.Collections.Generic.List[string]]::new()
    $i = 0

    while ($i -lt $lines.Count)
    {
        $line        = $lines[$i]
        $trimmed     = $line.TrimStart()
        $indent      = $line.Length - $trimmed.Length
        $indentStr   = $line.Substring(0, $indent)

        $isCtrl = $trimmed -match '^(if|else if|for|foreach|while)\s*\('
        $isElse = $trimmed -match '^else\s*$'

        if ($isCtrl)
        {
            $depth   = 0
            $condEnd = -1
            for ($c = 0; $c -lt $trimmed.Length; $c++)
            {
                if ($trimmed[$c] -eq '(') { $depth++ }
                elseif ($trimmed[$c] -eq ')')
                {
                    $depth--
                    if ($depth -eq 0) { $condEnd = $c; break }
                }
            }

            if ($condEnd -ge 0)
            {
                $after = $trimmed.Substring($condEnd + 1).Trim()

                if ($after.Length -gt 0 -and $after[0] -ne '{')
                {
                    $condPart = $trimmed.Substring(0, $condEnd + 1)
                    $result.Add($indentStr + $condPart)
                    $result.Add($indentStr + '{')
                    $result.Add($indentStr + '    ' + $after)
                    $result.Add($indentStr + '}')
                    $i++
                    continue
                }

                if ($after.Length -eq 0 -and ($i + 1) -lt $lines.Count)
                {
                    $nextLine    = $lines[$i + 1]
                    $nextTrimmed = $nextLine.TrimStart()
                    if ($nextTrimmed.Length -gt 0 -and $nextTrimmed[0] -ne '{')
                    {
                        $result.Add($line)
                        $result.Add($indentStr + '{')
                        $result.Add($nextLine)
                        $result.Add($indentStr + '}')
                        $i += 2
                        continue
                    }
                }
            }
        }
        elseif ($isElse)
        {
            if (($i + 1) -lt $lines.Count)
            {
                $nextLine    = $lines[$i + 1]
                $nextTrimmed = $nextLine.TrimStart()
                if ($nextTrimmed.Length -gt 0 -and $nextTrimmed[0] -ne '{' -and $nextTrimmed -notmatch '^if\s*\(')
                {
                    $result.Add($line)
                    $result.Add($indentStr + '{')
                    $result.Add($nextLine)
                    $result.Add($indentStr + '}')
                    $i += 2
                    continue
                }
            }
        }

        $result.Add($line)
        $i++
    }

    return $result
}

function Move-BracesToOwnLine
{
    param([string[]]$lines)

    $result  = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $lines)
    {
        $trimmed   = $line.TrimStart()
        $indent    = $line.Length - $trimmed.Length
        $indentStr = $line.Substring(0, $indent)

        if ($trimmed -eq '{' -or $trimmed -eq '' -or
            $trimmed.StartsWith('[') -or $trimmed.StartsWith('//') -or
            $trimmed.StartsWith('*') -or $trimmed.StartsWith('/*'))
        {
            $result.Add($line)
            continue
        }

        if ($line -match '^(.*[^\s])\s*\{$')
        {
            $before        = $Matches[1]
            $beforeTrimmed = $before.TrimStart()

            $skip = $false
            if ($beforeTrimmed -match '^\s*(var |new |\w+\s*=)') { $skip = $true }
            if ($beforeTrimmed -match '[,=]\s*$')                 { $skip = $true }
            if ($beforeTrimmed -match '\{.*(?:get|set|init)')     { $skip = $true }
            if ($before -match '(?<!=)=(?!=)')                    { $skip = $true }

            if (-not $skip)
            {
                $result.Add($before)
                $result.Add($indentStr + '{')
                continue
            }
        }

        $result.Add($line)
    }

    return $result
}

$totalChanged = 0

foreach ($file in $files)
{
    $original = [System.IO.File]::ReadAllText($file.FullName)
    $content  = $original

    # 1. Remove comment-only lines (// and ///)
    # 2. Remove #region / #endregion
    # 3. private -> public, protected -> public
    # 4. Fix private set / private init
    # 5. Remove alignment spaces before = (not ==, !=, >=, <=, =>)
    # 6. Remove alignment spaces before { get; set; }
    # 7. Remove alignment spaces between type and member name

    $lines    = $content -split "`n"
    $newLines = [System.Collections.Generic.List[string]]::new()

    foreach ($line in $lines)
    {
        $trimmed = $line.TrimStart()

        if ($trimmed -match '^///')      { continue }
        if ($trimmed -match '^//')       { continue }
        if ($trimmed -match '^#region')  { continue }
        if ($trimmed -match '^#endregion') { continue }

        $line = $line -replace '\{\s*get;\s*private set;\s*\}',  '{ get; set; }'
        $line = $line -replace '\{\s*get;\s*private init;\s*\}', '{ get; init; }'

        if ($trimmed -match '^private ')   { $line = $line -replace '\bprivate\b',   'public' }
        if ($trimmed -match '^protected ') { $line = $line -replace '\bprotected\b', 'public' }

        $newLines.Add($line)
    }

    # Collapse excess blank lines (max 1 consecutive)
    $collapsed  = [System.Collections.Generic.List[string]]::new()
    $blankCount = 0
    foreach ($l in $newLines)
    {
        if ($l.Trim() -eq '')
        {
            $blankCount++
            if ($blankCount -le 1) { $collapsed.Add($l) }
        }
        else
        {
            $blankCount = 0
            $collapsed.Add($l)
        }
    }

    $content = $collapsed -join "`n"

    # Remove alignment spaces before = (not ==, !=, >=, <=, =>)
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '(\w|\)|\])\s{2,}(=)(?!=)',
        '$1 $2'
    )

    # Remove alignment spaces before { get; set; } style auto-props
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '(\w)\s{2,}(\{[^}]*(?:get|set|init)[^}]*\})',
        '$1 $2'
    )

    # Remove alignment spaces between type keyword and member name
    $content = [System.Text.RegularExpressions.Regex]::Replace(
        $content,
        '((?:public|private|protected|static|readonly|override|virtual|abstract|partial|new)\s+(?:\w+(?:<[^>]*>)?(?:\[\])?)\s*)\s{2,}(\w)',
        '$1 $2'
    )

    # Add braces to brace-less control flow
    $linesArr = $content -split "`n"
    $braced   = Add-Braces $linesArr
    $content  = $braced -join "`n"

    # Move { to its own line (Allman style)
    $linesArr2 = $content -split "`n"
    $allman    = Move-BracesToOwnLine $linesArr2
    $content   = $allman -join "`n"

    if ($content -ne $original)
    {
        [System.IO.File]::WriteAllText($file.FullName, $content, [System.Text.UTF8Encoding]::new($false))
        Write-Host "Formatted: $($file.Name)"
        $totalChanged++
    }
}

Write-Host ""
Write-Host "Done. $totalChanged file(s) updated."
