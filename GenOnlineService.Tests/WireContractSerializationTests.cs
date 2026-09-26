using System.Text.Json;
using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Guards the exact wire keys the released client's JSON deserializer already expects, and the new
// keys the client team is coding against. PropertyNamingPolicy = null everywhere (matching
// Program.cs's controller JSON options, and System.Text.Json's own default for the plain
// JsonSerializer.Serialize calls websocket messages use) means every property serializes under its
// exact C# name.
public class WireContractSerializationTests
{
	[Fact]
	public async Task LobbyMember_SerializesJoinSequenceUnderItsExactPascalCaseName()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		await lobby.AddMember(owner, "Owner", 0, true, TestHelpers.MakeLobbyPreferences());

		string json = JsonSerializer.Serialize(lobby);
		using JsonDocument doc = JsonDocument.Parse(json);

		JsonElement ownerMemberJson = doc.RootElement.GetProperty("Members")[0];
		Assert.True(ownerMemberJson.TryGetProperty("JoinSequence", out JsonElement joinSequenceProp));
		Assert.Equal(1, joinSequenceProp.GetInt64());
	}

	[Fact]
	public void MatchmakerJoinLobby_SerializesLobbySnapshotAlongsideLobbyId()
	{
		WebSocketMessage_MatchmakerJoinLobby joinAction = new()
		{
			lobby_id = 42,
			lobby = TestHelpers.MakeLobby(TestHelpers.MakeUserSession())
		};

		string json = JsonSerializer.Serialize(joinAction);
		using JsonDocument doc = JsonDocument.Parse(json);

		// lobby_id (snake_case) must still be present unchanged for the current client.
		Assert.Equal(42, doc.RootElement.GetProperty("lobby_id").GetInt64());
		// lobby is the new, additive field carrying the same shape GET lobby returns.
		Assert.True(doc.RootElement.TryGetProperty("lobby", out JsonElement lobbyProp));
		Assert.Equal(JsonValueKind.Object, lobbyProp.ValueKind);
	}

	[Fact]
	public void FullMeshConnectivityCheckOutcome_SerializesMeshCompleteAndMissingConnectionsUnchanged()
	{
		// Locks in the pre-existing wire shape for FULL_MESH_CONNECTIVITY_CHECK_RESPONSE_COMPLETE_TO_HOST
		// so a later change to the reason field can't silently break the fields the client already reads.
		WebSocketMessage_FullMeshConnectivityCheckOutcome outcome = new()
		{
			mesh_complete = false,
			missing_connections = new List<MissingConnectionEntry>
			{
				new() { source_user_id = 1, target_user_id = 2 }
			}
		};

		string json = JsonSerializer.Serialize(outcome);
		using JsonDocument doc = JsonDocument.Parse(json);

		Assert.False(doc.RootElement.GetProperty("mesh_complete").GetBoolean());
		Assert.Equal(1, doc.RootElement.GetProperty("missing_connections")[0].GetProperty("source_user_id").GetInt64());
	}
}
