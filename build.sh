#!/bin/bash
# Megabyte Systems - SignTool Build & Publish Script

echo -e "\033[0;36m========================================"
echo "  SignTool - Build & Publish"
echo -e "========================================\033[0m"
echo ""

# Find latest Windows SDK SignTool (for Windows/WSL environments)
echo -e "\033[0;33mLocating latest Windows SDK SignTool...\033[0m"
if [ -d "/mnt/c/Program Files (x86)/Windows Kits/10/bin" ]; then
    WINDOWS_KITS_PATH="/mnt/c/Program Files (x86)/Windows Kits/10/bin"
    LATEST_SDK=$(ls -v "$WINDOWS_KITS_PATH" | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1)
    
    if [ ! -z "$LATEST_SDK" ]; then
        SIGNTOOL_PATH="$WINDOWS_KITS_PATH/$LATEST_SDK/x64/signtool.exe"
        if [ -f "$SIGNTOOL_PATH" ]; then
            # Convert WSL path to Windows path
            WINDOWS_PATH="C:\\Program Files (x86)\\Windows Kits\\10\\bin\\$LATEST_SDK\\x64\\signtool.exe"
            echo -e "\033[0;32mFound SignTool: $WINDOWS_PATH\033[0m"
            
            # Update SignTool.cs with the latest path
            if [ -f "SignTool.cs" ]; then
                sed -i -E "s|private static readonly string SignToolPath = .*|private static readonly string SignToolPath = \n            @\"$WINDOWS_PATH\";|" SignTool.cs
                echo -e "\033[0;32mUpdated SignTool.cs with latest SDK path\033[0m"
            fi
        else
            echo -e "\033[0;33mWarning: signtool.exe not found in SDK path\033[0m"
        fi
    else
        echo -e "\033[0;33mWarning: No Windows SDK version found\033[0m"
    fi
elif [ -d "C:/Program Files (x86)/Windows Kits/10/bin" ]; then
    # Git Bash on Windows
    WINDOWS_KITS_PATH="C:/Program Files (x86)/Windows Kits/10/bin"
    LATEST_SDK=$(ls "$WINDOWS_KITS_PATH" | grep -E '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' | sort -V | tail -n 1)
    
    if [ ! -z "$LATEST_SDK" ]; then
        SIGNTOOL_PATH="$WINDOWS_KITS_PATH/$LATEST_SDK/x64/signtool.exe"
        if [ -f "$SIGNTOOL_PATH" ]; then
            WINDOWS_PATH="C:\\\\Program Files (x86)\\\\Windows Kits\\\\10\\\\bin\\\\$LATEST_SDK\\\\x64\\\\signtool.exe"
            echo -e "\033[0;32mFound SignTool: $WINDOWS_PATH\033[0m"
            
            if [ -f "SignTool.cs" ]; then
                sed -i -E "s|private static readonly string SignToolPath = .*|private static readonly string SignToolPath = \n            @\"C:\\\\\\\\Program Files (x86)\\\\\\\\Windows Kits\\\\\\\\10\\\\\\\\bin\\\\\\\\$LATEST_SDK\\\\\\\\x64\\\\\\\\signtool.exe\";|" SignTool.cs
                echo -e "\033[0;32mUpdated SignTool.cs with latest SDK path\033[0m"
            fi
        else
            echo -e "\033[0;33mWarning: signtool.exe not found in SDK path\033[0m"
        fi
    else
        echo -e "\033[0;33mWarning: No Windows SDK version found\033[0m"
    fi
else
    echo -e "\033[0;33mWarning: Windows Kits path not found\033[0m"
fi
echo ""

# Clean previous builds
echo -e "\033[0;33mCleaning previous builds...\033[0m"
dotnet clean --configuration Release

# Build the project
echo -e "\033[0;33mBuilding project...\033[0m"
dotnet build --configuration Release

if [ $? -ne 0 ]; then
    echo -e "\033[0;31mBuild failed!\033[0m"
    exit 1
fi

# Pack as NuGet tool
echo -e "\033[0;33mPacking as global tool...\033[0m"
dotnet pack --configuration Release

if [ $? -ne 0 ]; then
    echo -e "\033[0;31mPack failed!\033[0m"
    exit 1
fi

# Uninstall previous version (if exists)
echo -e "\033[0;33mUninstalling previous version (if exists)...\033[0m"
dotnet tool uninstall --global MegabyteSystems.SignTool 2>/dev/null

# Install the tool globally
echo -e "\033[0;33mInstalling tool globally...\033[0m"
dotnet tool install --global --add-source ./bin/Release MegabyteSystems.SignTool

if [ $? -eq 0 ]; then
    echo ""
    echo -e "\033[0;32m========================================"
    echo "  SUCCESS!"
    echo -e "========================================\033[0m"
    echo ""
    echo -e "\033[0;36mTool installed as: mst\033[0m"
    echo ""
    echo -e "\033[0;33mUsage examples:\033[0m"
    echo -e "\033[0;37m  mst -dr \"C:\\MyProject\"\033[0m"
    echo -e "\033[0;37m  mst -exe \"C:\\MyApp.exe\"\033[0m"
    echo -e "\033[0;37m  mst -d \"C:\\Bin\" -types exe,dll\033[0m"
    echo ""
    
    # Test the installation
    echo -e "\033[0;33mTesting installation...\033[0m"
    echo ""
    mst
    
    echo ""
    echo -e "\033[0;32mInstallation test complete!\033[0m"
else
    echo -e "\033[0;31mInstallation failed!\033[0m"
    exit 1
fi