# Megabyte Systems SignTool (MST) - Documentation

## Overview

**MST** (Megabyte Systems Tool) is a bulk code signing utility for Windows executables and libraries. It provides automated signing with SafeNet USB token certificates, parallel processing capabilities, and intelligent retry mechanisms.

### Key Features

- **Bulk Signing**: Process entire directories recursively or non-recursively
- **Single File Signing**: Sign individual `.exe`, `.dll`, or any supported file
- **Parallel Processing**: Multi-threaded execution for optimal performance
- **Smart Filtering**: Automatically signs all EXE files and only DLL files starting with "MPTS"
- **Timestamp Failover**: Automatically tries multiple timestamp servers (DigiCert, Sectigo, GlobalSign, Comodo)
- **Duplicate Detection**: Skips already-signed files
- **Comprehensive Logging**: Creates `signing_log.txt` with timestamp and server information

---

## Installation

### Prerequisites
- .NET 8.0 Runtime or SDK
- Windows Kits SignTool.exe (automatically detected)
- Valid code signing certificate with thumbprint configured

### Install as Global Tool

**PowerShell:**
```powershell
.\build.ps1
```

**Bash (Git Bash/WSL):**
```bash
chmod +x build.sh
./build.sh
```

After installation, `mst` command is available globally from any directory.

### Uninstall
```powershell
dotnet tool uninstall --global MegabyteSystems.SignTool
```

---

## Command Syntax

```
mst <mode> <path> [options]
```

### Modes

| Mode | Description | Example |
|------|-------------|---------|
| `-dr` | Recursive directory scan (default) | `mst -dr "C:\MyProject"` |
| `-d` | Non-recursive directory scan | `mst -d "C:\Bin"` |
| `-exe` | Sign single `.exe` file | `mst -exe "C:\App.exe"` |
| `-dll` | Sign single `.dll` file | `mst -dll "C:\Lib.dll"` |
| `-file` | Sign any single file | `mst -file "C:\Driver.sys"` |
| `<dir> <name>` | Smart search for file by name | `mst "C:\Build" TCW0300` |
| `-remove` | Remove signature from single file | `mst -remove "C:\App.exe"` |
| `-remove-dr` | Remove signatures recursively | `mst -remove-dr "C:\Signed"` |
| `-remove-d` | Remove signatures non-recursively | `mst -remove-d "C:\Bin"` |

### Options

| Option | Description | Example |
|--------|-------------|---------|
| `-types` | Filter file extensions (comma-separated) | `-types exe,dll,msi` |

### Supported File Types
`exe`, `dll`, `msi`, `sys`, `ocx`, `cab`, `cat`

---

## Usage Examples

### 1. Sign Single Executable

**PowerShell:**
```powershell
mst -exe "C:\MyApp\Program.exe"
```

**Bash:**
```bash
mst -exe "/c/MyApp/Program.exe"
```

**Output:**
```
Starting code signing process...
Mode: Single File
Target: C:\MyApp\Program.exe

PROCESSING SINGLE FILE: Program.exe
TEST SIGNING: Program.exe
  Attempting Standard approach...
  SUCCESS: Standard approach worked

PROCESSING: Program.exe
  Location: C:\MyApp
  Attempting signature with: Digicert (Primary)
  SUCCESS: Signed with Digicert (Primary)

SIGNING SUMMARY
===============
Successfully signed: 1
Failed: 0
Skipped: 0
Total processed: 1
```

---

### 2. Sign Single DLL

**PowerShell:**
```powershell
mst -dll "C:\Libraries\MPTSCore.dll"
```

**Bash:**
```bash
mst -dll "/c/Libraries/MPTSCore.dll"
```

---

### 3. Recursive Directory Signing (All EXEs + MPTS* DLLs)

**PowerShell:**
```powershell
mst -dr "C:\TFS\MPTS2010\Production\24. Executables"
```

**Bash:**
```bash
mst -dr "/c/TFS/MPTS2010/Production/24. Executables"
```

**What Gets Signed:**
- ✅ All `.exe` files recursively
- ✅ Only `.dll` files starting with `MPTS` (e.g., `MPTSCore.dll`, `MPTSUtil.dll`)
- ❌ Other DLLs are skipped

**Output:**
```
SCANNING DIRECTORY: C:\TFS\MPTS2010\Production\24. Executables
Mode: Recursive

Found 45 .exe files
Found 12 .dll files starting with 'MPTS' (filtered from 238 total)
TOTAL FILES TO PROCESS: 57

STARTING PARALLEL PROCESSING WITH 16 THREADS
PROCESSING: TCW0300.exe
  Location: C:\TFS\...\TC
  Attempting signature with: Digicert (Primary)
  SUCCESS: Signed with Digicert (Primary)
...
```

---

### 4. Non-Recursive Directory Signing

**PowerShell:**
```powershell
mst -d "C:\Build\Output"
```

**Bash:**
```bash
mst -d "/c/Build/Output"
```

Only processes files in the specified directory (no subdirectories).

---

### 5. Sign Only EXE Files (Exclude DLLs)

**PowerShell:**
```powershell
mst -dr "C:\MyProject" -types exe
```

**Bash:**
```bash
mst -dr "/c/MyProject" -types exe
```

---

### 6. Sign Multiple File Types

**PowerShell:**
```powershell
mst -dr "C:\Installer" -types exe,dll,msi
```

**Bash:**
```bash
mst -dr "/c/Installer" -types exe,dll,msi
```

**Note:** DLL filtering still applies (only `MPTS*` DLLs are signed).

---

### 7. Sign System Drivers

**PowerShell:**
```powershell
mst -d "C:\Drivers" -types sys,cat
```

