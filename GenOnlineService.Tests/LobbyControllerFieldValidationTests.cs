using GenOnlineService;
using GenOnlineService.Controllers;
using Xunit;

namespace GenOnlineService.Tests;

public class LobbyControllerFieldValidationTests
{
	[Theory]
	[InlineData(0)]  // LOBBY_MAP
	[InlineData(19)] // HOST_ACTION_BULK_SLOT_UPDATE (highest defined value)
	public void TryParseLobbyUpdateField_AcceptsDefinedValues(int rawValue)
	{
		bool bParsed = LobbyController.TryParseLobbyUpdateField(rawValue, out ELobbyUpdateField field);

		Assert.True(bParsed);
		Assert.Equal((ELobbyUpdateField)rawValue, field);
	}

	[Theory]
	[InlineData(20)]
	[InlineData(-1)]
	[InlineData(int.MaxValue)]
	public void TryParseLobbyUpdateField_RejectsOutOfRangeValues(int rawValue)
	{
		bool bParsed = LobbyController.TryParseLobbyUpdateField(rawValue, out _);

		Assert.False(bParsed);
	}
}
