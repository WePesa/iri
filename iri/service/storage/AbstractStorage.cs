using System;

// 1.1.2.3

namespace com.iota.iri.service.storage
{


	public abstract class AbstractStorage
	{

		public const int CELL_SIZE = 2048;

		public const int CELLS_PER_CHUNK = 65536;
		public const int CHUNK_SIZE = CELL_SIZE * CELLS_PER_CHUNK;
		public const int MAX_NUMBER_OF_CHUNKS = 16384; // Limits the storage capacity to ~1 billion transactions

		public const int TIPS_FLAGS_OFFSET = 0, TIPS_FLAGS_SIZE = MAX_NUMBER_OF_CHUNKS * CELLS_PER_CHUNK / sbyte.SIZE;

		public const int SUPER_GROUPS_OFFSET = TIPS_FLAGS_OFFSET + TIPS_FLAGS_SIZE, SUPER_GROUPS_SIZE = (short.MAX_VALUE - short.MIN_VALUE + 1) * CELL_SIZE;

		public const int CELLS_OFFSET = SUPER_GROUPS_OFFSET + SUPER_GROUPS_SIZE;

		public const int TRANSACTIONS_TO_REQUEST_OFFSET = 0, TRANSACTIONS_TO_REQUEST_SIZE = CHUNK_SIZE;

		public const int ANALYZED_TRANSACTIONS_FLAGS_OFFSET = TRANSACTIONS_TO_REQUEST_OFFSET + TRANSACTIONS_TO_REQUEST_SIZE, ANALYZED_TRANSACTIONS_FLAGS_SIZE = MAX_NUMBER_OF_CHUNKS * CELLS_PER_CHUNK / sbyte.SIZE;

		public const int ANALYZED_TRANSACTIONS_FLAGS_COPY_OFFSET = ANALYZED_TRANSACTIONS_FLAGS_OFFSET + ANALYZED_TRANSACTIONS_FLAGS_SIZE, ANALYZED_TRANSACTIONS_FLAGS_COPY_SIZE = ANALYZED_TRANSACTIONS_FLAGS_SIZE;

		public const int GROUP = 0; // transactions GROUP means that's it's a non-leaf node (leafs store transaction bytes)
		public const int PREFILLED_SLOT = 1; // means that we know only hash of the tx, the rest is unknown yet: only another tx references that hash
		public const int FILLED_SLOT = -1; // knows the hash only coz another tx references that hash

		protected internal const int ZEROTH_POINTER_OFFSET = 64;

		protected internal static readonly sbyte[] ZEROED_BUFFER = new sbyte[CELL_SIZE];

		protected internal static readonly sbyte[] mainBuffer = new sbyte[CELL_SIZE];
		protected internal static readonly sbyte[] auxBuffer = new sbyte[CELL_SIZE];

//JAVA TO VB & C# CONVERTER WARNING: 'final' parameters are not allowed in .NET:
//ORIGINAL LINE: public static long value(final byte[] buffer, final int offset)
		public static long value(sbyte[] buffer, int offset)
		{
			return ((long)(buffer[offset] & 0xFF)) + (((long)(buffer[offset + 1] & 0xFF)) << 8) + (((long)(buffer[offset + 2] & 0xFF)) << 16) + (((long)(buffer[offset + 3] & 0xFF)) << 24) + (((long)(buffer[offset + 4] & 0xFF)) << 32) + (((long)(buffer[offset + 5] & 0xFF)) << 40) + (((long)(buffer[offset + 6] & 0xFF)) << 48) + (((long)(buffer[offset + 7] & 0xFF)) << 56);
		}

//JAVA TO VB & C# CONVERTER WARNING: 'final' parameters are not allowed in .NET:
//ORIGINAL LINE: public static void setValue(final byte[] buffer, final int offset, final long value)
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

		protected internal static bool flush(ByteBuffer buffer)
		{

			try
			{
				((MappedByteBuffer) buffer).force();
				return true;

			}
			catch (Exception e)
			{
				return false;
			}
		}

		protected internal virtual void emptyMainBuffer()
		{
			Array.Copy(ZEROED_BUFFER, 0, mainBuffer, 0, CELL_SIZE);
		}

//JAVA TO VB & C# CONVERTER WARNING: Method 'throws' clauses are not available in .NET:
//ORIGINAL LINE: public abstract void init() throws IOException;
		public abstract void init();

		public abstract void shutdown();
	}

}