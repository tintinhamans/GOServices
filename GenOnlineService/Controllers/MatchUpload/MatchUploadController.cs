/*
**    GeneralsOnline Game Services - Backend Services for Command & Conquer Generals Online: Zero Hour
**    Copyright (C) 2025  GeneralsOnline Development Team
**
**    This program is free software: you can redistribute it and/or modify
**    it under the terms of the GNU Affero General Public License as
**    published by the Free Software Foundation, either version 3 of the
**    License, or (at your option) any later version.
*/

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace GenOnlineService.Controllers
{
	public sealed class MatchUploadCompletionRequest
	{
		public string upload_confirmation_token { get; set; } = String.Empty;
		public List<string>? upload_confirmation_tokens { get; set; }
	}

	public sealed class MatchUploadCompletionResult
	{
		public bool success { get; set; }
		public int confirmed_uploads { get; set; }
		public IReadOnlyList<S3CredentialManager.UploadConfirmationResult> uploads { get; set; } = Array.Empty<S3CredentialManager.UploadConfirmationResult>();
	}

	[ApiController]
	[Authorize(Roles = "GameClient")]
	[Route("env/{environment}/contract/{contract_version}/MatchUpload")]
	public sealed class MatchUploadController : ControllerBase
	{
		[HttpPost("Complete")]
		public async Task<ActionResult<MatchUploadCompletionResult>> Complete([FromBody] MatchUploadCompletionRequest request)
		{
			Int64 userID = TokenHelper.GetUserID(this);
			EUserSessionType sessionType = TokenHelper.GetSessionType(this);
			if (userID == -1 || !SessionHelpers.SessionTypeHasAccessTo(sessionType, ESessionAccessType.Gameplay))
			{
				return StatusCode((int)HttpStatusCode.Forbidden, new MatchUploadCompletionResult());
			}

			var confirmationTokens = (request.upload_confirmation_tokens ?? new List<string>())
				.Where(token => !String.IsNullOrWhiteSpace(token))
				.ToList();
			if (!String.IsNullOrWhiteSpace(request.upload_confirmation_token))
			{
				confirmationTokens.Add(request.upload_confirmation_token);
			}
			confirmationTokens = confirmationTokens.Distinct(StringComparer.Ordinal).ToList();

			if (confirmationTokens.Count == 0 || confirmationTokens.Count > 16)
			{
				return BadRequest(new MatchUploadCompletionResult());
			}

			IReadOnlyList<S3CredentialManager.UploadConfirmationResult> results =
				await S3CredentialManager.ConfirmUploads(confirmationTokens, userID);
			int confirmedUploads = results.Count(result => result.Success);

			return Ok(new MatchUploadCompletionResult
			{
				success = confirmedUploads == confirmationTokens.Count,
				confirmed_uploads = confirmedUploads,
				uploads = results
			});
		}
	}
}
