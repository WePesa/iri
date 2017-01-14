using System.Collections.Concurrent;
using System.Collections.Generic;
using iri.utils;
using System.Linq;
using System.Collections.Generic;

namespace com.iota.iri
{

	using Curl = com.iota.iri.hash.Curl;
	using ISS = com.iota.iri.hash.ISS;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;
	using Storage = com.iota.iri.service.Storage;
	using Converter = com.iota.iri.utils.Converter;


	public class Milestone
	{

		private static Hash COORDINATOR = new Hash("KPWCHICGJZXKE9GSUDXZYUAPLHAKAHYHDXNPHENTERYMMBQOPSQIDENXKLKCEYCPVTZQLEEJVYJZV9BWU");

		public static Hash latestMilestone = Hash.NULL_HASH, latestSolidSubtangleMilestone = Hash.NULL_HASH;
		public static int latestMilestoneIndex, latestSolidSubtangleMilestoneIndex;

		private static readonly HashSet<long?> analyzedMilestoneCandidates = new HashSet<long?>();
        private static readonly IDictionary<int?, Hash> milestones = new ConcurrentDictionary<int?, Hash>();

		public static void updateLatestMilestone()
		{

			foreach (long? pointer in Storage.addressTransactions(Storage.addressPointer(COORDINATOR.Sbytes())))
			{

				if (analyzedMilestoneCandidates.Add(pointer))
				{

					Transaction transaction = Storage.loadTransaction((long)pointer);
					if (transaction.currentIndex == 0)
					{

						int index = (int) Converter.longValue(transaction.Trits(), Transaction.TAG_TRINARY_OFFSET, 15);
						if (index > latestMilestoneIndex)
						{

							Bundle bundle = new Bundle(transaction.bundle);
							foreach (IList<Transaction> bundleTransactions in bundle.transactions)
							{

								if (bundleTransactions[0].pointer == transaction.pointer)
								{

									Transaction transaction2 = Storage.loadTransaction(transaction.trunkTransactionPointer);
									if (transaction2.type == Storage.FILLED_SLOT && transaction.branchTransactionPointer == transaction2.trunkTransactionPointer)
									{

										int[] trunkTransactionTrits = new int[Transaction.TRUNK_TRANSACTION_TRINARY_SIZE];
										Converter.getTrits(transaction.trunkTransaction, trunkTransactionTrits);

										int[] signatureFragmentTrits = Arrays.copyOfRange(transaction.Trits(), Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET, Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET + Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_SIZE);

										int[] hash = ISS.address(ISS.digest(Arrays.copyOf(ISS.normalizedBundle(trunkTransactionTrits), ISS.NUMBER_OF_FRAGMENT_CHUNKS), signatureFragmentTrits));

										int indexCopy = index;
										for (int i = 0; i < 20; i++)
										{

											Curl curl = new Curl();
											if ((indexCopy & 1) == 0)
											{

												curl.absorb(hash, 0, hash.Length);
												curl.absorb(transaction2.Trits(), i * Curl.HASH_LENGTH, Curl.HASH_LENGTH);

											}
											else
											{

												curl.absorb(transaction2.Trits(), i * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
												curl.absorb(hash, 0, hash.Length);
											}
											curl.squeeze(hash, 0, hash.Length);

											indexCopy >>= 1;
										}

										if ((new Hash(hash)).Equals(COORDINATOR))
										{

											latestMilestone = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
											latestMilestoneIndex = index;

											milestones.Add(latestMilestoneIndex, latestMilestone);
										}
									}

									break;
								}
							}
						}
					}
				}
			}
		}

		public static void updateLatestSolidSubtangleMilestone()
		{

			for (int milestoneIndex = latestMilestoneIndex; milestoneIndex > latestSolidSubtangleMilestoneIndex; milestoneIndex--)
			{

				Hash milestone = milestones[milestoneIndex];
				if (milestone != null)
				{

					bool solid = true;

					lock (Storage.analyzedTransactionsFlags)
					{

						Storage.clearAnalyzedTransactionsFlags();

						List<long?> nonAnalyzedTransactions = new List<long?>();
						nonAnalyzedTransactions.Add(Storage.transactionPointer(milestone.Sbytes()));
						long? pointer;
						while ((pointer = nonAnalyzedTransactions.Poll()) != null)
						{

							if (Storage.setAnalyzedTransactionFlag((long)pointer))
							{

								Transaction transaction2 = Storage.loadTransaction((long)pointer);
								if (transaction2.type == Storage.PREFILLED_SLOT)
								{

									solid = false;

									break;

								}
								else
								{
									nonAnalyzedTransactions.Add(transaction2.trunkTransactionPointer);
									nonAnalyzedTransactions.Add(transaction2.branchTransactionPointer);
								}
							}
						}
					}

					if (solid)
					{

						latestSolidSubtangleMilestone = milestone;
						latestSolidSubtangleMilestoneIndex = milestoneIndex;

						return;
					}
				}
			}
		}
	}

}