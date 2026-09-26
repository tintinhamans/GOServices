using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

public class LobbyManagerConcurrencyTests
{
	[Fact]
	public async Task GenerateNextLobbyID_IsUniqueUnderConcurrentCreation()
	{
		// LobbyManager's constructor only stores the service provider, so it's safe to construct
		// without a real DI container for this ID-generation-only test.
		LobbyManager lobbyManager = new LobbyManager(services: null!);

		const int callsPerTask = 200;
		const int taskCount = 8;

		Task<Int64[]>[] tasks = new Task<Int64[]>[taskCount];
		for (int t = 0; t < taskCount; t++)
		{
			tasks[t] = Task.Run(() =>
			{
				Int64[] ids = new Int64[callsPerTask];
				for (int i = 0; i < callsPerTask; i++)
				{
					ids[i] = lobbyManager.GenerateNextLobbyID();
				}
				return ids;
			});
		}

		Int64[][] results = await Task.WhenAll(tasks);
		List<Int64> allIDs = results.SelectMany(ids => ids).ToList();

		Assert.Equal(taskCount * callsPerTask, allIDs.Count);
		Assert.Equal(allIDs.Count, allIDs.Distinct().Count());
	}

	[Fact]
	public async Task ProbeCounters_AreAccurateUnderConcurrentRegistration()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);

		const int concurrentCalls = 500;
		Task[] tasks = new Task[concurrentCalls];
		for (int i = 0; i < concurrentCalls; i++)
		{
			tasks[i] = Task.Run(() =>
			{
				lobby.RegisterProbeSent_Type1(owner.m_UserID);
				lobby.RegisterProbeSent_Type2(owner.m_UserID);
				lobby.RegisterProbeResponse_Type1(owner.m_UserID);
				lobby.RegisterProbeResponse_Type2(owner.m_UserID);
			});
		}

		await Task.WhenAll(tasks);

		// A prior check-then-write pattern on the backing ConcurrentDictionary could lose
		// increments under this kind of contention; AddOrUpdate must not.
		Assert.Equal(concurrentCalls, lobby.GetProbeSentCount_Type1ForTests(owner.m_UserID));
		Assert.Equal(concurrentCalls, lobby.GetProbeSentCount_Type2ForTests(owner.m_UserID));
		Assert.Equal(concurrentCalls, lobby.GetProbeReceivedCount_Type1ForTests(owner.m_UserID));
		Assert.Equal(concurrentCalls, lobby.GetProbeReceivedCount_Type2ForTests(owner.m_UserID));
	}
}
