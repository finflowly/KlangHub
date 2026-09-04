using System;
using System.Collections.Generic;
using System.Linq;

namespace KlangHub.Classes
{
    /// <summary>
    /// The arithmetic behind the room fader, kept apart from the control that draws it so it can be reasoned
    /// about - and tested - without touching a speaker.
    /// <para>
    /// A room is a mix, not a level: the TV at 13 %, the soundbar at 11 %, the speaker at 20 %. Moving the
    /// room must scale that mix by one factor (26/22/40 at double), never flatten it to a single number.
    /// Two cases have no ratio to preserve and are decided explicitly: a silent room, where everyone simply
    /// goes to the target, and a single silent speaker inside a playing room, which would otherwise stay at
    /// zero forever and is lifted to the room's new level instead.
    /// </para>
    /// </summary>
    public static class RoomVolume
    {
        /// <summary>The level a room reads at: the average of its speakers.</summary>
        public static int LevelOf(IReadOnlyList<int> volumes) =>
            volumes.Count == 0 ? 0 : (int)Math.Round(volumes.Average());

        /// <summary>
        /// Scales <paramref name="volumes"/> so the room lands on <paramref name="target"/> percent, with each
        /// speaker kept inside its own hard cap from <paramref name="caps"/>. A capped speaker simply stops
        /// where it is told to stop - the protection wins over the ratio, which is the whole point of the cap.
        /// </summary>
        public static int[] Scale(IReadOnlyList<int> volumes, IReadOnlyList<int> caps, int target)
        {
            if (volumes.Count == 0)
                return Array.Empty<int>();
            if (caps.Count != volumes.Count)
                throw new ArgumentException("one cap per speaker is required", nameof(caps));

            target = Math.Clamp(target, 0, 100);
            int current = LevelOf(volumes);
            var result = new int[volumes.Count];

            for (int i = 0; i < volumes.Count; i++)
            {
                int wanted;
                if (target == 0)
                    wanted = 0;
                else if (current <= 0 || volumes[i] <= 0)
                    wanted = target;                       // nothing to scale from: join the room
                else
                    wanted = (int)Math.Round(volumes[i] * (target / (float)current));

                result[i] = Math.Clamp(wanted, 0, Math.Clamp(caps[i], 0, 100));
            }

            return result;
        }
    }
}
