using System.Collections.Generic;
using System.IO;
using slf4net;

// 1.1.2.3.

namespace com.iota.iri.service.storage
{


    using Logger = org.slf4j.Logger;
    using LoggerFactory = org.slf4j.LoggerFactory;

    using Hash = com.iota.iri.model.Hash;
    using Transaction = com.iota.iri.model.Transaction;

    public class StorageTransactions : AbstractStorage
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageTransactions));

        private static readonly StorageTransactions _instance = new StorageTransactions();
        private const string TRANSACTIONS_FILE_NAME = "transactions.iri";

        private FileChannel transactionsChannel;
        private ByteBuffer transactionsTipsFlags;

        private readonly ByteBuffer[] transactionsChunks = new ByteBuffer[MAX_NUMBER_OF_CHUNKS];

        public static long transactionsNextPointer = CELLS_OFFSET - SUPER_GROUPS_OFFSET;

        public override void init()
        {
            try
            {
                transactionsChannel = FileChannel.open(Paths.get(TRANSACTIONS_FILE_NAME), StandardOpenOption.CREATE,
                    StandardOpenOption.READ, StandardOpenOption.WRITE);
                transactionsTipsFlags = transactionsChannel.map(FileChannel.MapMode.READ_WRITE, TIPS_FLAGS_OFFSET,
                    TIPS_FLAGS_SIZE);
                transactionsChunks[0] = transactionsChannel.map(FileChannel.MapMode.READ_WRITE, SUPER_GROUPS_OFFSET,
                    SUPER_GROUPS_SIZE);
                long transactionsChannelSize = transactionsChannel.size();
                while (true)
                {

                    if ((transactionsNextPointer & (CHUNK_SIZE - 1)) == 0)
                    {
                        transactionsChunks[(int) (transactionsNextPointer >> 27)] =
                            transactionsChannel.map(FileChannel.MapMode.READ_WRITE,
                                SUPER_GROUPS_OFFSET + transactionsNextPointer, CHUNK_SIZE);
                    }
                    if (transactionsChannelSize - transactionsNextPointer - SUPER_GROUPS_OFFSET > CHUNK_SIZE)
                    {
                        transactionsNextPointer += CHUNK_SIZE;
                    }
                    else
                    {

                        transactionsChunks[(int) (transactionsNextPointer >> 27)].get(mainBuffer);
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
                        transactionsNextPointer += CELL_SIZE;
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
            if (transactionsNextPointer == CELLS_OFFSET - SUPER_GROUPS_OFFSET)
            {

                // No need to zero "mainBuffer", it already contains only zeros
                setValue(mainBuffer, Transaction.TYPE_OFFSET, FILLED_SLOT);
                appendToTransactions(true);

                emptyMainBuffer();
                setValue(mainBuffer, 128 << 3, CELLS_OFFSET - SUPER_GROUPS_OFFSET);
                ((ByteBuffer)transactionsChunks[0].position((128 + (128 << 8)) << 11)).put(mainBuffer);

                emptyMainBuffer();
                Storage.instance().updateBundleAddressTagAndApprovers(CELLS_OFFSET - SUPER_GROUPS_OFFSET);
            }
        }

        public override void shutdown()
        {
            ((MappedByteBuffer)transactionsTipsFlags).force();
            for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && transactionsChunks[i] != null; i++)
            {
                log.Info("Flushing transactions chunk #" + i);
                flush(transactionsChunks[i]);
            }
            try
            {
                transactionsChannel.close();
            }
            catch (IOException e)
            {
                log.Error("Shutting down Storage Transaction error: ", e);
            }
        }

        public virtual void appendToTransactions(bool tip)
        {

            ((ByteBuffer)transactionsChunks[(int)(transactionsNextPointer >> 27)].position((int)(transactionsNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

            if (tip)
            {
                long index = (transactionsNextPointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) >> 11;
                transactionsTipsFlags.put((int)(index >> 3), (sbyte)(transactionsTipsFlags.get((int)(index >> 3)) | (1 << (index & 7))));
            }

            if (((transactionsNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
            {

                try
                {
                    transactionsChunks[(int)(transactionsNextPointer >> 27)] = transactionsChannel.map(FileChannel.MapMode.READ_WRITE, SUPER_GROUPS_OFFSET + transactionsNextPointer, CHUNK_SIZE);
                }
                catch (IOException e)
                {
                    log.Error("Caught exception on appendToTransactions:", e);
                }
            }
        }

        public virtual long transactionPointer(sbyte[] hash) // Returns a negative value if the transaction hasn't been seen yet but was referenced
        {

            lock (typeof(Storage))
            {
                long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
                for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
                {

                    ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(auxBuffer);

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
            }
            throw new IllegalStateException("Corrupted storage");
        }

        public virtual Transaction loadTransaction(long pointer)
        {
            lock (typeof(Storage))
            {
                ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                return new Transaction(mainBuffer, pointer);
            }
        }

        public virtual Transaction loadTransaction(sbyte[] hash)
        {
            lock (typeof(Storage))
            {
                long pointer = transactionPointer(hash);
                return pointer > 0 ? loadTransaction(pointer) : null;
            }
        }

        public virtual void setTransactionValidity(long pointer, int validity)
        {
            lock (typeof(Storage))
            {
                transactionsChunks[(int)(pointer >> 27)].put(((int)(pointer & (CHUNK_SIZE - 1))) + Transaction.VALIDITY_OFFSET, (sbyte)validity);
            }
        }

        public virtual bool tipFlag(long pointer)
        {
            lock (typeof(Storage))
            {
                long index = (pointer - (CELLS_OFFSET - SUPER_GROUPS_OFFSET)) >> 11;
                return (transactionsTipsFlags.get((int)(index >> 3)) & (1 << (index & 7))) != 0;
            }
        }

        public virtual IList<Hash> tips()
		{
			lock (typeof(Storage))
			{
				IList<Hash> tips = new LinkedList<>();

				long pointer = CELLS_OFFSET - SUPER_GROUPS_OFFSET;
				while (pointer < transactionsNextPointer)
				{

					if (tipFlag(pointer))
					{
						tips.Add(new Hash(loadTransaction(pointer).hash, 0, Transaction.HASH_SIZE));
					}
					pointer += CELL_SIZE;
				}
				return tips;
			}
		}

        public virtual long storeTransaction(sbyte[] hash, Transaction transaction, bool tip) // Returns the pointer or 0 if the transaction was already in the storage and "transaction" value is not null
        {

            lock (typeof(Storage))
            {
                long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11, prevPointer = 0;

            MAIN_LOOP:
                for (int depth = 2; depth < Transaction.HASH_SIZE; depth++)
                {

                    ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);

                    if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
                    {

                        prevPointer = pointer;
                        if ((pointer = value(mainBuffer, (hash[depth] + 128) << 3)) == 0)
                        {

                            setValue(mainBuffer, (hash[depth] + 128) << 3, pointer = transactionsNextPointer);
                            ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

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

                                ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                                setValue(mainBuffer, (hash[depth - 1] + 128) << 3, transactionsNextPointer);
                                ((ByteBuffer)transactionsChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

                                for (int j = depth; j < i; j++)
                                {

                                    emptyMainBuffer();
                                    setValue(mainBuffer, (hash[j] + 128) << 3, transactionsNextPointer + CELL_SIZE);
                                    appendToTransactions(false);
                                }

                                emptyMainBuffer();
                                setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
                                setValue(mainBuffer, (hash[i] + 128) << 3, transactionsNextPointer + CELL_SIZE);
                                appendToTransactions(false);

                                Transaction.dump(mainBuffer, hash, transaction);
                                pointer = transactionsNextPointer;
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
                                ((ByteBuffer)transactionsChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
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

        public virtual ByteBuffer transactionsTipsFlags()
        {
            return transactionsTipsFlags;
        }

        public static StorageTransactions instance()
        {
            return _instance;
        }

        public virtual Transaction loadMilestone(Hash latestMilestone)
        {
            return loadTransaction(transactionPointer(latestMilestone.bytes()));
        }
    }


}