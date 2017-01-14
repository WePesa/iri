using System;
using System.Collections.Generic;
using iri.utils;

namespace com.iota.iri
{

	using Curl = com.iota.iri.hash.Curl;
	using ISS = com.iota.iri.hash.ISS;
	using Transaction = com.iota.iri.model.Transaction;
	using Storage = com.iota.iri.service.Storage;
	using Converter = com.iota.iri.utils.Converter;


	public class Bundle
	{

        public List<List<Transaction>> transactions = new List<List<Transaction>>();

		public Bundle(sbyte[] bundle)
		{

			long bundlePointer = Storage.bundlePointer(bundle);
			if (bundlePointer != 0)
			{

				IDictionary<long?, Transaction> bundleTransactions = new Dictionary<long?, Transaction>();
				foreach (long transactionPointer in Storage.bundleTransactions(bundlePointer))
				{
					bundleTransactions.Add(transactionPointer, Storage.loadTransaction(transactionPointer));
				}

                foreach (KeyValuePair<long?, Transaction> item in bundleTransactions)
                {
                    var transaction = item.Value;

					if (transaction.currentIndex == 0 && transaction.Validity() >= 0)
					{

                        List<Transaction> instanceTransactions = new List<Transaction>();

						long lastIndex = transaction.lastIndex;
						long bundleValue = 0;
						int i = 0;
					MAIN_LOOP:
						while (true)
						{

							instanceTransactions.Add(transaction);

							if (transaction.currentIndex != i || transaction.lastIndex != lastIndex || ((bundleValue += transaction.value) < -Transaction.SUPPLY || bundleValue > Transaction.SUPPLY))
							{

								Storage.setTransactionValidity(instanceTransactions[0].pointer, -1);

								break;
							}

							if (i++ == lastIndex) // It's supposed to become -3812798742493 after 3812798742493 and to go "down" to -1 but we hope that noone will create such long bundles
							{

								if (bundleValue == 0)
								{

									if (instanceTransactions[0].Validity() == 0)
									{
										Curl bundleHash = new Curl();
										foreach (Transaction transaction2 in instanceTransactions)
										{

											bundleHash.absorb(transaction2.Trits(), Transaction.ESSENCE_TRINARY_OFFSET, Transaction.ESSENCE_TRINARY_SIZE);
										}

										int[] bundleHashTrits = new int[Transaction.BUNDLE_TRINARY_SIZE];
										bundleHash.squeeze(bundleHashTrits, 0, bundleHashTrits.Length);
										if (Array.Equals(Converter.bytes(bundleHashTrits, 0, Transaction.BUNDLE_TRINARY_SIZE), instanceTransactions[0].bundle))
										{

											int[] normalizedBundle = ISS.normalizedBundle(bundleHashTrits);

											for (int j = 0; j < instanceTransactions.Count;)
											{

												transaction = instanceTransactions[j];
												if (transaction.value < 0)
												{

													Curl address = new Curl();
													int offset = 0;
													do
													{

														address.absorb(ISS.digest(Arrays.copyOfRange(normalizedBundle, offset, offset = (offset + ISS.NUMBER_OF_FRAGMENT_CHUNKS) % (Curl.HASH_LENGTH / Converter.NUMBER_OF_TRITS_IN_A_TRYTE)), Arrays.copyOfRange(instanceTransactions[j].Trits(), Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET, Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET + Transaction.SIGNATURE_MESSAGE_FRAGMENT_TRINARY_SIZE)), 0, Curl.HASH_LENGTH);

													} while (++j < instanceTransactions.Count && Array.Equals(instanceTransactions[j].address, transaction.address) && instanceTransactions[j].value == 0);

													int[] addressTrits = new int[Transaction.ADDRESS_TRINARY_SIZE];
													address.squeeze(addressTrits, 0, addressTrits.Length);
													if (!Array.Equals(Converter.bytes(addressTrits, 0, Transaction.ADDRESS_TRINARY_SIZE), transaction.address))
													{

														Storage.setTransactionValidity(instanceTransactions[0].pointer, -1);

														goto MAIN_LOOP;
													}

												}
												else
												{
													j++;
												}
											}

											Storage.setTransactionValidity(instanceTransactions[0].pointer, 1);
											transactions.Add(instanceTransactions);
										}
										else
										{
											Storage.setTransactionValidity(instanceTransactions[0].pointer, -1);
										}
									}
									else
									{
										transactions.Add(instanceTransactions);
									}
								}
								else
								{
									Storage.setTransactionValidity(instanceTransactions[0].pointer, -1);
								}

								break;

							}
							else
							{
								transaction = bundleTransactions[transaction.trunkTransactionPointer];
								if (transaction == null)
								{
									break;
								}
							}
						}
					}
				}
			}
		}
	}

}