SPlugin
=======
SPlugin is a plugin for TShock server. Provides a list of killings and stats such as destroyed / placed tiles and deaths of players.

REST API
--------

To enable the REST API find the following lines in the config.json file which is part of TShock server:
```
"RestApiEnabled": false,
"RestApiPort": 7878,
```
Change the "false" to "true" and restart the server.

**[GET] /player_kills**
```
Returns a list of killings of specific player

@param name (string)
@return json object

Example:
http://127.0.0.1:7878/player_kills?name=your_name

Response:
{
  "status": 200,
  "Green Slime": 43,
  "Bald Zombie": 26,
  "Squirrel": 2
}
```

**[GET] /player_stats**
```
Returns the stats of specific player

@param name (string)
@return json object

Example:
http://127.0.0.1:7878/player_stats?name=your_name

Response:
{
  "status": 200,
  "tiles_destroyed": 532,
  "tiles_placed": 235,
  "deaths": 0
}
```

**[GET] /all_players_kills**
```
Returns a lists of killings of each player

@return json object

Example:
http://127.0.0.1:7878/all_players_kills

Response:
{
  "status": 200,
  "User1": {
    "Green Slime": 451,
    "Blue Slime": 343
  }
  "User2": {
    "Bunny": 232,
    "Bald Zombie": 56
  }
}
```

**[GET] /all_players_stats**
```
Returns the stats for all player

@return json object

Example:
http://127.0.0.1:7878/all_players_stats

Response:
{
  "status": 200,
  "User1": {
    "tiles_destroyed": 4232,
    "tiles_placed": 2712,
    "deaths": 3
  },
  "User2": {
    "tiles_destroyed": 8423,
    "tiles_placed": 1023,
    "deaths": 6
  }
}
```
