using GenOnlineService;

namespace GenOnlineService.Tests;

// Small factories for the pieces of the lobby model that are constructible without a database or
// a live websocket connection, so lobby/member logic can be unit tested in isolation.
internal static class TestHelpers
{
	private static Int64 s_NextUserID = 1;

	public static UserSession MakeUserSession(string continent = "NA", string country = "US")
	{
		Int64 userID = Interlocked.Increment(ref s_NextUserID);
		return new UserSession(userID, EUserSessionType.GameClient, KnownClients.EKnownClients.gen_online_30hz, continent, country, 0.0, 0.0);
	}

	public static Lobby MakeLobby(UserSession owner, int maxPlayers = 8, Int64 lobbyID = 1)
	{
		return new Lobby(
			lobbyID,
			owner,
			"Test Lobby",
			ELobbyState.GAME_SETUP,
			"TestMap",
			"maps\\TestMap\\TestMap.map",
			vanilla_teams: false,
			starting_cash: 10000,
			limit_superweapons: false,
			track_stats: false,
			passworded: false,
			password: string.Empty,
			map_official: true,
			rng_seed: 1,
			network_room: -1,
			allow_observers: false,
			max_cam_height: Constants.g_DefaultCameraMaxHeight,
			exe_crc: 12345,
			ini_crc: 67890,
			max_players: maxPlayers,
			lobbyType: ELobbyType.CustomGame,
			inAnticheatID: EKnownAnticheatID.NONE);
	}

	public static UserLobbyPreferences MakeLobbyPreferences()
	{
		return new UserLobbyPreferences
		{
			favorite_side = -1,
			favorite_color = -1,
			favorite_limit_superweapons = false,
			favorite_starting_money = 0
		};
	}
}
