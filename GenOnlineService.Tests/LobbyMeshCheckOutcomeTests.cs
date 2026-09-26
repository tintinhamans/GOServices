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
	public async Task StartingANewCheckCompletesThePreviousOneAsSuperseded()
	{
		(UserSession owner, Lobby lobby) = await MakeLobbyWithRegisteredHostAsync(extraMembers: 0);

		await lobby.StartFullMeshConnectivityCheck();
		Int64 firstCheckID = lobby.FullMeshCheckID;

		await lobby.StartFullMeshConnectivityCheck();
		Int64 secondCheckID = lobby.FullMeshCheckID;

		Assert.NotEqual(firstCheckID, secondCheckID);

		// The first StartFullMeshConnectivityCheck call queued nothing (nothing to supersede yet);
		// the second call is the one that must have completed the first check before starting itself.
		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = ReadOutcome(owner);
		Assert.False(outcome.mesh_complete);
		Assert.Equal("check_superseded", outcome.reason);

		// The second (current) check is still pending - only one outcome was sent.
		Assert.True(lobby.PendingFullMeshConnectivityChecks);
		Assert.False(owner.OutboundWebsocketSends.TryRead(out _));
	}
}
