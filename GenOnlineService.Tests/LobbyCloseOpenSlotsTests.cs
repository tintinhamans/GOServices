using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

public class LobbyCloseOpenSlotsTests
{
	[Fact]
	public async Task CloseOpenSlots_ClosesOnlyOpenSlotsAndLeavesOccupiedSlotsAlone()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner, maxPlayers: 4);
		await lobby.AddMember(owner, "Owner", 0, true, TestHelpers.MakeLobbyPreferences());

		await lobby.CloseOpenSlots();

		Assert.Equal(EPlayerType.SLOT_PLAYER, lobby.GetMemberFromSlot(0)!.SlotState);
		for (int i = 1; i < Lobby.maxLobbySize; i++)
		{
			Assert.Equal(EPlayerType.SLOT_CLOSED, lobby.GetMemberFromSlot(i)!.SlotState);
		}
	}

	[Fact]
	public async Task CloseOpenSlots_ThenStartFullMeshConnectivityCheck_DoesNotDeadlock()
	{
		// Regression guard: both are separately-gated operations on the same lobby. Awaiting them
		// back to back (as the websocket handler does) must never nest one gate acquisition inside
		// the other.
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		Lobby lobby = TestHelpers.MakeLobby(owner);
		await lobby.AddMember(owner, "Owner", 0, true, TestHelpers.MakeLobbyPreferences());

		Task sequence = Task.Run(async () =>
		{
			await lobby.CloseOpenSlots();
			await lobby.StartFullMeshConnectivityCheck();
		});

		Task completed = await Task.WhenAny(sequence, Task.Delay(TimeSpan.FromSeconds(5)));

		Assert.Same(sequence, completed);
		await sequence; // re-throw if it faulted
		Assert.True(lobby.PendingFullMeshConnectivityChecks);
	}
}
