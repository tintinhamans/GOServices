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

using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GenOnlineService.Controllers
{
	public enum EVersionCheckResult
	{
		OK = 0,
		Failed = 1,
		NeedsUpdate = 2
	}

	public abstract class APIResult
	{
		public string Serialize()
		{
			return JsonSerializer.Serialize(this, GetReturnType());
		}

		public abstract Type GetReturnType();
	}

	public class POST_VersionCheck_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return typeof(POST_VersionCheck_Result);
		}

		public EVersionCheckResult result { get; set; } = EVersionCheckResult.Failed;
		public string patcher_name { get; set; } = string.Empty;
		public string patcher_path { get; set; } = string.Empty;
		public long patcher_size { get; set; }
	}

	public class GET_VersionManifest_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return typeof(GET_VersionManifest_Result);
		}

		public uint execrc_60 { get; set; }
	}

	[ApiController]
	[Route("env/{environment}/contract/{contract_version}/[controller]")]
	public class VersionCheckController : ControllerBase
	{
		private readonly ConfigurationFileCache _configurationFiles;
		private readonly FileCrcCache _fileCrcs;

		public VersionCheckController(ConfigurationFileCache configurationFiles, FileCrcCache fileCrcs)
		{
			_configurationFiles = configurationFiles;
			_fileCrcs = fileCrcs;
		}

		[HttpPost(Name = "PostVersionCheck")]
		public Task<APIResult> Post()
		{
			return VersionHelper.CheckVersion(Request.Body, _configurationFiles, _fileCrcs);
		}
	}

	[ApiController]
	[Route("env/{environment}/contract/{contract_version}/[controller]")]
	public class VersionManifestController : ControllerBase
	{
		private readonly FileCrcCache _fileCrcs;

		public VersionManifestController(FileCrcCache fileCrcs)
		{
			_fileCrcs = fileCrcs;
		}

		[HttpGet(Name = "GetVersionManifest")]
		public APIResult Get()
		{
			return VersionHelper.GetManifest(_fileCrcs);
		}
	}

	internal static class VersionHelper
	{
		private const string ClientExecutableName = "OutpostOnlineZH_60.exe";

#if DEBUG && !LARGE_PATCH_TEST
		private const bool EnforceVersionCheck = false;
#else
		private const bool EnforceVersionCheck = true;
#endif

		private static readonly HttpClient HttpClient = new();
		private static readonly JsonSerializerOptions JsonOptions = new()
		{
			NumberHandling = JsonNumberHandling.AllowReadingFromString,
			PropertyNameCaseInsensitive = true
		};

		public static APIResult GetManifest(FileCrcCache fileCrcs)
		{
			return new GET_VersionManifest_Result
			{
				execrc_60 = CalculateClientExecutableCrc(fileCrcs)
			};
		}

		public static async Task<APIResult> CheckVersion(
			Stream requestBody,
			ConfigurationFileCache configurationFiles,
			FileCrcCache fileCrcs)
		{
			using StreamReader reader = new(requestBody);
			return await CheckVersion(await reader.ReadToEndAsync(), configurationFiles, fileCrcs);
		}

		public static async Task<APIResult> CheckVersion(
			string jsonData,
			ConfigurationFileCache configurationFiles,
			FileCrcCache fileCrcs)
		{
			POST_VersionCheck_Result result = new();

			try
			{
				VersionCheckRequest? request = JsonSerializer.Deserialize<VersionCheckRequest>(jsonData, JsonOptions);
				if (request?.execrc is not uint executableCrc
					|| request.ver is not int version
					|| request.netver is not int networkVersion
					|| request.servicesver is not int servicesVersion)
				{
					return result;
				}

				bool versionMatches = version == Constants.GENERALS_ONLINE_VERSION;
				bool networkVersionMatches = networkVersion == Constants.GENERALS_ONLINE_NET_VERSION;
				bool servicesVersionMatches = servicesVersion == Constants.GENERALS_ONLINE_SERVICE_VERSION;
				bool executableMatches = executableCrc == CalculateClientExecutableCrc(fileCrcs);

				if (!EnforceVersionCheck
					|| (versionMatches && networkVersionMatches && servicesVersionMatches && executableMatches))
				{
					result.result = EVersionCheckResult.OK;
					return result;
				}

				PatchData? patchData = JsonSerializer.Deserialize<PatchData>(
					configurationFiles.GetContents("patchdata.json"),
					JsonOptions);

				if (patchData == null
					|| string.IsNullOrWhiteSpace(patchData.patcher_name)
					|| string.IsNullOrWhiteSpace(patchData.patcher_path))
				{
					return result;
				}

				result.patcher_name = patchData.patcher_name;
				result.patcher_path = patchData.patcher_path;
				result.patcher_size = await GetPatcherSize(result);
				result.result = EVersionCheckResult.NeedsUpdate;
			}
			catch (JsonException)
			{
				return result;
			}
			catch (IOException)
			{
				return result;
			}
			catch (UnauthorizedAccessException)
			{
				return result;
			}

			return result;
		}

		private static uint CalculateClientExecutableCrc(FileCrcCache fileCrcs)
		{
#if DEBUG
			return 0;
#else
			return fileCrcs.Get(GetCrcFilePath(ClientExecutableName));
#endif
		}

		private static Task<long> GetPatcherSize(POST_VersionCheck_Result result)
		{
#if !DEBUG
			return Task.FromResult(new FileInfo(GetCrcFilePath(result.patcher_name)).Length);
#elif LARGE_PATCH_TEST
			result.patcher_path = "http://ipv4.download.thinkbroadband.com/100MB.zip";
			return Task.FromResult(100L * 1048576);
#else
			return GetHttpSize(result.patcher_path);
#endif
		}

		private static async Task<long> GetHttpSize(string url)
		{
			try
			{
				using HttpRequestMessage request = new(HttpMethod.Head, url);
				using HttpResponseMessage response = await HttpClient.SendAsync(request);

				if (response.IsSuccessStatusCode && response.Content.Headers.ContentLength.HasValue)
				{
					return response.Content.Headers.ContentLength.Value;
				}
			}
			catch (HttpRequestException)
			{
				return -1;
			}

			return -1;
		}

		private static string GetCrcFilePath(string fileName)
		{
			return Path.Combine(Directory.GetCurrentDirectory(), "crcfiles", fileName);
		}

		private sealed class VersionCheckRequest
		{
			public uint? execrc { get; set; }
			public int? ver { get; set; }
			public int? netver { get; set; }
			public int? servicesver { get; set; }
		}

		private sealed class PatchData
		{
			public string patcher_name { get; set; } = string.Empty;
			public string patcher_path { get; set; } = string.Empty;
		}
	}
}
