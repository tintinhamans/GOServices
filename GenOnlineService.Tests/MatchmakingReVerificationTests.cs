using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers MatchmakingBucket.VerifyMatchIsStillValid - the gate that must block START_GAME whenever
// the lobby no longer holds exactly the players the match was formed with, a player's connection
// is no longer live, or the mesh check that passed is stale. This is the actual mechanism wired
// into both re-verification checkpoints (before the countdown, and right before StartGame); the
// full async Tick()/DB-backed state machine around it is not exercised here since CreateLobby
// needs a live database.
public class MatchmakingReVerificationTests
{
	private static MatchmakingManager.MatchmakingBucket MakeBucket(UserSession owner, int matchFormedSize)
	{
		MatchmakingManager.MatchmakingBucket bucket = new(0, owner, 2, 2, new ConcurrentList<int>(), 0, 0, EKnownAnticheatID.NONE);
		bucket.SetMatchFormedSizeForTests(matchFormedSize);
		return bucket;
	}

	private static async Task<Lobby> MakeFullyPassedLobbyAsync(UserSession owner, UserSession second)
	{
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		await lobby.AddMember(owner, "Owner", 0, true, prefs);
		await lobby.AddMember(second, "Second", 0, true, prefs);

		await lobby.StartFullMeshConnectivityCheck();

		// Both members report full connectivity to each other, so the check judges as complete.
		await lobby.StoreFullMeshConnectivityResponse(owner.m_UserID, new WebSocketMessage_FullMeshConnectivityCheckResponseFromUser
		{
			mesh_check_id = lobby.FullMeshCheckID,
			attempt = lobby.FullMeshCheckAttempt,
			connectivity_map = new List<Int64> { second.m_UserID }
		});
		await lobby.StoreFullMeshConnectivityResponse(second.m_UserID, new WebSocketMessage_FullMeshConnectivityCheckResponseFromUser
		{
			mesh_check_id = lobby.FullMeshCheckID,
			attempt = lobby.FullMeshCheckAttempt,
			connectivity_map = new List<Int64> { owner.m_UserID }
		});

		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();
		return lobby;
	}

	[Fact]
	public async Task HappyPath_StillValid_WhenNothingChangedSinceTheCheckPassed()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		UserSession second = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(second);

		Lobby lobby = await MakeFullyPassedLobbyAsync(owner, second);
		Assert.True(lobby.LastFullMeshConnectivityCheckOutcome);

		MatchmakingManager.MatchmakingBucket bucket = MakeBucket(owner, matchFormedSize: 2);
		bucket.AddMemberForTests(second);

		bool bValid = bucket.VerifyMatchIsStillValid(lobby, out string failureReason);

		Assert.True(bValid);
		Assert.Equal(string.Empty, failureReason);
	}

	[Fact]
	public async Task Invalid_WhenAMemberLeftTheLobbyAfterFormation()
	{
		// Mirrors "lobby leave during countdown": the lobby now holds fewer players than the match
		// was formed with.
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		UserSession second = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(second);

		Lobby lobby = await MakeFullyPassedLobbyAsync(owner, second);

		LobbyMember secondMember = lobby.GetMemberFromUserID(second.m_UserID)!;
		await lobby.RemoveMember(secondMember);

		MatchmakingManager.MatchmakingBucket bucket = MakeBucket(owner, matchFormedSize: 2);
		bucket.AddMemberForTests(second);

		bool bValid = bucket.VerifyMatchIsStillValid(lobby, out string failureReason);

		Assert.False(bValid);
		Assert.NotEmpty(failureReason);
	}

	[Fact]
	public async Task Invalid_WhenAMembersSessionHasGoneAbandoned()
	{
		// Mirrors "abandoned session during countdown": the player is still technically a lobby
		// member (30s grace period) but their connection is no longer live.
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		UserSession second = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(second);

		Lobby lobby = await MakeFullyPassedLobbyAsync(owner, second);
		second.MarkAbandoned();

		MatchmakingManager.MatchmakingBucket bucket = MakeBucket(owner, matchFormedSize: 2);
		bucket.AddMemberForTests(second);

		bool bValid = bucket.VerifyMatchIsStillValid(lobby, out string failureReason);

		Assert.False(bValid);
		Assert.Contains(second.m_UserID.ToString(), failureReason);
	}

	[Fact]
	public async Task Invalid_WhenMembershipChangedAfterTheCheckPassed()
	{
		// A join right after the check passed also invalidates it - LastFullMeshConnectivityCheckOutcome
		// is cleared to null by the membership-version mechanism, which VerifyMatchIsStillValid reads.
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		UserSession second = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(second);

		Lobby lobby = await MakeFullyPassedLobbyAsync(owner, second);
		Assert.True(lobby.LastFullMeshConnectivityCheckOutcome);

		UserSession third = TestHelpers.MakeUserSession();
		await lobby.AddMember(third, "Third", 0, true, TestHelpers.MakeLobbyPreferences());

		MatchmakingManager.MatchmakingBucket bucket = MakeBucket(owner, matchFormedSize: 2);
		bucket.AddMemberForTests(second);

		bool bValid = bucket.VerifyMatchIsStillValid(lobby, out string failureReason);

		Assert.False(bValid);
	}
}
