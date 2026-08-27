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

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GenOnlineService.Controllers
{
	public class RouteHandler_GET_MOTD_Result : APIResult
	{
		public override Type GetReturnType()
		{
			return this.GetType();
		}

		public string MOTD { get; set; } = String.Empty;
	}



	[ApiController]
	[Authorize(Roles = "GameClient,ChatClient,GameLauncher,Monitor")]
	[Route("env/{environment}/contract/{contract_version}/[controller]")]
	public class MOTDController : ControllerBase
	{
		private readonly ConfigurationFileCache _configurationFiles;

		public MOTDController(ConfigurationFileCache configurationFiles)
		{
			_configurationFiles = configurationFiles;
		}

		[HttpGet(Name = "GetMOTD")]

		public APIResult Get()
		{
			RouteHandler_GET_MOTD_Result result = new RouteHandler_GET_MOTD_Result();

			try
			{
				string template = _configurationFiles.GetContents("motd.txt");
				int numPlayers = GenOnlineService.WebSocketManager.GetNumberOfUsersOnline();
				result.MOTD = String.Format(template, numPlayers);
			}
			catch (FormatException)
			{
				return result;
			}

			return result;
		}
	}
}
