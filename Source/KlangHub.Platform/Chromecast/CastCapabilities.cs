using System;

namespace KlangHub.Application
{
    /// <summary>What a device says it can do, from the "ca" bitmask in its mDNS TXT record.</summary>
    [Flags]
    public enum CastCapability
    {
        None = 0,
        VideoOut = 1 << 0,
        VideoIn = 1 << 1,
        AudioOut = 1 << 2,
        AudioIn = 1 << 3,
        DeveloperMode = 1 << 4,
        MultizoneGroup = 1 << 5,
    }

    /// <summary>
    /// Reads the "ca" field. Only the low six bits are described by Google; devices set higher ones too and
    /// their meaning is not published, so those are kept as a raw number instead of being guessed at.
    ///
    /// Measured here (2026-09-04): television 264709 = video out + audio out (+ bits 9, 11, 18);
    /// Google Home and Enchant 198660 = audio out only (+ bits 11, 16, 17); soundbar 199172 = the same plus
    /// bit 9, which the television also sets. Video out is therefore the one reliable way to tell a screen
    /// from a speaker without asking Google.
    /// </summary>
    public static class CastCapabilities
    {
        private const int NamedMask = 0b11_1111;

        public static CastCapability Of(string? ca)
            => (CastCapability)(Raw(ca) & NamedMask);

        public static int Raw(string? ca)
            => int.TryParse(ca, out var value) && value > 0 ? value : 0;

        /// <summary>The bits this build cannot name - shown as a number so the display stays honest about
        /// what is known and what is merely observed.</summary>
        public static int UndocumentedBits(string? ca)
            => Raw(ca) & ~NamedMask;

        public static bool Has(string? ca, CastCapability capability)
            => (Of(ca) & capability) == capability && capability != CastCapability.None;
    }
}
