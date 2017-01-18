using System;
using System.Collections.Generic;

using System.IO;
using System.IO.MemoryMappedFiles;
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
        private MemoryMappedViewStream analyzedTransactionsFlags, analyzedTransactionsFlagsCopy;

		private readonly sbyte[] transactionToRequest = new sbyte[Transaction.HASH_SIZE];
		private readonly object transactionToRequestMonitor = new object();
		private int previousNumberOfTransactions;

		public volatile int numberOfTransactionsToRequest;

		private FileChannel scratchpadChannel = null;


		public override void init()
		{
		    try
		    {
		        scratchpadChannel = FileChannel.open(Paths.get(SCRATCHPAD_FILE_NAME), StandardOpenOption.CREATE,
		            StandardOpenOption.READ, StandardOpenOption.WRITE);
		        transactionsToRequest = scratchpadChannel.map(FileChannel.MapMode.READ_WRITE, TRANSACTIONS_TO_REQUEST_OFFSET,
		            TRANSACTIONS_TO_REQUEST_SIZE);
		        analyzedTransactionsFlags = scratchpadChannel.map(FileChannel.MapMode.READ_WRITE,
		            ANALYZED_TRANSACTIONS_FLAGS_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_SIZE);
		        analyzedTransactionsFlagsCopy = scratchpadChannel.map(FileChannel.MapMode.READ_WRITE,
		            ANALYZED_TRANSACTIONS_FLAGS_COPY_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE);
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

		public virtual void transactionToRequest(sbyte[] buffer, int offset)
		{

			lock (transactionToRequestMonitor)
			{

				if (numberOfTransactionsToRequest == 0)
				{

					long beginningTime = System.currentTimeMillis();

					lock (analyzedTransactionsFlags)
					{

						clearAnalyzedTransactionsFlags();

						LinkedList<long?> nonAnalyzedTransactions = new LinkedList<>(Collections.singleton(StorageTransactions.instance().transactionPointer(Milestone.latestMilestone.bytes())));

						long? pointer;
						while ((pointer = nonAnalyzedTransactions.RemoveFirst()) != null)
						{

							if (AnalyzedTransactionFlag = pointer)
							{

								Transaction transaction = StorageTransactions.instance().loadTransaction(pointer);
								if (transaction.type == Storage.PREFILLED_SLOT)
								{

									((ByteBuffer) transactionsToRequest.position(numberOfTransactionsToRequest++ * Transaction.HASH_SIZE)).put(transaction.hash); // Only 2'917'776 hashes can be stored this way without overflowing the buffer, we assume that nodes will never need to store that many hashes, so we don't need to cap "numberOfTransactionsToRequest"
								}
								else
								{
									nonAnalyzedTransactions.AddLast(transaction.trunkTransactionPointer);
									nonAnalyzedTransactions.AddLast(transaction.branchTransactionPointer);
								}
							}
						}
					}

					long transactionsNextPointer = StorageTransactions.transactionsNextPointer;
					log.Info("Transactions to request = {}", numberOfTransactionsToRequest + " / " + (transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE + " (" + (System.currentTimeMillis() - beginningTime) + " ms / " + (numberOfTransactionsToRequest == 0 ? 0 : (previousNumberOfTransactions == 0 ? 0 : (((transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE - previousNumberOfTransactions) * 100) / numberOfTransactionsToRequest)) + "%)");
					previousNumberOfTransactions = (int)((transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE);
				}

				if (numberOfTransactionsToRequest == 0)
				{
					Array.Copy(Hash.NULL_HASH.bytes(), 0, buffer, offset, Transaction.HASH_SIZE);
				}
				else
				{
					((ByteBuffer) transactionsToRequest.position(--numberOfTransactionsToRequest * Transaction.HASH_SIZE)).get(transactionToRequest);
					Array.Copy(transactionToRequest, 0, buffer, offset, Transaction.HASH_SIZE);
				}
			}
		}

		public virtual void clearAnalyzedTransactionsFlags()
		{
			analyzedTransactionsFlags.position(0);
			for (int i = 0; i < ANALYZED_TRANSACTIONS_FLAGS_SIZE / CELL_SIZE; i++)
			{
				analyzedTransactionsFlags.put(ZEROED_BUFFER);
			}
		}

		public virtual bool analyzedTransactionFlag(long pointer)
		{
			pointer -= CELLS_OFFSET - SUPER_GROUPS_OFFSET;
			return (analyzedTransactionsFlags.get((int)(pointer >> (11 + 3))) & (1 << ((pointer >> 11) & 7))) != 0;
		}

		public virtual bool AnalyzedTransactionFlag
		{
			set
			{
	
				value -= CELLS_OFFSET - SUPER_GROUPS_OFFSET;
	
				int value = analyzedTransactionsFlags.get((int)(value >> (11 + 3)));
				if ((value & (1 << ((value >> 11) & 7))) == 0)
				{
					analyzedTransactionsFlags.put((int)(value >> (11 + 3)), (sbyte)(value | (1 << ((value >> 11) & 7))));
					return true;
				}
				return false;
			}
		}

		public virtual void saveAnalyzedTransactionsFlags()
		{
			analyzedTransactionsFlags.position(0);
			analyzedTransactionsFlagsCopy.position(0);
			analyzedTransactionsFlagsCopy.put(analyzedTransactionsFlags);
		}

		public virtual void loadAnalyzedTransactionsFlags()
		{
			analyzedTransactionsFlagsCopy.position(0);
			analyzedTransactionsFlags.position(0);
			analyzedTransactionsFlags.put(analyzedTransactionsFlagsCopy);
		}

		public virtual ByteBuffer AnalyzedTransactionsFlags
		{
			get
			{
				return analyzedTransactionsFlags;
			}
		}

		public virtual ByteBuffer AnalyzedTransactionsFlagsCopy
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