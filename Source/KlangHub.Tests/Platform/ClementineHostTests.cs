using KlangHub.Platform.Clementine;
using Xunit;

namespace KlangHub.Tests.Platform
{
    /// <summary>
    /// Which address KlangHub uses to reach Clementine on this machine, and why the obvious one is wrong.
    /// <para>
    /// Measured on 2026-09-05. Clementine's network remote refuses connections that do not come from a
    /// private network, and its own check (NetworkRemote::IpIsPrivate) lists 127.0.0.0/8 and ::1/128 - but
    /// <b>not</b> ::ffff:127.0.0.1, which is what IPv4 localhost looks like when it arrives on the IPv6
    /// dual-stack socket. On Windows that is exactly the socket doing the listening (the server binds
    /// :::5500), so a connection to 127.0.0.1 is accepted at the TCP level, judged "a connection from
    /// public ip", and dropped without a word. The symptom is a socket that opens and then goes silent -
    /// no error, no disconnect message, nothing in any log.
    /// </para>
    /// </summary>
    public class ClementineHostTests
    {
        [Fact]
        public void Tries_ipv6_localhost_first()
        {
            // Not cosmetic ordering: 127.0.0.1 first means every connection is silently rejected on a
            // dual-stack machine, and the television stays blank with nothing to explain why.
            Assert.Equal("::1", ClementineRemoteSource.Hosts[0]);
        }

        [Fact]
        public void Still_tries_ipv4_localhost()
        {
            // A machine with IPv6 switched off entirely still has to work.
            Assert.Contains("127.0.0.1", ClementineRemoteSource.Hosts);
        }

        [Fact]
        public void Uses_clementines_own_default_port()
        {
            Assert.Equal(5500, ClementineRemoteSource.Port);
        }
    }
}
