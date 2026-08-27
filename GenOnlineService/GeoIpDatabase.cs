using MaxMind.GeoIP2;

namespace GenOnlineService
{
	public sealed class GeoIpDatabase : IDisposable
	{
		public DatabaseReader? Reader { get; }

		public GeoIpDatabase(IConfiguration configuration, IHostEnvironment environment, ILogger<GeoIpDatabase> logger)
		{
			string? databasePath = configuration["GeoIP:DatabasePath"];
			if (string.IsNullOrWhiteSpace(databasePath))
			{
				logger.LogWarning("GeoIP:DatabasePath is not configured; using fallback coordinates.");
				return;
			}
			databasePath = Path.GetFullPath(databasePath, environment.ContentRootPath);

			try
			{
				Reader = new DatabaseReader(databasePath);
			}
			catch (Exception exception)
			{
				logger.LogWarning(exception, "GeoIP database unavailable at {DatabasePath}; using fallback coordinates.", databasePath);
			}
		}

		public void Dispose()
		{
			Reader?.Dispose();
		}
	}
}
