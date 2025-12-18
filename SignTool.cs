using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Cryptography.X509Certificates;

namespace SignTool
{
    class Program
    {
        private static readonly string SignToolPath = 
            @"C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe";
        private static readonly string CertificateThumbprint = "";
        private static readonly string LogFilePath = "signing_log.txt";
        private static readonly List<string> _signedFiles = [];
        private static readonly List<string> _failedFiles = [];
        
        private static readonly string[] TimestampServers = [
            "http://timestamp.digicert.com",
            "http://timestamp.sectigo.com",
            "http://rfc3161timestamp.globalsign.com/advanced",
            "http://timestamp.comodoca.com/rfc3161"
        ];
        
        private static int _successCount = 0;
        private static int _failCount = 0;
        private static int _skippedCount = 0;
        private static readonly object _lockObject = new();
        private static bool _isRemoveMode = false;

        // Argument parsing results
        private class Arguments
        {
            public string? Path { get; set; }
            public bool Recursive { get; set; } = true;
            public HashSet<string> Extensions { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public bool IsSingleFile { get; set; } = false;
            public bool RemoveSignature { get; set; } = false;
        }

        private static bool CertificateExists(string thumbprint)
        {
            using var store = new X509Store(StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);

            return store.Certificates
                .Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false)
                .Count > 0;
        }

        static async Task Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==================================================");
            Console.WriteLine("    Megabyte Systems - Bulk Code Signing Tool");
            Console.WriteLine("==================================================");
            Console.ResetColor();
            Console.WriteLine($"Certificate: Megabyte Systems, Inc. (OV)");
            Console.WriteLine($"Thumbprint: {CertificateThumbprint}");
            Console.WriteLine($"SignTool: {SignToolPath}");
            Console.WriteLine($"Framework: .NET 8.0");
            Console.WriteLine();

            if (!CertificateExists(CertificateThumbprint))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("WARNING: Signing certificate not found. Skipping code signing.");
                Console.ResetColor();
                Environment.Exit(0);
            }


