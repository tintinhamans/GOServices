using GenOnlineService;
using GenOnlineService.Controllers;
using Xunit;

namespace GenOnlineService.Tests;

public class LobbyControllerSlotStateTests
{
	[Theory]
	[InlineData((ushort)EPlayerType.SLOT_OPEN)]
	[InlineData((ushort)EPlayerType.SLOT_CLOSED)]
	[InlineData((ushort)EPlayerType.SLOT_EASY_AI)]
	[InlineData((ushort)EPlayerType.SLOT_MED_AI)]
	[InlineData((ushort)EPlayerType.SLOT_BRUTAL_AI)]
	[InlineData((ushort)EPlayerType.SLOT_PLAYER)]
	public void TryParseSlotState_AcceptsEveryRealEnumValue(ushort rawValue)
	{
		bool bParsed = LobbyController.TryParseSlotState(rawValue, out EPlayerType slotState);

		Assert.True(bParsed);
		Assert.Equal((EPlayerType)rawValue, slotState);
	}

	[Theory]
	[InlineData((ushort)6)]
	[InlineData((ushort)7)]
	[InlineData(ushort.MaxValue)]
	public void TryParseSlotState_RejectsOutOfRangeValues(ushort rawValue)
	{
		bool bParsed = LobbyController.TryParseSlotState(rawValue, out _);

		Assert.False(bParsed);
	}
}
