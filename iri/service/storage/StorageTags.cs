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

	public class StorageTags : AbstractStorage
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageTags));

        private static readonly StorageTags _instance = new StorageTags();

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

		private const string TAGS_FILE_NAME = "tags.iri";

		public override void init()
		{
		    try
		    {
                tagsChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(TAGS_FILE_NAME), FileMode.OpenOrCreate, "tagsMap", SUPER_GROUPS_SIZE);

                tagsChunks[0] = tagsChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

                long tagsChannelSize = SUPER_GROUPS_SIZE; // tagsChannel.size();

		        while (true)
		        {

		            if ((TagsNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {
		                // tagsChunks[(int) (TagsNextPointer >> 27)] = tagsChannel.map(FileChannel.MapMode.READ_WRITE, TagsNextPointer, CHUNK_SIZE);
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
		    }
		    catch
		    {
		        throw new IOException();
		    }
		}

		public override void shutdown()
		{
			for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && tagsChunks[i] != null; i++)
			{
				log.Info("Flushing tags chunk #" + i);
				flush(tagsChunks[i]);
			}
			try
			{
				tagsChannel.Dispose();
			}
			catch (Exception e)
			{
				log.Error("Shutting down Storage Tag error: ", e);
			}
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

                    log.Error("Caught exception on appendToTags:", e);
                }
            }
        }

		public virtual void updateTags(long transactionPointer, Transaction transaction)
		{
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
                                    tagsChunks[(int)(prevPointer >> 27)].Position = (int)(prevPointer & (CHUNK_SIZE - 1));
                                    tagsChunks[(int)(prevPointer >> 27)].Read((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

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
		}

		public static StorageTags instance()
		{
            return _instance;
		}
	}

}