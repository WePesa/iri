using System;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Threading;
using slf4net;

// 1.1.2.3

namespace com.iota.iri.service.storage
{

	using Transaction = com.iota.iri.model.Transaction;

	public class StorageApprovers : AbstractStorage
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageApprovers));

        private static readonly StorageApprovers _instance = new StorageApprovers();

		private const string APPROVERS_FILE_NAME = "approvers.iri";
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

		public override void init()
		{
		    try
		    {
                approversChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(APPROVERS_FILE_NAME), FileMode.OpenOrCreate, "approversMap", SUPER_GROUPS_SIZE);

                approversChunks[0] = approversChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

                long approversChannelSize = SUPER_GROUPS_SIZE; // approversChannel.size();


		        while (true)
		        {

		            if ((ApproversNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {
                        approversChunks[(int)(ApproversNextPointer >> 27)] =
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
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		public override void shutdown()
		{
			for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && approversChunks[i] != null; i++)
			{
				log.Info("Flushing approvers chunk #" + i);
				flush(approversChunks[i]);
			}

			try
			{
				approversChannel.Dispose();
			}
			catch (Exception e)
			{
				log.Error("Shutting down Storage Approvers error: ", e);
			}
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

		private static void appendToApprovers()
		{

			// ((ByteBuffer)approversChunks[(int)(ApproversNextPointer >> 27)].position((int)(ApproversNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            approversChunks[(int)(ApproversNextPointer >> 27)].Position = (int)(ApproversNextPointer & (CHUNK_SIZE - 1));
            approversChunks[(int)(ApproversNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((ApproversNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{
					// approversChunks[(int)(ApproversNextPointer >> 27)] = approversChannel.map(FileChannel.MapMode.READ_WRITE, ApproversNextPointer, CHUNK_SIZE);
                    approversChunks[(int)(ApproversNextPointer >> 27)] = approversChannel.CreateViewStream(ApproversNextPointer, CHUNK_SIZE);
				}
				catch (IOException e)
				{
					log.Error("Caught exception on appendToApprovers:", e);
				}
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

		public static StorageApprovers instance()
		{
            return _instance;
		}
	}

}