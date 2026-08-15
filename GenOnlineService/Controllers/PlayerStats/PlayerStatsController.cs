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

using Amazon.S3.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GenOnlineService.Controllers
{
	public class RouteHandler_GET_PlayerStats_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return this.GetType();
		}

		public PlayerStats? stats { get; set; } = null;
	}

	public class RouteHandler_GET_PlayerStatsBatch_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return this.GetType();
		}
		public List<PlayerStats> stats { get; set; } = new();
		public List<Int64> missing_user_ids { get; set; } = new();
	}

	public class RouteHandler_GET_PlayerStatsBatch_Input
	{
		public List<Int64>? user_ids { get; set; }
	}

    public class RouteHandler_PUT_PlayerStats_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return this.GetType();
		}

		public PlayerStats? stats { get; set; } = null;
	}

	internal static class PlayerStatsUpdateParser
	{
		internal const int MaxPayloadEntries = (int)EStatIndex.LASTLADDERHOST + 1;

		internal static Dictionary<int, int> ParseIntegerUpdates(string jsonData)
		{
			using JsonDocument document = JsonDocument.Parse(jsonData);
			if (document.RootElement.ValueKind != JsonValueKind.Array)
			{
				throw new JsonException("Player stats payload must be an array.");
			}

			if (document.RootElement.GetArrayLength() > MaxPayloadEntries)
			{
				throw new JsonException($"Player stats payload exceeds {MaxPayloadEntries} entries.");
			}

			Dictionary<int, int> updates = new();
			int statId = 0;
			foreach (JsonElement element in document.RootElement.EnumerateArray())
			{
				if (element.ValueKind == JsonValueKind.Number
					&& element.TryGetInt32(out int statValue)
					&& IsSupportedIntegerStat(statId))
				{
					updates[statId] = statValue;
				}

				statId++;
			}

			return updates;
		}

		private static bool IsSupportedIntegerStat(int statId)
		{
			if (statId < 0 || statId >= MaxPayloadEntries)
			{
				return false;
			}

			EStatIndex stat = (EStatIndex)statId;
			return stat != EStatIndex.OPTIONS
				&& stat != EStatIndex.SYSTEM_SPEC
				&& stat != EStatIndex.LASTLADDERHOST;
		}
	}

	[ApiController]
	[Route("env/{environment}/contract/{contract_version}/[controller]")]
	public class PlayerStatsController : ControllerBase
	{
		private readonly IDbContextFactory<AppDbContext> _dbFactory;
		private readonly ILogger<PlayerStatsController> _logger;

		public PlayerStatsController(IDbContextFactory<AppDbContext> dbFactory, ILogger<PlayerStatsController> logger)
		{
			_logger = logger;
			_dbFactory = dbFactory;
		}

		[HttpGet("{userID}")]
		[Authorize(Roles = "Player,Monitor")]
		public async Task<APIResult> Get(Int64 userID)
		{
			// TODO_ASP: Set error codes properly in all places (and use variable, not magic numbers)
			RouteHandler_GET_PlayerStats_Result result = new RouteHandler_GET_PlayerStats_Result();
			result.stats = new PlayerStats(userID, EloConfig.BaseRating, 0, EloConfig.BaseRating); // return 0s by default, incase client tries to use it

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};

			// get from cache (just get any user, all sessions will have stats stored against them)
			SharedUserData? userData = WebSocketManager.GetSharedDataForUser(userID);

			// if user is offline, hit DB, could be a friends list inspection for example
			if (userData == null)
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				PlayerStats playerStats = await Database.UserStats.GetPlayerStats(db, userID);

				if (playerStats == null)
				{
					Response.StatusCode = (int)HttpStatusCode.NotFound;
				}
				else
				{
					result.stats = playerStats;
				}

				return result;
			}
			else if (userData.GameStats == null) // if the session exists but no stats exist, this is a problem
			{
				Response.StatusCode = (int)HttpStatusCode.NotFound;
				return result;
			}

			result.stats = userData.GameStats;
			return result;
		}

		private const int MaxBatchUsers = 4096;

		// Bulk endpoint
		[HttpPost("Batch")]
		[Authorize(Roles = "GameClient,ChatClient,GameLauncher,Monitor")]
		public async Task<APIResult> PostBatched()
		{
			RouteHandler_GET_PlayerStatsBatch_Result result = new();

			var options = new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			};

			RouteHandler_GET_PlayerStatsBatch_Input? inputData;
			try
			{
				inputData = await JsonSerializer.DeserializeAsync<RouteHandler_GET_PlayerStatsBatch_Input>(
					HttpContext.Request.Body,
					options,
					HttpContext.RequestAborted);
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Rejected malformed player stats batch request");
				Response.StatusCode = (int)HttpStatusCode.BadRequest;
				return result;
			}

			if (inputData?.user_ids == null || inputData.user_ids.Count > MaxBatchUsers)
			{
				Response.StatusCode = (int)HttpStatusCode.BadRequest;
				return result;
			}

			List<Int64> inputUserIds = inputData.user_ids;
			List<Int64> requestedUserIds = inputUserIds.Distinct().ToList();
			Dictionary<Int64, PlayerStats> statsByUser = new(requestedUserIds.Count);
			List<Int64> usersMissingFromCache = new();

			foreach (Int64 userID in requestedUserIds)
			{
				SharedUserData? userData = WebSocketManager.GetSharedDataForUser(userID);
				if (userData?.GameStats != null)
				{
					statsByUser[userID] = userData.GameStats;
				}
				else
				{
					usersMissingFromCache.Add(userID);
				}
			}

			try
			{
				if (usersMissingFromCache.Count > 0)
				{
					await using var db = await _dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
					Dictionary<Int64, PlayerStats> databaseStats =
						await Database.UserStats.GetPlayerStatsBatchFromDatabase(
							db,
							usersMissingFromCache,
							HttpContext.RequestAborted);

					foreach ((Int64 userID, PlayerStats stats) in databaseStats)
					{
						statsByUser[userID] = stats;
					}
				}
			}
			catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to load fallback player stats for batch request");
				SentrySdk.CaptureException(ex);
				Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
				return result;
			}

			foreach (Int64 userID in inputUserIds)
			{
				if (statsByUser.TryGetValue(userID, out PlayerStats? stats))
				{
					result.stats.Add(stats);
				}
			}

			result.missing_user_ids = requestedUserIds
				.Where(userID => !statsByUser.ContainsKey(userID))
				.ToList();

			return result;
		}

        [HttpPut]
		[Authorize(Roles = "GameClient")]
		public async Task<APIResult> Put()
		{
			RouteHandler_PUT_PlayerStats_Result result = new();
			Int64 userId = TokenHelper.GetUserID(this);
			EUserSessionType sessionType = TokenHelper.GetSessionType(this);

			if (userId == -1)
			{
				Response.StatusCode = (int)HttpStatusCode.Unauthorized;
				return result;
			}

			if (!SessionHelpers.SessionTypeHasAccessTo(sessionType, ESessionAccessType.Gameplay))
			{
				Response.StatusCode = (int)HttpStatusCode.Forbidden;
				return result;
			}

			Dictionary<int, int> updates;
			try
			{
				using StreamReader reader = new(HttpContext.Request.Body);
				string jsonData = await reader.ReadToEndAsync(HttpContext.RequestAborted);
				updates = PlayerStatsUpdateParser.ParseIntegerUpdates(jsonData);
			}
			catch (JsonException ex)
			{
				_logger.LogWarning(ex, "Rejected malformed player stats update for user {UserId}", userId);
				Response.StatusCode = (int)HttpStatusCode.BadRequest;
				return result;
			}

			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync(HttpContext.RequestAborted);
				await Database.UserStats.UpdatePlayerStats(db, userId, updates, HttpContext.RequestAborted);

				// Publish to the live cache only after durable persistence succeeds.
				SharedUserData? userData = WebSocketManager.GetSharedDataForUser(userId);
				if (userData?.GameStats != null)
				{
					foreach ((int statId, int statValue) in updates)
					{
						userData.GameStats.ProcessFromDB((EStatIndex)statId, statValue);
					}

					result.stats = userData.GameStats;
				}
			}
			catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Failed to persist player stats update for user {UserId}", userId);
				SentrySdk.CaptureException(ex);
				Response.StatusCode = (int)HttpStatusCode.ServiceUnavailable;
			}

			return result;
		}
	}
}
