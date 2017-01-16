using System;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri.hash
{


    ///
    /// <summary> * (c) 2016 Come-from-Beyond </summary>
    /// 
	public class ISS
	{

		public const int NUMBER_OF_FRAGMENT_CHUNKS = 27;
		private const int FRAGMENT_LENGTH = Curl.HASH_LENGTH * NUMBER_OF_FRAGMENT_CHUNKS;
		private const int NUMBER_OF_SECURITY_LEVELS = 3;

		private const int MIN_TRIT_VALUE = -1, MAX_TRIT_VALUE = 1;
		private const int TRYTE_WIDTH = 3;
		private const int MIN_TRYTE_VALUE = -13, MAX_TRYTE_VALUE = 13;


		public static int[] subseed(int[] seed, int index)
		{

			if (index < 0)
			{

				throw new Exception("Invalid subseed index: " + index);
			}

			int[] subseedPreimage = new int[seed.Length];

			Array.Copy(seed, subseedPreimage, seed.Length);

			while (index-- > 0)
			{

				for (int i = 0; i < subseedPreimage.Length; i++)
				{

					if (++subseedPreimage[i] > MAX_TRIT_VALUE)
					{
						subseedPreimage[i] = MIN_TRIT_VALUE;
					}
					else
					{
						break;
					}
				}
			}

			int[] subseed = new int[Curl.HASH_LENGTH];

			Curl hash = new Curl();
			hash.absorb(subseedPreimage, 0, subseedPreimage.Length);
			hash.squeeze(subseed, 0, subseed.Length);

			return subseed;
		}


		public static int[] key(int[] subseed, int numberOfFragments)
		{

			if (subseed.Length != Curl.HASH_LENGTH)
			{

				throw new Exception("Invalid subseed length: " + subseed.Length);
			}
			if (numberOfFragments <= 0)
			{

				throw new Exception("Invalid number of key fragments: " + numberOfFragments);
			}


			int[] key = new int[FRAGMENT_LENGTH * numberOfFragments];

			Curl hash = new Curl();
			hash.absorb(subseed, 0, subseed.Length);
			hash.squeeze(key, 0, key.Length);

			return key;
		}


		public static int[] digests(int[] key)
		{

			if (key.Length == 0 || key.Length % FRAGMENT_LENGTH != 0)
			{

				throw new Exception("Invalid key length: " + key.Length);
			}


			int[] digests = new int[key.Length / FRAGMENT_LENGTH * Curl.HASH_LENGTH];

			for (int i = 0; i < key.Length / FRAGMENT_LENGTH; i++)
			{

				int[] buffer = Arrays.copyOfRange(key, i * FRAGMENT_LENGTH, (i + 1) * FRAGMENT_LENGTH);
				for (int j = 0; j < NUMBER_OF_FRAGMENT_CHUNKS; j++)
				{

					for (int k = MAX_TRYTE_VALUE - MIN_TRYTE_VALUE; k-- > 0;)
					{

						Curl chash = new Curl();
						chash.absorb(buffer, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
						chash.squeeze(buffer, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
					}
				}

				Curl hash = new Curl();
				hash.absorb(buffer, 0, buffer.Length);
				hash.squeeze(digests, i * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
			}

			return digests;
		}

		public static int[] address(int[] digests)
		{

			if (digests.Length == 0 || digests.Length % Curl.HASH_LENGTH != 0)
			{

				throw new Exception("Invalid digests length: " + digests.Length);
			}

			int[] address = new int[Curl.HASH_LENGTH];

			Curl hash = new Curl();
			hash.absorb(digests, 0, digests.Length);
			hash.squeeze(address, 0, address.Length);

			return address;
		}


		public static int[] normalizedBundle(int[] bundle)
		{

			if (bundle.Length != Curl.HASH_LENGTH)
			{

				throw new Exception("Invalid bundle length: " + bundle.Length);
			}

			int[] normalizedBundle = new int[Curl.HASH_LENGTH / TRYTE_WIDTH];

			for (int i = 0; i < NUMBER_OF_SECURITY_LEVELS; i++)
			{

				int sum = 0;
				for (int j = i * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j < (i + 1) * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j++)
				{

					normalizedBundle[j] = bundle[j * TRYTE_WIDTH] + bundle[j * TRYTE_WIDTH + 1] * 3 + bundle[j * TRYTE_WIDTH + 2] * 9;
					sum += normalizedBundle[j];
				}
				if (sum > 0)
				{

					while (sum-- > 0)
					{

						for (int j = i * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j < (i + 1) * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j++)
						{

							if (normalizedBundle[j] > MIN_TRYTE_VALUE)
							{

								normalizedBundle[j]--;

								break;
							}
						}
					}

				}
				else
				{

					while (sum++ < 0)
					{

						for (int j = i * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j < (i + 1) * (Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS); j++)
						{

							if (normalizedBundle[j] < MAX_TRYTE_VALUE)
							{

								normalizedBundle[j]++;

								break;
							}
						}
					}
				}
			}

			return normalizedBundle;
		}

		public static int[] signatureFragment(int[] normalizedBundleFragment, int[] keyFragment)
		{

			if (normalizedBundleFragment.Length != Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS)
			{

				throw new Exception("Invalid normalized bundle fragment length: " + normalizedBundleFragment.Length);
			}
			if (keyFragment.Length != FRAGMENT_LENGTH)
			{

				throw new Exception("Invalid key fragment length: " + keyFragment.Length);
			}

			int[] signatureFragment = new int[keyFragment.Length];

			Array.Copy(keyFragment, signatureFragment, keyFragment.Length);

			for (int j = 0; j < NUMBER_OF_FRAGMENT_CHUNKS; j++)
			{

				for (int k = MAX_TRYTE_VALUE - normalizedBundleFragment[j]; k-- > 0;)
				{

					Curl hash = new Curl();
					hash.absorb(signatureFragment, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
					hash.squeeze(signatureFragment, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
				}
			}

			return signatureFragment;
		}

		public static int[] digest(int[] normalizedBundleFragment, int[] signatureFragment)
		{

			if (normalizedBundleFragment.Length != Curl.HASH_LENGTH / TRYTE_WIDTH / NUMBER_OF_SECURITY_LEVELS)
			{

				throw new Exception("Invalid normalized bundle fragment length: " + normalizedBundleFragment.Length);
			}
			if (signatureFragment.Length != FRAGMENT_LENGTH)
			{

				throw new Exception("Invalid signature fragment length: " + signatureFragment.Length);
			}

			int[] digest = new int[Curl.HASH_LENGTH];

			int[] buffer = new int[FRAGMENT_LENGTH];

			Array.Copy(signatureFragment, buffer, FRAGMENT_LENGTH);
			for (int j = 0; j < NUMBER_OF_FRAGMENT_CHUNKS; j++)
			{

				for (int k = normalizedBundleFragment[j] - MIN_TRYTE_VALUE; k-- > 0;)
				{

					Curl chash = new Curl();
					chash.absorb(buffer, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
					chash.squeeze(buffer, j * Curl.HASH_LENGTH, Curl.HASH_LENGTH);
				}
			}

			Curl hash = new Curl();
			hash.absorb(buffer, 0, buffer.Length);
			hash.squeeze(digest, 0, digest.Length);

			return digest;
		}
	}
}