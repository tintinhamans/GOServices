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

using Microsoft.EntityFrameworkCore;
using System;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace GenOnlineService.Controllers
{
	public partial class WebSocketController
	{
		// per-message context handed to a handler; keeps handler signatures small and dependency-free
		private sealed class WSContext
		{
			public required UserWebSocketInstance SourceWS { get; init; }
			public required UserSession SourceSession { get; init; }
			public required SharedUserData SourceUserData { get; init; }
			public required ArraySegment<byte> Buffer { get; init; }
			public required LobbyManager LobbyManager { get; init; }
			public required IDbContextFactory<AppDbContext> DbFactory { get; init; }

			public ReadOnlySpan<byte> Payload => Buffer.AsSpan();
		}

		// dispatch table replacing the previous if/else chain over EWebSocketMessageID
		private static readonly Dictionary<EWebSocketMessageID, Func<WSContext, Task>> s_messageHandlers = new()
		{
			[EWebSocketMessageID.PING] = Handle_Ping,
			[EWebSocketMessageID.SOCIAL_SUBSCRIBE_REALTIME_UPDATES] = Handle_SocialSubscribeRealtimeUpdates,
			[EWebSocketMessageID.SOCIAL_UNSUBSCRIBE_REALTIME_UPDATES] = Handle_SocialUnsubscribeRealtimeUpdates,
			[EWebSocketMessageID.SOCIAL_FRIEND_CHAT_MESSAGE_CLIENT_TO_SERVER] = Handle_SocialFriendChatMessage,
			[EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_CLIENT] = Handle_NetworkRoomChat,
			[EWebSocketMessageID.NETWORK_ROOM_CHANGE_ROOM] = Handle_NetworkRoomChangeRoom,
			[EWebSocketMessageID.NETWORK_ROOM_MARK_READY] = Handle_NetworkRoomMarkReady,
			[EWebSocketMessageID.PLAYER_NAME_CHANGE] = Handle_PlayerNameChange,
			[EWebSocketMessageID.LOBBY_CHANGE_PASSWORD] = Handle_LobbyChangePassword,
			[EWebSocketMessageID.LOBBY_REMOVE_PASSWORD] = Handle_LobbyRemovePassword,
			[EWebSocketMessageID.LOBBY_ROOM_CHAT_FROM_CLIENT] = Handle_LobbyRoomChat,
			[EWebSocketMessageID.START_GAME_COUNTDOWN_STARTED] = Handle_StartGameCountdownStarted,
			[EWebSocketMessageID.START_GAME] = Handle_StartGame,
			[EWebSocketMessageID.FULL_MESH_CONNECTIVITY_CHECK_HOST_REQUESTS_BEGIN] = Handle_FullMeshConnectivityCheckBegin,
			[EWebSocketMessageID.FULL_MESH_CONNECTIVITY_CHECK_RESPONSE] = Handle_FullMeshConnectivityCheckResponse,
			[EWebSocketMessageID.NETWORK_CONNECTION_CLIENT_REQUEST_SIGNALLING] = Handle_NetworkRequestSignalling,
			[EWebSocketMessageID.NETWORK_SIGNAL] = Handle_NetworkSignal,
			[EWebSocketMessageID.ANTICHEAT_MESSAGE] = Handle_AnticheatMessage,
		};

		private static Dictionary<string, JsonElement>? TryParseDataDictionary(ReadOnlySpan<byte> payload)
		{
			try
			{
				return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payload, JsonOpts);
			}
			catch
			{
				return null;
			}
		}

		private static async Task Handle_Ping(WSContext ctx)
		{
			// liveness is already recorded by the receive loop for all inbound traffic
			await ctx.SourceWS.SendPong();
		}

		private static Task Handle_SocialSubscribeRealtimeUpdates(WSContext ctx)
		{
			ctx.SourceSession.SetSubscribedToRealtimeSocialUpdates(true);
			return Task.CompletedTask;
		}

		private static Task Handle_SocialUnsubscribeRealtimeUpdates(WSContext ctx)
		{
			ctx.SourceSession.SetSubscribedToRealtimeSocialUpdates(false);
			return Task.CompletedTask;
		}

		private static Task Handle_SocialFriendChatMessage(WSContext ctx)
		{
			WebSocketMessage_Social_FriendChatMessage_Inbound? chatMessage =
				JsonSerializer.Deserialize<WebSocketMessage_Social_FriendChatMessage_Inbound>(ctx.Payload, JsonOpts);

			if (chatMessage == null)
			{
				return Task.CompletedTask;
			}

			// must be online & friends
			SharedUserData? targetUserData = WebSocketManager.GetSharedDataForUser(chatMessage.target_user_id);

			if (targetUserData != null)
			{
				if (ctx.SourceUserData.GetSocialContainer().Friends.Contains(chatMessage.target_user_id)
					&& targetUserData.GetSocialContainer().Friends.Contains(ctx.SourceSession.m_UserID))
				{
					// make websocket msg
					WebSocketMessage_Social_FriendChatMessage_Outbound outboundMsg = new();
					outboundMsg.msg_id = (int)EWebSocketMessageID.SOCIAL_FRIEND_CHAT_MESSAGE_SERVER_TO_CLIENT;
					outboundMsg.source_user_id = ctx.SourceWS.m_UserID;
					outboundMsg.target_user_id = chatMessage.target_user_id;
					outboundMsg.message = String.Format("{0}: {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
					byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));

					// send to both on all websockets
					WebsocketHelper.SendToAllSessionsOfUser(chatMessage.target_user_id, bytesJSON);
					WebsocketHelper.SendToAllSessionsOfUser(ctx.SourceWS.m_UserID, bytesJSON);
				}
			}
			else
			{
				// ok, they can chat, send the message to both of them
				WebSocketMessage_Social_FriendChatMessage_Outbound outboundMsg = new();
				outboundMsg.msg_id = (int)EWebSocketMessageID.SOCIAL_FRIEND_CHAT_MESSAGE_SERVER_TO_CLIENT;
				outboundMsg.source_user_id = ctx.SourceSession.m_UserID;
				outboundMsg.target_user_id = chatMessage.target_user_id;
				outboundMsg.message = String.Format("This user is not online. Offline messaging is not supported.");

				// send to source
				byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));

				ctx.SourceSession.QueueWebsocketSend(bytesJSON);
			}

			return Task.CompletedTask;
		}

		private static Task Handle_NetworkRoomChat(WSContext ctx)
		{
			// must be in a room
			if (ctx.SourceSession.networkRoomID == -1)
			{
				return Task.CompletedTask;
			}

			WebSocketMessage_NetworkRoomChatMessageInbound? chatMessage =
				JsonSerializer.Deserialize<WebSocketMessage_NetworkRoomChatMessageInbound>(ctx.Payload, JsonOpts);

			if (chatMessage == null)
			{
				return Task.CompletedTask;
			}

			// response
			WebSocketMessage_NetworkRoomChatMessageOutbound outboundMsg = new WebSocketMessage_NetworkRoomChatMessageOutbound();
			outboundMsg.msg_id = (int)EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_SERVER;

			if (chatMessage.action)
			{
				outboundMsg.message = String.Format("{0} {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
				outboundMsg.admin = false; // dont care for actions
				outboundMsg.name_change = false;
			}
			else
			{
				if (ctx.SourceUserData.IsAdmin())
				{
					outboundMsg.message = String.Format("[\u2605\u2605GO STAFF\u2605\u2605]    [{0}] {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
					outboundMsg.admin = true;
					outboundMsg.name_change = false;
				}
				else
				{
					outboundMsg.message = String.Format("[{0}] {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
					outboundMsg.admin = false;
					outboundMsg.name_change = false;
				}
			}

			outboundMsg.action = chatMessage.action;

			// Serialize once before broadcasting
			byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));

			// send it to everyone in the same room
			foreach (var sessionDataByClient in WebSocketManager.GetUserDataCache())
			{
				foreach (var sessionData in sessionDataByClient.Value)
				{
					UserSession targetSess = sessionData.Value;
					if (targetSess.networkRoomID == ctx.SourceSession.networkRoomID)
					{
						SharedUserData? targetUserSharedData = WebSocketManager.GetSharedDataForUser(targetSess.m_UserID);

						if (targetUserSharedData != null)
						{
							// is it blocked by either side? dont deliver the chat
							bool bBlocked = targetUserSharedData.GetSocialContainer().Blocked.Contains(ctx.SourceSession.m_UserID) ||
								ctx.SourceUserData.GetSocialContainer().Blocked.Contains(targetSess.m_UserID);

							if (!bBlocked)
							{
								targetSess.QueueWebsocketSend(bytesJSON);
							}
						}
					}
				}
			}

			// send message to discord
			if (Program.g_Discord != null && chatMessage.message != null)
			{
				Program.g_Discord.SendNetworkRoomChat(ctx.SourceSession.networkRoomID, ctx.SourceSession.m_UserID, ctx.SourceUserData.m_strDisplayName, chatMessage.message);
			}

			return Task.CompletedTask;
		}

		private static async Task Handle_NetworkRoomChangeRoom(WSContext ctx)
		{
			Dictionary<string, JsonElement>? data = TryParseDataDictionary(ctx.Payload);

			if (data != null && data.TryGetValue("room", out JsonElement roomElement))
			{
				Int16 roomID = roomElement.GetInt16();
				await ctx.SourceSession.UpdateSessionNetworkRoom(roomID);
			}
		}

		private static Task Handle_NetworkRoomMarkReady(WSContext ctx)
		{
			Dictionary<string, JsonElement>? data = TryParseDataDictionary(ctx.Payload);

			if (data != null && data.TryGetValue("ready", out JsonElement readyElement))
			{
				bool bReady = readyElement.GetBoolean();

				Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);
				if (lobby != null)
				{
					LobbyMember? member = lobby.GetMemberFromUserID(ctx.SourceSession.m_UserID);

					if (member != null)
					{
						member.SetReadyState(bReady);
					}
				}
			}

			return Task.CompletedTask;
		}

		private static async Task Handle_PlayerNameChange(WSContext ctx)
		{
			// must be in a room
			if (ctx.SourceSession.networkRoomID == -1)
			{
				return;
			}

			WebSocketMessage_NameChange? nameChangeRequest =
				JsonSerializer.Deserialize<WebSocketMessage_NameChange>(ctx.Payload, JsonOpts);

			if (nameChangeRequest == null)
			{
				return;
			}

			// TODO: Move this to a file or DB
			List<string> lstProtectedNames = new List<string>()
			{
				"admin",
				"staff",
				"mass^",
				"mas^",
				"m4ss^",
				"m4s^",
				"moderator",
				"hitler",
				"h1tler",
				"h1tl3r",
				"hittler",
				"h1ttler",
				"h1ttl3r",
				"olda",
				"oldanalytics",
				"ibra",
				"x64",
				"ronin"
			};

			string strNameRequestLower = nameChangeRequest.name.ToLower();

			// dont allow protected names
			if (!ctx.SourceUserData.IsAdmin())
			{
				foreach (string strProtectedName in lstProtectedNames)
				{
					if (strNameRequestLower.Contains(strProtectedName))
					{
						// response back to user
						WebSocketMessage_NetworkRoomChatMessageOutbound outboundMsg = new WebSocketMessage_NetworkRoomChatMessageOutbound();
						outboundMsg.msg_id = (int)EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_SERVER;
						outboundMsg.message = String.Format("--NAME CHANGE-- The display name you tried to set contains a protected word/phrase ({0} - {1})", nameChangeRequest.name, strProtectedName);
						outboundMsg.admin = true; // dont care for actions
						outboundMsg.action = false;
						outboundMsg.name_change = true;
						byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));
						ctx.SourceSession.QueueWebsocketSend(bytesJSON);

						return;
					}
				}
			}

			if (strNameRequestLower.StartsWith(" ") || strNameRequestLower.EndsWith(" "))
			{
				// response back to user
				WebSocketMessage_NetworkRoomChatMessageOutbound outboundMsg = new WebSocketMessage_NetworkRoomChatMessageOutbound();
				outboundMsg.msg_id = (int)EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_SERVER;
				outboundMsg.message = String.Format("--NAME CHANGE-- Display names cannot begin or end with spaces ({0})", nameChangeRequest.name);
				outboundMsg.admin = true; // dont care for actions
				outboundMsg.action = false;
				outboundMsg.name_change = true;
				byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));
				ctx.SourceSession.QueueWebsocketSend(bytesJSON);

				return;
			}

			// dont allow numeric (X) endings, those are protected
			if (System.Text.RegularExpressions.Regex.IsMatch(nameChangeRequest.name, @"\((1[0-9]|20|[0-9])\)$"))
			{
				// Remove the protected numeric ending
				nameChangeRequest.name = System.Text.RegularExpressions.Regex.Replace(nameChangeRequest.name, @"\((1[0-9]|20|[0-9])\)$", "");
			}

			if (nameChangeRequest.name.Length >= 3 && nameChangeRequest.name.Length <= 16)
			{
				await using var db = await ctx.DbFactory.CreateDbContextAsync();
				bool nameSet = await Database.Users.SetDisplayName(db, ctx.SourceSession.m_UserID, nameChangeRequest.name);
				if (nameSet)
				{
					// response
					WebSocketMessage_NetworkRoomChatMessageOutbound outboundMsg = new WebSocketMessage_NetworkRoomChatMessageOutbound();
					outboundMsg.msg_id = (int)EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_SERVER;

					outboundMsg.message = String.Format("--NAME CHANGE-- {0} has changed their display name to {1}", ctx.SourceUserData.m_strDisplayName, nameChangeRequest.name);
					outboundMsg.admin = true;
					outboundMsg.action = false;
					outboundMsg.name_change = true;

					// Serialize once before broadcasting
					byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));

					// send it to the person doing the name change and everyone in the room
					foreach (var sessionDataByClient in WebSocketManager.GetUserDataCache())
					{
						foreach (var sessionData in sessionDataByClient.Value)
						{
							UserSession targetSess = sessionData.Value;
							if (targetSess.networkRoomID == ctx.SourceSession.networkRoomID)
							{
								SharedUserData? targetUserSharedData = WebSocketManager.GetSharedDataForUser(targetSess.m_UserID);

								if (targetUserSharedData != null)
								{
									// is it blocked by either side? dont deliver the chat
									bool bBlocked = targetUserSharedData.GetSocialContainer().Blocked.Contains(ctx.SourceSession.m_UserID) ||
										ctx.SourceUserData.GetSocialContainer().Blocked.Contains(targetSess.m_UserID);

									if (!bBlocked)
									{
										targetSess.QueueWebsocketSend(bytesJSON);
									}
								}
							}
						}
					}

					ctx.SourceUserData.m_strDisplayName = nameChangeRequest.name;
					await WebSocketManager.MarkRoomMemberListAsDirty(ctx.SourceSession.networkRoomID);
				}
				else
				{
					// response back to user
					WebSocketMessage_NetworkRoomChatMessageOutbound outboundMsg = new WebSocketMessage_NetworkRoomChatMessageOutbound();
					outboundMsg.msg_id = (int)EWebSocketMessageID.NETWORK_ROOM_CHAT_FROM_SERVER;
					outboundMsg.message = String.Format("--NAME CHANGE-- The display name you tried to set is already in use by another user ({0})", nameChangeRequest.name);
					outboundMsg.admin = true; // dont care for actions
					outboundMsg.action = false;
					outboundMsg.name_change = true;
					byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));
					ctx.SourceSession.QueueWebsocketSend(bytesJSON);
				}
			}
		}

		private static Task Handle_LobbyChangePassword(WSContext ctx)
		{
			// must be in a lobby
			Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);
			if (lobby != null)
			{
				// must be owner too
				if (lobby.Owner == ctx.SourceSession.m_UserID)
				{
					WebSocketMessage_LobbyPasswordChange? passwordChangeRequest =
						JsonSerializer.Deserialize<WebSocketMessage_LobbyPasswordChange>(ctx.Payload, JsonOpts);

					if (passwordChangeRequest != null)
					{
						lobby.AddPassword(passwordChangeRequest.new_password);
					}
				}
			}

			return Task.CompletedTask;
		}

		private static Task Handle_LobbyRemovePassword(WSContext ctx)
		{
			// must be in a lobby
			Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);
			if (lobby != null)
			{
				// must be owner too
				if (lobby.Owner == ctx.SourceSession.m_UserID)
				{
					lobby.RemovePassword();
				}
			}

			return Task.CompletedTask;
		}

		private static Task Handle_LobbyRoomChat(WSContext ctx)
		{
			// must be in a lobby
			if (ctx.SourceSession.currentLobbyID == -1)
			{
				return Task.CompletedTask;
			}

			WebSocketMessage_LobbyChatMessageInbound? chatMessage =
				JsonSerializer.Deserialize<WebSocketMessage_LobbyChatMessageInbound>(ctx.Payload, JsonOpts);

			if (chatMessage == null)
			{
				return Task.CompletedTask;
			}

			// get lobby
			Lobby? playerLobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

			if (playerLobby == null)
			{
				return Task.CompletedTask;
			}

			// response
			WebSocketMessage_LobbyChatMessageOutbound outboundMsg = new WebSocketMessage_LobbyChatMessageOutbound();
			outboundMsg.msg_id = (int)EWebSocketMessageID.LOBBY_CHAT_FROM_SERVER;
			outboundMsg.user_id = ctx.SourceSession.m_UserID;

			if (chatMessage.action)
			{
				outboundMsg.message = String.Format("{0} {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
			}
			else if (chatMessage.announcement)
			{
				outboundMsg.message = String.Format("{0}", chatMessage.message);
			}
			else
			{
				outboundMsg.message = String.Format("[{0}] {1}", ctx.SourceUserData.m_strDisplayName, chatMessage.message);
			}

			outboundMsg.action = chatMessage.action;
			outboundMsg.announcement = chatMessage.announcement;
			outboundMsg.show_announcement_to_host = chatMessage.show_announcement_to_host;

			// Serialize once before broadcasting
			byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundMsg));

			foreach (LobbyMember lobbyMember in playerLobby.Members)
			{
				if (lobbyMember != null)
				{
					// need to check announcement flag?
					if (outboundMsg.announcement && !outboundMsg.show_announcement_to_host)
					{
						// is it host?
						if (lobbyMember.UserID == ctx.SourceSession.m_UserID)
						{
							continue;
						}
					}

					if (lobbyMember.GetSession().TryGetTarget(out UserSession? sess))
					{
						if (sess != null)
						{
							sess.QueueWebsocketSend(bytesJSON);
						}
					}
				}
			}

			return Task.CompletedTask;
		}

		private static Task Handle_StartGameCountdownStarted(WSContext ctx)
		{
			// must be in a lobby
			Lobby? lobbyInfo = null;
			if (ctx.SourceSession.currentLobbyID != -1)
			{
				// must be lobby owner too
				lobbyInfo = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

				if (lobbyInfo == null || lobbyInfo.Owner != ctx.SourceSession.m_UserID)
				{
					return Task.CompletedTask;
				}
			}

			if (lobbyInfo == null)
			{
				return Task.CompletedTask;
			}

			// lock slots
			lobbyInfo.CloseOpenSlots();

			return Task.CompletedTask;
		}

		private static async Task Handle_StartGame(WSContext ctx)
		{
			// must be in a lobby
			Lobby? lobbyInfo = null;
			if (ctx.SourceSession.currentLobbyID != -1)
			{
				// must be lobby owner too
				lobbyInfo = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

				if (lobbyInfo == null || lobbyInfo.Owner != ctx.SourceSession.m_UserID)
				{
					return;
				}
			}

			if (lobbyInfo == null)
			{
				return;
			}

			// start match + create placeholder match
			await lobbyInfo.UpdateState(ELobbyState.INGAME);

			// simple websocket msg, has no data, so dont even read anything
			foreach (LobbyMember lobbyMember in lobbyInfo.Members)
			{
				if (lobbyMember != null)
				{
					if (lobbyMember.GetSession().TryGetTarget(out UserSession? sess))
					{
						if (sess != null)
						{
							// response
							WebSocketMessage_StartMatch startCommand = new WebSocketMessage_StartMatch();
							startCommand.msg_id = (int)EWebSocketMessageID.START_GAME;
							S3CredentialManager.PresignedUpload? upload = await S3CredentialManager.GetPresignedUpload(EMetadataFileType.FILE_TYPE_SCREENSHOT, EScreenshotType.SCREENSHOT_TYPE_LOADSCREEN, lobbyInfo.MatchID, lobbyMember.UserID, lobbyMember.SlotIndex, lobbyInfo.TimeCreated);
							startCommand.screenshot_url = upload?.Url ?? String.Empty;
							startCommand.screenshot_upload_confirmation_token = upload?.ConfirmationToken ?? String.Empty;

							// Serialize once before broadcasting
							byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(startCommand));

							sess.QueueWebsocketSend(bytesJSON);
						}
					}
				}
			}
		}

		private static Task Handle_FullMeshConnectivityCheckBegin(WSContext ctx)
		{
			// Host has requested this, as part of the start game flow

			// must be in a lobby
			Lobby? lobbyInfo = null;
			if (ctx.SourceSession.currentLobbyID != -1)
			{
				// must be lobby owner too
				lobbyInfo = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

				if (lobbyInfo == null || lobbyInfo.Owner != ctx.SourceSession.m_UserID)
				{
					return Task.CompletedTask;
				}
			}

			if (lobbyInfo == null)
			{
				return Task.CompletedTask;
			}

			// lock slots (more people joining when we're already doing connectivity checks won't help the situation)
			lobbyInfo.CloseOpenSlots();

			// mark lobby as in progress of full mesh connectivity checks
			lobbyInfo.StartFullMeshConnectivityCheck();

			// start full mesh connectivity checks
			lobbyInfo.SendFullMeshConnectivityCheckRequestToMembers();

			return Task.CompletedTask;
		}

		private static async Task Handle_FullMeshConnectivityCheckResponse(WSContext ctx)
		{
			// process a response from a user
			WebSocketMessage_FullMeshConnectivityCheckResponseFromUser? fullMeshMsg =
				JsonSerializer.Deserialize<WebSocketMessage_FullMeshConnectivityCheckResponseFromUser>(ctx.Payload, JsonOpts);

			// store response
			if (fullMeshMsg != null)
			{
				Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);
				if (lobby != null)
				{
					await lobby.StoreFullMeshConnectivityResponse(ctx.SourceSession.m_UserID, fullMeshMsg);
				}
			}
		}

		private static Task Handle_NetworkRequestSignalling(WSContext ctx)
		{
			WebSocketMessage_RequestSignaling? signalingRequest =
				JsonSerializer.Deserialize<WebSocketMessage_RequestSignaling>(ctx.Payload, JsonOpts);

			System.Diagnostics.Debug.WriteLine("Signal restart request received from {0}!", ctx.SourceSession.m_UserID);

			if (signalingRequest != null)
			{
				// Our protocol is just [payload]
				// And everything is in text.

				// find the dest players connection
				UserSession? targetSession = WebSocketManager.GetSessionFromUser(signalingRequest.target_user_id, EUserSessionType.GameClient); // signalling NEEDS a game client session
				if (targetSession != null)
				{
					Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

					if (lobby != null)
					{
						LobbyMember? targetUser = lobby.GetMemberFromUserID(targetSession.m_UserID);
						LobbyMember? sourceUser = lobby.GetMemberFromUserID(ctx.SourceSession.m_UserID);

						if (sourceUser != null && targetUser != null)
						{
							// send signal start to source player
							WebSocketMessage_NetworkStartSignalling joiningPlayerMsg = new WebSocketMessage_NetworkStartSignalling();
							joiningPlayerMsg.msg_id = (int)EWebSocketMessageID.NETWORK_CONNECTION_START_SIGNALLING;
							joiningPlayerMsg.lobby_id = ctx.SourceSession.currentLobbyID;
							joiningPlayerMsg.user_id = targetUser.UserID;
							joiningPlayerMsg.preferred_port = targetUser.Port;
							joiningPlayerMsg.middleware_id = targetUser.MiddlewareUserID;
							ctx.SourceSession.QueueWebsocketSend(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(joiningPlayerMsg)));

							// send the reverse to the target player
							WebSocketMessage_NetworkStartSignalling existingPlayerMsg = new WebSocketMessage_NetworkStartSignalling();
							existingPlayerMsg.msg_id = (int)EWebSocketMessageID.NETWORK_CONNECTION_START_SIGNALLING;
							existingPlayerMsg.lobby_id = ctx.SourceSession.currentLobbyID;
							existingPlayerMsg.user_id = sourceUser.UserID;
							existingPlayerMsg.preferred_port = sourceUser.Port;
							existingPlayerMsg.middleware_id = sourceUser.MiddlewareUserID;
							targetSession.QueueWebsocketSend(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(existingPlayerMsg)));
						}
					}
				}
			}

			return Task.CompletedTask;
		}

		private static Task Handle_NetworkSignal(WSContext ctx)
		{
			WebSocketMessage_SignalBidirectional? signal =
				JsonSerializer.Deserialize<WebSocketMessage_SignalBidirectional>(ctx.Payload, JsonOpts);

			if (signal != null)
			{
				// Our protocol is just [payload]
				// And everything is in text.

				// find the dest players connection
				UserSession? targetSession = WebSocketManager.GetSessionFromUser(signal.target_user_id, EUserSessionType.GameClient); // network signals only goto game clients
				if (targetSession != null)
				{
					Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

					if (lobby != null)
					{
						LobbyMember? targetUser = lobby.GetMemberFromUserID(targetSession.m_UserID);
						LobbyMember? sourceUser = lobby.GetMemberFromUserID(ctx.SourceSession.m_UserID);

						if (sourceUser != null && targetUser != null)
						{
							// now into json for our ws msg format
							// NOTE: outbound msg doesnt need sender ID, we only need that to determine target on the server, everything else is included in the payload
							WebSocketMessage_SignalBidirectional outboundSignal = new WebSocketMessage_SignalBidirectional();
							outboundSignal.msg_id = (int)EWebSocketMessageID.NETWORK_SIGNAL;
							outboundSignal.target_user_id = ctx.SourceSession.m_UserID; // user here is the person who sent it to us
							outboundSignal.payload = signal.payload;
							byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundSignal));

							targetSession.QueueWebsocketSend(bytesJSON);
						}
					}
				}
			}

			return Task.CompletedTask;
		}

		private static Task Handle_AnticheatMessage(WSContext ctx)
		{
			WebSocketMessage_AnticheatMessage? acMsg = JsonSerializer.Deserialize<WebSocketMessage_AnticheatMessage>(ctx.Payload, JsonOpts);

			if (acMsg != null)
			{
				// Our protocol is just [payload]
				// And everything is in text.

				// find the dest players connection
				UserSession? targetSession = WebSocketManager.GetSessionFromUser(acMsg.target_user_id, EUserSessionType.GameClient); // network signals only goto game clients
				if (targetSession != null)
				{
					Lobby? lobby = ctx.LobbyManager.GetLobby(ctx.SourceSession.currentLobbyID);

					if (lobby != null)
					{
						LobbyMember? targetUser = lobby.GetMemberFromUserID(targetSession.m_UserID);
						LobbyMember? sourceUser = lobby.GetMemberFromUserID(ctx.SourceSession.m_UserID);

						if (sourceUser != null && targetUser != null)
						{
							// now into json for our ws msg format
							// NOTE: outbound msg doesnt need sender ID, we only need that to determine target on the server, everything else is included in the payload
							WebSocketMessage_AnticheatMessage outboundACMsg = new WebSocketMessage_AnticheatMessage();
							outboundACMsg.msg_id = (int)EWebSocketMessageID.ANTICHEAT_MESSAGE;
							outboundACMsg.target_user_id = ctx.SourceSession.m_UserID; // user here is the person who sent it to us
							outboundACMsg.payload = acMsg.payload;
							byte[] bytesJSON = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(outboundACMsg));

							targetSession.QueueWebsocketSend(bytesJSON);
						}
					}
				}
			}

			return Task.CompletedTask;
		}
	}
}
