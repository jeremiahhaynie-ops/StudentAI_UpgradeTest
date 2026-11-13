using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel;

namespace StudentAI;

public static class AppLauncherService
{
    public sealed record Target(
        string Name,
        string Kind,
        string Value);

    private static readonly Dictionary<string, Target> BuiltIns =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["youtube"] = new(
                "YouTube in your browser",
                "web",
                "https://www.youtube.com"),

            ["google"] = new(
                "Google in your browser",
                "web",
                "https://www.google.com"),

            ["spotify"] = new(
                "Spotify in your browser",
                "web",
                "https://open.spotify.com"),

            ["notepad"] = new(
                "Notepad",
                "exe",
                "notepad.exe"),

            ["calculator"] = new(
                "Calculator",
                "exe",
                "calc.exe"),

            ["file explorer"] = new(
                "File Explorer",
                "exe",
                "explorer.exe"),

            ["settings"] = new(
                "Windows Settings",
                "uri",
                "ms-settings:")
        };

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["you tube"] = "youtube",
            ["youtube website"] = "youtube",
            ["google website"] = "google",
            ["spotify website"] = "spotify",
            ["explorer"] = "file explorer",
            ["windows explorer"] = "file explorer",
            ["windows settings"] = "settings",
            ["system settings"] = "settings",
            ["calc"] = "calculator"
        };

    private static readonly HashSet<string> BlockedExecutableNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "cmd",
            "powershell",
            "pwsh",
            "wscript",
            "cscript",
            "mshta",
            "rundll32",
            "regsvr32",
            "msiexec"
        };

    private static readonly Regex WhitespacePattern = new(
        @"\s+",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    private static readonly Regex LaunchPattern = new(
        @"^(?:(?:hey|hi|hello|okay|ok)[,\s]+)?" +
        @"(?:axtrazz[,\s:]+)?" +
        @"(?:(?:can|could|would|will)\s+you\s+)?" +
        @"(?:please\s+)?" +
        @"(?:open|launch|start)\s+" +
        @"(?<target>.+)$",
        RegexOptions.IgnoreCase |
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(250));

    public static bool IsLaunchCommand(string text)
    {
        return TryExtractTarget(text, out _);
    }

    public static Target? Resolve(
        string text,
        Dictionary<string, string> custom)
    {
        if (!TryExtractTarget(text, out string requestedName))
        {
            return null;
        }

        // First honor an exact approved name before removing
        // polite suffixes or optional words.
        Target? target = FindApprovedTarget(
            requestedName,
            custom);

        if (target is not null)
        {
            return target;
        }

        string candidate = requestedName;

        // Handles combinations such as:
        // "the calculator app for me please"
        for (int attempt = 0; attempt < 6; attempt++)
        {
            string simplified = RemoveOptionalWords(candidate);

            if (string.Equals(
                simplified,
                candidate,
                StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            candidate = simplified;

            target = FindApprovedTarget(
                candidate,
                custom);

            if (target is not null)
            {
                return target;
            }
        }

        return null;
    }

    private static bool TryExtractTarget(
        string? text,
        out string requestedName)
    {
        requestedName = "";

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Launch commands should be short.
        if (text.Length > 1000)
        {
            return false;
        }

        string normalized = NormalizeName(text);

        Match match = LaunchPattern.Match(normalized);

        if (!match.Success)
        {
            return false;
        }

        requestedName = NormalizeName(
            match.Groups["target"].Value);

        return requestedName.Length > 0;
    }

    private static string NormalizeName(string value)
    {
        string normalized = WhitespacePattern.Replace(
            value.Trim(),
            " ");

        // Remove sentence punctuation at the edges, while
        // preserving punctuation within an application's name.
        normalized = normalized.Trim(
            ' ',
            '.',
            ',',
            '!',
            '?',
            ';',
            ':');

        return normalized;
    }

    private static string RemoveOptionalWords(string value)
    {
        string result = NormalizeName(value);

        if (result.EndsWith(
            " please",
            StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeName(
                result.Substring(
                    0,
                    result.Length - " please".Length));
        }

        if (result.EndsWith(
            " for me",
            StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeName(
                result.Substring(
                    0,
                    result.Length - " for me".Length));
        }

        if (result.EndsWith(
            " application",
            StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeName(
                result.Substring(
                    0,
                    result.Length - " application".Length));
        }

        if (result.EndsWith(
            " app",
            StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeName(
                result.Substring(
                    0,
                    result.Length - " app".Length));
        }

        if (result.StartsWith(
            "the ",
            StringComparison.OrdinalIgnoreCase))
        {
            return NormalizeName(
                result.Substring("the ".Length));
        }

        return result;
    }

    private static Target? FindApprovedTarget(
        string requestedName,
        Dictionary<string, string> custom)
    {
        string key = NormalizeName(requestedName);

        if (BuiltIns.TryGetValue(
            key,
            out Target? builtIn))
        {
            return builtIn;
        }

        // Compare custom names without depending on whether
        // their stored dictionary is case-sensitive.
        foreach (KeyValuePair<string, string> entry in custom)
        {
            if (string.Equals(
                NormalizeName(entry.Key),
                key,
                StringComparison.OrdinalIgnoreCase))
            {
                return new Target(
                    entry.Key,
                    "custom",
                    entry.Value);
            }
        }

        if (Aliases.TryGetValue(
                key,
                out string? canonicalName) &&
            BuiltIns.TryGetValue(
                canonicalName,
                out Target? aliasedTarget))
        {
            return aliasedTarget;
        }

        return null;
    }

    public static string ValidateCustomPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "Select an application's executable file.");
        }

        if (!Path.IsPathFullyQualified(path))
        {
            throw new InvalidOperationException(
                "Use the application's full local file path.");
        }

        string full = Path.GetFullPath(path);

        // Reject UNC/network paths and device-path syntax.
        if (full.StartsWith(
            @"\\",
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Network and device executable paths are not allowed.");
        }

        if (!File.Exists(full) ||
            !Path.GetExtension(full).Equals(
                ".exe",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Select an existing .exe file using its full local path.");
        }

        string executableName =
            Path.GetFileNameWithoutExtension(full);

        if (BlockedExecutableNames.Contains(executableName))
        {
            throw new InvalidOperationException(
                "Command shells and script/installer hosts are not allowed.");
        }

        return full;
    }

    public static async Task OpenAsync(Target target)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

#if WINDOWS
        if (target.Kind == "web")
        {
            bool opened = await Browser.Default.OpenAsync(
                new Uri(target.Value),
                BrowserLaunchMode.External);

            if (!opened)
            {
                throw new InvalidOperationException(
                    "The browser could not open the website.");
            }

            return;
        }

        if (target.Kind == "uri")
        {
            bool opened = await Launcher.Default.OpenAsync(
                new Uri(target.Value));

            if (!opened)
            {
                throw new InvalidOperationException(
                    "Windows could not open that app link.");
            }

            return;
        }

        string executablePath;

        if (target.Kind == "custom")
        {
            executablePath =
                ValidateCustomPath(target.Value);
        }
        else if (target.Kind == "exe")
        {
            string windowsDirectory =
                Environment.GetFolderPath(
                    Environment.SpecialFolder.Windows);

            executablePath =
                target.Value.Equals(
                    "explorer.exe",
                    StringComparison.OrdinalIgnoreCase)
                ? Path.Combine(
                    windowsDirectory,
                    "explorer.exe")
                : Path.Combine(
                    windowsDirectory,
                    "System32",
                    target.Value);

            if (!File.Exists(executablePath))
            {
                throw new FileNotFoundException(
                    "Application not found.",
                    executablePath);
            }
        }
        else
        {
            throw new InvalidOperationException(
                "Unknown launch type.");
        }

        using Process? process = Process.Start(
            new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true
            });
#else
        await Task.CompletedTask;

        throw new PlatformNotSupportedException(
            "App launching is configured for Windows.");
#endif
    }
}