            string localSignToolPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "signtool.exe");
            if (!File.Exists(localSignToolPath))
            {
                try { File.Copy(SignToolPath, localSignToolPath, true); }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Warning: Could not copy signtool.exe locally: {ex.Message}");
                    Console.WriteLine("Will use original path instead.");
                    Console.ResetColor();
                }
            }

            string currentDir = AppDomain.CurrentDomain.BaseDirectory;
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            if (!currentPath.Contains(currentDir))
            {
                Environment.SetEnvironmentVariable("PATH", currentPath + ";" + currentDir);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Added to PATH: {currentDir}");
                Console.ResetColor();
            }
            Console.WriteLine();
            if (args.Length == 0 || args[0] == "-help" || args[0] == "--help" 
                || args[0] == "-h" || args[0] == "/?" || args[0] == "help")
            {
                PrintUsage();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            var parsedArgs = ParseArguments(args);
            if (parsedArgs == null || string.IsNullOrEmpty(parsedArgs.Path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Invalid arguments provided.");
                Console.ResetColor();
                Console.WriteLine();
                PrintUsage();
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Starting code signing process...");
            Console.WriteLine($"Mode: {(parsedArgs.IsSingleFile ?
                "Single File" : (parsedArgs.Recursive ? "Recursive Directory" : "Non-Recursive Directory"))}");
            Console.WriteLine($"Target: {parsedArgs.Path}");
            if (parsedArgs.Extensions.Count > 0)
            {
                Console.WriteLine($"Extensions: {string.Join(", ", parsedArgs.Extensions)}");
            }
            Console.WriteLine();

            _isRemoveMode = parsedArgs.RemoveSignature;

            try
            {
                if (parsedArgs.IsSingleFile)
                {
                    await ProcessSingleFileAsync(parsedArgs.Path, parsedArgs.RemoveSignature);
                }
                else
                {
                    await ProcessDirectoryAsync(parsedArgs.Path, parsedArgs.Recursive, parsedArgs.Extensions, parsedArgs.RemoveSignature);
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"CRITICAL ERROR: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.ResetColor();
            }

            PrintSummary();
            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static void PrintUsage()
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("USAGE:");
            Console.WriteLine("  mst -dr <directory>           # Recursive directory scan (default)");
            Console.WriteLine("  mst -d <directory>            # Non-recursive directory scan");
            Console.WriteLine("  mst -exe <file.exe>           # Sign single EXE file");
            Console.WriteLine("  mst -dll <file.dll>           # Sign single DLL file");
            Console.WriteLine("  mst -file <path>              # Sign any single file");
            Console.WriteLine("  mst <directory> <filename>    # Smart search and sign");
            Console.WriteLine("  mst -remove <file>            # Remove signature from single file");
            Console.WriteLine("  mst -remove-dr <directory>    # Remove signatures recursively");
            Console.WriteLine("  mst -remove-d <directory>     # Remove signatures non-recursively");
            Console.WriteLine();
            Console.WriteLine("OPTIONAL FILE TYPE FILTERS (can combine multiple):");
            Console.WriteLine("  -types exe                         # Only .exe files");
            Console.WriteLine("  -types dll                         # Only .dll files");
            Console.WriteLine("  -types exe,dll                     # Both .exe and .dll files");
            Console.WriteLine("  -types exe,dll,msi,sys,ocx,cab,cat # Multiple types");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  mst -dr \"C:\\MyProject\"");
            Console.WriteLine("  mst -d \"C:\\MyProject\\bin\" -types exe,dll");
            Console.WriteLine("  mst -exe \"C:\\MyApp.exe\"");
            Console.WriteLine("  mst -dll \"C:\\MyLibrary.dll\"");
            Console.WriteLine("  mst -dr \"C:\\MyProject\" -types exe");
            Console.WriteLine("  mst \"C:\\MyProject\" TCW0300");
            Console.WriteLine("  mst \"C:\\Build\" MPTSCore");
            Console.WriteLine("  mst -remove \"C:\\MyApp.exe\"");
            Console.WriteLine("  mst -remove-dr \"C:\\SignedBinaries\"");
            Console.WriteLine("  mst -remove-d \"C:\\SingleFolder\"");
            Console.ResetColor();
        }

        private static Arguments? ParseArguments(string[] args)
        {
            var result = new Arguments();
            
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].ToLower();
                
                switch (arg)
                {
                    case "-dr":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.Recursive = true;
                        result.IsSingleFile = false;
                        break;
                        
                    case "-d":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.Recursive = false;
                        result.IsSingleFile = false;
                        break;
                        
                    case "-exe":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.IsSingleFile = true;
                        if (!result.Path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"ERROR: -exe flag requires a file ending with .exe");
                            Console.ResetColor();
                            return null;
                        }
                        break;
                        
                    case "-dll":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.IsSingleFile = true;
                        if (!result.Path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"ERROR: -dll flag requires a file ending with .dll");
                            Console.ResetColor();
                            return null;
                        }
                        break;
                        
                    case "-file":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.IsSingleFile = true;
                        break;
                        
                    case "-types":
                        if (i + 1 >= args.Length) return null;
                        string[] types = args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries);
                        foreach (string type in types)
                        {
                            string cleanType = type.Trim().ToLower();
                            if (!cleanType.StartsWith("."))
                            {
                                cleanType = "." + cleanType;
                            }
                            result.Extensions.Add(cleanType);
                        }
                        break;
                        
                    case "-remove":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.IsSingleFile = true;
                        result.RemoveSignature = true;
                        break;                        
                    
                    case "-remove-dr":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.Recursive = true;
                        result.IsSingleFile = false;
                        result.RemoveSignature = true;
                        break;
                        
                    case "-remove-d":
                        if (i + 1 >= args.Length) return null;
                        result.Path = args[++i];
                        result.Recursive = false;
                        result.IsSingleFile = false;
                        result.RemoveSignature = true;
                        break;

                    default:
                        // Smart search: <directory> <searchTerm>
                        if (string.IsNullOrEmpty(result.Path))
                        {
                            result.Path = args[i];
                            result.Recursive = true;
                            result.IsSingleFile = false;
                        }
                        else if (i == args.Length - 1 && !string.IsNullOrEmpty(result.Path) && !result.IsSingleFile)
                        {
                            // Second positional argument - treat as smart search
                            string searchTerm = args[i];
                            string? foundFile = SmartFindFile(result.Path, searchTerm);
                            if (foundFile != null)
                            {
                                result.Path = foundFile;
                                result.IsSingleFile = true;
                            }
                            else
                            {
                                return null;
                            }
                        }
                        break;
                }
            }
            
            // If no extensions specified, use defaults
            if (result.Extensions.Count == 0 && !result.IsSingleFile)
            {
                result.Extensions.Add(".exe");
                result.Extensions.Add(".dll");
            }
            
            return result;
        }

        private static string? SmartFindFile(string basePath, string searchTerm)
        {
            if (!Directory.Exists(basePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: Directory does not exist: {basePath}");
                Console.ResetColor();
                return null;
            }

            var allFiles = Directory.GetFiles(basePath, "*.*", SearchOption.AllDirectories)
                .Where(f => {
                    string fileName = Path.GetFileNameWithoutExtension(f);
                    string ext = Path.GetExtension(f).ToLower();
                    return fileName.Equals(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                           (ext == ".exe" || ext == ".dll" || ext == ".msi" || ext == ".sys" || 
                            ext == ".ocx" || ext == ".cab" || ext == ".cat");
                })
                .ToList();

            if (allFiles.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: No files found matching '{searchTerm}' in {basePath}");
                Console.ResetColor();
                return null;
            }

            if (allFiles.Count == 1)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Found: {allFiles[0]}");
                Console.ResetColor();
                return allFiles[0];
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Multiple files matching '{searchTerm}' found. Select one of the following:");
            Console.ResetColor();
            
            for (int i = 0; i < allFiles.Count; i++)
            {
                Console.WriteLine($"{i + 1}. {allFiles[i]}");
            }
            
            Console.Write("\nEnter selection (1-" + allFiles.Count + "): ");
            string? input = Console.ReadLine();
            
            if (int.TryParse(input, out int selection) && selection >= 1 && selection <= allFiles.Count)
            {
                string selectedFile = allFiles[selection - 1];
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Selected: {selectedFile}");
                Console.ResetColor();
                return selectedFile;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("ERROR: Invalid selection");
                Console.ResetColor();
                return null;
            }
        }

        private static async Task ProcessSingleFileAsync(string filePath, bool removeSignature = false)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"PROCESSING SINGLE FILE: {filePath}");
            Console.WriteLine($"Mode: {(removeSignature ? "REMOVE SIGNATURE" : "SIGN")}");
            Console.ResetColor();

            if (!File.Exists(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: File does not exist: {filePath}");
                Console.ResetColor();
                return;
            }
            if (removeSignature)
            {
                await RemoveSignatureAsync(filePath);
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("TESTING WITH PASSWORD PROMPT IF NEEDED...");
                Console.ResetColor();
                await TestSignWithPasswordPrompt(filePath);
                Console.WriteLine();

                if (!IsFileAlreadySigned(filePath))
                {
                    await SignFileAsync(filePath);
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"File already signed successfully during test: {Path.GetFileName(filePath)}");
                    Console.ResetColor();
                    lock (_lockObject) { _successCount++; }
                }
            }
        }

        private static async Task RemoveSignatureAsync(string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"REMOVING SIGNATURE: {fileName}");
            Console.ResetColor();

            try
            {
                string arguments = $"remove /s \"{filePath}\"";
                bool success = await ExecuteSignToolAsync(arguments, filePath, false);

                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"SUCCESS: File unsigned - {fileName}");
                    Console.ResetColor();
                    lock (_lockObject) { _successCount++; }
                }
                else
                {
                    if (!IsFileAlreadySigned(filePath))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"SKIPPED: File has no signature to remove - {fileName}");
                        Console.ResetColor();
                        lock (_lockObject) { _skippedCount++; }
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"FAILED: Could not remove signature from {fileName}");
                        Console.ResetColor();
                        lock (_lockObject) { _failCount++; }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR removing signature from {fileName}: {ex.Message}");
                Console.ResetColor();
                lock (_lockObject) { _failCount++; }
            }
        }

        private static bool IsFileAlreadySigned(string filePath)
        {
            try
            {
                using var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = SignToolPath,
                    Arguments = $"verify /pa \"{filePath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                
                process.Start();
                process.WaitForExit(10000);
                return process.ExitCode == 0;
            }
            catch { return false; }
        }

        private static void LogSignedFile(string filePath, string timestampServer, DateTime timestamp)
        {
            lock (_lockObject)
            {
                _signedFiles.Add(filePath);
                
                File.AppendAllText(LogFilePath, 
                    $"{timestamp:yyyy-MM-dd HH:mm:ss} | {GetServerName(timestampServer)} | {filePath}{Environment.NewLine}");
            }
        }

        private static async Task RetryFailedFilesAsync()
        {
            if (_failedFiles.Count == 0) return;

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"RETRYING {_failedFiles.Count} FAILED FILES...");
            Console.ResetColor();

            var retryFailedFiles = new List<string>(_failedFiles);
            _failedFiles.Clear();

            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };

            await Parallel.ForEachAsync(retryFailedFiles, options, async (filePath, cancellationToken) =>
            {
                await SignFileAsync(filePath, true);
            });
        }

        private static async Task ProcessDirectoryAsync(string directoryPath, bool recursive, HashSet<string> extensions, bool removeSignature = false)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"SCANNING DIRECTORY: {directoryPath}");
                Console.WriteLine($"Mode: {(recursive ? "Recursive" : "Non-Recursive")}");
                Console.WriteLine($"Action: {(removeSignature ? "REMOVE SIGNATURES" : "SIGN FILES")}");
                Console.ResetColor();
                
                if (!Directory.Exists(directoryPath))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"ERROR: Directory does not exist: {directoryPath}");
                    Console.ResetColor();
                    return;
                }

                var executables = FindAllExecutables(directoryPath, recursive, extensions);
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"FOUND {executables.Count} FILES TO PROCESS");
                Console.ResetColor();
                Console.WriteLine();
                
                if (executables.Count == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"INFO: No files found matching extensions: {string.Join(", ", extensions)}");
                    Console.ResetColor();
                    return;
                }

                if (removeSignature)
                {
                    await RemoveSignaturesFromDirectory(executables);
                }
                else
                {
                    if (executables.Count > 0)
                    {
                        var firstFile = executables.First();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("TESTING WITH FIRST FILE TO HANDLE PASSWORD PROMPT...");
                        Console.ResetColor();
                        await TestSignWithPasswordPrompt(firstFile);
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("CONTINUING WITH BULK SIGNING...");
                        Console.ResetColor();
                        Console.WriteLine();
                    }

                    var options = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount * 2
                    };
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"STARTING PARALLEL PROCESSING WITH {options.MaxDegreeOfParallelism} THREADS");
                    Console.ResetColor();
                    await Parallel.ForEachAsync(executables, options, async (executable, cancellationToken) =>
                    {
                        await SignFileAsync(executable);
                    });

                    if (_failedFiles.Count > 0)
                    {
                        await RetryFailedFilesAsync();
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ACCESS DENIED: Cannot access directory {directoryPath}");
                Console.WriteLine($"Details: {ex.Message}");
                Console.ResetColor();
            }
            catch (PathTooLongException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"PATH TOO LONG: {directoryPath}");
                Console.WriteLine($"Details: {ex.Message}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"UNEXPECTED ERROR scanning directory {directoryPath}");
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.ResetColor();
            }
    }
        
        private static async Task RemoveSignaturesFromDirectory(List<string> files)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount * 2 };
            
            await Parallel.ForEachAsync(files, options, async (filePath, cancellationToken) =>
            {
                await RemoveSignatureAsync(filePath);
            });
        }

        private static async Task TestSignWithPasswordPrompt(string testFilePath)
        {
            string fileName = Path.GetFileName(testFilePath);
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"TEST SIGNING: {fileName}");
            Console.ResetColor();
            var signingApproaches = new[]
            {
                new
                {
                    Name = "Standard",
                    Arguments = $"sign /fd SHA256 /tr {TimestampServers[0]} " +
                    $"/td SHA256 /sha1 {CertificateThumbprint} \"{testFilePath}\""
                }
            };

            foreach (var approach in signingApproaches)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  Attempting {approach.Name} approach...");
                Console.ResetColor();
                
                bool success = await ExecuteSignToolAsync(approach.Arguments, testFilePath, true);
                
                if (success)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  SUCCESS: {approach.Name} approach worked");
                    Console.ResetColor();
                    return;
                }
            }
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("  WARNING: Password prompt may appear for each file");
            Console.ResetColor();
        }

        private static List<string> FindAllExecutables(string directoryPath, bool recursive, HashSet<string> extensions)
        {
            var executables = new List<string>();
            try
            {
                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                
                foreach (string extension in extensions)
                {
                    try
                    {
                        string pattern = extension.StartsWith(".") ? "*" + extension : "*." + extension;
                        var files = Directory.GetFiles(directoryPath, pattern, searchOption);
                        
                        var filteredFiles = files.Where(f =>
                        {
                            string ext = Path.GetExtension(f).ToLower();
                            string fileName = Path.GetFileName(f);
                            
                            if (ext == ".exe") return true;
                            
                            if (ext == ".dll")
                                return fileName.StartsWith("MPTS", StringComparison.OrdinalIgnoreCase); // Only MPTS* DLLs
                            
                            return true;
                        }).ToArray();
                        
                        executables.AddRange(filteredFiles);
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        
                        if (extension.ToLower() == ".dll")
                        {
                            Console.WriteLine($"Found {filteredFiles.Length} {extension} files starting with 'MPTS' (filtered from {files.Length} total)");
                        }
                        else
                        {
                            Console.WriteLine($"Found {filteredFiles.Length} {extension} files");
                        }
                        Console.ResetColor();
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"WARNING: Access denied for some {extension} files");
                        Console.WriteLine($"Details: {ex.Message}");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"WARNING: Error searching for {extension} files");
                        Console.WriteLine($"Details: {ex.Message}");
                        Console.ResetColor();
                    }
                }
                
                executables = executables.Distinct().OrderBy(f => f).ToList();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"TOTAL FILES TO PROCESS: {executables.Count}");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR during file discovery in {directoryPath}");
                Console.WriteLine($"Details: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.ResetColor();
            }
            return executables;
        }        
        
        private static async Task SignFileAsync(string filePath, bool isRetry = false)
        {
            string fileName = Path.GetFileName(filePath);
            string? directory = Path.GetDirectoryName(filePath);
            if (!isRetry && IsFileAlreadySigned(filePath))
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"SKIPPED: {fileName} (already signed)");
                Console.ResetColor();
                lock (_lockObject) { _skippedCount++; }
                return;
            }
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"PROCESSING: {fileName}" + (isRetry ? " [RETRY]" : ""));
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Location: {directory ?? "Unknown Directory"}");
            Console.ResetColor();

            try
            {
                if (!File.Exists(filePath))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  SKIPPED: File not found - {fileName}");
                    Console.ResetColor();
                    lock (_lockObject) { _skippedCount++; }
                    return;
                }

                FileInfo fileInfo = new(filePath);
                if (fileInfo.Length == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  SKIPPED: Empty file - {fileName}");
                    Console.ResetColor();
                    lock (_lockObject) { _skippedCount++; }
                    return;
                }
                bool signed = false;
                string lastError = "Unknown error";
                foreach (string timestampServer in TimestampServers)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.DarkCyan;
                        Console.WriteLine($"  Attempting signature with: " +
                            $"{GetServerName(timestampServer)}");
                        Console.ResetColor();
                        string arguments = $"sign /fd SHA256 /tr {timestampServer} /td SHA256 " +
                            $"/sha1 {CertificateThumbprint} \"{filePath}\"";
                        bool success = await ExecuteSignToolAsync(arguments, filePath, false);

                        if (success)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"  SUCCESS: Signed with {GetServerName(timestampServer)}");
                            Console.ResetColor();
                            lock (_lockObject) { _successCount++; }
                            LogSignedFile(filePath, timestampServer, DateTime.Now);
                            signed = true;
                            break;
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.DarkYellow;
                            Console.WriteLine($"  Failed with {GetServerName(timestampServer)}");
                            Console.ResetColor();
                            lastError = $"Failed with {GetServerName(timestampServer)}";
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  EXCEPTION with " +
                            $"{GetServerName(timestampServer)}: {ex.Message}");
                        Console.ResetColor();
                        lastError = ex.Message;
                    }
                }

                if (!signed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  FAILED: Could not sign {fileName}");
                    Console.WriteLine($"  Last error: {lastError}");
                    Console.ResetColor();
                    
                    lock (_lockObject)
                    {
                        _failCount++;
                        _failedFiles.Add(filePath);
                    }
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  ACCESS DENIED: Cannot access file {fileName}");
                Console.WriteLine($"  Details: {ex.Message}");
                Console.ResetColor();
                
                lock (_lockObject)
                {
                    _failCount++;
                }
            }
            catch (IOException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  IO ERROR: {fileName}");
                Console.WriteLine($"  Details: {ex.Message}");
                Console.ResetColor();
                lock (_lockObject)
                {
                    _failCount++;
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  UNEXPECTED ERROR with {fileName}");
                Console.WriteLine($"  Error: {ex.Message}");
                Console.WriteLine($"  Stack trace: {ex.StackTrace}");
                Console.ResetColor();
                lock (_lockObject)
                {
                    _failCount++;
                }
            }
            Console.WriteLine();
        }

        private static async Task<bool> ExecuteSignToolAsync(
            string arguments, string filePath, bool isTest)
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = SignToolPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process();
                process.StartInfo = processStartInfo;
                bool started = process.Start();
                if (!started)
                {
                    if (!isTest)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("      Failed to start signtool process");
                        Console.ResetColor();
                    }
                    return false;
                }

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                bool exited = process.WaitForExit(60000);
                if (!exited)
                {
                    if (!isTest)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("      TIMEOUT: Process took too long, killing...");
                        Console.ResetColor();
                    }
                    process.Kill();
                    return false;
                }

                string output = await outputTask;
                string error = await errorTask;
                if (!string.IsNullOrEmpty(output) && !isTest)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"      Output: {output.Trim()}");
                    Console.ResetColor();
                }

                if (!string.IsNullOrEmpty(error) && !isTest)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"      Error: {error.Trim()}");
                    Console.ResetColor();
                }

                if (process.ExitCode == 0)
                {
                    return true;
                }
                else
                {
                    if (!isTest)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"      Exit code: {process.ExitCode}");
                        Console.ResetColor();
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                if (!isTest)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"      Process exception: {ex.Message}");
                    Console.ResetColor();
                }
                return false;
            }
        }

        private static string GetServerName(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "Unknown Server";

            return url switch
            {
                "http://timestamp.digicert.com" => "Digicert (Primary)",
                "http://timestamp.sectigo.com" => "Sectigo",
                "http://rfc3161timestamp.globalsign.com/advanced" => "GlobalSign",
                "http://timestamp.comodoca.com/rfc3161" => "Comodo",
                _ => url ?? "Unknown Server"
            };
        }

        private static void PrintSummary()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("SIGNING SUMMARY");
            Console.WriteLine("===============");
            Console.ResetColor();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Successfully {(_isRemoveMode ? "unsigned" : "signed")}: {_successCount}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Failed: {_failCount}");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"Skipped: {_skippedCount}");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Total processed: {_successCount + _failCount + _skippedCount}");
            Console.ResetColor();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"Log file created: {Path.GetFullPath(LogFilePath)}");
            Console.WriteLine("Timestamp Server Priority:");
            foreach (var server in TimestampServers)
            {
                Console.WriteLine($"-> {GetServerName(server)}");
            }
            Console.ResetColor();
        }
    }
}





