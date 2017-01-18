using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using slf4net;

// 1.1.2.3.

namespace com.iota.iri.service.storage
{


    // using Logger = org.slf4j.Logger;
    // using LoggerFactory = org.slf4j.LoggerFactory;

    using Hash = com.iota.iri.model.Hash;
    using Transaction = com.iota.iri.model.Transaction;

    public class StorageTransactions : AbstractStorage
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageTransactions));

        private static readonly StorageTransactions _instance = new StorageTransactions();
        private const string TRANSACTIONS_FILE_NAME = "transactions.iri";

        private static MemoryMappedFile transactionsChannel;
        private static MemoryMappedViewStream transactionsTipsFlags;

        private readonly static MemoryMappedViewStream[] transactionsChunks = new MemoryMappedViewStream[MAX_NUMBER_OF_CHUNKS];

        /// <summary>
        ///  original code was without volatile... it might be a bug so I would be on the safe side
        /// </summary>
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


        public override void init()
        {
            try
            {
                transactionsChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(TRANSACTIONS_FILE_NAME), FileMode.OpenOrCreate, "transactionsMap", TIPS_FLAGS_SIZE + SUPER_GROUPS_SIZE);
                transactionsTipsFlags = transactionsChannel.CreateViewStream(TIPS_FLAGS_OFFSET, TIPS_FLAGS_SIZE);

                transactionsChunks[0] = transactionsChannel.CreateViewStream(SUPER_GROUPS_OFFSET, SUPER_GROUPS_SIZE);

                long transactionsChannelSize = TIPS_FLAGS_SIZE + SUPER_GROUPS_SIZE; // transactionsChannel.size();

                while (true)
                {

                    if ((TransactionsNextPointer & (CHUNK_SIZE - 1)) == 0)
                    {
                        // transactionsChunks[(int) (TransactionsNextPointer >> 27)] = transactionsChannel.map(FileChannel.MapMode.READ_WRITE, SUPER_GROUPS_OFFSET + TransactionsNextPointer, CHUNK_SIZE);
                        transactionsChunks[(int)(TransactionsNextPointer >> 27)] = transactionsChannel.CreateViewStream(SUPER_GROUPS_OFFSET + TransactionsNextPointer, CHUNK_SIZE);
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
            }
            catch
            {
                throw new IOException();
            }
        }

        public virtual void updateBundleAddressTagApprovers()
        {
            if (TransactionsNextPointer == CELLS_OFFSET - SUPER_GROUPS_OFFSET)
            {

                // No need to zero "mainBuffer", it already contains only zeros
                setValue(mainBuffer, Transaction.TYPE_OFFSET, FILLED_SLOT);
                appendToTransactions(true);

                emptyMainBuffer();
                setValue(mainBuffer, 128 << 3, CELLS_OFFSET - SUPER_GROUPS_OFFSET);
                // ((ByteBuffer)transactionsChunks[0].position((128 + (128 << 8)) << 11)).put(mainBuffer);
                transactionsChunks[0].Position = (128 + (128 << 8)) << 11;
                transactionsChunks[0].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

                emptyMainBuffer();
                Storage.instance().updateBundleAddressTagAndApprovers(CELLS_OFFSET - SUPER_GROUPS_OFFSET);
            }
        }

        public override void shutdown()
        {
            transactionsTipsFlags.Flush();

            for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && transactionsChunks[i] != null; i++)
            {
                log.Info("Flushing transactions chunk #" + i);
                flush(transactionsChunks[i]);
            }
            try
            {
                transactionsChannel.Dispose();
            }
            catch (IOException e)
            {
                log.Error("Shutting down Storage Transaction error: ", e);
            }
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

                    log.Error("Caught exception on appendToTransactions:", e);
                }
            }
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
        public virtual Transaction loadTransaction(long pointer)
        {        
            // ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
            transactionsChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
            transactionsChunks[(int)(pointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length); 

            return new Transaction(mainBuffer, pointer);        
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual Transaction loadTransaction(sbyte[] hash)
        {
            long pointer = transactionPointer(hash);
            return pointer > 0 ? loadTransaction(pointer) : null;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static void setTransactionValidity(long pointer, int validity)
        {
            // transactionsChunks[(int)(pointer >> 27)].put(((int)(pointer & (CHUNK_SIZE - 1))) + Transaction.VALIDITY_OFFSET, (sbyte)validity);

            transactionsChunks[(int)(pointer >> 27)].Write(BitConverter.GetBytes(validity), ((int)(pointer & (CHUNK_SIZE - 1))) + Transaction.VALIDITY_OFFSET, 1);
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public static bool tipFlag(long pointer)
        {

            long index = (pointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) >> 11;
            // return (transactionsTipsFlags.get((int)(index >> 3)) & (1 << (index & 7))) != 0;

            sbyte[] sbyteArray = new sbyte[1];
            transactionsTipsFlags.Read((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
            long result = BitConverter.ToInt64((byte[])(Array)sbyteArray, 0);
            result &= (1 << (int)(index & 7));
            return result != 0;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual IList<Hash> tips()
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

        public virtual long storeTransaction(sbyte[] hash, Transaction transaction, bool tip) // Returns the pointer or 0 if the transaction was already in the storage and "transaction" value is not null
        {

            lock (typeof(Storage))
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
                                Storage.instance().updateBundleAddressTagAndApprovers(pointer);
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
                                transactionsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                transactionsChunks[(int)(prevPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

                                setValue(mainBuffer, (hash[depth - 1] + 128) << 3, TransactionsNextPointer);

                                // ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                transactionsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                transactionsChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

                                for (int j = depth; j < i; j++)
                                {

                                    emptyMainBuffer();
                                    setValue(mainBuffer, (hash[j] + 128) << 3, TransactionsNextPointer + CELL_SIZE);
                                    appendToTransactions(false);
                                }

                                emptyMainBuffer();
                                setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
                                setValue(mainBuffer, (hash[i] + 128) << 3, TransactionsNextPointer + CELL_SIZE);
                                appendToTransactions(false);

                                Transaction.dump(mainBuffer, hash, transaction);
                                pointer = TransactionsNextPointer;
                                appendToTransactions(transaction != null || tip);
                                if (transaction != null)
                                {
                                    Storage.instance().updateBundleAddressTagAndApprovers(pointer);
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
                                Storage.instance().updateBundleAddressTagAndApprovers(pointer);
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
        }

        public virtual MemoryMappedViewStream TransactionsTipsFlags()
        {
            return transactionsTipsFlags;
        }

        public static StorageTransactions instance()
        {
            return _instance;
        }

        public virtual Transaction loadMilestone(Hash latestMilestone)
        {
            return loadTransaction(transactionPointer(latestMilestone.Sbytes()));
        }
    }


}