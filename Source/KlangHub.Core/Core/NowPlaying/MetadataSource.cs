namespace KlangHub.Core.NowPlaying
{
    /// <summary>
    /// Where a piece of metadata came from, ordered by how much it deserves to be believed. The order is
    /// the whole point: it is what lets a later, weaker answer lose to an earlier, stronger one.
    /// </summary>
    public enum MetadataSource
    {
        /// <summary>Nothing has been contributed yet.</summary>
        None = 0,

        /// <summary>Guessed from "Artist - Title.mp3" and the folder around it. A last resort.</summary>
        FileName = 1,

        /// <summary>
        /// What a player writes in its own title bar. A guess, but a live one - it changes with the music.
        /// For a player that tells Windows nothing at all (Clementine, measured 2026-09-05) this is the
        /// only source that needs no setup whatsoever, which is why it outranks a bare file name.
        /// </summary>
        WindowTitle = 2,

        /// <summary>A now-playing text file the user set up. Deliberately filled in, but it can go stale.</summary>
        NowPlayingFile = 3,

        /// <summary>Windows' own now-playing session. Live, but players report to it carelessly.</summary>
        SystemMediaControls = 4,

        /// <summary>
        /// A player's own remote-control protocol - Clementine's network remote being the one KlangHub
        /// speaks. The richest live source there is: album, length, position, the file on disc and the
        /// cover as bytes, straight out of the player's own library rather than guessed from anything.
        /// </summary>
        PlayerRemote = 5,

        /// <summary>
        /// The tags of the file itself. The most honest source there is - it is what is actually written on
        /// the disc, not what some player decided to say about it.
        /// </summary>
        FileTags = 6
    }
}
