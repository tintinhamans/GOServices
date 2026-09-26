using System.Text.Json;
using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

// Guards against the new MatchmakerJoinLobby.lobby snapshot (or any other Lobby serialization
// path - GET lobby, GET lobbies) ever leaking the plaintext lobby password.
public class LobbySnapshotPasswordTests
{
	[Fact]
	public void SerializedLobby_NeverContainsAPasswordValue()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		lobby.AddPassword("super-secret-password");

		string json = JsonSerializer.Serialize(lobby);

		// Lobby.Password is [JsonIgnore]'d, so the key must not exist at all - not just be blank -
		// on every path that serializes a Lobby, including the MatchmakerJoinLobby.lobby snapshot.
		using JsonDocument doc = JsonDocument.Parse(json);
		Assert.False(doc.RootElement.TryGetProperty("Password", out _));
		Assert.False(doc.RootElement.TryGetProperty("password", out _));
		Assert.DoesNotContain("super-secret-password", json);

		// IsPassworded (the flag clients actually need) is still present and correct.
		Assert.True(doc.RootElement.GetProperty("IsPassworded").GetBoolean());
	}

	[Fact]
	public void MatchmakerJoinLobbySnapshot_NeverContainsAPasswordValue()
	{
		UserSession owner = TestHelpers.MakeUserSession();
		Lobby lobby = TestHelpers.MakeLobby(owner);
		lobby.AddPassword("super-secret-password");

		WebSocketMessage_MatchmakerJoinLobby joinAction = new()
		{
			lobby_id = lobby.LobbyID,
			lobby = lobby
		};

		string json = JsonSerializer.Serialize(joinAction);

		Assert.DoesNotContain("super-secret-password", json);
		using JsonDocument doc = JsonDocument.Parse(json);
		JsonElement lobbyElement = doc.RootElement.GetProperty("lobby");
		Assert.False(lobbyElement.TryGetProperty("Password", out _));
	}
}
