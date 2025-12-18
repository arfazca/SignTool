# Megabyte Systems - SignTool Build & Publish Script

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  SignTool - Build & Publish" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Find latest Windows SDK SignTool
Write-Host "Locating latest Windows SDK SignTool..." -ForegroundColor Yellow
$windowsKitsPath = "C:\Program Files (x86)\Windows Kits\10\bin"
if (Test-Path $windowsKitsPath) {
    $sdkVersions = Get-ChildItem -Path $windowsKitsPath -Directory | 
                   Where-Object { $_.Name -match '^\d+\.\d+\.\d+\.\d+$' } |
                   Sort-Object Name -Descending |
                   Select-Object -First 1
    
    if ($sdkVersions) {
        $signToolPath = Join-Path $sdkVersions.FullName "x64\signtool.exe"
        if (Test-Path $signToolPath) {
            Write-Host "Found SignTool: $signToolPath" -ForegroundColor Green
            
            # Update SignTool.cs with the latest path
            $csFilePath = ".\SignTool.cs"
            if (Test-Path $csFilePath) {
                $content = Get-Content $csFilePath -Raw
                $pattern = 'private static readonly string SignToolPath = \s*@"[^"]*";'
                $replacement = "private static readonly string SignToolPath = `n            @`"$signToolPath`";"
                $content = $content -replace $pattern, $replacement
                Set-Content -Path $csFilePath -Value $content
                Write-Host "Updated SignTool.cs with latest SDK path" -ForegroundColor Green
            }
        } else {
            Write-Host "Warning: signtool.exe not found in SDK path" -ForegroundColor Yellow
        }
    } else {
        Write-Host "Warning: No Windows SDK version found" -ForegroundColor Yellow
    }
} else {
    Write-Host "Warning: Windows Kits path not found" -ForegroundColor Yellow
}
Write-Host ""

# Clean previous builds
Write-Host "Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean --configuration Release

# Build the project
Write-Host "Building project..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Pack as NuGet tool
Write-Host "Packing as global tool..." -ForegroundColor Yellow
dotnet pack --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed!" -ForegroundColor Red
    exit 1
}

# Uninstall previous version (if exists)
Write-Host "Uninstalling previous version (if exists)..." -ForegroundColor Yellow

try {
    dotnet tool uninstall --global MegabyteSystems.SignTool 2>$null
} catch {
    Write-Host "Previous version not installed. Continuing..." -ForegroundColor DarkYellow
}

# Install the tool globally
Write-Host "Installing tool globally..." -ForegroundColor Yellow
$nupkgPath = Get-ChildItem -Path ".\bin\Release\*.nupkg" | Select-Object -First 1
dotnet tool install --global --add-source ./bin/Release MegabyteSystems.SignTool

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  SUCCESS!" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "Tool installed as: mst" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "USAGE:" -ForegroundColor Yellow
    Write-Host "  mst -dr <directory>           # Recursive directory scan (default)" -ForegroundColor White
    Write-Host "  mst -d <directory>            # Non-recursive directory scan" -ForegroundColor White
    Write-Host "  mst -exe <file.exe>           # Sign single EXE file" -ForegroundColor White
    Write-Host "  mst -dll <file.dll>           # Sign single DLL file" -ForegroundColor White
    Write-Host "  mst -file <path>              # Sign any single file" -ForegroundColor White
    Write-Host "  mst -remove <file.exe>        # Remove signature from single file" -ForegroundColor White
    Write-Host "  mst -remove-dr <directory>    # Remove signatures recursively" -ForegroundColor White
    Write-Host "  mst -remove-d <directory>     # Remove signatures non-recursively" -ForegroundColor White
    Write-Host ""
    Write-Host "OPTIONAL FILE TYPE FILTERS (can combine multiple):" -ForegroundColor Yellow
    Write-Host "  -types exe                         # Only .exe files" -ForegroundColor White
    Write-Host "  -types dll                         # Only .dll files" -ForegroundColor White
    Write-Host "  -types exe,dll                     # Both .exe and .dll files" -ForegroundColor White
    Write-Host "  -types exe,dll,msi,sys,ocx,cab,cat # Multiple types" -ForegroundColor White
    Write-Host ""
    Write-Host "EXAMPLES:" -ForegroundColor Yellow
    Write-Host "  mst -dr ""C:\MyProject""" -ForegroundColor White
    Write-Host "  mst -d ""C:\MyProject\bin"" -types exe,dll" -ForegroundColor White
    Write-Host "  mst -exe ""C:\MyApp.exe""" -ForegroundColor White
    Write-Host "  mst -dll ""C:\MyLibrary.dll""" -ForegroundColor White
    Write-Host "  mst -dr ""C:\MyProject"" -types exe" -ForegroundColor White
    Write-Host "  mst -remove ""C:\MyApp.exe""" -ForegroundColor White
    Write-Host "  mst -remove-dr ""C:\SignedBinaries""" -ForegroundColor White
    Write-Host "  mst -remove-d ""C:\SingleFolder""" -ForegroundColor White
    Write-Host ""
    
    # Test the installation
    Write-Host "Testing installation..." -ForegroundColor Yellow
    Write-Host ""
    mst
    
    Write-Host ""
    Write-Host "Installation test complete!" -ForegroundColor Green
} else {
    Write-Host "Installation failed!" -ForegroundColor Red
    exit 1
}