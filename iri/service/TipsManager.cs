using System;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Threading;
using iri.utils;
using slf4net;

namespace com.iota.iri.service
{

	using Bundle = com.iota.iri.Bundle;
	using Milestone = com.iota.iri.Milestone;
	using Snapshot = com.iota.iri.Snapshot;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;


	public class TipsManager
	{

        private static readonly slf4net.ILogger log = LoggerFactory.GetLogger(typeof(TipsManager));

		private static bool shuttingDown;

		public static void launch()
		{

			(new Thread(() => { while (!shuttingDown) { try { int previousLatestMilestoneIndex = Milestone.latestMilestoneIndex; int previousSolidSubtangleLatestMilestoneIndex = Milestone.latestSolidSubtangleMilestoneIndex; Milestone.updateLatestMilestone(); Milestone.updateLatestSolidSubtangleMilestone(); if (previousLatestMilestoneIndex != Milestone.latestMilestoneIndex) { log.Info("Latest milestone has changed from #" + previousLatestMilestoneIndex + " to #" + Milestone.latestMilestoneIndex); } if (previousSolidSubtangleLatestMilestoneIndex != Milestone.latestSolidSubtangleMilestoneIndex) { log.Info("Latest SOLID SUBTANGLE milestone has changed from #" + previousSolidSubtangleLatestMilestoneIndex + " to #" + Milestone.latestSolidSubtangleMilestoneIndex); } Thread.Sleep(5000); } catch (Exception e) { log.Error("Error during TipsManager Milestone updating", e); } } })).Start();
		}

