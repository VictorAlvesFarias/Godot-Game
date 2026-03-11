# Garante permissões de Administrador
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Start-Process powershell "-ExecutionPolicy Bypass -NoExit -File `"$PSCommandPath`"" -Verb RunAs
    exit
}

[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Defina a versão desejada aqui
$version = "4.3"
$url = "https://github.com/godotengine/godot/releases/download/$version-stable/Godot_v${version}-stable_mono_win64.zip"

$tempZip = "$env:TEMP\godot_dotnet.zip"
$tempExtract = "$env:TEMP\godot_dotnet"
$installDir = "C:\Tools\Godot"

Write-Host "Baixando Godot v$version..." -ForegroundColor Cyan
Invoke-WebRequest $url -OutFile $tempZip

if (Test-Path $tempExtract) { Remove-Item $tempExtract -Recurse -Force }
Expand-Archive $tempZip -DestinationPath $tempExtract -Force

# Garante que a pasta de destino exista e esteja limpa
if (Test-Path $installDir) { Remove-Item "$installDir\*" -Recurse -Force }
New-Item -ItemType Directory -Force -Path $installDir | Out-Null

$extractedFolder = Get-ChildItem $tempExtract | Where-Object { $_.PSIsContainer } | Select-Object -First 1
Move-Item "$($extractedFolder.FullName)\*" $installDir -Force

# --- NOVA LÓGICA DE RENOMEAÇÃO ---
Write-Host "Renomeando executáveis para nomes simplificados..." -ForegroundColor Yellow

$oldConsoleExe = Get-ChildItem $installDir -Filter "*console.exe" | Select-Object -First 1
$oldNormalExe = Get-ChildItem $installDir -Filter "*.exe" | Where-Object { $_.Name -notlike "*console*" } | Select-Object -First 1

# Caminhos novos
$newNormalPath = Join-Path $installDir "godot.exe"
$newConsolePath = Join-Path $installDir "godot_console.exe"

Rename-Item $oldNormalExe.FullName -NewName "godot.exe" -Force
Rename-Item $oldConsoleExe.FullName -NewName "godot_console.exe" -Force
# --------------------------------

# Limpeza
Remove-Item $tempExtract -Recurse -Force
Remove-Item $tempZip -Force

# Atualiza o PATH
$path = [Environment]::GetEnvironmentVariable("Path", "Machine")
if ($path -notlike "*$installDir*") {
    [Environment]::SetEnvironmentVariable("Path", "$path;$installDir", "Machine")
    Write-Host "PATH atualizado. Reinicie o terminal após terminar." -ForegroundColor Green
}

# Atalho na Área de Trabalho (apontando para o novo nome)
$desktop = [Environment]::GetFolderPath("Desktop")
$WshShell = New-Object -ComObject WScript.Shell
$shortcut = $WshShell.CreateShortcut("$desktop\Godot .NET.lnk")
$shortcut.TargetPath = $newNormalPath
$shortcut.WorkingDirectory = $installDir
$shortcut.Save()

Write-Host "Instalação concluída! Arquivos em $installDir" -ForegroundColor Green
Write-Host "Executável: godot.exe | Console: godot_console.exe" -ForegroundColor Green