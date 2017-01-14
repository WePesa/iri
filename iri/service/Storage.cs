using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using iri.utils;
using slf4net;

namespace com.iota.iri.service
{

	using Milestone = com.iota.iri.Milestone;
	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;

	public class Storage
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(Storage));

        public const int SBYTE_SIZE_IN_BITS = 8;

		public const int CELL_SIZE = 2048;
		private const int CELLS_PER_CHUNK = 65536;
		private const int CHUNK_SIZE = CELL_SIZE * CELLS_PER_CHUNK;
		private const int MAX_NUMBER_OF_CHUNKS = 16384; // Limits the storage capacity to ~1 billion transactions

        private const int TIPS_FLAGS_OFFSET = 0, TIPS_FLAGS_SIZE = MAX_NUMBER_OF_CHUNKS * CELLS_PER_CHUNK / SBYTE_SIZE_IN_BITS;
		public const int SUPER_GROUPS_OFFSET = TIPS_FLAGS_OFFSET + TIPS_FLAGS_SIZE, SUPER_GROUPS_SIZE = (short.MaxValue - short.MinValue + 1) * CELL_SIZE;
		public const int CELLS_OFFSET = SUPER_GROUPS_OFFSET + SUPER_GROUPS_SIZE;

		private const int TRANSACTIONS_TO_REQUEST_OFFSET = 0, TRANSACTIONS_TO_REQUEST_SIZE = CHUNK_SIZE;
        private const int ANALYZED_TRANSACTIONS_FLAGS_OFFSET = TRANSACTIONS_TO_REQUEST_OFFSET + TRANSACTIONS_TO_REQUEST_SIZE, ANALYZED_TRANSACTIONS_FLAGS_SIZE = MAX_NUMBER_OF_CHUNKS * CELLS_PER_CHUNK / SBYTE_SIZE_IN_BITS;
		private const int ANALYZED_TRANSACTIONS_FLAGS_COPY_OFFSET = ANALYZED_TRANSACTIONS_FLAGS_OFFSET + ANALYZED_TRANSACTIONS_FLAGS_SIZE, ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE = ANALYZED_TRANSACTIONS_FLAGS_SIZE;

		private const int GROUP = 0;
		public const int PREFILLED_SLOT = 1;
		public const int FILLED_SLOT = -1;

		public static readonly sbyte[] ZEROED_BUFFER = new sbyte[CELL_SIZE];

		private const string TRANSACTIONS_FILE_NAME = "transactions.iri";
		private const string BUNDLES_FILE_NAME = "bundles.iri";
		private const string ADDRESSES_FILE_NAME = "addresses.iri";
		private const string TAGS_FILE_NAME = "tags.iri";
		private const string APPROVERS_FILE_NAME = "approvers.iri";
		private const string SCRATCHPAD_FILE_NAME = "scratchpad.iri";

		private const int ZEROTH_POINTER_OFFSET = 64;

        private static MemoryMappedFile transactionsChannel;
        public static MemoryMappedViewStream transactionsTipsFlags;
        private static readonly MemoryMappedViewStream[] transactionsChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];


        private static long transactionsNextPointer = CELLS_OFFSET - SUPER_GROUPS_OFFSET;
        internal static long TransactionsNextPointer
        {
            get
            {
                return Interlocked.Read(ref transactionsNextPointer);
            }
            set
            {
                Interlocked.Exchange(ref transactionsNextPointer, value);
            }
        }


		private static readonly sbyte[] mainBuffer = new sbyte[CELL_SIZE], auxBuffer = new sbyte[CELL_SIZE];
		public static readonly sbyte[][] approvedTransactionsToStore = new sbyte[2][];

		public static int numberOfApprovedTransactionsToStore;

        private static MemoryMappedFile bundlesChannel;
        private static readonly MemoryMappedViewStream[] bundlesChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];

        private static long bundlesNextPointer = SUPER_GROUPS_SIZE;

        internal static long BundlesNextPointer
        {
            get
            {
                return Interlocked.Read(ref bundlesNextPointer);
            }
            set
            {
                Interlocked.Exchange(ref bundlesNextPointer, value);
            }
        }


        private static MemoryMappedFile addressesChannel;
        private static readonly MemoryMappedViewStream[] addressesChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];
        private static long addressesNextPointer = SUPER_GROUPS_SIZE;

        internal static long AddressesNextPointer
        {
            get
            {
                return Interlocked.Read(ref addressesNextPointer);
            }
            set
            {
                Interlocked.Exchange(ref addressesNextPointer, value);
            }
        }

        private static MemoryMappedFile tagsChannel;
        private static readonly MemoryMappedViewStream[] tagsChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];
		private static long tagsNextPointer = SUPER_GROUPS_SIZE;

        internal static long TagsNextPointer
        {
            get
            {
                return Interlocked.Read(ref tagsNextPointer);
            }
            set
            {
                Interlocked.Exchange(ref tagsNextPointer, value);
            }
        }


        private static MemoryMappedFile approversChannel;
        private static readonly MemoryMappedViewStream[] approversChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];
		private static long approversNextPointer = SUPER_GROUPS_SIZE;

        internal static long ApproversNextPointer
        {
            get
            {
                return Interlocked.Read(ref approversNextPointer);
            }
            set
            {
                Interlocked.Exchange(ref approversNextPointer, value);
            }
        }

        private static MemoryMappedViewStream transactionsToRequest;
		public volatile static int numberOfTransactionsToRequest;
		private static readonly sbyte[] transactionToRequest = new sbyte[Transaction.HASH_SIZE];

        public static MemoryMappedViewStream analyzedTransactionsFlags, analyzedTransactionsFlagsCopy;

		private static bool launched;

		private static readonly object transactionToRequestMonitor = new object();
		private static int previousNumberOfTransactions;

        static Storage()
        {
            transactionsChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(TRANSACTIONS_FILE_NAME), FileMode.OpenOrCreate, "transactionsMap", TIPS_FLAGS_SIZE + SUPER_GROUPS_SIZE);
        }

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void launch()
		{
		    try
		    {
    
                // transactionsChannel = FileChannel.open(Paths.get(TRANSACTIONS_FILE_NAME), StandardOpenOption.CREATE,
                //    StandardOpenOption.READ, StandardOpenOption.WRITE);

                transactionsTipsFlags = transactionsChannel.CreateViewStream(TIPS_FLAGS_OFFSET, TIPS_FLAGS_SIZE);

                transactionsChunks[0] = transactionsChannel.CreateViewStream(SUPER_GROUPS_OFFSET, SUPER_GROUPS_SIZE);

		        long transactionsChannelSize = TIPS_FLAGS_SIZE + SUPER_GROUPS_SIZE; // transactionsChannel.size();

		        while (true)
		        {

		            if ((TransactionsNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {

		                transactionsChunks[(int) (TransactionsNextPointer >> 27)] =
                            transactionsChannel.CreateViewStream(SUPER_GROUPS_OFFSET + TransactionsNextPointer, CHUNK_SIZE);
		            }

		            if (transactionsChannelSize - TransactionsNextPointer - SUPER_GROUPS_OFFSET > CHUNK_SIZE)
		            {

		                TransactionsNextPointer += CHUNK_SIZE;

		            }
		            else
		            {

		                // transactionsChunks[(int) (TransactionsNextPointer >> 27)].get(mainBuffer);
                        transactionsChunks[(int)(TransactionsNextPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		                bool empty = true;
		                foreach (int value in mainBuffer)
		                {

		                    if (value != 0)
		                    {
		                        empty = false;
		                        break;
		                    }
		                }
		                if (empty)
		                {
		                    break;
		                }

		                TransactionsNextPointer += CELL_SIZE;
		            }
		        }

                bundlesChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(BUNDLES_FILE_NAME), FileMode.OpenOrCreate, "bundlesMap", SUPER_GROUPS_SIZE);

                bundlesChunks[0] = bundlesChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

		        long bundlesChannelSize = SUPER_GROUPS_SIZE; // bundlesChannel.size();
		        while (true)
		        {

		            if ((BundlesNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {

                        bundlesChunks[(int)(BundlesNextPointer >> 27)] = bundlesChannel.CreateViewStream(BundlesNextPointer, CHUNK_SIZE);
		            }

		            if (bundlesChannelSize - BundlesNextPointer > CHUNK_SIZE)
		            {

		                BundlesNextPointer += CHUNK_SIZE;

		            }
		            else
		            {

		                // bundlesChunks[(int) (BundlesNextPointer >> 27)].get(mainBuffer);
                        bundlesChunks[(int)(BundlesNextPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		                bool empty = true;
		                foreach (int value in mainBuffer)
		                {

		                    if (value != 0)
		                    {
		                        empty = false;
		                        break;
		                    }
		                }
		                if (empty)
		                {
		                    break;
		                }

		                BundlesNextPointer += CELL_SIZE;
		            }
		        }

                addressesChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(ADDRESSES_FILE_NAME), FileMode.OpenOrCreate, "addressesMap", SUPER_GROUPS_SIZE);

		        addressesChunks[0] = addressesChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

		        long addressesChannelSize = SUPER_GROUPS_SIZE; // addressesChannel.size();
		        while (true)
		        {

		            if ((AddressesNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {

		                addressesChunks[(int) (AddressesNextPointer >> 27)] =
                            addressesChannel.CreateViewStream(AddressesNextPointer, CHUNK_SIZE);
		            }

		            if (addressesChannelSize - AddressesNextPointer > CHUNK_SIZE)
		            {

		                AddressesNextPointer += CHUNK_SIZE;

		            }
		            else
		            {

		                // addressesChunks[(int) (AddressesNextPointer >> 27)].get(mainBuffer);
                        addressesChunks[(int)(AddressesNextPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		                bool empty = true;
		                foreach (int value in mainBuffer)
		                {

		                    if (value != 0)
		                    {

		                        empty = false;

		                        break;
		                    }
		                }
		                if (empty)
		                {

		                    break;
		                }

		                AddressesNextPointer += CELL_SIZE;
		            }
		        }

                tagsChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(TAGS_FILE_NAME), FileMode.OpenOrCreate, "tagsMap", SUPER_GROUPS_SIZE);

		        tagsChunks[0] = tagsChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

		        long tagsChannelSize = SUPER_GROUPS_SIZE; // tagsChannel.size();
		        while (true)
		        {

		            if ((TagsNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {

                        tagsChunks[(int)(TagsNextPointer >> 27)] = tagsChannel.CreateViewStream(TagsNextPointer, CHUNK_SIZE);
		            }

		            if (tagsChannelSize - TagsNextPointer > CHUNK_SIZE)
		            {

		                TagsNextPointer += CHUNK_SIZE;

		            }
		            else
		            {

		                // tagsChunks[(int) (TagsNextPointer >> 27)].get(mainBuffer);
                        tagsChunks[(int)(TagsNextPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		                bool empty = true;
		                foreach (int value in mainBuffer)
		                {
		                    if (value != 0)
		                    {
		                        empty = false;
		                        break;
		                    }
		                }
		                if (empty)
		                {
		                    break;
		                }

		                TagsNextPointer += CELL_SIZE;
		            }
		        }

                approversChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(APPROVERS_FILE_NAME), FileMode.OpenOrCreate, "approversMap", SUPER_GROUPS_SIZE);

		        approversChunks[0] = approversChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

		        long approversChannelSize = SUPER_GROUPS_SIZE; // approversChannel.size();
		        while (true)
		        {

		            if ((ApproversNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {
		                approversChunks[(int) (ApproversNextPointer >> 27)] =
                            approversChannel.CreateViewStream(ApproversNextPointer, CHUNK_SIZE);
		            }

		            if (approversChannelSize - ApproversNextPointer > CHUNK_SIZE)
		            {
		                ApproversNextPointer += CHUNK_SIZE;

		            }
		            else
		            {

		                // approversChunks[(int) (ApproversNextPointer >> 27)].get(mainBuffer);
                        approversChunks[(int)(ApproversNextPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		                bool empty = true;
		                foreach (int value in mainBuffer)
		                {

		                    if (value != 0)
		                    {
		                        empty = false;
		                        break;
		                    }
		                }
		                if (empty)
		                {
		                    break;
		                }

		                ApproversNextPointer += CELL_SIZE;
		            }
		        }

                MemoryMappedFile scratchpadChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(SCRATCHPAD_FILE_NAME), FileMode.OpenOrCreate, "scratchpadMap", 402653184);

		        transactionsToRequest = scratchpadChannel.CreateViewStream(TRANSACTIONS_TO_REQUEST_OFFSET, TRANSACTIONS_TO_REQUEST_SIZE);

		        analyzedTransactionsFlags = scratchpadChannel.CreateViewStream(ANALYZED_TRANSACTIONS_FLAGS_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_SIZE);

		        analyzedTransactionsFlagsCopy = scratchpadChannel.CreateViewStream(ANALYZED_TRANSACTIONS_FLAGS_COPY_OFFSET, ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE);

		        scratchpadChannel.Dispose();

		        if (TransactionsNextPointer == CELLS_OFFSET - SUPER_GROUPS_OFFSET)
		        {

		            // No need to zero "mainBuffer", it already contains only zeros
		            setValue(mainBuffer, Transaction.TYPE_OFFSET, FILLED_SLOT);
		            appendToTransactions(true);

		            Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
		            setValue(mainBuffer, 128 << 3, CELLS_OFFSET - SUPER_GROUPS_OFFSET);

		            // ((ByteBuffer) transactionsChunks[0].position((128 + (128 << 8)) << 11)).put(mainBuffer);
                    transactionsChunks[0].Position = (128 + (128 << 8)) << 11;
                    transactionsChunks[0].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

		            Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
		            updateBundleAddressTagAndApprovers(CELLS_OFFSET - SUPER_GROUPS_OFFSET);
		        }

		        launched = true;
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void shutDown()
		{

			if (launched)
			{

				transactionsTipsFlags.Flush();
				for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && transactionsChunks[i] != null; i++)
				{
					log.Info("Flushing transactions chunk #" + i);
					flush(transactionsChunks[i]);
				}

				for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && bundlesChunks[i] != null; i++)
				{
					log.Info("Flushing bundles chunk #" + i);
					flush(bundlesChunks[i]);
				}

				for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && addressesChunks[i] != null; i++)
				{
					log.Info("Flushing addresses chunk #" + i);
					flush(addressesChunks[i]);
				}

				for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && tagsChunks[i] != null; i++)
				{
					log.Info("Flushing tags chunk #" + i);
					flush(tagsChunks[i]);
				}

				for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && approversChunks[i] != null; i++)
				{
					log.Info("Flushing approvers chunk #" + i);
					flush(approversChunks[i]);
				}

				Console.WriteLine("DB successfully flushed");

				try
				{

					transactionsChannel.Dispose();
					bundlesChannel.Dispose();
					addressesChannel.Dispose();
					tagsChannel.Dispose();
					approversChannel.Dispose();

				}
				catch (Exception e)
				{
				}
			}
		}


        private static bool flush(MemoryMappedViewStream buffer)
		{

			try
			{
				buffer.Flush();
				return true;

			}
			catch (Exception e)
			{
				return false;
			}
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long storeTransaction(sbyte[] hash, Transaction transaction, bool tip) // Returns the pointer or 0 if the transaction was already in the storage and "transaction" value is not null
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11, prevPointer = 0;

		MAIN_LOOP:
			for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
			{

				// ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                transactionsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                transactionsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					prevPointer = pointer;
					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{

						setValue(mainBuffer, (hash[depth] + 128) << 3, pointer = TransactionsNextPointer);

                        // ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                        transactionsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                        transactionsChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

						Transaction.dump(mainBuffer, hash, transaction);
						appendToTransactions(transaction != null || tip);
						if (transaction != null)
						{

							updateBundleAddressTagAndApprovers(pointer);
						}

						goto MAIN_LOOP;
					}

				}
				else
				{

					for (int i = depth; i < Transaction.HASH_SIZE; i++)
					{

						if (mainBuffer[Transaction.HASH_OFFSET + i] != hash[i])
						{

							int differentHashByte = mainBuffer[Transaction.HASH_OFFSET + i];

							// ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
						    transactionsChunks[(int) (prevPointer >> 27)].Position = (int) (prevPointer & (CHUNK_SIZE - 1));
                            transactionsChunks[(int)(prevPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							setValue(mainBuffer, (hash[depth - 1] + 128) << 3, TransactionsNextPointer);

							// ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            transactionsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                            transactionsChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							for (int j = depth; j < i; j++)
							{

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, (hash[j] + 128) << 3, TransactionsNextPointer + CELL_SIZE);
								appendToTransactions(false);
							}

							Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
							setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
							setValue(mainBuffer, (hash[i] + 128) << 3, TransactionsNextPointer + CELL_SIZE);
							appendToTransactions(false);

							Transaction.dump(mainBuffer, hash, transaction);
							pointer = TransactionsNextPointer;
							appendToTransactions(transaction != null || tip);
							if (transaction != null)
							{

								updateBundleAddressTagAndApprovers(pointer);
							}

							goto MAIN_LOOP;
						}
					}

					if (transaction != null)
					{

						if (mainBuffer[Transaction.TYPE_OFFSET] == PREFILLED_SLOT)
						{

							Transaction.dump(mainBuffer, hash, transaction);
							// ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            transactionsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                            transactionsChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							updateBundleAddressTagAndApprovers(pointer);

						}
						else
						{

							pointer = 0;
						}
					}

					goto MAIN_LOOP;
				}
			}

			return pointer;
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long transactionPointer(sbyte[] hash) // Returns a negative value if the transaction hasn't been seen yet but was referenced
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
			{

				// ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(auxBuffer);
                transactionsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                transactionsChunks[(int)(pointer >> 27)].Read((byte[])(Array)auxBuffer, 0, auxBuffer.Length); 

				if (auxBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					if ((pointer = value(auxBuffer, (hash[depth] + 128) << 3)) == 0)
					{

						return 0;
					}

				}
				else
				{

					for (; depth < Transaction.HASH_SIZE; depth++)
					{

						if (auxBuffer[Transaction.HASH_OFFSET + depth] != hash[depth])
						{

							return 0;
						}
					}

					return auxBuffer[Transaction.TYPE_OFFSET] == PREFILLED_SLOT ? -pointer : pointer;
				}
			}

            throw new InvalidOperationException("Corrupted storage");
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static Transaction loadTransaction(long pointer)
		{

			// ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
            transactionsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
            transactionsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

			return new Transaction(mainBuffer, pointer);
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static Transaction loadTransaction(sbyte[] hash)
		{

			long pointer = transactionPointer(hash);
			return pointer > 0 ? loadTransaction(pointer) : null;
		}

		public static void TransactionToRequest(sbyte[] buffer, int offset)
		{

			lock (transactionToRequestMonitor)
			{

				if (numberOfTransactionsToRequest == 0)
				{

					long beginningTime = DateTimeExtensions.currentTimeMillis();

					lock (analyzedTransactionsFlags)
					{

						clearAnalyzedTransactionsFlags();

                        List<long?> nonAnalyzedTransactions = new List<long?> { transactionPointer(Milestone.latestMilestone.Sbytes()) };

						long? pointer;
						while ((pointer = nonAnalyzedTransactions.Poll()) != null)
						{

                            if (Storage.setAnalyzedTransactionFlag((long)pointer))
							{


								Transaction transaction = Storage.loadTransaction((long)pointer);
								if (transaction.type == Storage.PREFILLED_SLOT)
								{

									// ((ByteBuffer) transactionsToRequest.position(numberOfTransactionsToRequest++ * Transaction.HASH_SIZE)).put(transaction.hash); // Only 2'917'776 hashes can be stored this way without overflowing the buffer, we assume that nodes will never need to store that many hashes, so we don't need to cap "numberOfTransactionsToRequest"
                                    transactionsToRequest.Position = numberOfTransactionsToRequest++ * Transaction.HASH_SIZE;
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

                    Console.WriteLine("Transactions to request = " + numberOfTransactionsToRequest + " / " + (TransactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE + " (" + (DateTimeExtensions.currentTimeMillis() - beginningTime) + " ms / " + (numberOfTransactionsToRequest == 0 ? 0 : (previousNumberOfTransactions == 0 ? 0 : (((TransactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE - previousNumberOfTransactions) * 100) / numberOfTransactionsToRequest)) + "%)");
					previousNumberOfTransactions = (int)((TransactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) / CELL_SIZE);
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


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static bool tipFlag(long pointer)
		{

			long index = (pointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) >> 11;
			// return (transactionsTipsFlags.get((int)(index >> 3)) & (1 << (index & 7))) != 0;

            sbyte[] sbyteArray = new sbyte[1];
		    transactionsTipsFlags.Read((byte[]) (Array) sbyteArray, (int) (index >> 3), 1);
            long result = BitConverter.ToInt64((byte[])(Array)sbyteArray, 0);
            result &= (1 << (int)(index & 7));
            return result != 0;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IList<Hash> tips()
		{

            List<Hash> tips = new List<Hash>();

			long pointer = CELLS_OFFSET - SUPER_GROUPS_OFFSET;
			while (pointer < TransactionsNextPointer)
			{

				if (tipFlag(pointer))
				{
					tips.Add(new Hash(loadTransaction(pointer).hash, 0, Transaction.HASH_SIZE));
				}

				pointer += CELL_SIZE;
			}

			return tips;
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

        public static bool setAnalyzedTransactionFlag(long pointer)
        {
            pointer -= CELLS_OFFSET - SUPER_GROUPS_OFFSET;

            // int value = analyzedTransactionsFlags.get((int) (pointer >> (11 + 3)));

            byte[] thisBuffer = new byte[4];
            analyzedTransactionsFlags.Read((byte[])thisBuffer, (int)(pointer >> (11 + 3)), thisBuffer.Length);
            int value = BitConverter.ToInt32(thisBuffer, 0);

            if ((value & (1 << (int)((pointer >> 11) & 7))) == 0) 
            {

                // analyzedTransactionsFlags.put((int)(pointer >> (11 + 3)), (byte)(value | (1 << (int)((pointer >> 11) & 7))));

                byte[] thisByte = BitConverter.GetBytes((value | (1 << (int)((pointer >> 11) & 7))));
                analyzedTransactionsFlags.Write(thisByte, (int)(pointer >> (11 + 3)), 1);

                return true;

            } 
            else 
            {

                return false;
            }
        }

        //public static bool AnalyzedTransactionFlag
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
        //        else
        //        {
	
        //            return false;
        //        }
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

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long bundlePointer(sbyte[] hash)
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.BUNDLE_SIZE; depth++)
			{

				// ((ByteBuffer)bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                bundlesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{
						return 0;
					}

				}
				else
				{

					for (; depth < Transaction.BUNDLE_SIZE; depth++)
					{

						if (mainBuffer[Transaction.HASH_OFFSET + depth] != hash[depth])
						{
							return 0;
						}
					}

					return pointer;
				}
			}

            throw new InvalidOperationException("Corrupted storage");
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IList<long?> bundleTransactions(long pointer)
		{

            List<long?> bundleTransactions = new List<long?>();

			if (pointer != 0)
			{

				// ((ByteBuffer) bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                bundlesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				int offset = ZEROTH_POINTER_OFFSET - sizeof(long);
				while (true)
				{

					while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long))
					{

						long transactionPointer = value(mainBuffer, offset);
						if (transactionPointer == 0)
						{

							break;

						}
						else
						{

							bundleTransactions.Add(transactionPointer);
						}
					}
					if (offset == CELL_SIZE - sizeof(long))
					{

						long nextCellPointer = value(mainBuffer, offset);
						if (nextCellPointer == 0)
						{

							break;

						}
						else
						{

							// ((ByteBuffer) bundlesChunks[(int)(nextCellPointer >> 27)].position((int)(nextCellPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                            bundlesChunks[(int)(nextCellPointer >> 27)].Position = (int)(nextCellPointer & (CHUNK_SIZE - 1));
                            bundlesChunks[(int)(nextCellPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							offset = -sizeof(long);
						}

					}
					else
					{

						break;
					}
				}
			}

			return bundleTransactions;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long addressPointer(sbyte[] hash)
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.ADDRESS_SIZE; depth++)
			{

				// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                addressesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{

						return 0;
					}

				}
				else
				{

					for (; depth < Transaction.ADDRESS_SIZE; depth++)
					{

						if (mainBuffer[Transaction.HASH_OFFSET + depth] != hash[depth])
						{

							return 0;
						}
					}

					return pointer;
				}
			}

            throw new InvalidOperationException("Corrupted storage");
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IList<long?> addressTransactions(long pointer)
		{

            List<long?> addressTransactions = new List<long?>();

			if (pointer != 0)
			{

				// ((ByteBuffer) addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                addressesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				int offset = ZEROTH_POINTER_OFFSET - sizeof(long);
				while (true)
				{

					while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long))
					{

						long transactionPointer = value(mainBuffer, offset);
						if (transactionPointer == 0)
						{
							break;
						}
						else
						{
							addressTransactions.Add(transactionPointer);
						}
					}
					if (offset == CELL_SIZE - sizeof(long))
					{

						long nextCellPointer = value(mainBuffer, offset);
						if (nextCellPointer == 0)
						{
							break;
						}
						else
						{
							// ((ByteBuffer) addressesChunks[(int)(nextCellPointer >> 27)].position((int)(nextCellPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                            addressesChunks[(int)(nextCellPointer >> 27)].Position = (int)(nextCellPointer & (CHUNK_SIZE - 1));
                            addressesChunks[(int)(nextCellPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							offset = -sizeof(long);
						}

					}
					else
					{
						break;
					}
				}
			}

			return addressTransactions;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long tagPointer(sbyte[] hash)
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.TAG_SIZE; depth++)
			{

				// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                tagsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{
						return 0;
					}

				}
				else
				{

					for (; depth < Transaction.TAG_SIZE; depth++)
					{
						if (mainBuffer[Transaction.HASH_OFFSET + depth] != hash[depth])
						{
							return 0;
						}
					}

					return pointer;
				}
			}

            throw new InvalidOperationException("Corrupted storage");
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IList<long?> tagTransactions(long pointer)
		{

            List<long?> tagTransactions = new List<long?>();

			if (pointer != 0)
			{

				// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                tagsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				int offset = ZEROTH_POINTER_OFFSET - sizeof(long);
				while (true)
				{

					while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long))
					{

						long transactionPointer = value(mainBuffer, offset);
						if (transactionPointer == 0)
						{

							break;

						}
						else
						{

							tagTransactions.Add(transactionPointer);
						}
					}
					if (offset == CELL_SIZE - sizeof(long))
					{

						long nextCellPointer = value(mainBuffer, offset);
						if (nextCellPointer == 0)
						{

							break;

						}
						else
						{

							// ((ByteBuffer) tagsChunks[(int)(nextCellPointer >> 27)].position((int)(nextCellPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                            tagsChunks[(int)(nextCellPointer >> 27)].Position = (int)(nextCellPointer & (CHUNK_SIZE - 1));
                            tagsChunks[(int)(nextCellPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							offset = -sizeof(long);
						}

					}
					else
					{

						break;
					}
				}
			}

			return tagTransactions;
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static long approveePointer(sbyte[] hash)
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
			{

				// ((ByteBuffer)approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                approversChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{

						return 0;
					}

				}
				else
				{

					for (; depth < Transaction.HASH_SIZE; depth++)
					{

						if (mainBuffer[Transaction.HASH_OFFSET + depth] != hash[depth])
						{

							return 0;
						}
					}

					return pointer;
				}
			}

            throw new InvalidOperationException("Corrupted storage");
		}


		[MethodImpl(MethodImplOptions.Synchronized)]
		public static IList<long?> approveeTransactions(long pointer)
		{
            List<long?> approveeTransactions = new List<long?>();

			if (pointer != 0)
			{

				// ((ByteBuffer) approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                approversChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

				int offset = ZEROTH_POINTER_OFFSET - sizeof(long);
				while (true)
				{

					while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long))
					{

						long transactionPointer = value(mainBuffer, offset);

						if (transactionPointer == 0)
						{

							break;

						}
						else
						{

							approveeTransactions.Add(transactionPointer);
						}
					}
					if (offset == CELL_SIZE - sizeof(long))
					{

						long nextCellPointer = value(mainBuffer, offset);

						if (nextCellPointer == 0)
						{

							break;

						}
						else
						{

							// ((ByteBuffer) approversChunks[(int)(nextCellPointer >> 27)].position((int)(nextCellPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                            approversChunks[(int)(nextCellPointer >> 27)].Position = (int)(nextCellPointer & (CHUNK_SIZE - 1));
                            approversChunks[(int)(nextCellPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							offset = -sizeof(long);
						}

					}
					else
					{

						break;
					}
				}
			}

			return approveeTransactions;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public static void setTransactionValidity(long pointer, int validity)
		{
			// transactionsChunks[(int)(pointer >> 27)].put(((int)(pointer & (CHUNK_SIZE - 1))) + Transaction.VALIDITY_OFFSET, (sbyte)validity);

            transactionsChunks[(int)(pointer >> 27)].Write(BitConverter.GetBytes(validity), ((int)(pointer & (CHUNK_SIZE - 1))) + Transaction.VALIDITY_OFFSET, 1);
		}

		public static long value(sbyte[] buffer, int offset)
		{

			return ((long)(buffer[offset] & 0xFF)) + (((long)(buffer[offset + 1] & 0xFF)) << 8) + (((long)(buffer[offset + 2] & 0xFF)) << 16) + (((long)(buffer[offset + 3] & 0xFF)) << 24) + (((long)(buffer[offset + 4] & 0xFF)) << 32) + (((long)(buffer[offset + 5] & 0xFF)) << 40) + (((long)(buffer[offset + 6] & 0xFF)) << 48) + (((long)(buffer[offset + 7] & 0xFF)) << 56);
		}

		public static void setValue(sbyte[] buffer, int offset, long value)
		{

			buffer[offset] = (sbyte)value;
			buffer[offset + 1] = (sbyte)(value >> 8);
			buffer[offset + 2] = (sbyte)(value >> 16);
			buffer[offset + 3] = (sbyte)(value >> 24);
			buffer[offset + 4] = (sbyte)(value >> 32);
			buffer[offset + 5] = (sbyte)(value >> 40);
			buffer[offset + 6] = (sbyte)(value >> 48);
			buffer[offset + 7] = (sbyte)(value >> 56);
		}

		private static void appendToTransactions(bool tip)
		{

			// ((ByteBuffer)transactionsChunks[(int)(TransactionsNextPointer >> 27)].position((int)(TransactionsNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            transactionsChunks[(int)(TransactionsNextPointer >> 27)].Position = (int)(TransactionsNextPointer & (CHUNK_SIZE - 1));
            transactionsChunks[(int)(TransactionsNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (tip)
			{

				long index = (TransactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) >> 11;

				// transactionsTipsFlags.put((int)(index >> 3), (sbyte)(transactionsTipsFlags.get((int)(index >> 3)) | (1 << (index & 7))));
                sbyte[] sbyteArray = new sbyte[1];
                transactionsTipsFlags.Read((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
                sbyteArray[0] |= (sbyte)(1 << (int)(index & 7));
                transactionsTipsFlags.Write((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
			}

			if (((TransactionsNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{

                    transactionsChunks[(int)(TransactionsNextPointer >> 27)] = transactionsChannel.CreateViewStream(SUPER_GROUPS_OFFSET + TransactionsNextPointer, CHUNK_SIZE);

				}
				catch (IOException e)
				{

					e.ToString();
				}
			}
		}

		private static void appendToBundles()
		{

			// ((ByteBuffer)bundlesChunks[(int)(BundlesNextPointer >> 27)].position((int)(BundlesNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            bundlesChunks[(int)(BundlesNextPointer >> 27)].Position = (int)(BundlesNextPointer & (CHUNK_SIZE - 1));
            bundlesChunks[(int)(BundlesNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((BundlesNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{

                    bundlesChunks[(int)(BundlesNextPointer >> 27)] = bundlesChannel.CreateViewStream(BundlesNextPointer, CHUNK_SIZE);

				}
				catch (IOException e)
				{

					e.ToString();
				}
			}
		}

		private static void appendToAddresses()
		{

			// ((ByteBuffer)addressesChunks[(int)(AddressesNextPointer >> 27)].position((int)(AddressesNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            addressesChunks[(int)(AddressesNextPointer >> 27)].Position = (int)(AddressesNextPointer & (CHUNK_SIZE - 1));
            addressesChunks[(int)(AddressesNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((AddressesNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{

                    addressesChunks[(int)(AddressesNextPointer >> 27)] = addressesChannel.CreateViewStream(AddressesNextPointer, CHUNK_SIZE);

				}
				catch (IOException e)
				{

					e.ToString();
				}
			}
		}

		private static void appendToTags()
		{

			// ((ByteBuffer) tagsChunks[(int)(TagsNextPointer >> 27)].position((int)(TagsNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            tagsChunks[(int)(TagsNextPointer >> 27)].Position = (int)(TagsNextPointer & (CHUNK_SIZE - 1));
            tagsChunks[(int)(TagsNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((TagsNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{

                    tagsChunks[(int)(TagsNextPointer >> 27)] = tagsChannel.CreateViewStream(TagsNextPointer, CHUNK_SIZE);

				}
				catch (IOException e)
				{

					e.ToString();
				}
			}
		}

		private static void appendToApprovers()
		{

			// ((ByteBuffer)approversChunks[(int)(ApproversNextPointer >> 27)].position((int)(ApproversNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            approversChunks[(int)(ApproversNextPointer >> 27)].Position = (int)(ApproversNextPointer & (CHUNK_SIZE - 1));
            approversChunks[(int)(ApproversNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((ApproversNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{

                    approversChunks[(int)(ApproversNextPointer >> 27)] = approversChannel.CreateViewStream(ApproversNextPointer, CHUNK_SIZE);

				}
				catch (IOException e)
				{

					e.ToString();
				}
			}
		}

		private static void updateBundleAddressTagAndApprovers(long transactionPointer)
		{

			Transaction transaction = new Transaction(mainBuffer, transactionPointer);
			for (int j = 0; j < numberOfApprovedTransactionsToStore; j++)
			{

				storeTransaction(approvedTransactionsToStore[j], null, false);
			}
			numberOfApprovedTransactionsToStore = 0;

			{
				long pointer = ((transaction.bundle[0] + 128) + ((transaction.bundle[1] + 128) << 8)) << 11, prevPointer = 0;
				for (int depth = 2; depth < Transaction.BUNDLE_SIZE; depth++)
				{

					// ((ByteBuffer)bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                    bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                    bundlesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

					if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
					{

						prevPointer = pointer;
						if ((pointer = value(mainBuffer, (transaction.bundle[depth] + 128) << 3)) == 0)
						{

							setValue(mainBuffer, (transaction.bundle[depth] + 128) << 3, BundlesNextPointer);
							// ((ByteBuffer)bundlesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            bundlesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                            bundlesChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
							mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
							Array.Copy(transaction.bundle, 0, mainBuffer, 8, Transaction.BUNDLE_SIZE);
							setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
							appendToBundles();

							break;
						}

					}
					else
					{

						bool sameBundle = true;

						for (int i = depth; i < Transaction.BUNDLE_SIZE; i++)
						{

							if (mainBuffer[Transaction.HASH_OFFSET + i] != transaction.bundle[i])
							{

								int differentHashByte = mainBuffer[Transaction.HASH_OFFSET + i];

								// ((ByteBuffer)bundlesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                bundlesChunks[(int)(pointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                bundlesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

								setValue(mainBuffer, (transaction.bundle[depth - 1] + 128) << 3, BundlesNextPointer);

								// ((ByteBuffer)bundlesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                bundlesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                bundlesChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

								for (int j = depth; j < i; j++)
								{

									Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
									setValue(mainBuffer, (transaction.bundle[j] + 128) << 3, BundlesNextPointer + CELL_SIZE);
									appendToBundles();
								}

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
								setValue(mainBuffer, (transaction.bundle[i] + 128) << 3, BundlesNextPointer + CELL_SIZE);
								appendToBundles();

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
								Array.Copy(transaction.bundle, 0, mainBuffer, 8, Transaction.BUNDLE_SIZE);
								setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
								appendToBundles();

								sameBundle = false;

								break;
							}
						}

						if (sameBundle)
						{

							int offset = ZEROTH_POINTER_OFFSET;
							while (true)
							{

								while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long) && value(mainBuffer, offset) != 0)
								{

								// Do nothing
								}
								if (offset == CELL_SIZE - sizeof(long))
								{

									long nextCellPointer = value(mainBuffer, offset);
									if (nextCellPointer == 0)
									{

										setValue(mainBuffer, offset, BundlesNextPointer);
										// ((ByteBuffer)bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                        bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                        bundlesChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

										Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
										setValue(mainBuffer, 0, transactionPointer);
										appendToBundles();

										break;

									}
									else
									{

										pointer = nextCellPointer;
										// ((ByteBuffer)bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                        bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                        bundlesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

										offset = -sizeof(long);
									}

								}
								else
								{

									setValue(mainBuffer, offset, transactionPointer);
									// ((ByteBuffer)bundlesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                    bundlesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                    bundlesChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

									break;
								}
							}
						}

						break;
					}
				}
			}

			{
				long pointer = ((transaction.address[0] + 128) + ((transaction.address[1] + 128) << 8)) << 11, prevPointer = 0;
				for (int depth = 2; depth < Transaction.ADDRESS_SIZE; depth++)
				{

					// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                    addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                    addressesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

					if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
					{

						prevPointer = pointer;
						if ((pointer = value(mainBuffer, (transaction.address[depth] + 128) << 3)) == 0)
						{

							setValue(mainBuffer, (transaction.address[depth] + 128) << 3, AddressesNextPointer);
							// ((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            addressesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                            addressesChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
							mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
							Array.Copy(transaction.address, 0, mainBuffer, 8, Transaction.ADDRESS_SIZE);
							setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
							appendToAddresses();

							break;
						}

					}
					else
					{

						bool sameAddress = true;

						for (int i = depth; i < Transaction.ADDRESS_SIZE; i++)
						{

							if (mainBuffer[Transaction.HASH_OFFSET + i] != transaction.address[i])
							{

								int differentHashByte = mainBuffer[Transaction.HASH_OFFSET + i];

								// ((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                addressesChunks[(int)(pointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                addressesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

								setValue(mainBuffer, (transaction.address[depth - 1] + 128) << 3, AddressesNextPointer);
								// ((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                addressesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                addressesChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

								for (int j = depth; j < i; j++)
								{

									Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
									setValue(mainBuffer, (transaction.address[j] + 128) << 3, AddressesNextPointer + CELL_SIZE);
									appendToAddresses();
								}

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
								setValue(mainBuffer, (transaction.address[i] + 128) << 3, AddressesNextPointer + CELL_SIZE);
								appendToAddresses();

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
								Array.Copy(transaction.address, 0, mainBuffer, 8, Transaction.ADDRESS_SIZE);
								setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
								appendToAddresses();

								sameAddress = false;

								break;
							}
						}

						if (sameAddress)
						{

							int offset = ZEROTH_POINTER_OFFSET;
							while (true)
							{

								while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long) && value(mainBuffer, offset) != 0)
								{

								// Do nothing
								}
								if (offset == CELL_SIZE - sizeof(long))
								{

									long nextCellPointer = value(mainBuffer, offset);
									if (nextCellPointer == 0)
									{

										setValue(mainBuffer, offset, AddressesNextPointer);
										// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                        addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                        addressesChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

										Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
										setValue(mainBuffer, 0, transactionPointer);
										appendToAddresses();

										break;

									}
									else
									{

										pointer = nextCellPointer;
										// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                        addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                        addressesChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

										offset = -sizeof(long);
									}

								}
								else
								{

									setValue(mainBuffer, offset, transactionPointer);
									// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                    addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                    addressesChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

									break;
								}
							}
						}

						break;
					}
				}
			}

			for (int i = 0; i < Transaction.TAG_SIZE; i++)
			{

				if (transaction.tag[i] != 0)
				{

					long pointer = ((transaction.tag[0] + 128) + ((transaction.tag[1] + 128) << 8)) << 11, prevPointer = 0;
					for (int depth = 2; depth < Transaction.TAG_SIZE; depth++)
					{

						// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                        tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                        tagsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

						if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
						{

							prevPointer = pointer;
							if ((pointer = value(mainBuffer, (transaction.tag[depth] + 128) << 3)) == 0)
							{

								setValue(mainBuffer, (transaction.tag[depth] + 128) << 3, TagsNextPointer);
								// ((ByteBuffer) tagsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                tagsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                tagsChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
								Array.Copy(transaction.tag, 0, mainBuffer, 8, Transaction.TAG_SIZE);
								setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
								appendToTags();

								break;
							}

						}
						else
						{

							bool sameTag = true;

							for (int j = depth; j < Transaction.TAG_SIZE; j++)
							{

								if (mainBuffer[Transaction.HASH_OFFSET + j] != transaction.tag[j])
								{

									int differentHashByte = mainBuffer[Transaction.HASH_OFFSET + j];

									// ((ByteBuffer) tagsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                    tagsChunks[(int)(pointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                    tagsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

									setValue(mainBuffer, (transaction.tag[depth - 1] + 128) << 3, TagsNextPointer);
									// ((ByteBuffer) tagsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                    tagsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                    tagsChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

									for (int k = depth; k < j; k++)
									{

										Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
										setValue(mainBuffer, (transaction.tag[k] + 128) << 3, TagsNextPointer + CELL_SIZE);
										appendToTags();
									}

									Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
									setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
									setValue(mainBuffer, (transaction.tag[j] + 128) << 3, TagsNextPointer + CELL_SIZE);
									appendToTags();

									Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
									mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
									Array.Copy(transaction.tag, 0, mainBuffer, 8, Transaction.TAG_SIZE);
									setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
									appendToTags();

									sameTag = false;

									break;
								}
							}

							if (sameTag)
							{

								int offset = ZEROTH_POINTER_OFFSET;
								while (true)
								{

									while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long) && value(mainBuffer, offset) != 0)
									{

									// Do nothing
									}
									if (offset == CELL_SIZE - sizeof(long))
									{

										long nextCellPointer = value(mainBuffer, offset);
										if (nextCellPointer == 0)
										{

											setValue(mainBuffer, offset, TagsNextPointer);
											// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                            tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                            tagsChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

											Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
											setValue(mainBuffer, 0, transactionPointer);
											appendToTags();

											break;

										}
										else
										{

											pointer = nextCellPointer;
											// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                            tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                            tagsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

											offset = -sizeof(long);
										}

									}
									else
									{

										setValue(mainBuffer, offset, transactionPointer);
										// ((ByteBuffer) tagsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                        tagsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                        tagsChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

										break;
									}
								}
							}

							break;
						}
					}

					break;
				}
			}

			updateApprover(transaction.trunkTransaction, transactionPointer);
			if (transaction.branchTransactionPointer != transaction.trunkTransactionPointer)
			{

				updateApprover(transaction.branchTransaction, transactionPointer);
			}
		}


		private static void updateApprover(sbyte[] hash, long transactionPointer)
		{

			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11, prevPointer = 0;
			for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
			{

				// ((ByteBuffer)approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                approversChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

				if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
				{

					prevPointer = pointer;
					if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
					{

						setValue(mainBuffer, (hash[depth] + 128) << 3, ApproversNextPointer);
						// ((ByteBuffer)approversChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                        approversChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                        approversChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

						Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
						mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
						Array.Copy(hash, 0, mainBuffer, 8, Transaction.HASH_SIZE);
						setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
						appendToApprovers();

						return;
					}

				}
				else
				{

					for (int i = depth; i < Transaction.HASH_SIZE; i++)
					{

						if (mainBuffer[Transaction.HASH_OFFSET + i] != hash[i])
						{

							int differentHashByte = mainBuffer[Transaction.HASH_OFFSET + i];

							// ((ByteBuffer)approversChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                            approversChunks[(int)(pointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                            approversChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							setValue(mainBuffer, (hash[depth - 1] + 128) << 3, ApproversNextPointer);
							// ((ByteBuffer)approversChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            approversChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                            approversChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							for (int j = depth; j < i; j++)
							{

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, (hash[j] + 128) << 3, ApproversNextPointer + CELL_SIZE);
								appendToApprovers();
							}

							Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
							setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
							setValue(mainBuffer, (hash[i] + 128) << 3, ApproversNextPointer + CELL_SIZE);
							appendToApprovers();

							Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
							mainBuffer[Transaction.TYPE_OFFSET] = FILLED_SLOT;
							Array.Copy(hash, 0, mainBuffer, 8, Transaction.HASH_SIZE);
							setValue(mainBuffer, ZEROTH_POINTER_OFFSET, transactionPointer);
							appendToApprovers();

							return;
						}
					}

					int offset = ZEROTH_POINTER_OFFSET;
					while (true)
					{

						while ((offset += sizeof(long)) < CELL_SIZE - sizeof(long) && value(mainBuffer, offset) != 0)
						{

						// Do nothing
						}
						if (offset == CELL_SIZE - sizeof(long))
						{

							long nextCellPointer = value(mainBuffer, offset);
							if (nextCellPointer == 0)
							{

								setValue(mainBuffer, offset, ApproversNextPointer);
								// ((ByteBuffer)approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                approversChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, 0, transactionPointer);
								appendToApprovers();

								return;

							}
							else
							{

								pointer = nextCellPointer;
								// ((ByteBuffer)approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                                approversChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

								offset = -sizeof(long);
							}

						}
						else
						{

							setValue(mainBuffer, offset, transactionPointer);
							// ((ByteBuffer)approversChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                            approversChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
                            approversChunks[(int)(pointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

							return;
						}
					}
				}
			}
		}
	}

}