using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

#if DEBUG
// Regression guard for the exact bug class HOST_ACTION_KICK_USER hit: a callback that awaits
// another RunExclusiveAsync call on the same lobby must fail fast instead of deadlocking (the
// underlying SemaphoreSlim is not reentrant).
public class LobbyGateReentrancyTests
{
	[Fact]
	public async Task RunExclusiveAsync_ThrowsImmediately_WhenReenteredOnTheSameLobby()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);

		Task attempt = lobby.RunExclusiveAsync(async () =>
		{
			await lobby.RunExclusiveAsync(() => Task.CompletedTask);
		});

		Task completed = await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(5)));
		Assert.Same(attempt, completed);
		await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
	}

	[Fact]
	public async Task RunExclusiveAsyncGeneric_ThrowsImmediately_WhenReenteredOnTheSameLobby()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);

		Task<int> attempt = lobby.RunExclusiveAsync(async () =>
		{
			return await lobby.RunExclusiveAsync(() => Task.FromResult(1));
		});

		Task completed = await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(5)));
		Assert.Same(attempt, completed);
		await Assert.ThrowsAsync<InvalidOperationException>(() => attempt);
	}
}
#endif
