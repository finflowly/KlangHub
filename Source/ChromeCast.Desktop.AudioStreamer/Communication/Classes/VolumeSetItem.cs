using System;

namespace KlangHub.Communication.Classes
{
    public class VolumeSetItem
    {
        public Volume Setting { get; set; }
        public int RequestId { get; set; }
        public DateTime SendAt { get; set; }
    }
}
