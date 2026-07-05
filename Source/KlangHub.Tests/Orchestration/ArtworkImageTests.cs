using System.Linq;
using KlangHub.Classes;
using Xunit;

namespace KlangHub.Tests.Orchestration
{
    public class ArtworkImageTests
    {
        [Fact]
        public void Bytes_loads_the_embedded_artwork_as_a_valid_png()
        {
            var bytes = ArtworkImage.Bytes;

            Assert.True(bytes.Length > 1000, "expected a real embedded PNG");
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4).ToArray()); // PNG magic
        }
    }
}
