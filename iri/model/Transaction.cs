using System;
using System.Runtime.CompilerServices;

namespace com.iota.iri.model
{

	using Curl = com.iota.iri.hash.Curl;
	using Storage = com.iota.iri.service.Storage;
	using Converter = com.iota.iri.utils.Converter;


	public class Transaction
	{

		public const int SIZE = 1604;

        public const int TYPE_OFFSET = 0, TYPE_SIZE = sizeof(sbyte);
		public const int HASH_OFFSET = TYPE_OFFSET + TYPE_SIZE + ((sizeof(long) - (TYPE_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), HASH_SIZE = 46;

		private const int BYTES_OFFSET = HASH_OFFSET + HASH_SIZE + ((sizeof(long) - (HASH_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), BYTES_SIZE = SIZE;

		public const int ADDRESS_OFFSET = BYTES_OFFSET + BYTES_SIZE + ((sizeof(long) - (BYTES_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), ADDRESS_SIZE = 49;
		public const int VALUE_OFFSET = ADDRESS_OFFSET + ADDRESS_SIZE + ((sizeof(long) - (ADDRESS_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), VALUE_SIZE = sizeof(long);
		public const int TAG_OFFSET = VALUE_OFFSET + VALUE_SIZE + ((sizeof(long) - (VALUE_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), TAG_SIZE = 17;
		private const int CURRENT_INDEX_OFFSET = TAG_OFFSET + TAG_SIZE + ((sizeof(long) - (TAG_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), CURRENT_INDEX_SIZE = sizeof(long);
		private const int LAST_INDEX_OFFSET = CURRENT_INDEX_OFFSET + CURRENT_INDEX_SIZE + ((sizeof(long) - (CURRENT_INDEX_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), LAST_INDEX_SIZE = sizeof(long);
		public const int BUNDLE_OFFSET = LAST_INDEX_OFFSET + LAST_INDEX_SIZE + ((sizeof(long) - (LAST_INDEX_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), BUNDLE_SIZE = 49;
		private const int TRUNK_TRANSACTION_OFFSET = BUNDLE_OFFSET + BUNDLE_SIZE + ((sizeof(long) - (BUNDLE_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), TRUNK_TRANSACTION_SIZE = HASH_SIZE;
		private const int BRANCH_TRANSACTION_OFFSET = TRUNK_TRANSACTION_OFFSET + TRUNK_TRANSACTION_SIZE + ((sizeof(long) - (TRUNK_TRANSACTION_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), BRANCH_TRANSACTION_SIZE = HASH_SIZE;

		public const int VALIDITY_OFFSET = BRANCH_TRANSACTION_OFFSET + BRANCH_TRANSACTION_SIZE + ((sizeof(long) - (BRANCH_TRANSACTION_SIZE & (sizeof(long) - 1))) & (sizeof(long) - 1)), VALIDITY_SIZE = 1;

		public const long SUPPLY = 2779530283277761L; // = (3^33 - 1) / 2

		public const int SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET = 0, SIGNATURE_MESSAGE_FRAGMENT_TRINARY_SIZE = 6561;
		public const int ADDRESS_TRINARY_OFFSET = SIGNATURE_MESSAGE_FRAGMENT_TRINARY_OFFSET + SIGNATURE_MESSAGE_FRAGMENT_TRINARY_SIZE, ADDRESS_TRINARY_SIZE = 243;
		public const int VALUE_TRINARY_OFFSET = ADDRESS_TRINARY_OFFSET + ADDRESS_TRINARY_SIZE, VALUE_TRINARY_SIZE = 81, VALUE_USABLE_TRINARY_SIZE = 33;
		public const int TAG_TRINARY_OFFSET = VALUE_TRINARY_OFFSET + VALUE_TRINARY_SIZE, TAG_TRINARY_SIZE = 81;
		private const int TIMESTAMP_TRINARY_OFFSET = TAG_TRINARY_OFFSET + TAG_TRINARY_SIZE, TIMESTAMP_TRINARY_SIZE = 27;
		public const int CURRENT_INDEX_TRINARY_OFFSET = TIMESTAMP_TRINARY_OFFSET + TIMESTAMP_TRINARY_SIZE, CURRENT_INDEX_TRINARY_SIZE = 27;
		private const int LAST_INDEX_TRINARY_OFFSET = CURRENT_INDEX_TRINARY_OFFSET + CURRENT_INDEX_TRINARY_SIZE, LAST_INDEX_TRINARY_SIZE = 27;
		public const int BUNDLE_TRINARY_OFFSET = LAST_INDEX_TRINARY_OFFSET + LAST_INDEX_TRINARY_SIZE, BUNDLE_TRINARY_SIZE = 243;
		public const int TRUNK_TRANSACTION_TRINARY_OFFSET = BUNDLE_TRINARY_OFFSET + BUNDLE_TRINARY_SIZE, TRUNK_TRANSACTION_TRINARY_SIZE = 243;
		public const int BRANCH_TRANSACTION_TRINARY_OFFSET = TRUNK_TRANSACTION_TRINARY_OFFSET + TRUNK_TRANSACTION_TRINARY_SIZE, BRANCH_TRANSACTION_TRINARY_SIZE = 243;
		private const int NONCE_TRINARY_OFFSET = BRANCH_TRANSACTION_TRINARY_OFFSET + BRANCH_TRANSACTION_TRINARY_SIZE, NONCE_TRINARY_SIZE = 243;

		public const int TRINARY_SIZE = NONCE_TRINARY_OFFSET + NONCE_TRINARY_SIZE;

		public const int ESSENCE_TRINARY_OFFSET = ADDRESS_TRINARY_OFFSET, ESSENCE_TRINARY_SIZE = ADDRESS_TRINARY_SIZE + VALUE_TRINARY_SIZE + TAG_TRINARY_SIZE + TIMESTAMP_TRINARY_SIZE + CURRENT_INDEX_TRINARY_SIZE + LAST_INDEX_TRINARY_SIZE;

		private const int MIN_WEIGHT_MAGNITUDE = 18;

		public readonly int type;
		public readonly sbyte[] hash;

		public readonly sbyte[] bytes;

		public readonly sbyte[] address;
		public readonly long value;
		public readonly sbyte[] tag;
		public readonly long currentIndex, lastIndex;
		public readonly sbyte[] bundle;
		public readonly sbyte[] trunkTransaction;
		public readonly sbyte[] branchTransaction;

		public long trunkTransactionPointer;
		public long branchTransactionPointer;
		private readonly int validity;

		private int[] trits;

		public readonly long pointer;

		public int weightMagnitude;

		public Transaction(int[] trits)
		{

			this.trits = trits;
			bytes = Converter.bytes(trits);

			Curl curl = new Curl();
			curl.absorb(trits, 0, TRINARY_SIZE);
			int[] hashTrits = new int[Curl.HASH_LENGTH];
			curl.squeeze(hashTrits, 0, hashTrits.Length);
			hash = new sbyte[HASH_SIZE];
			Array.Copy(Converter.bytes(hashTrits), hash, HASH_SIZE);

			address = Converter.bytes(trits, ADDRESS_TRINARY_OFFSET, ADDRESS_TRINARY_SIZE);
			value = Converter.longValue(trits, VALUE_TRINARY_OFFSET, VALUE_USABLE_TRINARY_SIZE);
			Array.Copy(Converter.bytes(trits, TAG_TRINARY_OFFSET, TAG_TRINARY_SIZE), 0, tag = new sbyte[TAG_SIZE], 0, TAG_SIZE);
			currentIndex = Converter.longValue(trits, CURRENT_INDEX_TRINARY_OFFSET, CURRENT_INDEX_TRINARY_SIZE);
			lastIndex = Converter.longValue(trits, LAST_INDEX_TRINARY_OFFSET, LAST_INDEX_TRINARY_SIZE);
			Array.Copy(Converter.bytes(trits, BUNDLE_TRINARY_OFFSET, BUNDLE_TRINARY_SIZE), 0, bundle = new sbyte[BUNDLE_SIZE], 0, BUNDLE_SIZE);
			Array.Copy(Converter.bytes(trits, TRUNK_TRANSACTION_TRINARY_OFFSET, TRUNK_TRANSACTION_TRINARY_SIZE), 0, trunkTransaction = new sbyte[TRUNK_TRANSACTION_SIZE], 0, TRUNK_TRANSACTION_SIZE);
			Array.Copy(Converter.bytes(trits, BRANCH_TRANSACTION_TRINARY_OFFSET, BRANCH_TRANSACTION_TRINARY_SIZE), 0, branchTransaction = new sbyte[BRANCH_TRANSACTION_SIZE], 0, BRANCH_TRANSACTION_SIZE);

			type = Storage.FILLED_SLOT;

			trunkTransactionPointer = 0;
			branchTransactionPointer = 0;
			validity = 0;

			pointer = 0;
		}

		public Transaction(sbyte[] bytes, int[] trits, Curl curl)
		{

			this.bytes = new sbyte[BYTES_SIZE];
			Array.Copy(bytes, this.bytes, BYTES_SIZE);
			Converter.getTrits(this.bytes, this.trits = trits);

			for (int i = VALUE_TRINARY_OFFSET + VALUE_USABLE_TRINARY_SIZE; i < VALUE_TRINARY_OFFSET + VALUE_TRINARY_SIZE; i++)
			{

				if (trits[i] != 0)
				{

					throw new Exception("Invalid transaction value");
				}
			}

			curl.reset();
			curl.absorb(trits, 0, TRINARY_SIZE);

			int[] hashTrits = new int[Curl.HASH_LENGTH];
			curl.squeeze(hashTrits, 0, hashTrits.Length);

			hash = Converter.bytes(hashTrits);
			if (hash[Hash.SIZE_IN_BYTES - 4] != 0 || hash[Hash.SIZE_IN_BYTES - 3] != 0 || hash[Hash.SIZE_IN_BYTES - 2] != 0 || hash[Hash.SIZE_IN_BYTES - 1] != 0)
			{

				throw new Exception("Invalid transaction hash");
			}

			weightMagnitude = MIN_WEIGHT_MAGNITUDE;
			while (weightMagnitude < Curl.HASH_LENGTH && hashTrits[Curl.HASH_LENGTH - weightMagnitude - 1] == 0)
			{

				weightMagnitude++;
			}

			address = Converter.bytes(trits, ADDRESS_TRINARY_OFFSET, ADDRESS_TRINARY_SIZE);
			value = Converter.longValue(trits, VALUE_TRINARY_OFFSET, VALUE_USABLE_TRINARY_SIZE);
			Array.Copy(Converter.bytes(trits, TAG_TRINARY_OFFSET, TAG_TRINARY_SIZE), 0, tag = new sbyte[TAG_SIZE], 0, TAG_SIZE);
			currentIndex = Converter.longValue(trits, CURRENT_INDEX_TRINARY_OFFSET, CURRENT_INDEX_TRINARY_SIZE);
			lastIndex = Converter.longValue(trits, LAST_INDEX_TRINARY_OFFSET, LAST_INDEX_TRINARY_SIZE);
			Array.Copy(Converter.bytes(trits, BUNDLE_TRINARY_OFFSET, BUNDLE_TRINARY_SIZE), 0, bundle = new sbyte[BUNDLE_SIZE], 0, BUNDLE_SIZE);
			Array.Copy(Converter.bytes(trits, TRUNK_TRANSACTION_TRINARY_OFFSET, TRUNK_TRANSACTION_TRINARY_SIZE), 0, trunkTransaction = new sbyte[TRUNK_TRANSACTION_SIZE], 0, TRUNK_TRANSACTION_SIZE);
			Array.Copy(Converter.bytes(trits, BRANCH_TRANSACTION_TRINARY_OFFSET, BRANCH_TRANSACTION_TRINARY_SIZE), 0, branchTransaction = new sbyte[BRANCH_TRANSACTION_SIZE], 0, BRANCH_TRANSACTION_SIZE);

			type = Storage.FILLED_SLOT;

			trunkTransactionPointer = 0;
			branchTransactionPointer = 0;
			validity = 0;

			pointer = 0;
		}

		public Transaction(sbyte[] mainBuffer, long pointer)
		{

			type = mainBuffer[TYPE_OFFSET];
			Array.Copy(mainBuffer, HASH_OFFSET, hash = new sbyte[HASH_SIZE], 0, HASH_SIZE);

			Array.Copy(mainBuffer, BYTES_OFFSET, bytes = new sbyte[BYTES_SIZE], 0, BYTES_SIZE);

			Array.Copy(mainBuffer, ADDRESS_OFFSET, address = new sbyte[ADDRESS_SIZE], 0, ADDRESS_SIZE);
			value = Storage.value(mainBuffer, VALUE_OFFSET);
			Array.Copy(mainBuffer, TAG_OFFSET, tag = new sbyte[TAG_SIZE], 0, TAG_SIZE);
			currentIndex = Storage.value(mainBuffer, CURRENT_INDEX_OFFSET);
			lastIndex = Storage.value(mainBuffer, LAST_INDEX_OFFSET);
			Array.Copy(mainBuffer, BUNDLE_OFFSET, bundle = new sbyte[BUNDLE_SIZE], 0, BUNDLE_SIZE);
			Array.Copy(mainBuffer, TRUNK_TRANSACTION_OFFSET, trunkTransaction = new sbyte[TRUNK_TRANSACTION_SIZE], 0, TRUNK_TRANSACTION_SIZE);
			Array.Copy(mainBuffer, BRANCH_TRANSACTION_OFFSET, branchTransaction = new sbyte[BRANCH_TRANSACTION_SIZE], 0, BRANCH_TRANSACTION_SIZE);

			trunkTransactionPointer = Storage.transactionPointer(trunkTransaction);
			if (trunkTransactionPointer < 0)
			{

				trunkTransactionPointer = -trunkTransactionPointer;
			}
			branchTransactionPointer = Storage.transactionPointer(branchTransaction);
			if (branchTransactionPointer < 0)
			{

				branchTransactionPointer = -branchTransactionPointer;
			}

			validity = mainBuffer[VALIDITY_OFFSET];

			this.pointer = pointer;
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public virtual int[] Trits()
		{

			if (trits == null)
			{

				trits = new int[TRINARY_SIZE];
				Converter.getTrits(bytes, trits);
			}

			return trits;
		}


		public static void dump(sbyte[] mainBuffer, sbyte[] hash, Transaction transaction)
		{

			Array.Copy(Storage.ZEROED_BUFFER, 0, mainBuffer, 0, Storage.CELL_SIZE);
			Array.Copy(hash, 0, mainBuffer, HASH_OFFSET, HASH_SIZE);

			if (transaction == null)
			{

				mainBuffer[TYPE_OFFSET] = Storage.PREFILLED_SLOT;
			}
			else
			{

				mainBuffer[TYPE_OFFSET] = (sbyte)transaction.type;

				Array.Copy(transaction.bytes, 0, mainBuffer, BYTES_OFFSET, BYTES_SIZE);
				Array.Copy(transaction.address, 0, mainBuffer, ADDRESS_OFFSET, ADDRESS_SIZE);
				Storage.setValue(mainBuffer, VALUE_OFFSET, transaction.value);

				int[] trits = transaction.Trits();
				Array.Copy(Converter.bytes(trits, TAG_TRINARY_OFFSET, TAG_TRINARY_SIZE), 0, mainBuffer, TAG_OFFSET, TAG_SIZE);
				Storage.setValue(mainBuffer, CURRENT_INDEX_OFFSET, transaction.currentIndex);
				Storage.setValue(mainBuffer, LAST_INDEX_OFFSET, transaction.lastIndex);
				Array.Copy(Converter.bytes(trits, BUNDLE_TRINARY_OFFSET, BUNDLE_TRINARY_SIZE), 0, mainBuffer, BUNDLE_OFFSET, BUNDLE_SIZE);
				Array.Copy(transaction.trunkTransaction, 0, mainBuffer, TRUNK_TRANSACTION_OFFSET, TRUNK_TRANSACTION_SIZE);
				Array.Copy(transaction.branchTransaction, 0, mainBuffer, BRANCH_TRANSACTION_OFFSET, BRANCH_TRANSACTION_SIZE);

				long approvedTransactionPointer = Storage.transactionPointer(transaction.trunkTransaction);
				if (approvedTransactionPointer == 0)
				{

					Storage.approvedTransactionsToStore[Storage.numberOfApprovedTransactionsToStore++] = transaction.trunkTransaction;

				}
				else
				{

					if (approvedTransactionPointer < 0)
					{
						approvedTransactionPointer = -approvedTransactionPointer;
					}

					long index = (approvedTransactionPointer - (Storage.CELLS_OFFSET - Storage.SUPER_GROUPS_OFFSET)) >> 11;
					// Storage.transactionsTipsFlags.put((int)(index >> 3), (sbyte)(Storage.transactionsTipsFlags.get((int)(index >> 3)) & (0xFF ^ (1 << (index & 7)))));
                    sbyte[] sbyteArray = new sbyte[1];
                    Storage.transactionsTipsFlags.Read((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
                    sbyteArray[0] &= (sbyte)(0xFF ^ (1 << (int)(index & 7)));
                    Storage.transactionsTipsFlags.Write((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
				}
				if (!Array.Equals(transaction.branchTransaction, transaction.trunkTransaction))
				{

					approvedTransactionPointer = Storage.transactionPointer(transaction.branchTransaction);
					if (approvedTransactionPointer == 0)
					{

						Storage.approvedTransactionsToStore[Storage.numberOfApprovedTransactionsToStore++] = transaction.branchTransaction;

					}
					else
					{

						if (approvedTransactionPointer < 0)
						{

							approvedTransactionPointer = -approvedTransactionPointer;
						}

						long index = (approvedTransactionPointer - (Storage.CELLS_OFFSET - Storage.SUPER_GROUPS_OFFSET)) >> 11;
						// Storage.transactionsTipsFlags.put((int)(index >> 3), (sbyte)(Storage.transactionsTipsFlags.get((int)(index >> 3)) & (0xFF ^ (1 << (index & 7)))));
                        sbyte[] sbyteArray = new sbyte[1];
                        Storage.transactionsTipsFlags.Read((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
                        sbyteArray[0] &= (sbyte)(0xFF ^ (1 << (int)(index & 7)));
                        Storage.transactionsTipsFlags.Write((byte[])(Array)sbyteArray, (int)(index >> 3), 1);
					}
				}
			}
		}

		public virtual long Value()
		{
			return value;
		}

		public virtual int Validity()
		{
			return validity;
		}
	}


}