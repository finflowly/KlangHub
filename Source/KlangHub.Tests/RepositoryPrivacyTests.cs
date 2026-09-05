using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace KlangHub.Tests
{
    /// <summary>
    /// Keeps private details out of a public repository.
    /// <para>
    /// This repository is public: everything committed to it is readable by anyone, for good. A review on
    /// 2026-09-05 found a real name inside the shipped executable's copyright, a user's home directory in
    /// the documentation, a self-chosen room name, a soundbar's actual hardware address in three test
    /// files, and eight screenshots tracked in the repository root showing a local IP address, the
    /// speakers in a house and the audio hardware in a machine. None of it was put there deliberately.
    /// It accumulated, which is exactly why a rule that relies on remembering is not enough.
    /// </para>
    /// <para>
    /// So this runs with every test run, over the files git actually tracks, and it looks for the SHAPE
    /// of private data rather than for particular names - a list of forbidden names in a public file
    /// would publish the very thing it protects. Names belong in the local word list that
    /// tools/check-no-private-data.ps1 reads, which is never committed.
    /// </para>
    /// </summary>
    public class RepositoryPrivacyTests
    {
        private static readonly Regex AnyAddress =
            new(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}\b", RegexOptions.Compiled);

        /// <summary>A Windows home directory names its owner.</summary>
        private static readonly Regex HomeDirectory =
            new(@"[A-Za-z]:\\Users\\(?!<|%|\{)[A-Za-z0-9._-]+", RegexOptions.Compiled);

        private static readonly Regex HardwareAddress =
            new(@"\b([0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}\b", RegexOptions.Compiled);

        /// <summary>
        /// Addresses that are plainly invented and are here on purpose: tests need something MAC-shaped.
        /// Anything NOT on this list counts as real hardware in somebody's home - which is how a
        /// soundbar's actual address was found sitting in three test files.
        /// </summary>
        private static readonly HashSet<string> InventedHardwareAddresses = new(StringComparer.OrdinalIgnoreCase)
        {
            "00:00:00:00:00:00",   // what a device sends when it will not say
            "00:11:22:33:44:55",
            "A4:B1:C2:D3:E4:F5",
            "AA:BB:CC:DD:EE:FF",
        };

        /// <summary>
        /// File types that carry more than they show. A screenshot of a settings page has an IP address
        /// and a device list in it; a PDF carries whatever its author's tooling wrote into its metadata;
        /// a device log has the address of every speaker in a house and what was played on them.
        /// </summary>
        private static readonly string[] ForbiddenExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".pdf", ".log" };

        [Fact]
        public void No_tracked_file_carries_somebody_s_email_address()
        {
            var offenders = new List<string>();

            foreach (var (path, full) in TextFiles())
            {
                if (IsExempt(path))
                    continue;

                foreach (Match m in AnyAddress.Matches(File.ReadAllText(full)))
                {
                    if (!IsHarmlessAddress(m.Value))
                        offenders.Add($"{path}: {m.Value}");
                }
            }

            Assert.True(offenders.Count == 0,
                "An address that belongs to a person must not be committed to a public repository:\n  "
                + string.Join("\n  ", offenders.Take(20)));
        }

        [Fact]
        public void No_tracked_file_names_somebody_s_home_directory()
        {
            var offenders = new List<string>();

            foreach (var (path, full) in TextFiles())
            {
                if (IsExempt(path))
                    continue;

                foreach (Match m in HomeDirectory.Matches(File.ReadAllText(full)))
                    offenders.Add($"{path}: {m.Value}");
            }

            // %USERPROFILE% and $env:USERPROFILE say the same thing without naming the owner.
            Assert.True(offenders.Count == 0,
                "A home directory names its owner - write %USERPROFILE% instead:\n  "
                + string.Join("\n  ", offenders.Take(20)));
        }

        [Fact]
        public void No_tracked_file_carries_a_real_hardware_address()
        {
            var offenders = new List<string>();

            foreach (var (path, full) in TextFiles())
            {
                if (IsExempt(path))
                    continue;

                foreach (Match m in HardwareAddress.Matches(File.ReadAllText(full)))
                {
                    if (!InventedHardwareAddresses.Contains(m.Value))
                        offenders.Add($"{path}: {m.Value}");
                }
            }

            Assert.True(offenders.Count == 0,
                "A MAC address identifies a particular piece of hardware in somebody's home. Use one of "
                + "the invented ones listed in this test, or add yours there if it is plainly made up:\n  "
                + string.Join("\n  ", offenders.Take(20)));
        }

        [Fact]
        public void No_screenshots_logs_or_pdfs_are_tracked_outside_the_application_itself()
        {
            var offenders = Tracked()
                .Where(p => ForbiddenExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
                .Where(p => !IsShippedAsset(p))
                .ToList();

            Assert.True(offenders.Count == 0,
                "Screenshots, logs and PDFs carry things nobody chose to publish - an IP address, a list "
                + "of speakers, a name in a window title. .gitignore excludes them; these slipped past:\n  "
                + string.Join("\n  ", offenders.Take(20)));
        }

        // ---------------------------------------------------------------- what counts as harmless

        /// <summary>
        /// True when a match is not somebody's mailbox: deliberately unreal, or not an address at all.
        /// Inno Setup writes external function imports as "SetWindowTheme@uxtheme.dll", which has exactly
        /// the shape of one.
        /// </summary>
        private static bool IsHarmlessAddress(string candidate)
        {
            var at = candidate.LastIndexOf('@');
            if (at <= 0 || at == candidate.Length - 1)
                return true;

            var local = candidate[..at].ToLowerInvariant();
            var domain = candidate[(at + 1)..].ToLowerInvariant();

            // A native library, not a mailbox.
            if (domain.EndsWith(".dll") || domain.EndsWith(".exe") ||
                domain.EndsWith(".so") || domain.EndsWith(".dylib"))
                return true;

            if (local.Contains("noreply") || local.Contains("no-reply"))
                return true;

            // The domains RFC 2606 reserves so documentation can show an address without naming anybody,
            // plus GitHub's own no-reply domain.
            return domain is "example.com" or "example.org" or "example.net" or "example.invalid"
                or "invalid" or "localhost" or "users.noreply.github.com";
        }

        /// <summary>Files whose whole purpose is to describe these rules.</summary>
        private static bool IsExempt(string relativePath)
        {
            var p = relativePath.Replace('\\', '/');
            return p.EndsWith("RepositoryPrivacyTests.cs", StringComparison.OrdinalIgnoreCase)
                || p.EndsWith("tools/check-no-private-data.ps1", StringComparison.OrdinalIgnoreCase)
                || p.EndsWith(".gitignore", StringComparison.OrdinalIgnoreCase)
                || p.Equals("CLAUDE.md", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Images the application itself ships. Drawn by us, not captured from somebody's screen.
        /// <para>
        /// A list, not a set of directory prefixes. This used to exempt the whole of Source/ and the
        /// whole of installer/, which meant a screenshot saved one directory below the eight that were
        /// found in the root - Source/KlangHub/Resources/screenshot.png, say - would have passed every
        /// layer without a word. The point of the check is the file, not the folder it is in.
        /// </para>
        /// </summary>
        private static bool IsShippedAsset(string relativePath)
        {
            var p = relativePath.Replace('\\', '/');
            return ShippedAssets.Any(rx => rx.IsMatch(p));
        }

        private static readonly Regex[] ShippedAssets =
        {
            new Regex(@"^Source/KlangHub/KlangHub\.ico$", RegexOptions.IgnoreCase),
            new Regex(@"^Source/KlangHub/Resources/artwork\.png$", RegexOptions.IgnoreCase),
            new Regex(@"^Source/KlangHub/UserControls/[A-Za-z]+\.png$", RegexOptions.IgnoreCase),
            new Regex(@"^installer/wizard-(small|large)(-\d+)?\.bmp$", RegexOptions.IgnoreCase),
            new Regex(@"^receiver/assets/[a-z]+\.png$", RegexOptions.IgnoreCase),
        };

        /// <summary>
        /// Who the history says wrote it.
        /// <para>
        /// Nothing had ever looked at this. The name and mail address on a commit are as public as
        /// anything in the files, they travel with every clone, and GitHub prints them on every page -
        /// and that is exactly how commits carrying a real name and a private address came to be pushed
        /// without a single check objecting. The files were clean the whole time.
        /// </para>
        /// <para>
        /// Local branches and tags only: a remote-tracking ref is somebody else's history, not something
        /// this repository is about to publish.
        /// </para>
        /// </summary>
        [Fact]
        public void No_commit_is_signed_with_a_real_name_or_address()
        {
            var offenders = new List<string>();

            foreach (var identity in Git("log", "--branches", "--tags", "--format=%an <%ae>%n%cn <%ce>"))
            {
                if (identity.Length == 0 || IsAllowedIdentity(identity))
                    continue;

                // The finding names the shape, never the address: this output ends up in a log.
                offenders.Add(Regex.Replace(identity, @"[A-Za-z0-9._%+-]+@", "***@"));
            }

            Assert.True(offenders.Count == 0,
                "These commit identities are neither a noreply address nor the upstream author this fork "
                + "must keep attributing:" + Environment.NewLine
                + string.Join(Environment.NewLine, offenders.Distinct()));
        }

        /// <summary>
        /// Whoever signed a commit before this became a fork keeps their own identity - the licence asks
        /// for the attribution, and it was already public in the project we forked from. Everything since
        /// must be a noreply address.
        /// </summary>
        private static bool IsAllowedIdentity(string identity)
            => identity.EndsWith("@users.noreply.github.com>", StringComparison.OrdinalIgnoreCase)
               || UpstreamAuthors.Contains(identity);

        private static readonly string[] UpstreamAuthors =
        {
            "SamDel <github@deaut.nl>",
            "SamDel <25846417+SamDel@users.noreply.github.com>",
        };

        // ---------------------------------------------------------------- helpers

        /// <summary>
        /// Runs a git command in the repository and returns its lines. The arguments are passed one by
        /// one rather than as a single string: a --format carries spaces, and a single string would be
        /// split on them into arguments git has never heard of.
        /// </summary>
        private static IReadOnlyList<string> Git(params string[] arguments)
        {
            var start = new ProcessStartInfo("git")
            {
                WorkingDirectory = RepositoryRoot(),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start);
            Assert.NotNull(process);
            var output = process!.StandardOutput.ReadToEnd();

            // The result of the wait is checked. It was not, so a git that hung for thirty seconds gave
            // this test a short file list and a green tick - failing silently at the one moment it was
            // supposed to be protecting something.
            var command = string.Join(' ', arguments);
            Assert.True(process.WaitForExit(30_000), $"git {command} did not finish within 30 seconds.");
            Assert.True(process.ExitCode == 0, $"git {command} failed with exit code {process.ExitCode}.");

            return output.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
        }

        private static IEnumerable<(string Relative, string Full)> TextFiles()
        {
            var root = RepositoryRoot();
            foreach (var relative in Tracked())
            {
                var extension = Path.GetExtension(relative).ToLowerInvariant();
                if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".pdf" or ".ico"
                    or ".bin" or ".dll" or ".exe" or ".zip" or ".snk")
                    continue;

                var full = Path.Combine(root, relative);
                if (!File.Exists(full))
                    continue;   // deleted from disc but still in the index

                if (new FileInfo(full).Length > 2 * 1024 * 1024)
                    continue;   // not a source file; reading it would only slow the suite down

                yield return (relative, full);
            }
        }

        /// <summary>
        /// What git actually tracks. Deliberately not a directory walk: an ignored file is not a problem,
        /// and walking would flag every screenshot sitting in the working folder, which is allowed.
        /// </summary>
        private static IReadOnlyList<string> Tracked() => Git("ls-files");

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
                directory = directory.Parent;

            Assert.True(directory != null, "Could not find the repository root from " + AppContext.BaseDirectory);
            return directory!.FullName;
        }
    }
}
