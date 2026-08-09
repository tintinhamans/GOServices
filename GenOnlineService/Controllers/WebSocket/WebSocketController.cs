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

using Discord;
using MaxMind.GeoIP2;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Buffers;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace GenOnlineService.Controllers
{
	public partial class WebSocketController : ControllerBase
	{
		private readonly LobbyManager _lobbyManager;
		private readonly IDbContextFactory<AppDbContext> _dbFactory;

		public WebSocketController(LobbyManager lobbyManager, IDbContextFactory<AppDbContext> dbFactory)
		{
			_lobbyManager = lobbyManager;
			_dbFactory = dbFactory;
		}

		private static readonly JsonSerializerOptions JsonOpts = new()
		{
			PropertyNameCaseInsensitive = true,
			AllowOutOfOrderMetadataProperties = true
		};

		// GeoIP DB is designed to be reused; opening per request is expensive
		private static readonly DatabaseReader GeoIpReader = new("data/GeoLite2-City.mmdb");

		private struct WSMessageEnvelope
		{
			public int msg_id { get; set; }
		}

		[Route("/ws")]
		[Authorize(Roles = "GameClient,ChatClient,GameLauncher")]
		public async Task Get([FromHeader(Name = "is-reconnect")] bool bIsReconnect)
		{
			if (!HttpContext.WebSockets.IsWebSocketRequest)
			{
				HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
				return;
			}

			// create a session
			Int64 user_id = Convert.ToInt64(this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

			var firstEntryClientID = this.User.FindFirst("client_id");

			// client ID is mandatory
			if (firstEntryClientID == null)
			{
				// early out, dont accept WS
				HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
				return;
			}

			string ipAddress = IPHelpers.NormalizeIP(HttpContext.Connection.RemoteIpAddress?.ToString());
			string ipContinent = "NA";
			string ipCountry = "US";
			double dLatitude = 38.8977; // the whitehouse
			double dLongitude = -77.0365; // the whitehouse

			try
			{
				var city = GeoIpReader.City(ipAddress);

				ipContinent = city.Continent.Code;
				ipCountry = city.Country.IsoCode;

				if (city.Location.Longitude != null)
				{
					dLongitude = (double)city.Location.Longitude;
				}

				if (city.Location.Latitude != null)
				{
					dLatitude = (double)city.Location.Latitude;
				}
			}
			catch
			{
				// keep defaults
			}

			bool bIsAdmin = HttpContext.User.IsInRole("Admin");

			KnownClients.EKnownClients client_id = KnownClients.EKnownClients.unknown;
			if (int.TryParse(firstEntryClientID.Value, out int clientIDInt32))
			{
				// Validate if the int corresponds to a defined enum value
				if (System.Enum.IsDefined(typeof(KnownClients.EKnownClients), clientIDInt32))
				{
					client_id = (KnownClients.EKnownClients)clientIDInt32;
				}
			}

			// if unknown, error
			if (client_id == KnownClients.EKnownClients.unknown)
			{
				HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
				return;
			}

			EUserSessionType sessType = TokenHelper.GetSessionType(this);

			await using var db = await _dbFactory.CreateDbContextAsync();
			UserWebSocketInstance wsSess = await WebSocketManager.CreateSession(
				db,
				sessType,
				bIsReconnect,
				user_id,
				client_id,
				ipAddress,
				ipContinent,
				ipCountry,
				dLatitude,
				dLongitude,
				bIsAdmin);

			// if null, it was probably a reconnect and they need to fully reconnect, so return an error instead
			if (wsSess == null)
			{
				HttpContext.Response.StatusCode = StatusCodes.Status205ResetContent;
				return;
			}

			// accept WS
			WebSocket acceptedSocket;
			try
			{
				acceptedSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
			}
			catch
			{
				// nothing will ever activate this reservation now
				await WebSocketManager.CancelPendingActivation(wsSess);
				throw;
			}

			using var webSocket = acceptedSocket;

			if (!await WebSocketManager.ActivateSession(wsSess, webSocket))
			{
				using var closeCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await webSocket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "Connection superseded", closeCts.Token);
				return;
			}

			var buffer = new byte[8192 * 4];
			using var messageBuffer = new MemoryStream();
			const int MaxMessageSizeBytes = 64 * 1024; // hard cap; a fragmented message larger than this closes the connection
			WebSocketReceiveResult? receiveResult = null;

			while (webSocket.State == WebSocketState.Open && WebSocketManager.IsCurrentWebSocket(wsSess))
			{
				bool bDisconnectTest = false;
				if (bDisconnectTest)
				{
					await webSocket.CloseAsync(WebSocketCloseStatus.ProtocolError, "Disconnect Test", CancellationToken.None);
					break;
				}

				try
				{
					using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // timeout
					receiveResult = await webSocket.ReceiveAsync(
						new ArraySegment<byte>(buffer), cts.Token);
				}
				catch (OperationCanceledException)
				{
					if (!WebSocketManager.IsCurrentWebSocket(wsSess))
					{
						break;
					}

					// No message received in 30s; send a keep-alive pong and continue waiting.
					await wsSess.SendPong();
					continue;
				}
				catch (Exception ex)
				{
					// Log unexpected errors
					Console.WriteLine($"WebSocket error: {ex}");
					SentrySdk.CaptureException(ex);
					break;
				}

				if (!WebSocketManager.IsCurrentWebSocket(wsSess))
				{
					break;
				}

				// any inbound traffic proves the client is alive, not just an explicit PING
				wsSess.OnPing();

				if (receiveResult.MessageType == WebSocketMessageType.Close)
				{
					await wsSess.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing");
					break;
				}

				// accumulate fragments; a message can span multiple ReceiveAsync calls
				messageBuffer.Write(buffer, 0, receiveResult.Count);

				if (messageBuffer.Length > MaxMessageSizeBytes)
				{
					await wsSess.CloseAsync(WebSocketCloseStatus.MessageTooBig, "Message exceeds maximum allowed size");
					break;
				}

				if (!receiveResult.EndOfMessage)
				{
					continue;
				}

				byte[] messageBytes = messageBuffer.ToArray();
				messageBuffer.SetLength(0);

				var segment = new ArraySegment<byte>(messageBytes);

				UserSession? sourceUserData = WebSocketManager.GetSessionFromUser(wsSess.m_UserID, wsSess.m_SessionType);

				// if we lost session data, close WS
				if (sourceUserData == null || !ReferenceEquals(sourceUserData, wsSess.OwnerSession))
				{
					await wsSess.CloseAsync(WebSocketCloseStatus.NormalClosure, "User signed in from another point of presence [B]");
					break;
				}

				await ProcessWSMessage(wsSess, sourceUserData, receiveResult, segment);
			}

			Console.ForegroundColor = ConsoleColor.Cyan;
			SharedUserData? sourceData = WebSocketManager.GetSharedDataForUser(user_id);
			Console.WriteLine("WEBSOCKET DISCONNECT FOR {0}", sourceData == null ? "NULL" : sourceData.m_strDisplayName);
			Console.ForegroundColor = ConsoleColor.Gray;

			// close the session
			if (wsSess != null)
			{
				await WebSocketManager.DeleteSession(user_id, wsSess.m_SessionType, wsSess, false);
			}

			// do close (if in the correct state)
			if (webSocket.State == WebSocketState.Open || webSocket.State == WebSocketState.CloseReceived || webSocket.State == WebSocketState.CloseSent)
			{
				WebSocketCloseStatus closeStatus = WebSocketCloseStatus.PolicyViolation;
				string closeStatusDescription = "Protocol Error (Probably Disconnect)";
				if (receiveResult != null)
				{
					if (receiveResult.CloseStatus != null)
					{
						closeStatus = receiveResult.CloseStatus.Value;
						closeStatusDescription = receiveResult.CloseStatusDescription;
					}
				}

				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // timeout
				await webSocket.CloseAsync(closeStatus, closeStatusDescription, cts.Token);
			}
		}

		private async Task ProcessWSMessage(UserWebSocketInstance sourceWS, UserSession sourceUserSession, WebSocketReceiveResult receiveResult, ArraySegment<byte> buffer)
		{
			if (!await sourceWS.TryBeginMessageProcessingAsync())
			{
				return;
			}

			try
			{
				if (!WebSocketManager.IsCurrentWebSocket(sourceWS))
				{
					return;
				}

				SharedUserData? sourceUserData = WebSocketManager.GetSharedDataForUser(sourceUserSession.m_UserID);

				// shared data can vanish if the session was cleaned up concurrently; nothing to process without it
				if (sourceUserData == null)
				{
					return;
				}

				// we only process text or binary messages
				if (receiveResult.MessageType != WebSocketMessageType.Text &&
					receiveResult.MessageType != WebSocketMessageType.Binary)
				{
					return;
				}

				if (buffer.Array == null)
				{
					return;
				}

				WSMessageEnvelope envelope;
				try
				{
					envelope = JsonSerializer.Deserialize<WSMessageEnvelope>(buffer.AsSpan(), JsonOpts);
				}
				catch
				{
					// malformed
					return;
				}

				EWebSocketMessageID msgID = (EWebSocketMessageID)envelope.msg_id;

				if (!s_messageHandlers.TryGetValue(msgID, out Func<WSContext, Task>? handler))
				{
					return;
				}

				WSContext ctx = new()
				{
					SourceWS = sourceWS,
					SourceSession = sourceUserSession,
					SourceUserData = sourceUserData,
					Buffer = buffer,
					LobbyManager = _lobbyManager,
					DbFactory = _dbFactory
				};

				try
				{
					await handler(ctx);
				}
				catch
				{
					// swallow per-message exceptions to avoid killing the loop
					// you can add Sentry logging here if desired
				}
			}
			finally
			{
				sourceWS.EndMessageProcessing();
			}
		}
	}
}
