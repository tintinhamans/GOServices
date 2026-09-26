using GenOnlineService;
using Xunit;

namespace GenOnlineService.Tests;

public class UserSessionSettingsTests
{
	[Fact]
	public void ReconnectGracePeriodMS_ReadsFromConfiguredCoreSection()
	{
		// TestHelpers' module initializer seeds Program.g_Config with a distinctive value so this
		// assertion can only pass if the setting is actually read from configuration.
		Assert.Equal(TestHelpers.ConfiguredReconnectGracePeriodMSForTests, UserSessionSettings.ReconnectGracePeriodMS);
	}
}
