using System.Collections.Generic;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri
{


	using Curl = com.iota.iri.hash.Curl;
	using ISS = com.iota.iri.hash.ISS;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;
	using Storage = com.iota.iri.service.storage.Storage;
	using StorageAddresses = com.iota.iri.service.storage.StorageAddresses;
	using StorageScratchpad = com.iota.iri.service.storage.StorageScratchpad;
	using AbstractStorage = com.iota.iri.service.storage.AbstractStorage;
	using StorageTransactions = com.iota.iri.service.storage.StorageTransactions;
	using Converter = com.iota.iri.utils.Converter;

	public class Milestone
	{

		public static readonly Hash COORDINATOR = new Hash("KPWCHICGJZXKE9GSUDXZYUAPLHAKAHYHDXNPHENTERYMMBQOPSQIDENXKLKCEYCPVTZQLEEJVYJZV9BWU");

		public static Hash latestMilestone = Hash.NULL_HASH;
		public static Hash latestSolidSubtangleMilestone = Hash.NULL_HASH;

		public const int MILESTONE_START_INDEX = 13250;

		public static int latestMilestoneIndex = MILESTONE_START_INDEX;
		public static int latestSolidSubtangleMilestoneIndex = MILESTONE_START_INDEX;

		private static readonly Set<long?> analyzedMilestoneCandidates = new HashSet<>();
		private static readonly IDictionary<int?, Hash> milestones = new ConcurrentHashMap<>();

		public static void updateLatestMilestone() // refactor
		{

			foreach (long? pointer in StorageAddresses.instance().addressesOf(COORDINATOR))
			{

				if (analyzedMilestoneCandidates.add(pointer))
				{

					Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);
					if (transaction.currentIndex == 0)
					{

						int index = (int) Converter.longValue(transaction.trits(), Transaction.TAG_TRINARY_OFFSET, 15);
						if (index > latestMilestoneIndex)
						{

							Bundle bundle = new Bundle(transaction.bundle);
							foreach (IList<Transaction> bundleTransactions in bundle.Transactions)
							{

								if (bundleTransactions.get(0).pointer == transaction.pointer)
								{

									Transaction transaction2 = StorageTransactions.instance().loadTransaction(transaction.trunkTransactionPointer);
									if (transaction2.type == AbstractStorage.FILLED_SLOT && transaction.branchTransactionPointer == transaction2.trunkTransactionPointer)
									{

										int[] trunkTransactionTrits = new int[Transaction.TRUNK_TRANSACTION_TRINARY_SIZE];
										Converter.getTrits(transaction.trunkTransaction, trunkTransactionTrits);
										int[] signatureFragmentTrits = Arrays.copyOfRange(transaction.trits(), Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET, Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET + Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_SIZE);

										int[] hash = ISS.address(ISS.digest(Arrays.copyOf(ISS.normalizedBundle(trunkTransactionTrits), ISS.NUMBER_OF_FRAGMENT_CHUNKS), signatureFragmentTrits));

										int indexCopy = index;
										for (int i = 0; i < 20; i++)
										{

											Curl curl = new Curl();
											if ((indexCopy & 1) == 0)
											{
												curl.absorb(hash, 0, hash.Length);
												curl.absorb(transaction2.trits(), i * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
											}
											else
											{
												curl.absorb(transaction2.trits(), i * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
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

					lock (StorageScratchpad.instance().AnalyzedTransactionsFlags)
					{

						StorageScratchpad.instance().clearAnalyzedTransactionsFlags();

						LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>();
						nonAnalyzedTransactions.AddLast(StorageTransactions.instance().transactionPointer(milestone.bytes()));
						long? pointer;
						while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
						{

							if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
							{

								Transaction transaction2 = StorageTransactions.instance().loadTransaction(pointer);
								if (transaction2.type == AbstractStorage.PREFILLED_SLOT)
								{
									solid = false;
									break;

								}
								else
								{
									nonAnalyzedTransactions.AddLast(transaction2.trunkTransactionPointer);
									nonAnalyzedTransactions.AddLast(transaction2.branchTransactionPointer);
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