﻿using Azure.Core;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine.Security
{
	public class AccessTokenCredential : TokenCredential
	{
		private readonly string _token;

		public AccessTokenCredential(string token)
		{
			_token = token ?? throw new ArgumentNullException(nameof(token));
		}

		private AccessToken _GetAccessToken()
			=> new AccessToken(_token, DateTimeOffset.MaxValue);

		public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> _GetAccessToken();

		public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
			=> new ValueTask<AccessToken>(_GetAccessToken());
	}
}
