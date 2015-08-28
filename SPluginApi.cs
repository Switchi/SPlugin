using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.IO;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;
using Mono.Data.Sqlite;
using MySql.Data.MySqlClient;
using Rests;

namespace SPlugin
{
    public class SPluginApi
    {
        private Rest Rest;
        private Database DB;

        /* Constructor
         * */
        public SPluginApi(Rest Rest, Database DB)
        {
            this.Rest = Rest;
            this.DB = DB;
        }

        /* RegisterRestfulCommands
         * 
         * Registers splugin http commands
         * 
         * */
        public void RegisterRestfulCommands()
        {
            // Checks if authentication is enabled
            if (TShock.Config.EnableTokenEndpointAuthentication)
            {
                Rest.Register(new SecureRestCommand("/player_kills", PlayerKills));
                Rest.Register(new SecureRestCommand("/all_players_kills", AllPlayersKills));
                Rest.Register(new SecureRestCommand("/player_stats", PlayerStats));
                Rest.Register(new SecureRestCommand("/all_players_stats", AllPlayersStats));
            }
            else
            {
                Rest.Register(new RestCommand("/player_kills", (a) => this.PlayerKills(new RestRequestArgs(a.Verbs, a.Parameters, a.Request, SecureRest.TokenData.None))));
                Rest.Register(new RestCommand("/all_players_kills", (a) => this.AllPlayersKills(new RestRequestArgs(a.Verbs, a.Parameters, a.Request, SecureRest.TokenData.None))));
                Rest.Register(new RestCommand("/player_stats", (a) => this.PlayerStats(new RestRequestArgs(a.Verbs, a.Parameters, a.Request, SecureRest.TokenData.None))));
                Rest.Register(new RestCommand("/all_players_stats", (a) => this.AllPlayersStats(new RestRequestArgs(a.Verbs, a.Parameters, a.Request, SecureRest.TokenData.None))));
            }
        }

        [Description("Returns kills list of specific player.")]
        [Route("/player_kills")]
        [Noun("name", false, "The name of player.", typeof(String))]
        [Token]
        private object PlayerKills(RestRequestArgs args)
        {
            var Name = args.Parameters["name"];

            // Checks if parameters exists
            if (string.IsNullOrWhiteSpace(Name))
            {
                return RestMissingParam("name");
            }

            // Gets and checks if result (kills) are not empty
            var Kills = DB.GetKills(Name);
            if (Kills == null)
            {
                return RestError("Invalid name");
            }

            var Result = new RestObject();
            foreach (var Row in Kills)
            {
                Result.Add(Row.Key, Row.Value);
            }

            return Result;
        }

        [Description("Returns kills list of all players.")]
        [Route("/all_players_kills")]
        [Token]
        private object AllPlayersKills(RestRequestArgs args)
        {
            // Gets and checks if result (kills) are not empty
            var AllKills = DB.GetAllKills();
            if (AllKills == null)
            {
                return RestError("Empty database");
            }

            var Result = new RestObject();
            foreach (var Row in AllKills)
            {
                Result.Add(Row.Key, Row.Value);
            }
            return Result;
        }

        [Description("Returns specific player stats.")]
        [Route("/player_stats")]
        [Noun("name", false, "The name of player.", typeof(String))]
        [Token]
        private object PlayerStats(RestRequestArgs args)
        {
            var Name = args.Parameters["name"];

            // Checks if parameters exists
            if (string.IsNullOrWhiteSpace(Name))
            {
                return RestMissingParam("name");
            }

            // Gets and checks if result (stats) are not empty
            var Stats = DB.GetStats(Name);
            if (Stats == null)
            {
                return RestError("Invalid name");
            }

            var Result = new RestObject()
            {
                {"tiles_destroyed", Stats[0]},
                {"tiles_placed", Stats[1]},
                {"deaths", Stats[2]}
            };

            return Result;
        }

        [Description("Returns stats for each player.")]
        [Route("/all_players_stats")]
        [Token]
        private object AllPlayersStats(RestRequestArgs args)
        {
            // Gets and checks if result (stats) are not empty
            var AllStats = DB.GetAllStats();
            if (AllStats == null)
            {
                return RestError("Empty database");
            }

            var Result = new RestObject();
            foreach (var Row in AllStats)
            {
                Result.Add(Row.Key, new Dictionary<string, int>(){
                {"tiles_destroyed", Row.Value[0]},
                {"tiles_placed", Row.Value[1]},
                {"deaths", Row.Value[2]}});
            }
            return Result;
        }

        /* RestError
         * 
         * Returns RestObject with error message
         * 
         * */
        private RestObject RestError(string message, string status = "400")
        {
            return new RestObject(status) { Error = message };
        }

        /* RestMissingParam
         * 
         * Custom version of RestError
         * 
         * */
        private RestObject RestMissingParam(string var)
        {
            return RestError("Missing or empty " + var + " parameter");
        }

        /* RestMissingParam
         * 
         * Custom version of RestError
         * 
         * */
        private RestObject RestMissingParam(params string[] vars)
        {
            return RestMissingParam(string.Join(", ", vars));
        }
    }
}
