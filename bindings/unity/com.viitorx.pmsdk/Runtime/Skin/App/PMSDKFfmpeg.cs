using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>How much a converted video is reduced.</summary>
    public enum PMSDKConvertPreset
    {
        /// <summary>H.264 MP4, widths above 1920 are scaled down. Best quality that plays everywhere.</summary>
        Standard = 0,
        /// <summary>H.264 MP4 capped at 1280 wide: smaller, lighter on weak PCs.</summary>
        Light = 1,
    }

    /// <summary>
    /// FFmpeg helpers for the "this video cannot be played" flow: finding an installed
    /// ffmpeg, generating the install script (progress lines on stdout), building the
    /// conversion command line and parsing ffmpeg/installer output. Everything here is a pure
    /// function or file helper so it can be unit-tested; process handling is in
    /// <see cref="PMSDKFfmpegJob"/>.
    /// </summary>
    public static class PMSDKFfmpeg
    {
        public const string ProgressMarker = "PMSDK_PROGRESS";
        public const string LogMarker = "PMSDK_LOG";
        public const string ResultMarker = "PMSDK_RESULT";

        private static readonly Regex DurationRegex =
            new Regex(@"Duration:\s*(\d+):(\d+):(\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex OutTimeRegex =
            new Regex(@"^out_time_(?:us|ms)=(\d+)\s*$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex PercentRegex =
            new Regex(@"(?<![\d.])(\d{1,3})\s?%", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public static bool IsWindows {
            get {
                return Application.platform == RuntimePlatform.WindowsEditor
                    || Application.platform == RuntimePlatform.WindowsPlayer;
            }
        }

        private static string ExecutableName {
            get { return IsWindows ? "ffmpeg.exe" : "ffmpeg"; }
        }

        /// <summary>The folder the installer script copies FFmpeg into (per user, no admin needed).</summary>
        public static string AppInstallFolder {
            get {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(Path.Combine(local, "PMSDK"), "ffmpeg");
            }
        }

        // ------------------------------------------------------------------ locate

        /// <summary>Full path of an installed ffmpeg, or null. Looks on PATH (including the
        /// PATH stored in the registry, so an install done after this process started is found
        /// without a restart) and in the usual winget / app-local locations.</summary>
        public static string Locate()
        {
            List<string> directories = new List<string>();
            AddPathList(directories, Environment.GetEnvironmentVariable("PATH"));
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            AddPathList(directories, ReadRegistryPath(true));
            AddPathList(directories, ReadRegistryPath(false));
#endif
            if (IsWindows) {
                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                directories.Add(Path.Combine(AppInstallFolder, "bin"));
                directories.Add(Path.Combine(Path.Combine(Path.Combine(local, "Microsoft"), "WinGet"), "Links"));
                AddWingetPackageBins(directories, local);
                string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                if (!string.IsNullOrEmpty(programFiles)) {
                    directories.Add(Path.Combine(Path.Combine(programFiles, "ffmpeg"), "bin"));
                }
                directories.Add(@"C:\ffmpeg\bin");
            }
            return FindExecutable(directories, ExecutableName);
        }

        /// <summary>First existing <paramref name="fileName"/> in the directories (testable core of <see cref="Locate"/>).</summary>
        public static string FindExecutable(IEnumerable<string> directories, string fileName)
        {
            foreach (string directory in directories) {
                if (string.IsNullOrWhiteSpace(directory)) {
                    continue;
                }
                try {
                    string candidate = Path.Combine(directory.Trim().Trim('"'), fileName);
                    if (File.Exists(candidate)) {
                        return candidate;
                    }
                } catch (ArgumentException) {
                    // Malformed PATH entry - skip it.
                }
            }
            return null;
        }

        private static void AddPathList(List<string> target, string pathList)
        {
            if (string.IsNullOrEmpty(pathList)) {
                return;
            }
            string expanded = Environment.ExpandEnvironmentVariables(pathList);
            target.AddRange(expanded.Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries));
        }

        private static void AddWingetPackageBins(List<string> target, string localAppData)
        {
            try {
                string packages = Path.Combine(Path.Combine(Path.Combine(localAppData, "Microsoft"), "WinGet"), "Packages");
                if (!Directory.Exists(packages)) {
                    return;
                }
                foreach (string package in Directory.GetDirectories(packages, "*FFmpeg*")) {
                    foreach (string build in Directory.GetDirectories(package, "ffmpeg-*")) {
                        target.Add(Path.Combine(build, "bin"));
                    }
                    target.Add(package);
                }
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKFfmpeg: could not scan winget packages - " + ex.Message);
            }
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private static string ReadRegistryPath(bool user)
        {
            try {
                Microsoft.Win32.RegistryKey root = user ? Microsoft.Win32.Registry.CurrentUser : Microsoft.Win32.Registry.LocalMachine;
                string subKey = user ? "Environment" : @"SYSTEM\CurrentControlSet\Control\Session Manager\Environment";
                using (Microsoft.Win32.RegistryKey key = root.OpenSubKey(subKey)) {
                    if (key == null) {
                        return null;
                    }
                    return key.GetValue("Path", "", Microsoft.Win32.RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
                }
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKFfmpeg: could not read the registry PATH - " + ex.Message);
                return null;
            }
        }
#endif

        // ------------------------------------------------------------------ conversion command

        /// <summary>Output path next to the source (never overwrites): name_pmsdk.mp4, name_pmsdk_2.mp4 ...
        /// Falls back to <c>persistentDataPath/Converted</c> when the source folder is not writable.</summary>
        public static string BuildOutputPath(string inputPath)
        {
            string directory = SafeDirectory(inputPath);
            if (string.IsNullOrEmpty(directory) || !IsWritable(directory)) {
                directory = Path.Combine(Application.persistentDataPath, "Converted");
                Directory.CreateDirectory(directory);
            }
            string baseName = Path.GetFileNameWithoutExtension(inputPath) + "_pmsdk";
            string candidate = Path.Combine(directory, baseName + ".mp4");
            int counter = 2;
            while (File.Exists(candidate) || string.Equals(candidate, inputPath, StringComparison.OrdinalIgnoreCase)) {
                candidate = Path.Combine(directory, baseName + "_" + counter.ToString(CultureInfo.InvariantCulture) + ".mp4");
                counter++;
            }
            return candidate;
        }

        /// <summary>Arguments (one list entry per argument - no shell quoting involved) for the conversion.</summary>
        public static List<string> BuildConvertArguments(string inputPath, string outputPath, PMSDKConvertPreset preset)
        {
            int maxWidth = preset == PMSDKConvertPreset.Light ? 1280 : 1920;
            List<string> args = new List<string>();
            args.Add("-hide_banner");
            args.Add("-nostdin");
            args.Add("-y");
            args.Add("-i");
            args.Add(inputPath);
            args.Add("-map");
            args.Add("0:v:0");
            args.Add("-map");
            args.Add("0:a:0?");
            args.Add("-c:v");
            args.Add("libx264");
            args.Add("-preset");
            args.Add("medium");
            args.Add("-crf");
            args.Add(preset == PMSDKConvertPreset.Light ? "24" : "20");
            args.Add("-pix_fmt");
            args.Add("yuv420p");
            args.Add("-vf");
            args.Add("scale='min(" + maxWidth.ToString(CultureInfo.InvariantCulture) + ",iw)':-2");
            args.Add("-c:a");
            args.Add("aac");
            args.Add("-b:a");
            args.Add("160k");
            args.Add("-movflags");
            args.Add("+faststart");
            args.Add("-progress");
            args.Add("pipe:1");
            args.Add("-nostats");
            args.Add(outputPath);
            return args;
        }

        // ------------------------------------------------------------------ output parsing

        /// <summary>Seconds from an ffmpeg "Duration: 00:01:23.45" banner line, or null.</summary>
        public static double? ParseDurationSeconds(string line)
        {
            if (string.IsNullOrEmpty(line)) {
                return null;
            }
            Match m = DurationRegex.Match(line);
            if (!m.Success) {
                return null;
            }
            double hours = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
            double minutes = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            double seconds = double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
            return hours * 3600.0 + minutes * 60.0 + seconds;
        }

        /// <summary>Encoded-so-far seconds from an "-progress" line (<c>out_time_us=...</c>), or null.</summary>
        public static double? ParseProgressSeconds(string line)
        {
            if (string.IsNullOrEmpty(line)) {
                return null;
            }
            Match m = OutTimeRegex.Match(line.Trim());
            if (!m.Success) {
                return null;
            }
            double microseconds;
            if (!double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out microseconds)) {
                return null;
            }
            return microseconds / 1000000.0;
        }

        /// <summary>Installer markers: <c>PMSDK_PROGRESS 42 Downloading...</c>.</summary>
        public static bool TryParseProgressMarker(string line, out int percent, out string message)
        {
            percent = 0;
            message = "";
            if (string.IsNullOrEmpty(line)) {
                return false;
            }
            string trimmed = line.Trim();
            if (!trimmed.StartsWith(ProgressMarker + " ", StringComparison.Ordinal)) {
                return false;
            }
            string rest = trimmed.Substring(ProgressMarker.Length + 1).TrimStart();
            int space = rest.IndexOf(' ');
            string number = space < 0 ? rest : rest.Substring(0, space);
            if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out percent)) {
                return false;
            }
            percent = Mathf.Clamp(percent, 0, 100);
            message = space < 0 ? "" : rest.Substring(space + 1).Trim();
            return true;
        }

        /// <summary>Any "NN%" in a line (winget / tool progress), or -1.</summary>
        public static int ParsePercent(string line)
        {
            if (string.IsNullOrEmpty(line)) {
                return -1;
            }
            Match m = PercentRegex.Match(line);
            int value;
            if (m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)
                && value >= 0 && value <= 100) {
                return value;
            }
            return -1;
        }

        // ------------------------------------------------------------------ installer scripts

        /// <summary>Write the installer (bat + ps1) into <paramref name="folder"/> and return the bat path.
        /// The bat tries winget first (official package, adds itself to PATH) and falls back to a
        /// checksum-verified download from gyan.dev that is installed per user and added to the
        /// USER PATH. <c>--dry-run</c> prints fake progress and changes nothing.</summary>
        public static string WriteInstallScripts(string folder)
        {
            Directory.CreateDirectory(folder);
            string ps1 = Path.Combine(folder, "PMSDK_install_ffmpeg.ps1");
            string bat = Path.Combine(folder, "PMSDK_install_ffmpeg.bat");
            File.WriteAllText(ps1, InstallPowerShell.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            File.WriteAllText(bat, InstallBatch.Replace("\r\n", "\n").Replace("\n", "\r\n"));
            return bat;
        }

        private static string SafeDirectory(string path)
        {
            try {
                return Path.GetDirectoryName(path);
            } catch (ArgumentException) {
                return null;
            }
        }

        private static bool IsWritable(string directory)
        {
            try {
                string probe = Path.Combine(directory, ".pmsdk_write_test_" + Guid.NewGuid().ToString("N"));
                using (FileStream stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) {
                    stream.WriteByte(0);
                }
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private const string InstallBatch = @"@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul
set ""PS1=%~dp0PMSDK_install_ffmpeg.ps1""
if /I ""%~1""==""--dry-run"" (
  powershell -NoProfile -ExecutionPolicy Bypass -File ""%PS1%"" -DryRun
  exit /b !errorlevel!
)
echo PMSDK_PROGRESS 2 Checking for winget...
where winget >nul 2>nul
if not errorlevel 1 (
  echo PMSDK_PROGRESS 8 Installing FFmpeg with winget - this can take a few minutes...
  winget install --id Gyan.FFmpeg -e --silent --accept-package-agreements --accept-source-agreements
  if not errorlevel 1 (
    echo PMSDK_PROGRESS 100 FFmpeg installed with winget
    echo PMSDK_RESULT OK winget
    exit /b 0
  )
  echo PMSDK_LOG winget did not finish, using the direct download instead.
)
powershell -NoProfile -ExecutionPolicy Bypass -File ""%PS1%""
exit /b !errorlevel!
";

        private const string InstallPowerShell = @"param([switch]$DryRun)
$ErrorActionPreference = 'Stop'
function Progress([int]$p, [string]$m) { Write-Output (""PMSDK_PROGRESS {0} {1}"" -f $p, $m) }
try {
    if ($DryRun) {
        foreach ($p in 10, 30, 50, 70, 90) { Progress $p 'Dry run - nothing is installed'; Start-Sleep -Milliseconds 200 }
        Progress 100 'Dry run finished'
        Write-Output 'PMSDK_RESULT OK dry-run'
        exit 0
    }
    $url = 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip'
    $root = Join-Path $env:LOCALAPPDATA 'PMSDK\ffmpeg'
    $bin = Join-Path $root 'bin'
    $zip = Join-Path $env:TEMP 'pmsdk_ffmpeg_essentials.zip'
    $tmp = Join-Path $env:TEMP 'pmsdk_ffmpeg_extract'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Progress 5 'Preparing the download'
    $req = [Net.HttpWebRequest]::Create($url)
    $req.Method = 'HEAD'
    $resp = $req.GetResponse()
    $total = $resp.ContentLength
    $resp.Close()
    if (Test-Path $zip) { Remove-Item $zip -Force }
    $wc = New-Object Net.WebClient
    $wc.DownloadFileAsync([Uri]$url, $zip)
    while ($wc.IsBusy) {
        Start-Sleep -Milliseconds 400
        if ((Test-Path $zip) -and ($total -gt 0)) {
            $got = (Get-Item $zip).Length
            Progress (5 + [int](70 * $got / $total)) ('Downloading FFmpeg {0:N0} of {1:N0} MB' -f ($got / 1MB), ($total / 1MB))
        }
    }
    if ((-not (Test-Path $zip)) -or ((Get-Item $zip).Length -ne $total)) { throw 'The download did not finish' }
    Progress 78 'Verifying the download'
    $expected = ((Invoke-WebRequest -UseBasicParsing ($url + '.sha256')).Content.ToString().Trim() -split '\s+')[0].ToLower()
    $actual = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    if ($expected -ne $actual) { throw 'Checksum mismatch - the download was rejected' }
    Progress 84 'Extracting'
    if (Test-Path $tmp) { Remove-Item $tmp -Recurse -Force }
    Expand-Archive -Path $zip -DestinationPath $tmp -Force
    $src = Get-ChildItem $tmp -Directory | Select-Object -First 1
    $srcBin = Join-Path $src.FullName 'bin'
    Progress 92 'Installing'
    New-Item -ItemType Directory -Force -Path $bin | Out-Null
    Copy-Item (Join-Path $srcBin '*') $bin -Force
    Progress 96 'Adding FFmpeg to your PATH'
    $userPath = [Environment]::GetEnvironmentVariable('Path', 'User')
    if ($null -eq $userPath) { $userPath = '' }
    $already = ($userPath -split ';') | Where-Object { $_.TrimEnd('\') -ieq $bin }
    if (-not $already) {
        [Environment]::SetEnvironmentVariable('Path', (($userPath.TrimEnd(';') + ';' + $bin).TrimStart(';')), 'User')
    }
    Remove-Item $zip, $tmp -Recurse -Force -ErrorAction SilentlyContinue
    Progress 100 'FFmpeg installed'
    Write-Output ('PMSDK_RESULT OK ' + (Join-Path $bin 'ffmpeg.exe'))
    exit 0
} catch {
    Write-Output ('PMSDK_RESULT FAIL ' + $_.Exception.Message)
    exit 1
}
";
    }
}
