using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

public class LobbyJoinSequenceTests
{
	[Fact]
	public async Task JoinSequence_StartsAtOneAndIsMonotonicPerLobby()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();

		Assert.True(await lobby.AddMember(owner, "Owner", 0, true, prefs));

		UserSession second = TestHelpers.MakeUserSession();
		Assert.True(await lobby.AddMember(second, "Second", 0, true, prefs));

		Assert.Equal(1, lobby.GetMemberFromUserID(owner.m_UserID)!.JoinSequence);
		Assert.Equal(2, lobby.GetMemberFromUserID(second.m_UserID)!.JoinSequence);
	}

	[Fact]
	public void JoinSequence_IsZeroForOpenAndClosedPlaceholderSlots()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner, maxPlayers: 4);

		// Slots are pre-populated as OPEN (below max) or CLOSED (above max) placeholders, none of
		// which represent a human that ever went through AddMember.
		Assert.All(lobby.Members, member => Assert.Equal(0, member.JoinSequence));
	}

	[Fact]
	public async Task JoinSequence_IsPreservedAcrossHostMigrationAndRejoinGetsANewValue()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();

		await lobby.AddMember(owner, "Owner", 0, true, prefs);

		UserSession second = TestHelpers.MakeUserSession();
		await lobby.AddMember(second, "Second", 0, true, prefs);

		LobbyMember secondMemberBeforeMigration = lobby.GetMemberFromUserID(second.m_UserID)!;
		Assert.Equal(2, secondMemberBeforeMigration.JoinSequence);

		// Owner leaves: RemoveMember runs host migration on `second` as part of the same critical
		// section as the slot clear.
		LobbyMember ownerMember = lobby.GetMemberFromUserID(owner.m_UserID)!;
		await lobby.RemoveMember(ownerMember);

		Assert.Equal(second.m_UserID, lobby.Owner);
		LobbyMember secondMemberAfterMigration = lobby.GetMemberFromUserID(second.m_UserID)!;
		Assert.Equal(2, secondMemberAfterMigration.JoinSequence);
		Assert.Equal(0, secondMemberAfterMigration.SlotIndex);

		// The original owner rejoining is a brand new LobbyMember and must get a new, higher value.
		Assert.True(await lobby.AddMember(owner, "Owner", 0, true, prefs));
		Assert.Equal(3, lobby.GetMemberFromUserID(owner.m_UserID)!.JoinSequence);
	}
}
