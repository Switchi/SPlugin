using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace SPlugin
{
    public class Database
    {
        private readonly IDbConnection DB;

        private List<PlayerStats> PlayersStats = new List<PlayerStats>();

        /* Constructor
         * */
        private Database(IDbConnection DB)
        {
            this.DB = DB;
        }

        /* Init
         * 
         * Creates a sqlite database object
         * 
         * */
        public static Database Init()
        {
            // We need make path to db file and create connection
            string SqlPath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            var Connection = new SqliteConnection(string.Format("uri=file://{0},Version=3", SqlPath));

            var Database = new Database(Connection);
            //Database.InitPlayersList();
            return Database;
        }

        /* InitPlayersList
         * 
         * Initializes once players list for minimalize queries
         * 
         * Testing one thing
         * 
         * */
        internal void InitPlayersList()
        {
            using (var dbreader = QueryReader("SELECT * FROM user_stats"))
            {
                while (dbreader.Read())
                {
                    var Name = dbreader.Get<string>("user_id");
                    var TilesPlaced = dbreader.Get<int>("tiles_placed");
                    var TilesDestroyed = dbreader.Get<int>("tiles_destroyed");
                    var Deaths = dbreader.Get<int>("deaths");

                    var PlayerStats = new PlayerStats(Name, TilesPlaced, TilesDestroyed, Deaths);
                    PlayersStats.Add(PlayerStats);
                }
            }
            using (var dbreader = QueryReader("SELECT * FROM user_kills_stats"))
            {
                while (dbreader.Read())
                {
                    var PlayerName = dbreader.Get<string>("user_id");
                    var MobName = dbreader.Get<string>("mob_id");
                    var Kills = dbreader.Get<int>("kills");

                    PlayerStats PlayerStats = PlayersStats.Find(p => p.Name == PlayerName);
                    if (PlayerStats != null)
                    {
                        PlayerStats.Kills[MobName] = Kills;
                    }
                    else
                    {
                        PlayerStats = new PlayerStats(PlayerName, 0, 0, 0);
                        PlayersStats.Add(PlayerStats);
                    }
                }
            }
        }

        /* EnsureExists
         * 
         * Checks if tables exists and creates them if not
         * 
         * */
        public void EnsureExists(params SqlTable[] Tables)
        {
            // We want to use TShock creator for checking
            SqlTableCreator SqlCreator = new SqlTableCreator(DB,
                (IQueryBuilder)new SqliteQueryCreator());

            foreach (var Table in Tables)
            {
                SqlCreator.EnsureTableStructure(Table);
            }
        }

        /* QueryReader
         * 
         * Executes query and returns query result
         * 
         * */
        internal QueryResult QueryReader(string Query, params object[] Args)
        {
            return DB.QueryReader(Query, Args);
        }

        /* Query
         * 
         * Exetuces query and returns scalar 
         * 
         * */
        internal int Query(string Query, params object[] Args)
        {
            return DB.Query(Query, Args);
        }

        /* IncreaseKills
         * 
         * Increase kills counter in db for specific player and monster
         * todo: use int as id instead string
         * 
         * */
        internal void UpdateKills(string PlayerName, string MonsterName)
        {
            // Gets specific kills count
            bool IsExists = false;
            int KillsNum = 1;
            using (var dbreader = QueryReader("SELECT * FROM user_kills_stats WHERE user_id=@0 AND mob_id=@1;",
                PlayerName, MonsterName))
            {
                if (dbreader.Read())
                {
                    KillsNum += dbreader.Get<int>("kills");
                    IsExists = true;
                }
            }

            if (IsExists)
            {
                Query("UPDATE user_kills_stats SET kills=kills+1 WHERE user_id=@0 AND mob_id=@1;",
                    PlayerName, MonsterName);
            }
            else
            {
                Query("INSERT INTO user_kills_stats (user_id, mob_id, kills) VALUES (@0, @1, @2);",
                    PlayerName, MonsterName, KillsNum);
            }

            // Sends message about kills count
            if (KillsNum % 50 == 0)
            {
                TShockAPI.Utils.Instance.Broadcast(PlayerName + " kill " + MonsterName + " " + KillsNum + " times!", new Color(255, 0, 0));
            }
        }

        /* UpdateDestroyedTiles
         * 
         * Increase destroyed tiles counter for specific player
         * 
         * */
        internal void UpdateDestroyedTiles(string PlayerName)
        {
            // Gets destoryed tiles count by specific player
            bool IsExists = false;
            int TilesNum = 1;
            using (var dbreader = QueryReader("SELECT * FROM user_stats WHERE user_id=@0;", PlayerName))
            {
                if (dbreader.Read())
                {
                    TilesNum += dbreader.Get<int>("tiles_destroyed");
                    IsExists = true;
                }
            }

            if (IsExists)
            {
                Query("UPDATE user_stats SET tiles_destroyed=tiles_destroyed+1 WHERE user_id=@0;", PlayerName);
            }
            else
            {
                Query("INSERT INTO user_stats (user_id, tiles_destroyed, tiles_placed, deaths) VALUES (@0, 1, 0, 0);", PlayerName);
            }

            // Sends message about destroyed tiles count
            if (TilesNum % 1000 == 0)
            {
                TShockAPI.Utils.Instance.Broadcast(PlayerName + " destroyed " + TilesNum + " tiles", new Color(255, 0, 0));
            }
        }

        /* UpdateDestroyedTiles
         * 
         * Increase placed tiles counter for specific player
         * 
         * */
        internal void UpdatePlacedTiles(string PlayerName)
        {
            // Gets placed tiles count by specific player
            bool IsExists = false;
            int TilesNum = 1;
            using (var dbreader = QueryReader("SELECT * FROM user_stats WHERE user_id=@0;", PlayerName))
            {
                if (dbreader.Read())
                {
                    TilesNum += dbreader.Get<int>("tiles_placed");
                    IsExists = true;
                }
            }

            if (IsExists)
            {
                Query("UPDATE user_stats SET tiles_placed=tiles_placed+1 WHERE user_id=@0;", PlayerName);
            }
            else
            {
                Query("INSERT INTO user_stats (user_id, tiles_destroyed, tiles_placed, deaths) VALUES (@0, 0, 1, 0);", PlayerName);
            }

            // Sends message about placed tiles count
            if (TilesNum % 1000 == 0)
            {
                TShockAPI.Utils.Instance.Broadcast(PlayerName + " placed " + TilesNum + " tiles", new Color(255, 0, 0));
            }
        }

        /* UpdateDestroyedTiles
         * 
         * Increase deaths counter for specific player
         * 
         * */
        internal void UpdateDeaths(string PlayerName)
        {
            // Gets specific player deaths
            bool IsExists = false;
            int DeathsNum = 1;
            using (var dbreader = QueryReader("SELECT * FROM user_stats WHERE user_id=@0;", PlayerName))
            {
                if (dbreader.Read())
                {
                    DeathsNum += dbreader.Get<int>("deaths");
                    IsExists = true;
                }
            }

            if (IsExists)
            {
                Query("UPDATE user_stats SET deaths=deaths+1 WHERE user_id=@0;", PlayerName);
            }
            else
            {
                Query("INSERT INTO user_stats (user_id, tiles_destroyed, tiles_placed, deaths) VALUES (@0, 0, 0, 1);", PlayerName);
            }

            // Sends message about deaths count
            if (DeathsNum % 10 == 0)
            {
                TShockAPI.Utils.Instance.Broadcast(PlayerName + " died " + DeathsNum + " times!", new Color(255, 0, 0));
            }
        }

        /* GetKills
         * 
         * Returns all kills of specific player from db
         * 
         * */
        internal Dictionary<string, int> GetKills(string PlayerName)
        {
            var Result = new Dictionary<string, int>();
            using (var dbreader = QueryReader("SELECT * FROM user_kills_stats WHERE user_id=@0;", PlayerName))
            {
                while (dbreader.Read())
                {
                    var MobID = dbreader.Get<string>("mob_id");
                    Result[MobID] = dbreader.Get<int>("kills");
                }
            }
            return Result;
        }

        /* GetAllKills
         * 
         * Returns all kills of each player from db
         * 
         * */
        internal Dictionary<string, Dictionary<string, int>> GetAllKills()
        {
            var Result = new Dictionary<string, Dictionary<string, int>>();
            using (var dbreader = QueryReader("SELECT * FROM user_kills_stats;"))
            {
                while (dbreader.Read())
                {
                    var UserID = dbreader.Get<string>("user_id");
                    var MobID = dbreader.Get<string>("mob_id");
                    var Kills = dbreader.Get<int>("kills");

                    if (!Result.ContainsKey(UserID))
                    {
                        Result[UserID] = new Dictionary<string, int>();
                    }
                    Result[UserID][MobID] = Kills;
                }
            }
            return Result;
        }

        /* GetStats
         * 
         * Returns stats of specific player from db
         * 
         * */
        internal int[] GetStats(string PlayerName)
        {
            int[] Result = null;
            using (var dbreader = QueryReader("SELECT * FROM user_stats WHERE user_id=@0;", PlayerName))
            {
                if (dbreader.Read())
                {
                    var TilesDestroyed = dbreader.Get<int>("tiles_destroyed");
                    var TilesPlaced = dbreader.Get<int>("tiles_placed");
                    var Deaths = dbreader.Get<int>("deaths");

                    Result = new int[] {
                            TilesDestroyed,
                            TilesPlaced,
                            Deaths
                    };
                }
            }
            return Result;
        }

        /* GetAllStats
         * 
         * Returns all stats of each player from db
         * 
         * */
        internal Dictionary<string, int[]> GetAllStats()
        {
            var Result = new Dictionary<string, int[]>();
            using (var dbreader = QueryReader("SELECT * FROM user_stats;"))
            {
                while (dbreader.Read())
                {
                    var UserID = dbreader.Get<string>("user_id");
                    var TilesDestroyed = dbreader.Get<int>("tiles_destroyed");
                    var TilesPlaced = dbreader.Get<int>("tiles_placed");
                    var Deaths = dbreader.Get<int>("deaths");

                    if (!Result.ContainsKey(UserID))
                    {
                        Result[UserID] = new int[] {
                            TilesDestroyed,
                            TilesPlaced,
                            Deaths
                        };
                    }
                }
            }
            return Result;
        }
    }
}
