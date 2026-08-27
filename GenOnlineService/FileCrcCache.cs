using System.Collections.Concurrent;

namespace GenOnlineService
{
	public sealed class FileCrcCache
	{
		private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
		private readonly ConcurrentDictionary<string, object> _locks = new(StringComparer.OrdinalIgnoreCase);

		public uint Get(string path)
		{
			string fullPath = Path.GetFullPath(path);
			FileInfo file = new(fullPath);
			if (_entries.TryGetValue(fullPath, out Entry cached) && cached.Matches(file))
			{
				return cached.Crc;
			}

			lock (_locks.GetOrAdd(fullPath, _ => new object()))
			{
				file.Refresh();
				if (_entries.TryGetValue(fullPath, out cached) && cached.Matches(file))
				{
					return cached.Crc;
				}

				uint crc = CRC32Calculator.CalculateCRC32(fullPath);
				file.Refresh();
				_entries[fullPath] = new Entry(file.LastWriteTimeUtc, file.Length, crc);
				return crc;
			}
		}

		private readonly record struct Entry(DateTime LastWriteTimeUtc, long Length, uint Crc)
		{
			public bool Matches(FileInfo file)
			{
				return file.Exists && LastWriteTimeUtc == file.LastWriteTimeUtc && Length == file.Length;
			}
		}
	}
}
