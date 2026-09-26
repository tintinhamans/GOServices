using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers defect C: before the per-lobby gate, AddMember/RemoveMember/host migration could
// interleave and corrupt Members (two players in one slot, a lost/duplicated slot, etc.).
public class LobbySlotConcurrencyTests
{
	private static void AssertNoSlotCorruption(Lobby lobby)
	{
		Assert.Equal(Lobby.maxLobbySize, lobby.Members.Length);

		// Every occupied slot's own SlotIndex must match where it actually sits in the array.
		for (int i = 0; i < lobby.Members.Length; i++)
		{
			Assert.Equal(i, lobby.Members[i].SlotIndex);
		}

		// No UserID may occupy more than one slot at once.
		List<Int64> humanUserIDs = lobby.Members
			.Where(m => m.IsHuman())
			.Select(m => m.UserID)
			.ToList();
		Assert.Equal(humanUserIDs.Count, humanUserIDs.Distinct().Count());

		// NumCurrentPlayers must agree with a direct count of non-open/closed slots.
		int directCount = lobby.Members.Count(m => m.SlotState != EPlayerType.SLOT_CLOSED && m.SlotState != EPlayerType.SLOT_OPEN);
		Assert.Equal(directCount, lobby.NumCurrentPlayers);
	}

	[Fact]
	public async Task ConcurrentAddMember_NeverExceedsLobbySizeAndNeverDuplicatesASlot()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner, maxPlayers: Lobby.maxLobbySize);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();

		const int candidateCount = 30;
		UserSession[] sessions = Enumerable.Range(0, candidateCount).Select(_ => TestHelpers.MakeUserSession()).ToArray();

		Task<bool>[] joinTasks = sessions
			.Select(session => Task.Run(() => lobby.AddMember(session, $"P{session.m_UserID}", 0, true, prefs)))
			.ToArray();

		bool[] results = await Task.WhenAll(joinTasks);

		// The lobby only has maxLobbySize slots, so exactly that many joins can succeed - never more.
		Assert.Equal(Lobby.maxLobbySize, results.Count(r => r));
		AssertNoSlotCorruption(lobby);
	}

	[Fact]
	public async Task ConcurrentAddAndRemove_LeavesLobbyInAConsistentState()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner, maxPlayers: Lobby.maxLobbySize);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();

		// Lobby's constructor only records Owner - it does not add the owner as a member (that's
		// LobbyManager.CreateLobby's job in production). Add them explicitly so host migration below
		// has a real owner-as-member to migrate away from.
		Assert.True(await lobby.AddMember(owner, "Owner", 0, true, prefs));

		// Fill the remaining slots.
		UserSession[] initialSessions = Enumerable.Range(0, Lobby.maxLobbySize - 1).Select(_ => TestHelpers.MakeUserSession()).ToArray();
		foreach (UserSession session in initialSessions)
		{
			Assert.True(await lobby.AddMember(session, $"P{session.m_UserID}", 0, true, prefs));
		}

		AssertNoSlotCorruption(lobby);

		// Concurrently: half the members leave (including the owner, forcing host migration) while
		// an equal number of new sessions race to take the freed slots.
		UserSession[] leavers = new[] { owner }.Concat(initialSessions.Take(Lobby.maxLobbySize / 2 - 1)).ToArray();
		UserSession[] newcomers = Enumerable.Range(0, Lobby.maxLobbySize / 2).Select(_ => TestHelpers.MakeUserSession()).ToArray();

		List<Task> churnTasks = new();
		foreach (UserSession leaver in leavers)
		{
			churnTasks.Add(Task.Run(async () =>
			{
				LobbyMember? member = lobby.GetMemberFromUserID(leaver.m_UserID);
				if (member != null)
				{
					await lobby.RemoveMember(member);
				}
			}));
		}
		foreach (UserSession newcomer in newcomers)
		{
			churnTasks.Add(Task.Run(() => lobby.AddMember(newcomer, $"N{newcomer.m_UserID}", 0, true, prefs)));
		}

		await Task.WhenAll(churnTasks);

		AssertNoSlotCorruption(lobby);

		// The original owner left, so host migration must have promoted a still-present member -
		// Owner can never be left pointing at someone no longer in the lobby.
		Assert.NotEqual(owner.m_UserID, lobby.Owner);
		Assert.NotNull(lobby.GetMemberFromUserID(lobby.Owner));
	}
}
