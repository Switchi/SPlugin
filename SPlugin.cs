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
        public override Version Version { get { return new Version("1.0"); } }

        public override string Name { get { return "SPlugin"; } }

        public override string Author { get { return "Switchi"; } }

        private SqliteConnection DB;


        public SPlugin(Main game) : base(game) {}

        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);

            TShockAPI.GetDataHandlers.TileEdit += OnTileEdit;

            SetupDB();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);

                TShockAPI.GetDataHandlers.TileEdit -= OnTileEdit;
            }
            base.Dispose(disposing);
        }



        private void SetupDB()
        {
            //connection
            string sql = Path.Combine(TShock.SavePath, "tshock.sqlite");
            DB = new SqliteConnection(string.Format("uri=file://{0},Version=3", sql));

            //creator
            SqlTableCreator sqlcreator = new SqlTableCreator(DB,
                (IQueryBuilder)new SqliteQueryCreator());

            sqlcreator.EnsureTableStructure(new SqlTable("kills_stats",
                new SqlColumn("user_id", MySqlDbType.Text),
                new SqlColumn("mob_id", MySqlDbType.Text),
                new SqlColumn("kills", MySqlDbType.Int32)));

            sqlcreator.EnsureTableStructure(new SqlTable("mines_stats",
                new SqlColumn("user_id", MySqlDbType.Text),
                new SqlColumn("tiles", MySqlDbType.Int32)));
        }

        private void OnTileEdit(object sender, TShockAPI.GetDataHandlers.TileEditEventArgs args)
        {
            try
            {
                if (args.Action == GetDataHandlers.EditAction.KillTile)
                {
                    int tiles = 1;

                    TShockAPI.Utils.Instance.Broadcast("Tile has gone", 255, 0, 0);
                    using (QueryResult dbreader = DB.QueryReader("SELECT * FROM mines_stats WHERE user_id=@0;", args.Player.Name))
                    {
                        if (dbreader.Read())
                        {
                            tiles += dbreader.Get<int>("tiles");
                        }
                    }

                    if (tiles == 1)
                    {
                        DB.Query("INSERT INTO mines_stats (user_id, tiles) VALUES (@0, @1);", args.Player.Name, tiles);
                    }
                    else
                    {
                        DB.Query("UPDATE mines_stats SET tiles=@0 WHERE user_id=@1;", tiles, args.Player.Name);
                    }

                    if (tiles % 10 == 0)
                    {
                        string message = string.Format("{0} has mined {1} blocks!", args.Player.Name, tiles);
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

        private void OnGetData(GetDataEventArgs args)
        {
            switch(args.MsgID)
            {
                case PacketTypes.NpcStrike:
                    {
                        using (var data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length))
                        {
                            var reader = new BinaryReader(data);
                            var npcid = reader.ReadInt16();
                            var dmg = reader.ReadInt16();
                            var knockback = reader.ReadSingle();
                            var direction = reader.ReadByte();
                            var crit = reader.ReadBoolean();
                            int critmultiply = 1;
                            if (crit)
                            {
                                critmultiply = 2;
                            }
                            int actualdmg = (dmg - Main.npc[npcid].defense / 2) * critmultiply;
                            if (actualdmg < 0)
                            {
                                actualdmg = 1;
                            }
                            int playerid = args.Msg.whoAmI;

                            NPC monster = Main.npc[npcid];
                            if (monster != null)
                            {
                                if (actualdmg >= monster.life && monster.life > 0 && monster.active)
                                {
                                    Player player = Main.player[playerid];
                                    if (player != null)
                                    {
                                        int kills = 1;

                                        try
                                        {
                                            using (QueryResult dbreader = DB.QueryReader("SELECT * FROM kills_stats WHERE user_id=@0 AND mob_id=@1;", player.name, monster.name))
                                            {
                                                if (dbreader.Read())
                                                {
                                                    kills += dbreader.Get<int>("kills");
                                                }
                                            }

                                            if (kills == 1)
                                            {
                                                DB.Query("INSERT INTO kills_stats (user_id, mob_id, kills) VALUES (@0, @1, @2);", player.name, monster.name, kills);
                                            }
                                            else
                                            {
                                                DB.Query("UPDATE kills_stats SET kills=@0 WHERE user_id=@1 AND mob_id=@2;", kills, player.name, monster.name);
                                            }

                                            string message = string.Format("{0} killed {1} (total: {2})", player.name, monster.name, kills);
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

                case PacketTypes.TileKill:
                    break;

                default:
                    break;
            }
        }
    }
}
