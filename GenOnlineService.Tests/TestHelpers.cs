using System.Runtime.CompilerServices;
using GenOnlineService;
using Microsoft.Extensions.Configuration;

namespace GenOnlineService.Tests;

// Small factories for the pieces of the lobby model that are constructible without a database or
// a live websocket connection, so lobby/member logic can be unit tested in isolation.
internal static class TestHelpers
{
	// AddMember calls UserSession.TryUpdateSessionNetworkRoom, which reads the process-wide
	// RoomCatalog singleton. Initialize it once for the whole test run with a minimal catalog so
	// lobby tests don't need a real appsettings-style room catalog file on disk.
	[ModuleInitializer]
	internal static void InitializeRoomCatalogForTests()
	{
		RoomCatalog.InitializeFromJsonForTests("""[{"name":"Global","default":false,"rooms":[]}]""");
	}

	// Mesh-check tests need a bounded, near-instant attempt window instead of the real 8s default.
	// FullMeshCheckSettings.Get falls back to its default for any value <= 0, so 1ms (plus a short
	// sleep in the tests themselves) is used rather than 0. Also carries a distinctive
	// reconnect_grace_period_ms so config-driven tests can assert the value came from config
	// rather than from UserSessionSettings' own default.
	internal const Int64 ConfiguredReconnectGracePeriodMSForTests = 12345;

	[ModuleInitializer]
	internal static void InitializeMeshCheckSettingsForTests()
	{
		Program.g_Config = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Core:full_mesh_check_attempt_window_ms"] = "1",
				["Core:full_mesh_check_snapshot_interval_ms"] = "1",
				["Core:full_mesh_check_retry_delay_ms"] = "1",
				["Core:full_mesh_check_max_attempts"] = "1",
				["Core:reconnect_grace_period_ms"] = ConfiguredReconnectGracePeriodMSForTests.ToString(),
			})
			.Build();
	}

	// Registers a session in the same process-wide registry Lobby.CompleteFullMeshConnectivityCheckLocked
	// looks the host up in (WebSocketManager.GetSessionFromUser), so mesh-check tests can observe the
	// outcome message queued onto that session's outbound channel.
	public static void RegisterSessionForTests(UserSession session)
	{
		WebSocketManager.GetUserDataCache()[EUserSessionType.GameClient][session.m_UserID] = session;
	}

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