**Bash:**
```bash
mst -d "/c/Drivers" -types sys,cat
```

---

### 8. Smart File Search

Search for a file by name without specifying full path or extension.

**PowerShell:**
```powershell
mst "C:\TFS\MPTS2010\Production\24. Executables" TCW0300
```

**Bash:**
```bash
mst "/c/TFS/MPTS2010/Production/24. Executables" TCW0300
```

**Single Match Output:**
```
Found: C:\TFS\MPTS2010\Production\24. Executables\TC\TCW0300.exe
Starting code signing process...
```

**Multiple Matches Output:**
```
Multiple files matching 'MPTSCore' found. Select one of the following:
1. C:\Build\Debug\MPTSCore.dll
2. C:\Build\Release\MPTSCore.dll
3. C:\Build\Test\MPTSCore.exe

Enter selection (1-3): 2
Selected: C:\Build\Release\MPTSCore.dll
```

**Features:**
- Searches recursively through all subdirectories
- Matches filename without extension
- Supports all signable file types (exe, dll, msi, sys, ocx, cab, cat)
- Interactive selection for multiple matches
- Automatic signing after selection

---

### 9. Remove Signatures (Unsign Files)

Remove digital signatures from previously signed files.

**Single File:**
```powershell
mst -remove "C:\MyApp\Program.exe"
```

**Bash:**
```bash
mst -remove "/c/MyApp/Program.exe"
```

**Recursive Directory:**
```powershell
mst -remove-dr "C:\SignedBinaries"
```

**Non-Recursive Directory:**
```powershell
mst -remove-d "C:\Build\Output"
```

**With File Type Filter:**
```powershell
mst -remove-dr "C:\Project" -types exe
mst -remove-d "C:\Libs" -types dll
```

**Output:**
```
UNSIGNING: Program.exe
  Location: C:\MyApp
  Successfully removed signature

SIGNING SUMMARY
===============
Successfully unsigned: 1
Failed: 0
Skipped: 0
Total processed: 1
```

**Note:** Unsigning removes all digital signatures from a file. This is useful for:
- Testing signing workflows
- Removing expired certificates before re-signing
- Cleaning up development builds

---

## Advanced Features

### Automatic Timestamp Server Failover

MST tries timestamp servers in priority order:

1. **DigiCert** (Primary)
2. **Sectigo**
3. **GlobalSign**
4. **Comodo**

If DigiCert fails, it automatically tries the next server.

### Parallel Processing

- Utilizes `CPU cores × 2` threads
- Optimal for bulk signing operations
- First file is signed serially to handle password prompts

### Smart Retry Logic

Failed files are automatically retried once after the initial batch completes.

### Duplicate Detection

Already-signed files are skipped to avoid unnecessary re-signing:

```
SKIPPED: TCW0310.exe (already signed)
```

---

## Output and Logging

### Console Output

- **Green**: Successful operations
- **Red**: Errors and failures
- **Yellow**: Warnings and retries
- **Blue**: Skipped files
- **Cyan**: Informational messages
- **Gray**: Verbose details

### Log File

**Location:** `signing_log.txt` (created in current working directory)

**Format:**
```
2025-10-28 14:32:15 | Digicert (Primary) | C:\MyApp\Program.exe
2025-10-28 14:32:18 | Sectigo | C:\MyApp\MPTSCore.dll
2025-10-28 14:32:21 | Digicert (Primary) | C:\MyApp\Installer.msi
```

---

## Return Codes

| Code | Description |
|------|-------------|
| `0` | All files signed successfully |
| `1` | One or more files failed to sign |

---

## Troubleshooting

### Password Prompt Appears Repeatedly

**Cause:** SafeNet token requires PIN for each operation.

**Solution:** Ensure token is unlocked before running MST. The tool tests with the first file to minimize prompts.

### Access Denied Errors

**Cause:** Insufficient permissions or file in use.

**Solutions:**
- Run terminal as Administrator
- Close applications using the target files
- Check antivirus/security software

### Timestamp Server Timeouts

**Cause:** Network issues or server unavailability.

**Solution:** MST automatically tries backup servers. Ensure internet connectivity.

### DLL Not Being Signed

**Cause:** DLL doesn't start with "MPTS".

**Solution:** This is by design. Only `MPTS*` DLLs are signed. Use `-file` mode for individual non-MPTS DLLs:

```powershell
mst -dll "C:\Libraries\CustomLib.dll"
```

---

## Technical Specifications

- **Framework:** .NET 8.0
- **Certificate:** SHA256 with SHA256 timestamp
- **Hash Algorithm:** SHA256 (file digest)
- **Timestamp Protocol:** RFC 3161
- **Max Threads:** CPU cores × 2
- **Timeout:** 60 seconds per file
- **Retry Attempts:** 1 automatic retry for failed files

---

## Version History
**v1.0.0** - Initial Release
- Bulk signing with parallel processing
- MPTS DLL filtering
- Multi-timestamp server support
- Comprehensive logging

**v1.0.1** - Small Improvements 
- Local signtool.exe copy feature
- PATH environment variable update
- MPTS DLL filtering by Console Commands
- Improved file discovery logging

**v1.0.2** - Smart Search & Bug Fixes
- Smart file search by name feature
- Fixed false "unsigned" message during test signing
- Interactive file selection for multiple matches
- Improved single file signing workflow

**v1.0.3** - Build Script Improvements
- Automatic Windows SDK detection in build scripts
- Auto-injection of latest signtool.exe path
- Build script now tests installation after completion
- Fixed help command handling (-help, --help, -h, /?, help)
- Documentation updated with unsigning commands

---

*© 2025 Megabyte Systems, Inc. All rights reserved.*
