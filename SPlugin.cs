using System;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using System.Collections.Generic;

namespace SPlugin
{
    [ApiVersion(1, 21)]
    public class SPlugin : TerrariaPlugin
    {
        #region PluginProperties

        public override Version Version { get { return new Version("1.0"); } }

        public override string Name { get { return "SPlugin"; } }

        public override string Author { get { return "Switchi"; } }

        #endregion



        private Database DB;

        /* Constructor
         * */
        public SPlugin(Main Game) : base(Game) {}

        /* Initialize
         * 
         * Initialize plugin at start
         * 
         * */
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnGameInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData);
            ServerApi.Hooks.ServerJoin.Register(this, OnServerJoin);
        }

        /* Dispose
         * 
         * Dispose plugin before shutdown
         * 
         * */
        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnGameInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnServerJoin);
            }
            base.Dispose(Disposing);
        }

        /* OnGameInitialize
         * 
         * Called once at start
         * 
         * */
        private void OnGameInitialize(EventArgs Args)
        {
            SetupDB();

            SPluginApi Api = new SPluginApi(TShock.RestApi, DB);
            Api.RegisterRestfulCommands();
        }

        /* SetupDB
         * 
         * Initialize database
         * 
         * */
        private void SetupDB()
        {
            DB = Database.Init();

            // Our tables in db
            SqlTable UserKills = new SqlTable("user_kills_stats",
                new SqlColumn("id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("user_id", MySqlDbType.Text),
                new SqlColumn("mob_id", MySqlDbType.Text),
                new SqlColumn("kills", MySqlDbType.Int32));

            SqlTable UserStats = new SqlTable("user_stats",
                new SqlColumn("id", MySqlDbType.Int32) { Primary = true, AutoIncrement = true },
                new SqlColumn("user_id", MySqlDbType.Text) { Unique = true },
                new SqlColumn("tiles_destroyed", MySqlDbType.Int32),
                new SqlColumn("tiles_placed", MySqlDbType.Int32),
                new SqlColumn("deaths", MySqlDbType.Int32));

            DB.EnsureExists(UserKills, UserStats);
        }

        /* OnServerJoin
         * 
         * Called when player joined to server
         * 
         * */
        private void OnServerJoin(JoinEventArgs args)
        {
        }

        /* OnGetData
         * 
         * Called when packet has been received
         * 
         * */
        private void OnGetData(GetDataEventArgs Args)
        {
            // Reads data from received packet
            using (var PacketDataStream = new MemoryStream(Args.Msg.readBuffer, Args.Index, Args.Length))
            using (var PacketReader = new BinaryReader(PacketDataStream))
            {
                switch (Args.MsgID)
                {
                    case PacketTypes.NpcStrike:
                        {
                            var NpcID = PacketReader.ReadInt16();
                            var Damage = PacketReader.ReadInt16();
                            var Knockback = PacketReader.ReadSingle();
                            var Direction = PacketReader.ReadByte();
                            var IsCritical = PacketReader.ReadBoolean();

                            // Calculates damage, CriticalMultiplier will miltiply damage by 2 if was critical hit
                            int CriticalMultiplier = (IsCritical) ? 2 : 1;
                            int ActualDamage = (Damage - Main.npc[NpcID].defense / 2) * CriticalMultiplier;
                            if (ActualDamage < 0)
                            {
                                ActualDamage = 1;
                            }
                            int PlayerID = Args.Msg.whoAmI;

                            // Gets monster object by id
                            NPC Monster = Main.npc[NpcID];
                            if (Monster != null)
                            {
                                // Checks if monster will die after damage taken and gets information about who is the killer
                                if (ActualDamage >= Monster.life && Monster.life > 0 && Monster.active)
                                {
                                    Player Player = Main.player[PlayerID];
                                    if (Player != null)
                                    {
                                        DB.UpdateKills(Player.name, Monster.name);
                                    }
                                }
                            }
                        }
                        break;

                    case PacketTypes.PlayerKillMe:
                        {
                            var PlayerID = PacketReader.ReadByte();
                            var HitDirection = PacketReader.ReadByte();
                            var Damage = PacketReader.ReadInt16();
                            var PVP = PacketReader.ReadBoolean();
                            var DeathText = PacketReader.ReadString();

                            Player Player = Main.player[PlayerID];
                            DB.UpdateDeaths(Player.name);
                        }
                        break;

                    case PacketTypes.Tile:
                        {
                            var Action = PacketReader.ReadByte();
                            var TileX = PacketReader.ReadInt32();
                            var TileY = PacketReader.ReadInt32();

                            int PlayerID = Args.Msg.whoAmI;
                            Player Player = Main.player[PlayerID];

                            // Destoryed (0) or placed (1)
                            switch (Action)
                            {
                                case 0: DB.UpdateDestroyedTiles(Player.name); break;

                                case 1: DB.UpdatePlacedTiles(Player.name); break;

                                default:
                                    break;
                            }

                        }
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
