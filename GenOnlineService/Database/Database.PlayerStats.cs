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

using GenOnlineService;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.Text.Json;

// TODO_EFCORE: When updating this, make sure we preserve old ON DUPLICATE behavior, to overwrite the old data since day_of_year key will be re-used
public class UserStatsEntry
{
	public long UserId { get; set; }
	public string Stats { get; set; } = "{}";
}

// TODO_EFCORE: rename to user_stats
public class UserStatsConfiguration : IEntityTypeConfiguration<UserStatsEntry>
{
	public void Configure(EntityTypeBuilder<UserStatsEntry> builder)
	{
		builder.ToTable("user_stats_v2");

		builder.HasKey(x => x.UserId);

		builder.Property(x => x.UserId)
			.HasColumnName("user_id");

		builder.Property(x => x.Stats)
			.HasColumnName("stats")
			.HasColumnType("longtext")
			.UseCollation("utf8mb4_bin")     // matches your CREATE TABLE
			.IsRequired();

		// JSON validity constraint
		builder.HasCheckConstraint(
			"CK_user_stats_v2_stats_json_valid",
			"json_valid(`stats`)"
		);
	}
}


namespace Database
{
	public static class UserStats
	{
		private static readonly Func<AppDbContext, long, Task<string?>> _getUserStatsJson =
	EF.CompileAsyncQuery(
		(AppDbContext db, long userId) =>
			db.UserStats
			  .Where(s => s.UserId == userId)
			  .Select(s => s.Stats)
			  .FirstOrDefault()
	);


		public static async Task<PlayerStats> GetPlayerStats(
	AppDbContext db,
	long userId)
		{
			EloData elo;
			try
			{
				// Load ELO (already EF-based)
				elo = await Database.Users.GetELOData(db, userId);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ERROR] GetPlayerStats failed: {ex.Message}");
				SentrySdk.CaptureException(ex);
				return new PlayerStats(userId, EloConfig.BaseRating, 0, EloConfig.BaseRating);
			}

			PlayerStats ps = new PlayerStats(userId, elo.Rating, elo.NumMatches, elo.MonthlyRating);

			try
			{
				// Load stats JSON via EF
				string? json = await _getUserStatsJson(db, userId);

				if (string.IsNullOrEmpty(json))
					return ps; // no stats row → return ELO-only stats

				// Deserialize dictionary
				Dictionary<int, int>? dict =
					JsonSerializer.Deserialize<Dictionary<int, int>>(json);

				if (dict == null)
					return ps;

				// Feed into PlayerStats
				foreach (var kv in dict)
				{
					EStatIndex statId = (EStatIndex)kv.Key;
					int statValue = kv.Value;

					ps.ProcessFromDB(statId, statValue);
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ERROR] GetPlayerStats failed: {ex.Message}");
				SentrySdk.CaptureException(ex);
			}

			return ps;
		}

		public static async Task<Dictionary<long, PlayerStats>> GetPlayerStatsBatchFromDatabase(
			AppDbContext db,
			IReadOnlyCollection<long> userIds,
			CancellationToken cancellationToken = default)
		{
			long[] distinctUserIds = userIds.Distinct().ToArray();
			if (distinctUserIds.Length == 0)
			{
				return new Dictionary<long, PlayerStats>();
			}

			var users = await db.Users
				.AsNoTracking()
				.Where(user => distinctUserIds.Contains(user.ID))
				.Select(user => new
				{
					user.ID,
					user.EloRating,
					user.MonthlyEloRating,
					user.EloNumberOfMatches
				})
				.ToListAsync(cancellationToken);

			Dictionary<long, string> storedStats = await db.UserStats
				.AsNoTracking()
				.Where(entry => distinctUserIds.Contains(entry.UserId))
				.ToDictionaryAsync(entry => entry.UserId, entry => entry.Stats, cancellationToken);

			Dictionary<long, PlayerStats> result = new(users.Count);
			foreach (var user in users)
			{
				PlayerStats playerStats = new(
					user.ID,
					user.EloRating,
					user.EloNumberOfMatches,
					user.MonthlyEloRating);

				if (storedStats.TryGetValue(user.ID, out string? json) && !string.IsNullOrWhiteSpace(json))
				{
					Dictionary<int, int>? stats = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
					if (stats != null)
					{
						foreach ((int statId, int statValue) in stats)
						{
							try
							{
								playerStats.ProcessFromDB((EStatIndex)statId, statValue);
							}
							catch (ArgumentOutOfRangeException)
							{
								// Old rows may contain non-integer/unsupported legacy fields.
							}
						}
					}
				}

				result[user.ID] = playerStats;
			}

			return result;
		}

		public static async Task UpdatePlayerStats(
			AppDbContext db,
			long userId,
			IReadOnlyDictionary<int, int> statUpdates,
			CancellationToken cancellationToken = default)
		{
			if (statUpdates.Count == 0)
			{
				return;
			}

			string statsPatch = JsonSerializer.Serialize(statUpdates);

			// Apply the complete client patch in a single, row-atomic statement. This preserves
			// omitted legacy keys without the read/deserialize/write cycle that previously ran
			// once for every stat and allowed concurrent requests to overwrite each other.
			await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO user_stats_v2 (user_id, stats)
VALUES ({userId}, {statsPatch})
ON DUPLICATE KEY UPDATE
stats = JSON_MERGE_PATCH(COALESCE(stats, '{{}}'), VALUES(stats));", cancellationToken);
		}

	}
}
