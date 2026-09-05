using System;
using System.Security.Cryptography;

namespace KlangHub.Core.Streaming
{
    /// <summary>
    /// A random value made once per run, used to make the addresses this program hands out unguessable.
    /// <para>
    /// The streaming server has to be reachable from the network: that is how a speaker fetches the
    /// audio. What did not have to be reachable was everything else served on the same port. The cover
    /// art in particular went to anyone on the network who asked for <c>/artwork.png</c> - which is a
    /// live answer to "what are they listening to right now", handed out with no question asked, to a
    /// guest on the wireless or anything else that had found its way onto it.
    /// </para>
    /// <para>
    /// A secret in the address is the cheapest fix that keeps the picture working: whoever was given the
    /// URL can fetch it, and nobody else can arrive at it by trying. It is not a login, it does not
    /// survive a restart, and it does not need to - the URL is regenerated and handed to the device
    /// every time something is cast.
    /// </para>
    /// </summary>
    public static class SessionSecret
    {
        /// <summary>
        /// URL-safe, and long enough that guessing is not a strategy: 128 bits, from the system's
        /// cryptographic generator rather than Random, which is seeded predictably enough to matter here.
        /// </summary>
        public static string Value { get; } = Create();

        private static string Create()
        {
            var bytes = RandomNumberGenerator.GetBytes(16);
            return Convert.ToBase64String(bytes)
                          .Replace('+', '-')
                          .Replace('/', '_')
                          .TrimEnd('=');
        }

        /// <summary>
        /// Compares in constant time. The comparison is not the weak point here - an attacker cannot
        /// measure a single local socket read that finely - but a string comparison that returns on the
        /// first wrong character is a habit worth not having in code that checks a secret.
        /// </summary>
        public static bool Matches(string? candidate)
        {
            if (string.IsNullOrEmpty(candidate) || candidate.Length != Value.Length)
                return false;

            var difference = 0;
            for (var i = 0; i < Value.Length; i++)
                difference |= Value[i] ^ candidate[i];

            return difference == 0;
        }
    }
}
