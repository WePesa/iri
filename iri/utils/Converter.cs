using System;
using System.Text;
using iri.utils;

namespace com.iota.iri.utils
{

	public class Converter
	{

		public const int RADIX = 3;
		public const int MAX_TRIT_VALUE = (RADIX - 1) / 2, MIN_TRIT_VALUE = -MAX_TRIT_VALUE;

		public const int NUMBER_OF_TRITS_IN_A_BYTE = 5;
		public const int NUMBER_OF_TRITS_IN_A_TRYTE = 3;

		internal static readonly int[][] BYTE_TO_TRITS_MAPPINGS = new int[243][];
		internal static readonly int[][] TRYTE_TO_TRITS_MAPPINGS = new int[27][];

		public const string TRYTE_ALPHABET = "9ABCDEFGHIJKLMNOPQRSTUVWXYZ";

		public const int MIN_TRYTE_VALUE = -13, MAX_TRYTE_VALUE = 13;

		static Converter()
		{

			int[] trits = new int[NUMBER_OF_TRITS_IN_A_BYTE];

			for (int i = 0; i < 243; i++)
			{
				BYTE_TO_TRITS_MAPPINGS[i] = Arrays.copyOf(trits, NUMBER_OF_TRITS_IN_A_BYTE);
				increment(trits, NUMBER_OF_TRITS_IN_A_BYTE);
			}

			for (int i = 0; i < 27; i++)
			{
				TRYTE_TO_TRITS_MAPPINGS[i] = Arrays.copyOf(trits, NUMBER_OF_TRITS_IN_A_TRYTE);
				increment(trits, NUMBER_OF_TRITS_IN_A_TRYTE);
			}
		}

		public static long longValue(int[] trits, int offset, int size)
		{

			long value = 0;
			for (int i = size; i-- > 0;)
			{
				value = value * RADIX + trits[offset + i];
			}
			return value;
		}

		public static sbyte[] bytes(int[] trits, int offset, int size)
		{

			sbyte[] bytes = new sbyte[(size + NUMBER_OF_TRITS_IN_A_BYTE - 1) / NUMBER_OF_TRITS_IN_A_BYTE];
			for (int i = 0; i < bytes.Length; i++)
			{

				int value = 0;
				for (int j = (size - i * NUMBER_OF_TRITS_IN_A_BYTE) < 5 ? (size - i * NUMBER_OF_TRITS_IN_A_BYTE) : NUMBER_OF_TRITS_IN_A_BYTE; j-- > 0;)
				{
					value = value * RADIX + trits[offset + i * NUMBER_OF_TRITS_IN_A_BYTE + j];
				}
				bytes[i] = (sbyte)value;
			}

			return bytes;
		}

		public static sbyte[] bytes(int[] trits)
		{
			return bytes(trits, 0, trits.Length);
		}

		public static void getTrits(sbyte[] bytes, int[] trits)
		{

			int offset = 0;
			for (int i = 0; i < bytes.Length && offset < trits.Length; i++)
			{
				Array.Copy(BYTE_TO_TRITS_MAPPINGS[bytes[i] < 0 ? (bytes[i] + BYTE_TO_TRITS_MAPPINGS.Length) : bytes[i]], 0, trits, offset, trits.Length - offset < NUMBER_OF_TRITS_IN_A_BYTE ? (trits.Length - offset) : NUMBER_OF_TRITS_IN_A_BYTE);
				offset += NUMBER_OF_TRITS_IN_A_BYTE;
			}
			while (offset < trits.Length)
			{
				trits[offset++] = 0;
			}
		}

		public static int[] trits(string trytes)
		{

			int[] trits = new int[trytes.Length * NUMBER_OF_TRITS_IN_A_TRYTE];
			for (int i = 0; i < trytes.Length; i++)
			{
				Array.Copy(TRYTE_TO_TRITS_MAPPINGS[TRYTE_ALPHABET.IndexOf(trytes[i])], 0, trits, i * NUMBER_OF_TRITS_IN_A_TRYTE, NUMBER_OF_TRITS_IN_A_TRYTE);
			}

			return trits;
		}

		public static void copyTrits(long value, int[] destination, int offset, int size)
		{

			long absoluteValue = value < 0 ? -value : value;
			for (int i = 0; i < size; i++)
			{

				int remainder = (int)(absoluteValue % RADIX);
				absoluteValue /= RADIX;
				if (remainder > MAX_TRIT_VALUE)
				{

					remainder = MIN_TRIT_VALUE;
					absoluteValue++;
				}
				destination[offset + i] = remainder;
			}

			if (value < 0)
			{

				for (int i = 0; i < size; i++)
				{
					destination[offset + i] = -destination[offset + i];
				}
			}
		}


		public static string trytes(int[] trits, int offset, int size)
		{

			StringBuilder trytes = new StringBuilder();
			for (int i = 0; i < (size + NUMBER_OF_TRITS_IN_A_TRYTE - 1) / NUMBER_OF_TRITS_IN_A_TRYTE; i++)
			{

				int j = trits[offset + i * 3] + trits[offset + i * 3 + 1] * 3 + trits[offset + i * 3 + 2] * 9;
				if (j < 0)
				{

					j += TRYTE_ALPHABET.Length;
				}
				trytes.Append(TRYTE_ALPHABET[j]);
			}
			return trytes.ToString();
		}

		public static string trytes(int[] trits)
		{
			return trytes(trits, 0, trits.Length);
		}


		public static int tryteValue(int[] trits, int offset)
		{
			return trits[offset] + trits[offset + 1] * 3 + trits[offset + 2] * 9;
		}

		public static void increment(int[] trits, int size)
		{

			for (int i = 0; i < size; i++)
			{
				if (++trits[i] > Converter.MAX_TRIT_VALUE)
				{
					trits[i] = Converter.MIN_TRIT_VALUE;
				}
				else
				{
					break;
				}
			}
		}
	}

}