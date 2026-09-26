using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers the fix for HOST_ACTION_KICK_USER's un-awaited LeaveSpecificLobby call: the actual
// removal must happen as a SEPARATE gated operation after the validating gate use has released,
// never nested inside it (RemoveMember acquires the same lobby's non-reentrant gate).
public class LobbyKickFlowTests
{
	[Fact]
	public async Task GateValidationThenRemoveMember_CompletesWithoutDeadlockAndRemovesTheTarget()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		await lobby.AddMember(owner, "Owner", 0, true, prefs);

		UserSession target = TestHelpers.MakeUserSession();
		await lobby.AddMember(target, "Target", 0, true, prefs);

		Task sequence = Task.Run(async () =>
		{
			// Mirrors ApplyLobbyFieldUpdateAsync's gate use (validate target, hand back the ID)
			// followed by PerformKickAsync's RemoveMember call AFTER that gate is released.
			Int64? validatedUserID = await lobby.RunExclusiveAsync(
				() => Task.FromResult<Int64?>(lobby.GetMemberFromUserID(target.m_UserID)?.UserID));

			Assert.NotNull(validatedUserID);

			LobbyMember? member = lobby.GetMemberFromUserID(validatedUserID!.Value);
			if (member != null)
			{
				await lobby.RemoveMember(member);
			}
		});

		Task completed = await Task.WhenAny(sequence, Task.Delay(TimeSpan.FromSeconds(5)));

		Assert.Same(sequence, completed);
		await sequence; // re-throw if it faulted
		Assert.Null(lobby.GetMemberFromUserID(target.m_UserID));
	}
}
