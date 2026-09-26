using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

public class LoginCrcExpiryTests
{
	private static Int64 s_NextTestUserID = 2_000_000;
	private static Int64 NextUserID() => Interlocked.Increment(ref s_NextTestUserID);

	[Theory]
	[InlineData(0, false)]                                                   // just registered
	[InlineData(Helpers.c_LoginCrcEntryExpiryMS - 1, false)]                  // just under the boundary
	[InlineData(Helpers.c_LoginCrcEntryExpiryMS, true)]                       // exactly at the boundary
	[InlineData(Helpers.c_LoginCrcEntryExpiryMS + 60_000, true)]              // well past it
	public void IsLoginCrcEntryExpired_MatchesTheTenMinuteBoundary(Int64 elapsedMS, bool expectedExpired)
	{
		Int64 registeredAt = 1_000_000;
		Int64 now = registeredAt + elapsedMS;

		bool bExpired = Helpers.IsLoginCrcEntryExpired(registeredAt, now);

		Assert.Equal(expectedExpired, bExpired);
	}

	[Fact]
	public void PruneExpiredLoginCRCs_RemovesOnlyEntriesNeverConsumed_PastTheExpiryWindow()
	{
		Int64 freshUserID = NextUserID();
		Int64 staleExeUserID = NextUserID();
		Int64 staleGameUserID = NextUserID();

		Helpers.RegisterInitialPlayerExeCRC(freshUserID, "FRESH");
		Helpers.RegisterInitialPlayerGameCRCs(freshUserID, 1, 2);

		// Simulate entries registered long ago by writing directly with a stale timestamp - a real
		// login is Environment.TickCount64 "now", so a user who never opened a websocket would have
		// exactly this shape well past the expiry window.
		Int64 staleTimestamp = Environment.TickCount64 - Helpers.c_LoginCrcEntryExpiryMS - 1;
		Helpers.g_dictInitialExeCRCs[staleExeUserID] = ("STALE", staleTimestamp);
		Helpers.g_dictInitialGameCRCs[staleGameUserID] = (3, 4, staleTimestamp);

		Helpers.PruneExpiredLoginCRCs();

		Assert.True(Helpers.g_dictInitialExeCRCs.ContainsKey(freshUserID));
		Assert.True(Helpers.g_dictInitialGameCRCs.ContainsKey(freshUserID));
		Assert.False(Helpers.g_dictInitialExeCRCs.ContainsKey(staleExeUserID));
		Assert.False(Helpers.g_dictInitialGameCRCs.ContainsKey(staleGameUserID));
	}
}
