using System.Text.Json;
using GenOnlineService;
using GenOnlineService.Controllers;
using Xunit;

namespace GenOnlineService.Tests;

public class HelpersCrcTests
{
	private static Int64 s_NextTestUserID = 1_000_000;
	private static Int64 NextUserID() => Interlocked.Increment(ref s_NextTestUserID);

	private static Dictionary<string, JsonElement> ParsePayload(string json)
	{
		return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
	}

	[Fact]
	public void RegisterInitialPlayerCRCsFromLoginPayload_LegacyLayout_ExeCrcIsTreatedAsTheACHash()
	{
		// The currently released client sends only "exe_crc", and it IS the AC .text-section hash -
		// no ac_exe_crc, no game CRC data at all.
		Int64 userID = NextUserID();
		Dictionary<string, JsonElement> data = ParsePayload("""{"exe_crc":"ABCDEF0123456789"}""");

		Helpers.RegisterInitialPlayerCRCsFromLoginPayload(userID, data);

		Assert.True(Helpers.g_dictInitialExeCRCs.TryGetValue(userID, out var acEntry));
		Assert.Equal("ABCDEF0123456789", acEntry.ExeCrcHash);
		Assert.False(Helpers.g_dictInitialGameCRCs.ContainsKey(userID));
	}

	[Fact]
	public void RegisterInitialPlayerCRCsFromLoginPayload_NewLayout_SplitsACHashAndGameCRCs()
	{
		// A new client sends ac_exe_crc for AC, and exe_crc/ini_crc as the UInt32 game CRCs.
		Int64 userID = NextUserID();
		Dictionary<string, JsonElement> data = ParsePayload("""{"ac_exe_crc":"FEDCBA9876543210","exe_crc":12345,"ini_crc":67890}""");

		Helpers.RegisterInitialPlayerCRCsFromLoginPayload(userID, data);

		Assert.True(Helpers.g_dictInitialExeCRCs.TryGetValue(userID, out var acEntry));
		Assert.Equal("FEDCBA9876543210", acEntry.ExeCrcHash);

		Assert.True(Helpers.g_dictInitialGameCRCs.TryGetValue(userID, out var gameCRCs));
		Assert.Equal(12345u, gameCRCs.ExeCRC);
		Assert.Equal(67890u, gameCRCs.IniCRC);
	}

	[Fact]
	public void RegisterInitialPlayerCRCsFromLoginPayload_NewLayout_ParsesNumericStringsToo()
	{
		// exe_crc/ini_crc may arrive as JSON numbers or numeric strings depending on the client.
		Int64 userID = NextUserID();
		Dictionary<string, JsonElement> data = ParsePayload("""{"ac_exe_crc":"AA","exe_crc":"12345","ini_crc":"67890"}""");

		Helpers.RegisterInitialPlayerCRCsFromLoginPayload(userID, data);

		Assert.True(Helpers.g_dictInitialGameCRCs.TryGetValue(userID, out var gameCRCs));
		Assert.Equal(12345u, gameCRCs.ExeCRC);
		Assert.Equal(67890u, gameCRCs.IniCRC);
	}

	[Fact]
	public void UserSession_Constructor_ConsumesRegisteredGameCRCsExactlyOnce()
	{
		Int64 userID = NextUserID();
		Helpers.RegisterInitialPlayerGameCRCs(userID, 111, 222);

		UserSession session = new UserSession(userID, EUserSessionType.GameClient, KnownClients.EKnownClients.gen_online_30hz, "NA", "US", 0.0, 0.0);

		Assert.Equal(111u, session.ExeCRC);
		Assert.Equal(222u, session.IniCRC);
		Assert.False(Helpers.g_dictInitialGameCRCs.ContainsKey(userID));
	}

	[Fact]
	public void UserSession_Constructor_LeavesCRCsAtZero_WhenNothingWasRegistered()
	{
		Int64 userID = NextUserID();

		UserSession session = new UserSession(userID, EUserSessionType.GameClient, KnownClients.EKnownClients.gen_online_30hz, "NA", "US", 0.0, 0.0);

		Assert.Equal(0u, session.ExeCRC);
		Assert.Equal(0u, session.IniCRC);
	}

	[Theory]
	[InlineData(100u, 200u, 100u, 200u, false)] // known + match -> not rejected
	[InlineData(100u, 200u, 999u, 200u, true)]  // known + exe mismatch -> rejected
	[InlineData(100u, 200u, 100u, 999u, true)]  // known + ini mismatch -> rejected
	[InlineData(0u, 200u, 100u, 200u, false)]   // session exe CRC unknown -> never rejected
	[InlineData(100u, 0u, 100u, 200u, false)]   // session ini CRC unknown -> never rejected
	[InlineData(0u, 0u, 100u, 200u, false)]     // session has no CRC data at all (released client) -> never rejected
	public void ShouldRejectJoinForCrcMismatch_MatchesExpectedDecision(
		UInt32 sessionExeCrc, UInt32 sessionIniCrc, UInt32 lobbyExeCrc, UInt32 lobbyIniCrc, bool expectedReject)
	{
		bool bReject = LobbyController.ShouldRejectJoinForCrcMismatch(sessionExeCrc, sessionIniCrc, lobbyExeCrc, lobbyIniCrc);

		Assert.Equal(expectedReject, bReject);
	}
}
