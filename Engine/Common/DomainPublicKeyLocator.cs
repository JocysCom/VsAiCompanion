using DnsClient;
using MimeKit.Cryptography;
using Org.BouncyCastle.Crypto;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace JocysCom.VS.AiCompanion.Engine
{
	public class DomainPublicKeyLocator : DkimPublicKeyLocatorBase
	{

		private readonly LookupClient _lookupClient;
		private readonly Dictionary<string, AsymmetricKeyParameter> _cache = new Dictionary<string, AsymmetricKeyParameter>();

		public DomainPublicKeyLocator()
		{
			var options = new LookupClientOptions(
				System.Net.IPAddress.Parse("8.8.8.8"),
				System.Net.IPAddress.Parse("8.8.4.4")
			);
			options.UseCache = true;
			options.Retries = 4;
			_lookupClient = new LookupClient(options);
		}

		private AsymmetricKeyParameter DnsLookup(string domain, string selector, CancellationToken cancellationToken)
		{
			var query = $"{selector}._domainkey.{domain}";
			if (_cache.TryGetValue(query, out var pubkey))
				return pubkey;

			// Perform the DNS lookup for the TXT record
			var result = _lookupClient.Query(query, QueryType.TXT);
			var builder = new StringBuilder();
			foreach (var record in result.AllRecords)
			{
				if (record is DnsClient.Protocol.TxtRecord txtRecord)
				{
					foreach (var text in txtRecord.Text)
					{
						builder.Append(text);
					}
				}
			}
			var txt = builder.ToString();
			pubkey = GetPublicKey(txt);
			_cache.Add(query, pubkey);
			return pubkey;
		}

		public override AsymmetricKeyParameter LocatePublicKey(string methods, string domain, string selector, CancellationToken cancellationToken = default)
		{
			var methodList = methods.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
			foreach (var method in methodList)
			{
				if (method == "dns/txt")
					return DnsLookup(domain, selector, cancellationToken);
			}
			throw new NotSupportedException($"{methods} does not include any supported lookup methods.");
		}

		public override Task<AsymmetricKeyParameter> LocatePublicKeyAsync(string methods, string domain, string selector, CancellationToken cancellationToken = default)
		{
			// Example implementation
			return Task.Run(() => LocatePublicKey(methods, domain, selector, cancellationToken), cancellationToken);
		}

	}
}
