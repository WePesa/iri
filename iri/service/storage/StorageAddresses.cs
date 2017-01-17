using System;
using System.Collections.Generic;
using System.IO;
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

		private FileChannel addressesChannel;
		private readonly ByteBuffer[] addressesChunks = new ByteBuffer[MAX_NUMBER_OF_CHUNKS];
		private volatile long addressesNextPointer = SUPER_GROUPS_SIZE;

		public override void init()
		{
		    try
		    {

		        addressesChannel = FileChannel.open(Paths.get(ADDRESSES_FILE_NAME), StandardOpenOption.CREATE,
		            StandardOpenOption.READ, StandardOpenOption.WRITE);
		        addressesChunks[0] = addressesChannel.map(FileChannel.MapMode.READ_WRITE, 0, SUPER_GROUPS_SIZE);
		        long addressesChannelSize = addressesChannel.size();
		        while (true)
		        {

		            if ((addressesNextPointer & (CHUNK_SIZE - 1)) == 0)
		            {
		                addressesChunks[(int) (addressesNextPointer >> 27)] =
		                    addressesChannel.map(FileChannel.MapMode.READ_WRITE, addressesNextPointer, CHUNK_SIZE);
		            }

		            if (addressesChannelSize - addressesNextPointer > CHUNK_SIZE)
		            {
		                addressesNextPointer += CHUNK_SIZE;
		            }
		            else
		            {

		                addressesChunks[(int) (addressesNextPointer >> 27)].get(mainBuffer);
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
		                addressesNextPointer += CELL_SIZE;
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
				addressesChannel.close();
			}
			catch (IOException e)
			{
				log.Error("Shutting down Storage Addresses error: ", e);
			}
		}

		public virtual long addressPointer(sbyte[] hash)
		{
			lock (typeof(Storage))
			{
			long pointer = ((hash[0] + 128) + ((hash[1] + 128) << 8)) << 11;
			for (int depth = 2; depth < Transaction.ADDRESS_SIZE; depth++)
			{

				((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
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
			}
			throw new IllegalStateException("Corrupted storage");
		}

		public virtual IList<long?> addressTransactions(long pointer)
		{

			lock (typeof(Storage))
			{
			IList<long?> addressTransactions = new LinkedList<>();

			if (pointer != 0)
			{

				((ByteBuffer) addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
				int offset = ZEROTH_POINTER_OFFSET - long.BYTES;
				while (true)
				{

					while ((offset += long.BYTES) < CELL_SIZE - long.BYTES)
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
					if (offset == CELL_SIZE - long.BYTES)
					{

						long nextCellPointer = value(mainBuffer, offset);
						if (nextCellPointer == 0)
						{
							break;
						}
						else
						{
							((ByteBuffer) addressesChunks[(int)(nextCellPointer >> 27)].position((int)(nextCellPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
							offset = -long.BYTES;
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
		}

		public virtual void updateAddresses(long transactionPointer, Transaction transaction)
		{
			{
				long pointer = ((transaction.address[0] + 128) + ((transaction.address[1] + 128) << 8)) << 11, prevPointer = 0;
				for (int depth = 2; depth < Transaction.ADDRESS_SIZE; depth++)
				{

					((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);

					if (mainBuffer[Transaction.TYPE_OFFSET] == GROUP)
					{

						prevPointer = pointer;
						if ((pointer = value(mainBuffer, (transaction.address[depth] + 128) << 3)) == 0)
						{

							setValue(mainBuffer, (transaction.address[depth] + 128) << 3, addressesNextPointer);
							((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

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

								((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
								setValue(mainBuffer, (transaction.address[depth - 1] + 128) << 3, addressesNextPointer);
								((ByteBuffer)addressesChunks[(int)(prevPointer >> 27)].position((int)(prevPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

								for (int j = depth; j < i; j++)
								{

									Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
									setValue(mainBuffer, (transaction.address[j] + 128) << 3, addressesNextPointer + CELL_SIZE);
									appendToAddresses();
								}

								Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
								setValue(mainBuffer, (differentHashByte + 128) << 3, pointer);
								setValue(mainBuffer, (transaction.address[i] + 128) << 3, addressesNextPointer + CELL_SIZE);
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

								while ((offset += long.BYTES) < CELL_SIZE - long.BYTES && value(mainBuffer, offset) != 0)
								{

								// Do nothing
								}
								if (offset == CELL_SIZE - long.BYTES)
								{

									long nextCellPointer = value(mainBuffer, offset);
									if (nextCellPointer == 0)
									{

										setValue(mainBuffer, offset, addressesNextPointer);
										((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);

										Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
										setValue(mainBuffer, 0, transactionPointer);
										appendToAddresses();
										break;

									}
									else
									{
										pointer = nextCellPointer;
										((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).get(mainBuffer);
										offset = -long.BYTES;
									}
								}
								else
								{
									setValue(mainBuffer, offset, transactionPointer);
									((ByteBuffer)addressesChunks[(int)(pointer >> 27)].position((int)(pointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
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

			((ByteBuffer)addressesChunks[(int)(addressesNextPointer >> 27)].position((int)(addressesNextPointer & (CHUNK_SIZE - 1)))).put(mainBuffer);
			if (((addressesNextPointer += CELL_SIZE) & (CHUNK_SIZE - 1)) == 0)
			{

				try
				{
					addressesChunks[(int)(addressesNextPointer >> 27)] = addressesChannel.map(FileChannel.MapMode.READ_WRITE, addressesNextPointer, CHUNK_SIZE);
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
			return addressTransactions(addressPointer(hash.bytes()));
		}
	}

}