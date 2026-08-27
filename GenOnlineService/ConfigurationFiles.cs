namespace GenOnlineService
{
	internal static class ConfigurationFiles
	{
		internal static readonly string[] DefaultFileNames =
		{
			"motd.txt",
			"patchdata.json",
			"rooms.json",
			"serviceconfig.json"
		};

		public static string DirectoryPath { get; private set; } = Path.Combine(Directory.GetCurrentDirectory(), "config");

		public static void Initialize(string contentRootPath)
		{
			DirectoryPath = Path.Combine(contentRootPath, "config");
			Directory.CreateDirectory(DirectoryPath);

			string defaultsPath = Path.Combine(AppContext.BaseDirectory, "defaults");
			foreach (string fileName in DefaultFileNames)
			{
				string targetPath = GetPath(fileName);
				if (!File.Exists(targetPath))
				{
					File.Copy(Path.Combine(defaultsPath, fileName), targetPath);
				}
			}
		}

		public static string GetPath(string fileName)
		{
			return Path.Combine(DirectoryPath, fileName);
		}
	}
}
