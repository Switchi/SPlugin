using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;

namespace SPlugin
{
    [ApiVersion(1, 21)]
    public class SPlugin : TerrariaPlugin
    {
        #region Properties

        public override Version Version { get { return new Version("1.0"); } }

        public override string Name { get { return "SPlugin"; } }

        public override string Author { get { return "Switchi"; } }

        #endregion



        private SqliteConnection DB;

        // Constructor
        public SPlugin(Main game) : base(game) {}

        /* Initialize
         * 
         * Initialize plugin at start
         * 
         * */
        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);

            TShockAPI.GetDataHandlers.TileEdit += OnTileEdit;

            SetupDB();
        }

        /* Dispose
         * 
         * Dispose plugin before shutdown
         * 
         * */
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);

                TShockAPI.GetDataHandlers.TileEdit -= OnTileEdit;
            }
            base.Dispose(disposing);
        }


        /* SetupDB
         * 
         * Initialize database connection
         * 
         * */
        private void SetupDB()
        {
            // We need make path to db file and create connection
            string sql = Path.Combine(TShock.SavePath, "tshock.sqlite");
            DB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));

            // Bind sqlite connection with TShock table creator to make life easier
            SqlTableCreator sqlcreator = new SqlTableCreator(DB,
                (IQueryBuilder)new SqliteQueryCreator());

            // Ensure that our tables in db are exists
            sqlcreator.EnsureTableStructure(new SqlTable("kills_stats",
                new SqlColumn("user_id", MySqlDbType.Text),
                new SqlColumn("mob_id", MySqlDbType.Text),
                new SqlColumn("kills", MySqlDbType.Int32)));

            sqlcreator.EnsureTableStructure(new SqlTable("mines_stats",
                new SqlColumn("user_id", MySqlDbType.Text),
                new SqlColumn("tiles", MySqlDbType.Int32)));
        }

        /* OnTileEdit
         * 
         * Handle our hook when tile has been modified
         * 
         * */
        private void OnTileEdit(object sender, TShockAPI.GetDataHandlers.TileEditEventArgs args)
        {
            try
            {
                // check if tile has destroyed
                if (args.Action == GetDataHandlers.EditAction.KillTile)
                {
                    int TilesNum = 1;

                    using (QueryResult dbreader = DB.QueryReader("SELECT * FROM mines_stats WHERE user_id=@0;", args.Player.Name))
                    {
                        if (dbreader.Read())
                        {
                            TilesNum += dbreader.Get<int>("tiles");
                        }
                    }

                    // determine if use update or insert in database
                    if (TilesNum == 1)
                    {
                        DB.Query("INSERT INTO mines_stats (user_id, tiles) VALUES (@0, @1);", args.Player.Name, TilesNum);
                    }
                    else
                    {
                        DB.Query("UPDATE mines_stats SET tiles=@0 WHERE user_id=@1;", TilesNum, args.Player.Name);
                    }

                    // Every 10 tiles destroyed send on chat information about it
                    if (TilesNum % 10 == 0)
                    {
                        string message = string.Format("{0} has mined {1} blocks!", args.Player.Name, TilesNum);
                        TShockAPI.Utils.Instance.Broadcast(message, 255, 0, 0);
                    }
                }
            }
            catch (Exception ex)
            {
                TShockAPI.Utils.Instance.Broadcast(ex.Message, 255, 0, 0);
                TShockAPI.Utils.Instance.Broadcast("Something went wrong", 255, 0, 0);
            }
        }


        /* OnGetData
         * 
         * Hook which provides packet handling
         * 
         * */
        private void OnGetData(GetDataEventArgs args)
        {
            switch(args.MsgID)
            {
                case PacketTypes.NpcStrike:
                    {
                        // Read data from received packet
                        using (var PacketDataStream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
                        {
                            var PacketReader = new BinaryReader(PacketDataStream);
                            var NpcID = PacketReader.ReadInt16();
                            var Damage = PacketReader.ReadInt16();
                            var Knockback = PacketReader.ReadSingle();
                            var Direction = PacketReader.ReadByte();
                            var IsCritical = PacketReader.ReadBoolean();

                            // Calculate damage, CriticalMultiply will miltiply damage by 2 if was critical hit
                            int CriticalMultiply = (IsCritical) ? 1 : 2;

                            int ActualDamage = (Damage - Main.npc[NpcID].defense / 2) * CriticalMultiply;
                            if (ActualDamage < 0)
                            {
                                ActualDamage = 1;
                            }
                            int PlayerID = args.Msg.whoAmI;

                            // Get monster object by id
                            NPC Monster = Main.npc[NpcID];
                            if (Monster != null)
                            {
                                // Check if monster will die after damage taken and get information about who is the killer
                                if (ActualDamage >= Monster.life && Monster.life > 0 && Monster.active)
                                {
                                    Player player = Main.player[PlayerID];
                                    if (player != null)
                                    {
                                        int KillsNum = 1;

                                        try
                                        {
                                            // Get current monster's type kills count from db
                                            // Update number of kills if record exists or
                                            // Insert new record if do not exists
                                            using (QueryResult dbreader = DB.QueryReader("SELECT * FROM kills_stats WHERE user_id=@0 AND mob_id=@1;", player.name, Monster.name))
                                            {
                                                if (dbreader.Read())
                                                {
                                                    KillsNum += dbreader.Get<int>("kills");
                                                }
                                            }

                                            if (KillsNum == 1)
                                            {
                                                DB.Query("INSERT INTO kills_stats (user_id, mob_id, kills) VALUES (@0, @1, @2);", player.name, Monster.name, KillsNum);
                                            }
                                            else
                                            {
                                                DB.Query("UPDATE kills_stats SET kills=@0 WHERE user_id=@1 AND mob_id=@2;", KillsNum, player.name, Monster.name);
                                            }

                                            // Send same information about kills on chat
                                            string message = string.Format("{0} killed {1} (total: {2})", player.name, Monster.name, KillsNum);
                                            TShockAPI.Utils.Instance.Broadcast(message, 255, 0, 0);
                                        }
                                        catch (Exception ex)
                                        {
                                            TShockAPI.Utils.Instance.Broadcast(ex.Message, 255, 0, 0);
                                            TShockAPI.Utils.Instance.Broadcast("Something went wrong", 255, 0, 0);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                default:
                    break;
            }
        }
    }
}
