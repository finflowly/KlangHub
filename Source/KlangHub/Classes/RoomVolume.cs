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
    /// goes to the target, and a single silent speaker inside a room being turned UP, which would otherwise
    /// stay at zero forever and is lifted to the room's new level instead.
    /// </para>
    /// </summary>
    public static class RoomVolume
    {
        /// <summary>The level a room reads at: the average of its speakers.</summary>
        public static int LevelOf(IReadOnlyList<int> volumes) =>
            volumes.Count == 0 ? 0 : (int)Math.Round(volumes.Average());

        /// <summary>
        /// Scales <paramref name="volumes"/> towards <paramref name="target"/> percent, keeping the mix.
        ///
        /// The two ceilings are treated differently on purpose.
        ///
        /// A per-speaker CAP is a decision the user made about that speaker: it stops there and the rest of
        /// the room carries on past it. That is what the cap is for.
        ///
        /// The absolute 100 % is not a decision, it is the end of the scale - and a mix that runs into it
        /// is no longer a mix. A room of 5/5/80 asked to go to 40 % used to scale by 1.33, want 106 for the
        /// loud one and clamp it to 100: one speaker at FULL VOLUME in answer to a request for forty
        /// percent. So the factor is limited to what the loudest speaker can still take, everyone moves by
        /// that same smaller factor, and the room lands short of the target rather than a speaker landing
        /// far past it. Turning down is never limited - only the ceiling needs protecting.
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

            if (target == 0)
                return result;   // all zero

            bool louder = target > current;
            float factor = current <= 0 ? 0f : target / (float)current;

            // Limited by the end of the scale, not by the user's caps: a capped speaker stops on its own
            // without holding the room back.
            if (louder && current > 0)
                for (int i = 0; i < volumes.Count; i++)
                    if (volumes[i] > 0)
                        factor = Math.Min(factor, 100f / volumes[i]);

            for (int i = 0; i < volumes.Count; i++)
            {
                int wanted;
                if (volumes[i] <= 0)
                    // A deliberately silenced speaker joins the room only when the room is being turned UP.
                    // Turning a room DOWN used to switch it on: room 20/0, level 10, one press of "−" and
                    // the silent speaker started playing at 6 %.
                    wanted = louder ? target : 0;
                else if (current <= 0)
                    wanted = target;                       // nothing to scale from: join the room
                else
                    wanted = (int)Math.Round(volumes[i] * factor);

                result[i] = Math.Clamp(wanted, 0, Cap(caps, i));
            }

            return result;
        }

        private static int Cap(IReadOnlyList<int> caps, int i) => Math.Clamp(caps[i], 0, 100);
    }
}
