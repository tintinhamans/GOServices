using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers the CheckForTimeouts fix: a session snapshotted as abandoned+expired must not be cleared
// if a reconnect happened before the actual clear runs.
public class SessionTimeoutRecheckTests
{
	[Fact]
	public void ShouldStillClearAbandonedSession_RefusesWhenTheCurrentSessionIsADifferentObject()
	{
		// A reconnect between the snapshot and the clear replaces the registered session with a new
		// object (even if the new one also happens to look abandoned) - the old snapshot must never
		// be used to tear down whatever is live now.
		UserSession snapshotSession = TestHelpers.MakeUserSession();
		UserSession reconnectedSession = TestHelpers.MakeUserSession();

		bool bShouldClear = WebSocketManager.ShouldStillClearAbandonedSession(reconnectedSession, snapshotSession);

		Assert.False(bShouldClear);
	}

	[Fact]
	public void ShouldStillClearAbandonedSession_RefusesWhenNoLongerAbandoned()
	{
		// Same object, but a reconnect (or MarkNotAbandoned) means it's no longer abandoned.
		UserSession session = TestHelpers.MakeUserSession();

		bool bShouldClear = WebSocketManager.ShouldStillClearAbandonedSession(session, session);

		Assert.False(bShouldClear);
	}

	[Fact]
	public void ShouldStillClearAbandonedSession_RefusesWhenCurrentSessionIsGone()
	{
		// The session was already cleared/deregistered by something else between the snapshot and
		// this re-check (current == null).
		UserSession snapshotSession = TestHelpers.MakeUserSession();

		bool bShouldClear = WebSocketManager.ShouldStillClearAbandonedSession(null, snapshotSession);

		Assert.False(bShouldClear);
	}
}
