using System;
using System.Collections.Generic;

using System.IO;
using System.IO.MemoryMappedFiles;
using iri.utils;
using slf4net;

// 1.1.2.3

namespace com.iota.iri.service.storage
{


	// using Logger = org.slf4j.Logger;
	// using LoggerFactory = org.slf4j.LoggerFactory;

	using Milestone = com.iota.iri.Milestone;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;

	public class StorageScratchpad : AbstractStorage
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageScratchpad));

        private static readonly StorageScratchpad _instance = new StorageScratchpad();
		private const string SCRATCHPAD_FILE_NAME = "scratchpad.iri";

        private MemoryMappedViewStream transactionsToRequest;
        private static MemoryMappedViewStream analyzedTransactionsFlags, analyzedTransactionsFlagsCopy;

		private readonly sbyte[] transactionToRequest = new sbyte[Transaction.HASH_SIZE];
		private readonly object transactionToRequestMonitor = new object();
		private int previousNumberOfTransactions;

		public volatile int numberOfTransactionsToRequest;

        private MemoryMappedFile scratchpadChannel = null;


		public override void init()
		{
		    try
		    {
                MemoryMappedFile scratchpadChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(SCRATCHPAD_FILE_NAME), FileMode.OpenOrCreate, "scratchpadMap", TRANSACTIONS_TO_REQUEST_SIZE + ANALYZED_TRANSACTIONS_FLAGS_SIZE + ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE);
                transactionsToRequest = scratchpadChannel.CreateViewStream(TRANSACTIONS_TO_REQUEST_OFFSET, TRANSACTIONS_TO_REQUEST_SIZE);
                analyzedTransactionsFlags = scratchpadChannel.CreateViewStream(ANALYZED_TRANSACTIONS_FLAGS_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_SIZE);
                analyzedTransactionsFlagsCopy = scratchpadChannel.CreateViewStream(ANALYZED_TRANSACTIONS_FLAGS_COPY_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE);
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		public override void shutdown()
		{
			try
			{
				scratchpadChannel.Dispose();
			}
			catch (Exception e)
			{
				log.Error("Shutting down Storage Scratchpad error: ", e);
			}
		}

		public virtual void TransactionToRequest(sbyte[] buffer, int offset)
		{

			lock (transactionToRequestMonitor)
			{

				if (numberOfTransactionsToRequest == 0)
				{

					long beginningTime = DateTimeExtensions.currentTimeMillis();

					lock (analyzedTransactionsFlags)
					{

						clearAnalyzedTransactionsFlags();

                        List<long?> nonAnalyzedTransactions = new List<long?> { StorageTransactions.instance().transactionPointer(Milestone.latestMilestone.Sbytes()) };

						long? pointer;
                        while ((pointer = nonAnalyzedTransactions.Poll()) != null)
						{

							if (setAnalyzedTransactionFlag((long)pointer))
							{

								Transaction transaction = StorageTransactions.instance().loadTransaction((long)pointer);
								if (transaction.type == Storage.PREFILLED_SLOT)
								{

									// ((ByteBuffer) transactionsToRequest.position(numberOfTransactionsToRequest++ * Transaction.HASH_SIZE)).put(transaction.hash); // Only 2'917'776 hashes can be stored this way without overflowing the buffer, we assume that nodes will never need to store that many hashes, so we don't need to cap "numberOfTransactionsToRequest"
                                    transactionsToRequest.Position = numberOfTransactionsToRequest++ * Transaction.HASH_SIZE; // Only 2'917'776 hashes can be stored this way without overflowing the buffer, we assume that nodes will never need to store that many hashes, so we don't need to cap "numberOfTransactionsToRequest"
                                    transactionsToRequest.Write((byte[])(Array)transaction.hash, 0, transaction.hash.Length);
								}
								else
								{
									nonAnalyzedTransactions.Add(transaction.trunkTransactionPointer);
									nonAnalyzedTransactions.Add(transaction.branchTransactionPointer);
								}
							}
						}
					}

                    long transactionsNextPointer = StorageTransactions.TransactionsNextPointer;
                    log.Info("Transactions to request = {0}", numberOfTransactionsToRequest + " / " + (transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE + " (" + (DateTimeExtensions.currentTimeMillis() - beginningTime) + " ms / " + (numberOfTransactionsToRequest == 0 ? 0 : (previousNumberOfTransactions == 0 ? 0 : (((transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE - previousNumberOfTransactions) * 100) / numberOfTransactionsToRequest)) + "%)");
					previousNumberOfTransactions = (int)((transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE);
				}

				if (numberOfTransactionsToRequest == 0)
				{
					Array.Copy(Hash.NULL_HASH.Sbytes(), 0, buffer, offset, Transaction.HASH_SIZE);
				}
				else
				{
					// ((ByteBuffer) transactionsToRequest.position(--numberOfTransactionsToRequest * Transaction.HASH_SIZE)).get(transactionToRequest);
                    transactionsToRequest.Position = --numberOfTransactionsToRequest * Transaction.HASH_SIZE;
                    transactionsToRequest.Read((byte[])(Array)transactionToRequest, 0, transactionToRequest.Length);

					Array.Copy(transactionToRequest, 0, buffer, offset, Transaction.HASH_SIZE);
				}
			}
		}

        public static void clearAnalyzedTransactionsFlags()
        {
            analyzedTransactionsFlags.Position = 0;

            for (int i = 0; i < ANALYZED_TRANSACTIONS_FLAGS_SIZE / CELL_SIZE; i++)
            {
                analyzedTransactionsFlags.Write((byte[])(Array)ZEROED_BUFFER, 0, ZEROED_BUFFER.Length);
            }
        }

        public static bool analyzedTransactionFlag(long pointer)
        {

            pointer -= CELLS_OFFSET - SUPER_GROUPS_OFFSET;

            // return (analyzedTransactionsFlags.get((int)(pointer >> (11 + 3))) & (1 << ((pointer >> 11) & 7))) != 0;

            sbyte[] sbyteArray = new sbyte[1];
            analyzedTransactionsFlags.Read((byte[])(Array)sbyteArray, (int)(pointer >> (11 + 3)), 1);
            long result = BitConverter.ToInt64((byte[])(Array)sbyteArray, 0);
            result &= (1 << ((int)(pointer >> 11) & 7));
            return result != 0;

        }

        //public virtual bool AnalyzedTransactionFlag
        //{
        //    set
        //    {
	
        //        value -= CELLS_OFFSET - SUPER_GROUPS_OFFSET;
	
        //        int value = analyzedTransactionsFlags.get((int)(value >> (11 + 3)));
        //        if ((value & (1 << ((value >> 11) & 7))) == 0)
        //        {
        //            analyzedTransactionsFlags.put((int)(value >> (11 + 3)), (sbyte)(value | (1 << ((value >> 11) & 7))));
        //            return true;
        //        }
        //        return false;
        //    }
        //}

        public static void saveAnalyzedTransactionsFlags()
        {
            //analyzedTransactionsFlags.position(0);
            //analyzedTransactionsFlagsCopy.position(0);
            //analyzedTransactionsFlagsCopy.put(analyzedTransactionsFlags);

            analyzedTransactionsFlags.Position = 0;
            analyzedTransactionsFlagsCopy.Position = 0;
            analyzedTransactionsFlags.CopyTo(analyzedTransactionsFlagsCopy);
        }

        public static void loadAnalyzedTransactionsFlags()
        {

            //analyzedTransactionsFlagsCopy.position(0);
            //analyzedTransactionsFlags.position(0);
            //analyzedTransactionsFlags.put(analyzedTransactionsFlagsCopy);

            analyzedTransactionsFlagsCopy.Position = 0;
            analyzedTransactionsFlags.Position = 0;
            analyzedTransactionsFlagsCopy.CopyTo(analyzedTransactionsFlags);
        }

        public virtual MemoryMappedViewStream AnalyzedTransactionsFlags
		{
			get
			{
				return analyzedTransactionsFlags;
			}
		}

        public virtual MemoryMappedViewStream AnalyzedTransactionsFlagsCopy
		{
			get
			{
				return analyzedTransactionsFlagsCopy;
			}
		}

		public virtual int NumberOfTransactionsToRequest
		{
			get
			{
				return numberOfTransactionsToRequest;
			}
		}

		public static StorageScratchpad instance()
		{
            return _instance;
		}

	}

}