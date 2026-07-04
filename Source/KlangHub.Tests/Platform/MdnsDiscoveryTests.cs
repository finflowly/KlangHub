using KlangHub.Platform.Casting.Shared;
using Xunit;

namespace KlangHub.Tests.Platform
{
    public class MdnsDiscoveryTests
    {
        [Fact]
        public void ParseTxt_splits_key_value_pairs_case_insensitively()
        {
            var txt = new[] { "fn=Living Room", "et=1,3", "features=0x445F8A00", "flag", "" };

            var dict = MdnsDiscovery.ParseTxt(txt);

            Assert.Equal("Living Room", dict["fn"]);
            Assert.Equal("1,3", dict["ET"]);              // case-insensitive lookup
            Assert.Equal("0x445F8A00", dict["features"]);
            Assert.Equal("", dict["flag"]);               // bare key -> empty value
            Assert.False(dict.ContainsKey("missing"));
        }

        [Fact]
        public void ParseTxt_handles_null()
        {
            Assert.Empty(MdnsDiscovery.ParseTxt(null));
        }
    }
}
