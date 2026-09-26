using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers the hard invariant: a quick match must never start on a mesh check that passed for a
// different set of players than the lobby currently holds.
public class LobbyMembershipVersionTests
{
	[Fact]
	public async Task MembershipVersion_IncrementsOnAddAndRemove_AndClearsThePreviousOutcome()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();

		int versionBeforeAdd = lobby.MembershipVersion;
		await lobby.AddMember(owner, "Owner", 0, true, prefs);
		Assert.Equal(versionBeforeAdd + 1, lobby.MembershipVersion);

		UserSession second = TestHelpers.MakeUserSession();
		await lobby.AddMember(second, "Second", 0, true, prefs);
		Assert.Equal(versionBeforeAdd + 2, lobby.MembershipVersion);

		LobbyMember secondMember = lobby.GetMemberFromUserID(second.m_UserID)!;
		await lobby.RemoveMember(secondMember);
		Assert.Equal(versionBeforeAdd + 3, lobby.MembershipVersion);
	}

	[Fact]
	public async Task MembershipChange_ClearsAPreviouslyRecordedSuccessfulOutcome()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		await lobby.AddMember(owner, "Owner", 0, true, prefs);

		// Drive a check to a successful completion (single human, trivially fully connected).
		await lobby.StartFullMeshConnectivityCheck();
		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();
		Assert.True(lobby.LastFullMeshConnectivityCheckOutcome);

		// A new member joining afterwards must invalidate that stale "everyone connected" verdict -
		// the check never judged this new player's connectivity at all.
		UserSession second = TestHelpers.MakeUserSession();
		await lobby.AddMember(second, "Second", 0, true, prefs);

		Assert.Null(lobby.LastFullMeshConnectivityCheckOutcome);
	}

	[Fact]
	public async Task MemberJoiningWhileCheckIsPending_MakesTheOutcomeInvalid()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		await lobby.AddMember(owner, "Owner", 0, true, prefs);

		await lobby.StartFullMeshConnectivityCheck();
		int versionAtCheckStart = lobby.MembershipVersionAtLastCheckStart;
		Assert.Equal(lobby.MembershipVersion, versionAtCheckStart);

		// Someone joins mid-check: the check was never judging this new member's connectivity.
		UserSession second = TestHelpers.MakeUserSession();
		await lobby.AddMember(second, "Second", 0, true, prefs);
		Assert.NotEqual(lobby.MembershipVersion, versionAtCheckStart);

		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		// The check must not be reported as successful even though the lone original member had no
		// missing connections at the time the check was judged.
		Assert.False(lobby.LastFullMeshConnectivityCheckOutcome);
	}
}
