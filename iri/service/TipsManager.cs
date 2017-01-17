using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Threading;

using slf4net;

// 1.1.2.3

namespace com.iota.iri.service
{
    using Bundle = com.iota.iri.Bundle;
    using Milestone = com.iota.iri.Milestone;
    using Snapshot = com.iota.iri.Snapshot;
    using Hash = com.iota.iri.model.Hash;
    using Transaction = com.iota.iri.model.Transaction;
    using Storage = com.iota.iri.service.storage.Storage;
    using StorageApprovers = com.iota.iri.service.storage.StorageApprovers;
    using StorageScratchpad = com.iota.iri.service.storage.StorageScratchpad;
    using StorageTransactions = com.iota.iri.service.storage.StorageTransactions;

    public class TipsManager
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(TipsManager));

        private volatile bool shuttingDown;

        public virtual void init()
		{

			(new Thread(() => { while (!shuttingDown) { try { int previousLatestMilestoneIndex = Milestone.latestMilestoneIndex; int previousSolidSubtangleLatestMilestoneIndex = Milestone.latestSolidSubtangleMilestoneIndex; Milestone.updateLatestMilestone(); Milestone.updateLatestSolidSubtangleMilestone(); if (previousLatestMilestoneIndex != Milestone.latestMilestoneIndex) { log.Info("Latest milestone has changed from #" + previousLatestMilestoneIndex + " to #" + Milestone.latestMilestoneIndex); } if (previousSolidSubtangleLatestMilestoneIndex != Milestone.latestSolidSubtangleMilestoneIndex) { log.Info("Latest SOLID SUBTANGLE milestone has changed from #" + previousSolidSubtangleLatestMilestoneIndex + " to #" + Milestone.latestSolidSubtangleMilestoneIndex); } Thread.Sleep(5000); } catch (Exception e) { log.error("Error during TipsManager Milestone updating", e); } } })).start();
		}

        public virtual void shutDown()
        {
            shuttingDown = true;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        internal static Hash transactionToApprove(Hash extraTip, int depth)
		{

			Hash preferableMilestone = Milestone.latestSolidSubtangleMilestone;

			lock (StorageScratchpad.instance().AnalyzedTransactionsFlags)
			{

				StorageScratchpad.instance().clearAnalyzedTransactionsFlags();

				IDictionary<Hash, long?> state = new Dictionary<Hash, long?>(Snapshot.initialState);

				{
					int numberOfAnalyzedTransactions = 0;

//ORIGINAL LINE: final Queue<Long> nonAnalyzedTransactions = new LinkedList<>(Collections.singleton(StorageTransactions.instance().transactionPointer((extraTip == null ? preferableMilestone : extraTip).bytes())));
					LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>(Collections.singleton(StorageTransactions.instance().transactionPointer((extraTip == null ? preferableMilestone : extraTip).bytes())));
					long? pointer;
					while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
					{

						if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
						{

							numberOfAnalyzedTransactions++;

							Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);
							if (transaction.type == Storage.PREFILLED_SLOT)
							{
								return null;
							}
							else
							{

								if (transaction.currentIndex == 0)
								{

									bool validBundle = false;

									Bundle bundle = new Bundle(transaction.bundle);
									foreach (IList<Transaction> bundleTransactions in bundle.Transactions)
									{

										if (bundleTransactions.get(0).pointer == transaction.pointer)
										{

											validBundle = true;

											bundleTransactions.stream().filter(bundleTransaction -> bundleTransaction.value != 0).forEach(bundleTransaction -> { final Hash address = new Hash(bundleTransaction.address); final long? value = state[address]; state.Add(address, value == null ? bundleTransaction.value : (value + bundleTransaction.value)); });
											break;
										}
									}

									if (!validBundle)
									{
										return null;
									}
								}

								nonAnalyzedTransactions.AddLast(transaction.trunkTransactionPointer);
								nonAnalyzedTransactions.AddLast(transaction.branchTransactionPointer);
							}
						}
					}

					log.Info("Confirmed transactions = {}", numberOfAnalyzedTransactions);
				}

				IEnumerator<KeyValuePair<Hash, long?>> stateIterator = state.GetEnumerator();
				while (stateIterator.MoveNext())
				{

					KeyValuePair<Hash, long?> entry = stateIterator.Current;
					if (entry.Value <= 0)
					{

						if (entry.Value < 0)
						{
							log.Error("Ledger inconsistency detected");
							return null;
						}
						stateIterator.remove();
					}
				}

				StorageScratchpad.instance().saveAnalyzedTransactionsFlags();
				StorageScratchpad.instance().clearAnalyzedTransactionsFlags();

				HashSet<Hash> tailsToAnalyze = new HashSet<Hash>();

				Hash tip = preferableMilestone;
				if (extraTip != null)
				{

					Transaction transaction = StorageTransactions.instance().loadTransaction(StorageTransactions.instance().transactionPointer(tip.bytes()));
					while (depth-- > 0 && !tip.Equals(Hash.NULL_HASH))
					{

						tip = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
						do
						{
							transaction = StorageTransactions.instance().loadTransaction(transaction.trunkTransactionPointer);
						} while (transaction.currentIndex != 0);
					}
				}
				LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>(Collections.singleton(StorageTransactions.instance().transactionPointer(tip.bytes())));
				long? pointer;
				while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
				{

					if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
					{

						Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);

						if (transaction.currentIndex == 0)
						{
							tailsToAnalyze.add(new Hash(transaction.hash, 0, Transaction.HASH_SIZE));
						}

						StorageApprovers.instance().approveeTransactions(StorageApprovers.instance().approveePointer(transaction.hash)).forEach(nonAnalyzedTransactions::offer);
					}
				}

				if (extraTip != null)
				{

					StorageScratchpad.instance().loadAnalyzedTransactionsFlags();

					IEnumerator<Hash> tailsToAnalyzeIterator = tailsToAnalyze.GetEnumerator();
					while (tailsToAnalyzeIterator.MoveNext())
					{

						Transaction tail = StorageTransactions.instance().loadTransaction(tailsToAnalyzeIterator.Current.bytes());
						if (StorageScratchpad.instance().analyzedTransactionFlag(tail.pointer))
						{
							tailsToAnalyzeIterator.remove();
						}
					}
				}

				log.Info(tailsToAnalyze.size() + " tails need to be analyzed");
				Hash bestTip = preferableMilestone;
				int bestRating = 0;
				foreach (Hash tail in tailsToAnalyze)
				{

					StorageScratchpad.instance().loadAnalyzedTransactionsFlags();

					Set<Hash> extraTransactions = new HashSet<>();

					nonAnalyzedTransactions.Clear();
					nonAnalyzedTransactions.AddLast(StorageTransactions.instance().transactionPointer(tail.bytes()));
					while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
					{

						if (StorageScratchpad.instance().AnalyzedTransactionFlag = pointer)
						{

							Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);
							if (transaction.type == Storage.PREFILLED_SLOT)
							{
								extraTransactions = null;
								break;
							}
							else
							{
								extraTransactions.add(new Hash(transaction.hash, 0, Transaction.HASH_SIZE));
								nonAnalyzedTransactions.AddLast(transaction.trunkTransactionPointer);
								nonAnalyzedTransactions.AddLast(transaction.branchTransactionPointer);
							}
						}
					}

					if (extraTransactions != null)
					{

						HashSet<Hash> extraTransactionsCopy = new HashSet<Hash>(extraTransactions);

						foreach (Hash extraTransaction in extraTransactions)
						{

							Transaction transaction = StorageTransactions.instance().loadTransaction(extraTransaction.bytes());
							if (transaction != null && transaction.currentIndex == 0)
							{

								Bundle bundle = new Bundle(transaction.bundle);
								foreach (IList<Transaction> bundleTransactions in bundle.Transactions)
								{

									if (Array.Equals(bundleTransactions.get(0).hash, transaction.hash))
									{

										foreach (Transaction bundleTransaction in bundleTransactions)
										{

											if (!extraTransactionsCopy.remove(new Hash(bundleTransaction.hash, 0, Transaction.HASH_SIZE)))
											{
												extraTransactionsCopy = null;
												break;
											}
										}
										break;
									}
								}
							}
							if (extraTransactionsCopy == null)
							{
								break;
							}
						}

						if (extraTransactionsCopy != null && extraTransactionsCopy.Empty)
						{

							IDictionary<Hash, long?> stateCopy = new Dictionary<>(state);

							foreach (Hash extraTransaction in extraTransactions)
							{

								Transaction transaction = StorageTransactions.instance().loadTransaction(extraTransaction.bytes());
								if (transaction.value != 0)
								{
									Hash address = new Hash(transaction.address);
									long? value = stateCopy[address];
									stateCopy.Add(address, value == null ? transaction.value : (value + transaction.value));
								}
							}

							foreach (long value in stateCopy.Values)
							{
								if (value < 0)
								{
									extraTransactions = null;
									break;
								}
							}

							if (extraTransactions != null)
							{
								if (extraTransactions.size() > bestRating)
								{
									bestTip = tail;
									bestRating = extraTransactions.size();
								}
							}
						}
					}
				}
				log.Info("{} extra transactions approved", bestRating);
				return bestTip;
			}
		}

        private static TipsManager _instance = new TipsManager();

        private TipsManager()
        {
        }

        public static TipsManager instance()
        {
            return _instance;
        }
    }

}