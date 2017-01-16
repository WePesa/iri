using System;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri.model
{

	using Curl = com.iota.iri.hash.Curl;
	using Converter = com.iota.iri.utils.Converter;


	public class Hash
	{

		public const int SIZE_IN_BYTES = 49;

		public static readonly Hash NULL_HASH = new Hash(new int[Curl.HASH_LENGTH]);

		private readonly sbyte[] sbytes;
		private readonly int hashCode;

	// constructors' bill


		public Hash(sbyte[] sbytes, int offset, int size)
		{
			this.sbytes = new sbyte[SIZE_IN_BYTES];
			Array.Copy(sbytes, offset, this.sbytes, 0, size);
            hashCode = Arrays.hashCode(this.sbytes);
		}

		public Hash(sbyte[] bytes) : this(bytes, 0, SIZE_IN_BYTES)
		{
		}

		public Hash(int[] trits, int offset) : this(Converter.bytes(trits, offset, Curl.HASH_LENGTH))
		{
		}

		public Hash(int[] trits) : this(trits, 0)
		{
		}


		public Hash(string trytes) : this(Converter.trits(trytes))
		{
		}

	//

		public virtual int[] Trits()
		{
			int[] trits = new int[Curl.HASH_LENGTH];
			Converter.getTrits(sbytes, trits);
			return trits;
		}


		public override bool Equals(object obj)
		{
			return Array.Equals(sbytes, ((Hash)obj).sbytes);
		}

		public override int GetHashCode()
		{
			return hashCode;
		}

		public override string ToString()
		{
			return Converter.trytes(Trits());
		}

        public virtual sbyte[] Sbytes()
		{
			return sbytes;
		}
	}


}