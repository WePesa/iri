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

	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;

	public class StorageAddresses : AbstractStorage
	{

		private static readonly ILogger log = LoggerFactory.GetLogger(typeof(StorageAddresses));

        private static readonly StorageAddresses _instance = new StorageAddresses();
		private const string ADDRESSES_FILE_NAME = "addresses.iri";

        private MemoryMappedFile addressesChannel;
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

		public override void init()
		{
		    try
		    {
                addressesChannel = MemoryMappedFile.CreateFromFile(Path.GetFileName(ADDRESSES_FILE_NAME), FileMode.OpenOrCreate, "addressesMap", SUPER_GROUPS_SIZE);

                addressesChunks[0] = addressesChannel.CreateViewStream(0, SUPER_GROUPS_SIZE);

                long addressesChannelSize = SUPER_GROUPS_SIZE; // addressesChannel.size();

		        while (true)
		        {

		            if ((AddressesNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {
                        addressesChunks[(int)(AddressesNextPointer >> 27)] =
                            addressesChannel.CreateViewStream(AddressesNextPointer, CHUNK_SIZE);
		            }

		            if (addressesChannelSize - AddressesNextPointer > CHUNK_SIZE)
		            {
		                AddressesNextPointer += CHUNK_SIZE;
		            }
		            else
		            {

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

		    }
            catch
		    {
		       throw new IOException(); 
		    }

		}

		public override void shutdown()
		{
			for (int i = 0; i < MAX_NUMBER_OF_CHUNKS && addressesChunks[i] != null; i++)
			{
				log.Info("Flushing addresses chunk #" + i);
				flush(addressesChunks[i]);
			}
			try
			{
				addressesChannel.Dispose();
			}
			catch (IOException e)
			{
				log.Error("Shutting down Storage Addresses error: ", e);
			}
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

		public virtual void updateAddresses(long transactionPointer, Transaction transaction)
		{
			{
				long pointer = ((transaction.address[0] + 128) + ((transaction.address[1] + 128) << 8)) << 11, prevPointer = 0;
				for (int depth = 2; depth < Transaction.ADDRESS_SIZE; depth++)
				{

					// ((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
                    addressesChunks[(int)(pointer >> 27)].Position = (int)(pointer & (CHUNK_SIZE - 1));
				    addressesChunks[(int) (pointer >> 27)].Read((byte[]) (Array) mainBuffer, 0, mainBuffer.Length);

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
		}

		private void appendToAddresses()
		{

			// ((ByteBuffer)addressesChunks[(int)(AddressesNextPointer >> 27)].position((int)(AddressesNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
            addressesChunks[(int)(AddressesNextPointer >> 27)].Position = (int)(AddressesNextPointer & (CHUNK_SIZE - 1));
            addressesChunks[(int)(AddressesNextPointer >> 27)].Write((byte[])(Array)mainBuffer, 0, mainBuffer.Length);

			if (((AddressesNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{
					// addressesChunks[(int)(AddressesNextPointer >> 27)] = addressesChannel.map(FileChannel.MapMode.READ_WRITE, AddressesNextPointer, CHUNK_SIZE);
                    addressesChunks[(int)(AddressesNextPointer >> 27)] = addressesChannel.CreateViewStream(AddressesNextPointer, CHUNK_SIZE);
				}
				catch (IOException e)
				{
					log.Error("Caught exception on appendToAddresses:", e);
				}
			}
		}


		public static StorageAddresses instance()
		{
            return _instance;
		}

		public virtual IList<long?> addressesOf(Hash hash)
		{
			return addressTransactions(addressPointer(hash.Sbytes()));
		}
	}

}