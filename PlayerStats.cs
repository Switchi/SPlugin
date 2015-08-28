using System;
using System.Collections.Generic;

namespace SPlugin
{
    public class PlayerStats
    {
        public string Name { get; set; }
        public int TilesPlayed { get; set; }
        public int TilesDestroyed { get; set; }
        public int Deaths { get; set; }
        public Dictionary<string, int> Kills { get; set; }

        public PlayerStats(string Name, int TilesPlaced, int TilesDestroyed, int Deaths)
        {
            this.Name = Name;
            this.TilesPlayed = TilesPlaced;
            this.TilesDestroyed = TilesDestroyed;
            this.Deaths = Deaths;
            Kills = new Dictionary<string, int>();
        }
    }
}
