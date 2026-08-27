/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
**
**    This program is distributed in the hope that it will be useful,
**    but WITHOUT ANY WARRANTY; without even the implied warranty of
**    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
**    GNU Affero General Public License for more details.
**
**    You should have received a copy of the GNU Affero General Public License
**    along with this program.  If not, see <https://www.gnu.org/licenses/>.
*/

using System.Collections.Concurrent;
using System.Text.Json;

namespace GenOnlineService
{
	public sealed class ConfigurationFileCache : IDisposable
	{
		private static readonly JsonDocumentOptions RoomJsonOptions = new()
		{
			CommentHandling = JsonCommentHandling.Skip,
			AllowTrailingCommas = true
		};

		private readonly ConcurrentDictionary<string, string> _contents = new(StringComparer.OrdinalIgnoreCase);
		private readonly HashSet<string> _pendingFiles = new(StringComparer.OrdinalIgnoreCase);
		private readonly object _pendingFilesLock = new();
		private readonly ILogger<ConfigurationFileCache> _logger;
		private readonly FileSystemWatcher _watcher;
		private readonly Timer _reloadTimer;
		private bool _disposed;

		public event Action<string, string>? Changed;

		public ConfigurationFileCache(ILogger<ConfigurationFileCache> logger)
		{
			_logger = logger;

			foreach (string fileName in ConfigurationFiles.DefaultFileNames)
			{
				_contents[fileName] = Load(fileName);
			}

			_reloadTimer = new Timer(ReloadPendingFiles);
			_watcher = new FileSystemWatcher(ConfigurationFiles.DirectoryPath)
			{
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
			};
			_watcher.Changed += OnFileChanged;
			_watcher.Created += OnFileChanged;
			_watcher.Renamed += OnFileChanged;
			_watcher.Error += (_, args) => _logger.LogWarning(args.GetException(), "Configuration file watcher failed");
			_watcher.EnableRaisingEvents = true;
		}

		public string GetContents(string fileName)
		{
			return _contents.TryGetValue(fileName, out string? contents)
				? contents
				: throw new FileNotFoundException($"Configuration file '{fileName}' is not managed by the cache.");
		}

		private void OnFileChanged(object sender, FileSystemEventArgs args)
		{
			if (args.Name == null || !_contents.ContainsKey(args.Name))
			{
				return;
			}

			lock (_pendingFilesLock)
			{
				_pendingFiles.Add(args.Name);
				_reloadTimer.Change(TimeSpan.FromMilliseconds(250), Timeout.InfiniteTimeSpan);
			}
		}

		private void ReloadPendingFiles(object? state)
		{
			string[] pendingFiles;
			lock (_pendingFilesLock)
			{
				if (_disposed)
				{
					return;
				}

				pendingFiles = _pendingFiles.ToArray();
				_pendingFiles.Clear();
			}

			foreach (string fileName in pendingFiles)
			{
				try
				{
					string contents = Load(fileName);
					Changed?.Invoke(fileName, contents);
					_contents[fileName] = contents;
					_logger.LogInformation("Reloaded configuration file {FileName}", fileName);
				}
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
				{
					_logger.LogWarning(ex, "Failed to reload configuration file {FileName}", fileName);
				}
			}
		}

		private static string Load(string fileName)
		{
			string contents = File.ReadAllText(ConfigurationFiles.GetPath(fileName));
			if (Path.GetExtension(fileName).Equals(".json", StringComparison.OrdinalIgnoreCase))
			{
				JsonDocumentOptions options = fileName.Equals("rooms.json", StringComparison.OrdinalIgnoreCase)
					? RoomJsonOptions
					: default;
				using JsonDocument document = JsonDocument.Parse(contents, options);
			}

			return contents;
		}

		public void Dispose()
		{
			lock (_pendingFilesLock)
			{
				if (_disposed)
				{
					return;
				}

				_disposed = true;
			}

			_watcher.Dispose();
			_reloadTimer.Dispose();
		}
	}
}
