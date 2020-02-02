using NodaTime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SIE.Matching
{
	public class TransactionMatched
	{
		public int AccountId { get; set; }
		public decimal Amount { get; set; }
		public string CompanyName { get; set; } = string.Empty;
		public string ExtendedName { get; set; } = string.Empty;
		public LocalDate? DateRegistered { get; set; }
		public LocalDate? DateFinalized { get; set; }
		public int SNRegistered { get; set; }
		public int SNFinalized { get; set; }

		public override string ToString()
		{
			return $"{AccountId}\t{Amount}\t{DateRegistered?.ToSimpleDateString()}\t{DateFinalized?.ToSimpleDateString()}\t{CompanyName}\t{ExtendedName}";
		}

		public static List<int> RequiredAccountIds = new List<int> { 24400, 15200 }; //TODO: universal? Should be setting?

		public static List<TransactionMatched> FromVoucherMatches(MatchSLRResult matchResult, IEnumerable<int> requiredAccounts)
		{
			var result = matchResult.Matches.SelectMany(o =>
			{
				return o.SLR.TransactionsNonAdminOrCorrections.Select(tx =>
				{
					var name = o.SLR.Transactions.Single(tx2 => requiredAccounts.Contains(tx2.AccountId)).CompanyName;
					return new TransactionMatched
					{
						AccountId = tx.AccountId,
						Amount = tx.Amount,
						ExtendedName = tx.CompanyName != name ? tx.CompanyName : string.Empty,
						CompanyName = name,
						DateRegistered = o.SLR.Date,
						SNRegistered = o.SLR.SerialNumber,
						DateFinalized = o.Other.Date,
						SNFinalized = o.Other.SerialNumber,
					};
				});
			}).ToList();

			result.AddRange(
				matchResult.NotMatchedOther.SelectMany(o => o.TransactionsNonAdminOrCorrections.Select(tx => new TransactionMatched
				{
					AccountId = tx.AccountId,
					Amount = tx.Amount,
					CompanyName = tx.CompanyName,
					DateFinalized = tx.Date,
					SNFinalized = o.SerialNumber
				}))
			);

			result.AddRange(
				matchResult.NotMatchedSLR.SelectMany(o => o.TransactionsNonAdminOrCorrections.Select(tx => new TransactionMatched
				{
					AccountId = tx.AccountId,
					Amount = tx.Amount,
					CompanyName = tx.CompanyName,
					DateRegistered = tx.Date,
					SNRegistered = o.SerialNumber
				}))
			);

			return result;
		}
	}
}
