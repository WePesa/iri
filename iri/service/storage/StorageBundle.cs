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

    using Transaction = com.iota.iri.model.Transaction;

    public class StorageBundle : AbstractStorage
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageBundle));

        private static readonly StorageBundle _instance = new StorageBundle();
        private const string BUNDLES_FILE_NAME = "bundles.iri";

        private MemoryMappedFile bundlesChannel;
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

        public override void init()
        {
            try
            {
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
            }
            catch
            {
                throw new IOException();
            }

        }

        public override void shutdown()
        {
            for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && bundlesChunks[i] != null; i++)
            {
                log.Info("Flushing bundles chunk #" + i);
                flush(bundlesChunks[i]);
            }

            try
            {
                bundlesChannel.Dispose();
            }
            catch (IOException e)
            {
                log.Error("Shutting down Storage Bundle error: ", e);
            }

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

        public virtual void updateBundle(long transactionPointer, Transaction transaction)
        {
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

                            emptyMainBuffer();
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
                                bundlesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                bundlesChunks[(int)(prevPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

                                setValue(mainBuffer, (transaction.bundle[depth - 1] + 128) << 3, BundlesNextPointer);
                                // ((ByteBuffer)bundlesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
                                bundlesChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                bundlesChunks[(int)(prevPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

                                for (int j = depth; j < i; j++)
                                {
                                    emptyMainBuffer();
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
        }

        private void appendToBundles()
        {

            // ((ByteBuffer)bundlesChunks[(int)(BundlesNextPointer >> 27)].position((int)(BundlesNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            bundlesChunks[(int)(BundlesNextPointer >> 27)].Position = (int)(BundlesNextPointer & (CHUNK_SIZE - 1));
            bundlesChunks[(int)(BundlesNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

            if (((BundlesNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
            {

                try
                {
                    // bundlesChunks[(int)(BundlesNextPointer >> 27)] = bundlesChannel.map(FileChannel.MapMode.READ_WRITE, BundlesNextPointer, CHUNK_SIZE);
                    bundlesChunks[(int)(BundlesNextPointer >> 27)] = bundlesChannel.CreateViewStream(BundlesNextPointer, CHUNK_SIZE);
                }
                catch (IOException e)
                {
                    log.Error("Caught exception on appendToBundles:", e);
                }
            }
        }

        public static StorageBundle instance()
        {
            return _instance;
        }

        private StorageBundle()
        {
        }
    }


}