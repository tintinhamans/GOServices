using System.Text.Json;
using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Covers the "every started check completes" guarantee and the reason values that explain a
// failed FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST. TestHelpers configures a 1ms
// attempt window for the whole test run, so a short sleep is enough to guarantee the window has
// elapsed without waiting out the real 8s default.
public class LobbyMeshCheckOutcomeTests
{
	private static async Task<(UserSession owner, Lobby lobby)> MakeLobbyWithRegisteredHostAsync(int extraMembers)
	{
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		Assert.True(await lobby.AddMember(owner, "Owner", 0, true, prefs));

		for (int i = 0; i < extraMembers; i++)
		{
			UserSession other = TestHelpers.MakeUserSession();
			Assert.True(await lobby.AddMember(other, $"Player{i}", 0, true, prefs));
		}

		return (owner, lobby);
	}

	// AddMember queues its own join-handshake messages (signalling, AC register) onto every
	// member's outbound channel before any mesh-check outcome exists, so the outcome is not
	// necessarily the first (or only) message queued. Scan for it by msg_id instead.
	private static WebSocketMessage_FullMeshConnectivityCheckOutcome ReadOutcome(UserSession hostSession)
	{
		int wantedMsgID = (int)EWebSocketMessageID.FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST;

		while (hostSession.OutboundWebsocketSends.TryRead(out byte[]? bytes))
		{
			using JsonDocument doc = JsonDocument.Parse(bytes!);
			if (doc.RootElement.TryGetProperty("msg_id", out JsonElement msgIdProp) && msgIdProp.GetInt32() == wantedMsgID)
			{
				WebSocketMessage_FullMeshConnectivityCheckOutcome? outcome =
					JsonSerializer.Deserialize<WebSocketMessage_FullMeshConnectivityCheckOutcome>(bytes!);
				Assert.NotNull(outcome);
				return outcome!;
			}
		}

		throw new Xunit.Sdk.XunitException("No FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST message was queued.");
	}

	// AddMember queues its own unrelated handshake traffic, so "channel is empty" isn't the right
	// check for "no outcome was sent" - scan by msg_id instead, like ReadOutcome does.
	private static void AssertNoOutcomeQueued(UserSession session)
	{
		int wantedMsgID = (int)EWebSocketMessageID.FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST;

		while (session.OutboundWebsocketSends.TryRead(out byte[]? bytes))
		{
			using JsonDocument doc = JsonDocument.Parse(bytes!);
			if (doc.RootElement.TryGetProperty("msg_id", out JsonElement msgIdProp) && msgIdProp.GetInt32() == wantedMsgID)
			{
				Assert.Fail("A FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST was queued when none was expected.");
			}
		}
	}

	[Fact]
	public async Task SingleHumanLobby_ChecksCompleteWithNoReason()
	{
		(UserSession owner, Lobby lobby) = await MakeLobbyWithRegisteredHostAsync(extraMembers: 0);

		await lobby.StartFullMeshConnectivityCheck();
		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = ReadOutcome(owner);
		Assert.True(outcome.mesh_complete);
		Assert.Equal(string.Empty, outcome.reason);
	}

	[Fact]
	public async Task NoMemberEverReports_ReasonIsTimeout()
	{
		(UserSession owner, Lobby lobby) = await MakeLobbyWithRegisteredHostAsync(extraMembers: 1);

		await lobby.StartFullMeshConnectivityCheck();
		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = ReadOutcome(owner);
		Assert.False(outcome.mesh_complete);
		Assert.Equal("timeout", outcome.reason);
	}

	[Fact]
	public async Task MemberLeavingWhileCheckIsPending_ReasonIsMemberLeft()
	{
		// Needs at least 2 humans left AFTER the departure, otherwise the remaining single member
		// trivially has no missing connections and the check completes successfully instead.
		(UserSession owner, Lobby lobby) = await MakeLobbyWithRegisteredHostAsync(extraMembers: 2);

		await lobby.StartFullMeshConnectivityCheck();

		LobbyMember? leavingMember = lobby.Members.FirstOrDefault(m => m.IsHuman() && m.UserID != owner.m_UserID);
		Assert.NotNull(leavingMember);
		await lobby.RemoveMember(leavingMember!);

		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = ReadOutcome(owner);
		Assert.False(outcome.mesh_complete);
		Assert.Equal("member_left", outcome.reason);
	}

	[Fact]
	public async Task StartingANewCheckDiscardsThePreviousOneSilently()
	{
		// The released client keeps a single callback slot for this message and consumes the FIRST
		// one it receives, so a superseded check must send NOTHING - only the newer check's own
		// eventual completion may answer the client.
		(UserSession owner, Lobby lobby) = await MakeLobbyWithRegisteredHostAsync(extraMembers: 0);

		await lobby.StartFullMeshConnectivityCheck();
		Int64 firstCheckID = lobby.FullMeshCheckID;

		await lobby.StartFullMeshConnectivityCheck();
		Int64 secondCheckID = lobby.FullMeshCheckID;

		Assert.NotEqual(firstCheckID, secondCheckID);

		// Nothing was sent for the discarded first check.
		Assert.False(owner.OutboundWebsocketSends.TryRead(out _));
		Assert.True(lobby.PendingFullMeshConnectivityChecks);

		// The second (current) check still completes normally and is the one true answer delivered.
		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = ReadOutcome(owner);
		Assert.True(outcome.mesh_complete);
	}

	[Fact]
	public async Task CompletionIsNotSentWhenTheRequesterIsNoLongerOwner()
	{
		// The requesting owner leaves mid-check (forcing host migration) before it completes. The
		// new owner never asked for this check, so must not have it land in their callback slot as
		// an answer to a question they didn't ask - and the departed former owner is gone too.
		UserSession owner = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(owner);
		UserSession other = TestHelpers.MakeUserSession();
		TestHelpers.RegisterSessionForTests(other);

		Lobby lobby = TestHelpers.MakeLobby(owner);
		UserLobbyPreferences prefs = TestHelpers.MakeLobbyPreferences();
		Assert.True(await lobby.AddMember(owner, "Owner", 0, true, prefs));
		Assert.True(await lobby.AddMember(other, "Other", 0, true, prefs));

		await lobby.StartFullMeshConnectivityCheck();

		LobbyMember ownerMember = lobby.GetMemberFromUserID(owner.m_UserID)!;
		await lobby.RemoveMember(ownerMember);
		Assert.Equal(other.m_UserID, lobby.Owner);

		await Task.Delay(50);
		await lobby.ProcessPendingFullMeshConnectivityChecks();

		Assert.False(lobby.PendingFullMeshConnectivityChecks);
		AssertNoOutcomeQueued(owner);
		AssertNoOutcomeQueued(other);
	}
}