		public static void shutDown()
		{
			shuttingDown = true;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		internal static Hash transactionToApprove(Hash extraTip, int depth)
		{

			Hash preferableMilestone = Milestone.latestSolidSubtangleMilestone;

			lock (Storage.analyzedTransactionsFlags)
			{

				Storage.clearAnalyzedTransactionsFlags();

				IDictionary<Hash, long?> state = new Dictionary<Hash, long?>(Snapshot.initialState);

				{
					int numberOfAnalyzedTransactions = 0;

                    List<long?> nonAnalyzedTransactionsScope1 = new List<long?> { Storage.transactionPointer((extraTip == null ? preferableMilestone : extraTip).Sbytes()) };

					long? longPointer1;
                    while ((longPointer1 = nonAnalyzedTransactionsScope1.Poll()) != null)
					{

						if (Storage.setAnalyzedTransactionFlag((long)longPointer1))
						{

							numberOfAnalyzedTransactions++;

							Transaction transaction = Storage.loadTransaction((long)longPointer1);
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
									foreach (IList<Transaction> bundleTransactions in bundle.transactions)
									{

										if (bundleTransactions[0].pointer == transaction.pointer)
										{

											validBundle = true;

											foreach (Transaction bundleTransaction in bundleTransactions)
											{

												if (bundleTransaction.value != 0)
												{
													Hash address = new Hash(bundleTransaction.address);

													long? value = state[address];
													state.Add(address, value == null ? bundleTransaction.value : (value + bundleTransaction.value));
												}
											}

											break;
										}
									}

									if (!validBundle)
									{
										return null;
									}
								}

                                nonAnalyzedTransactionsScope1.Add(transaction.trunkTransactionPointer);
                                nonAnalyzedTransactionsScope1.Add(transaction.branchTransactionPointer);
							}
						}
					}

					log.Info("Confirmed transactions = {}", numberOfAnalyzedTransactions);
				}

                foreach (var currentItem in state)
				{

                    if (currentItem.Value <= 0)
					{

                        if (currentItem.Value < 0)
						{

							Console.WriteLine("Ledger inconsistency detected");

							return null;
						}

					    state.Remove(currentItem.Key);
					}
				}

				Storage.saveAnalyzedTransactionsFlags();
				Storage.clearAnalyzedTransactionsFlags();

				HashSet<Hash> tailsToAnalyze = new HashSet<Hash>();

				Hash tip = preferableMilestone;
				if (extraTip != null)
				{

					Transaction transaction = Storage.loadTransaction(Storage.transactionPointer(tip.Sbytes()));
					while (depth-- > 0 && !tip.Equals(Hash.NULL_HASH))
					{

						tip = new Hash(transaction.hash, 0, Transaction.HASH_SIZE);
						do
						{

							transaction = Storage.loadTransaction(transaction.trunkTransactionPointer);

						} while (transaction.currentIndex != 0);
					}
				}

                List<long?> nonAnalyzedTransactionsScope2 = new List<long?> { Storage.transactionPointer(tip.Sbytes()) };

				long? longPointer2;
                while ((longPointer2 = nonAnalyzedTransactionsScope2.Poll()) != null)
				{

					if (Storage.setAnalyzedTransactionFlag((long)longPointer2))
					{

						Transaction transaction = Storage.loadTransaction((long)longPointer2);

						if (transaction.currentIndex == 0)
						{
							tailsToAnalyze.Add(new Hash(transaction.hash, 0, Transaction.HASH_SIZE));
						}

						foreach (long? approverPointer in Storage.approveeTransactions(Storage.approveePointer(transaction.hash)))
						{
                            nonAnalyzedTransactionsScope2.Add(approverPointer);
						}
					}
				}

				if (extraTip != null)
				{
					Storage.loadAnalyzedTransactionsFlags();

                    foreach (var currentItem in tailsToAnalyze)
					{

                        Transaction tail = Storage.loadTransaction(currentItem.Sbytes());
						if (Storage.analyzedTransactionFlag(tail.pointer))
						{

                            tailsToAnalyze.Remove(currentItem);
						}
					}
				}

				log.Info(tailsToAnalyze.Count + " tails need to be analyzed");
				Hash bestTip = preferableMilestone;
				int bestRating = 0;
				foreach (Hash tail in tailsToAnalyze)
				{

					Storage.loadAnalyzedTransactionsFlags();

					HashSet<Hash> extraTransactions = new HashSet<Hash>();

                    nonAnalyzedTransactionsScope2.Clear();
                    nonAnalyzedTransactionsScope2.Add(Storage.transactionPointer(tail.Sbytes()));
                    while ((longPointer2 = nonAnalyzedTransactionsScope2.Poll()) != null)
					{

						if (Storage.setAnalyzedTransactionFlag((long)longPointer2))
						{
							Transaction transaction = Storage.loadTransaction((long)longPointer2);

							if (transaction.type == Storage.PREFILLED_SLOT)
							{
								extraTransactions = null;
								break;

							}
							else
							{

								extraTransactions.Add(new Hash(transaction.hash, 0, Transaction.HASH_SIZE));

                                nonAnalyzedTransactionsScope2.Add(transaction.trunkTransactionPointer);
                                nonAnalyzedTransactionsScope2.Add(transaction.branchTransactionPointer);
							}
						}
					}

					if (extraTransactions != null)
					{

						HashSet<Hash> extraTransactionsCopy = new HashSet<Hash>(extraTransactions);

						foreach (Hash extraTransaction in extraTransactions)
						{

							Transaction transaction = Storage.loadTransaction(extraTransaction.Sbytes());

							if (transaction != null && transaction.currentIndex == 0)
							{

								Bundle bundle = new Bundle(transaction.bundle);

								foreach (IList<Transaction> bundleTransactions in bundle.transactions)
								{

									if (Array.Equals(bundleTransactions[0].hash, transaction.hash))
									{

										foreach (Transaction bundleTransaction in bundleTransactions)
										{

											if (!extraTransactionsCopy.Remove(new Hash(bundleTransaction.hash, 0, Transaction.HASH_SIZE)))
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

						if (extraTransactionsCopy != null && extraTransactionsCopy.Count == 0)
						{

							IDictionary<Hash, long?> stateCopy = new Dictionary<Hash, long?>(state);

							foreach (Hash extraTransaction in extraTransactions)
							{

								Transaction transaction = Storage.loadTransaction(extraTransaction.Sbytes());

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

								if (extraTransactions.Count > bestRating)
								{
									bestTip = tail;
									bestRating = extraTransactions.Count;
								}
							}
						}
					}
				}
				log.Info(bestRating + " extra transactions approved");
				return bestTip;
			}
		}
	}

}