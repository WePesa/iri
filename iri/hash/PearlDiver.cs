using System;
using System.Runtime.CompilerServices;
using System.Threading;

// 1.1.2.3

namespace com.iota.iri.hash
{

    ///
    /// <summary> * (c) 2016 Come-from-Beyond </summary>
    /// 
    public class PearlDiver
    {

        public const int TRANSACTION_LENGTH = 8019;

        private const int CURL_HASH_LENGTH = 243;
        private const int CURL_STATE_LENGTH = CURL_HASH_LENGTH * 3;

        private const int RUNNING = 0;
        private const int CANCELLED = 1;
        private const int COMPLETED = 2;

        private volatile int state;

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual void cancel()
        {
            state = CANCELLED;
            notifyAll();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public virtual bool search(int[] transactionTrits, int minWeightMagnitude, int numberOfThreads)
		{

			if (transactionTrits.Length != TRANSACTION_LENGTH)
			{
				throw new Exception("Invalid transaction trits length: " + transactionTrits.Length);
			}
			if (minWeightMagnitude < 0 || minWeightMagnitude > CURL_HASH_LENGTH)
			{
				throw new Exception("Invalid min weight magnitude: " + minWeightMagnitude);
			}

			state = RUNNING;

			long[] midCurlStateLow = new long[CURL_STATE_LENGTH], midCurlStateHigh = new long[CURL_STATE_LENGTH];

			{
				for (int i = CURL_HASH_LENGTH; i < CURL_STATE_LENGTH; i++)
				{

					midCurlStateLow[i] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
					midCurlStateHigh[i] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
				}

				int offset = 0;
				long[] curlScratchpadLow = new long[CURL_STATE_LENGTH], curlScratchpadHigh = new long[CURL_STATE_LENGTH];
				for (int i = (TRANSACTION_LENGTH - CURL_HASH_LENGTH) / CURL_HASH_LENGTH; i-- > 0;)
				{

					for (int j = 0; j < CURL_HASH_LENGTH; j++)
					{

						switch (transactionTrits[offset++])
						{

							case 0:
							{

								midCurlStateLow[j] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
								midCurlStateHigh[j] = 0b1111111111111111111111111111111111111111111111111111111111111111L;

							}
							break;

							case 1:
							{

								midCurlStateLow[j] = 0b0000000000000000000000000000000000000000000000000000000000000000L;
								midCurlStateHigh[j] = 0b1111111111111111111111111111111111111111111111111111111111111111L;

							}
							break;

							default:
							{

								midCurlStateLow[j] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
								midCurlStateHigh[j] = 0b0000000000000000000000000000000000000000000000000000000000000000L;
							}
						break;
						}
					}

					transform(midCurlStateLow, midCurlStateHigh, curlScratchpadLow, curlScratchpadHigh);
				}

				midCurlStateLow[0] = 0b1101101101101101101101101101101101101101101101101101101101101101L;
				midCurlStateHigh[0] = 0b1011011011011011011011011011011011011011011011011011011011011011L;
				midCurlStateLow[1] = 0b1111000111111000111111000111111000111111000111111000111111000111L;
				midCurlStateHigh[1] = 0b1000111111000111111000111111000111111000111111000111111000111111L;
				midCurlStateLow[2] = 0b0111111111111111111000000000111111111111111111000000000111111111L;
				midCurlStateHigh[2] = 0b1111111111000000000111111111111111111000000000111111111111111111L;
				midCurlStateLow[3] = 0b1111111111000000000000000000000000000111111111111111111111111111L;
				midCurlStateHigh[3] = 0b0000000000111111111111111111111111111111111111111111111111111111L;
			}

			if (numberOfThreads <= 0)
			{
				numberOfThreads = Runtime.Runtime.availableProcessors() - 1;
				if (numberOfThreads < 1)
				{
					numberOfThreads = 1;
				}
			}

			Thread[] workers = new Thread[numberOfThreads];

			while (numberOfThreads-- > 0)
			{

				int threadIndex = numberOfThreads;
				Thread worker = (new Thread(() => { long[] midCurlStateCopyLow = new long[CURL_STATE_LENGTH], midCurlStateCopyHigh = new long[CURL_STATE_LENGTH]; Array.Copy(midCurlStateLow, 0, midCurlStateCopyLow, 0, CURL_STATE_LENGTH); Array.Copy(midCurlStateHigh, 0, midCurlStateCopyHigh, 0, CURL_STATE_LENGTH); for (int i = threadIndex; i-- > 0;) { increment(midCurlStateCopyLow, midCurlStateCopyHigh, CURL_HASH_LENGTH / 3, (CURL_HASH_LENGTH / 3) * 2); } long[] curlStateLow = new long[CURL_STATE_LENGTH], curlStateHigh = new long[CURL_STATE_LENGTH]; final long[] curlScratchpadLow = new long[CURL_STATE_LENGTH], curlScratchpadHigh = new long[CURL_STATE_LENGTH]; while (state == RUNNING) { increment(midCurlStateCopyLow, midCurlStateCopyHigh, (CURL_HASH_LENGTH / 3) * 2, CURL_HASH_LENGTH); Array.Copy(midCurlStateCopyLow, 0, curlStateLow, 0, CURL_STATE_LENGTH); Array.Copy(midCurlStateCopyHigh, 0, curlStateHigh, 0, CURL_STATE_LENGTH); transform(curlStateLow, curlStateHigh, curlScratchpadLow, curlScratchpadHigh); NEXT_BIT_INDEX: for (int bitIndex = 64; bitIndex-- > 0;) { for (int i = minWeightMagnitude; i-- > 0;) { if ((((int)(curlStateLow[CURL_HASH_LENGTH - 1 - i] >> bitIndex)) & 1) != (((int)(curlStateHigh[CURL_HASH_LENGTH - 1 - i] >> bitIndex)) & 1)) { continue NEXT_BIT_INDEX; } } synchronized (this) { if (state == RUNNING) { state = COMPLETED; for (int i = 0; i < CURL_HASH_LENGTH; i++) { transactionTrits[TRANSACTION_LENGTH - CURL_HASH_LENGTH + i] = ((((int)(midCurlStateCopyLow[i] >> bitIndex)) & 1) == 0) ? 1 : (((((int)(midCurlStateCopyHigh[i] >> bitIndex)) & 1) == 0) ? -1 : 0); } notifyAll(); } } break; } } }));
				workers[threadIndex] = worker;
				worker.Start();
			}

			try
			{
				while (state == RUNNING)
				{
					wait();
				}
			}
			catch (InterruptedException e)
			{
				state = CANCELLED;
			}

			for (int i = 0; i < workers.Length; i++)
			{
				try
				{
					workers[i].Join();
				}
				catch (InterruptedException e)
				{
					state = CANCELLED;
				}
			}

			return state == COMPLETED;
		}

        private static void transform(long[] curlStateLow, long[] curlStateHigh, long[] curlScratchpadLow, long[] curlScratchpadHigh)
        {

            int curlScratchpadIndex = 0;
            for (int round = 27; round-- > 0; )
            {

                Array.Copy(curlStateLow, 0, curlScratchpadLow, 0, CURL_STATE_LENGTH);
                Array.Copy(curlStateHigh, 0, curlScratchpadHigh, 0, CURL_STATE_LENGTH);

                for (int curlStateIndex = 0; curlStateIndex < CURL_STATE_LENGTH; curlStateIndex++)
                {

                    long alpha = curlScratchpadLow[curlScratchpadIndex];
                    long beta = curlScratchpadHigh[curlScratchpadIndex];
                    long gamma = curlScratchpadHigh[curlScratchpadIndex += (curlScratchpadIndex < 365 ? 364 : -365)];
                    long delta = (alpha | (~gamma)) & (curlScratchpadLow[curlScratchpadIndex] ^ beta);

                    curlStateLow[curlStateIndex] = ~delta;
                    curlStateHigh[curlStateIndex] = (alpha ^ gamma) | delta;
                }
            }
        }

        private static void increment(long[] midCurlStateCopyLow, long[] midCurlStateCopyHigh, int fromIndex, int toIndex)
		{

			for (int i = fromIndex; i < toIndex; i++)
			{
				if (midCurlStateCopyLow[i] == 0b0000000000000000000000000000000000000000000000000000000000000000L)
				{
					midCurlStateCopyLow[i] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
					midCurlStateCopyHigh[i] = 0b0000000000000000000000000000000000000000000000000000000000000000L;
				}
				else
				{
					if (midCurlStateCopyHigh[i] == 0b0000000000000000000000000000000000000000000000000000000000000000L)
					{
						midCurlStateCopyHigh[i] = 0b1111111111111111111111111111111111111111111111111111111111111111L;
					}
					else
					{
						midCurlStateCopyLow[i] = 0b0000000000000000000000000000000000000000000000000000000000000000L;
					}
					break;
				}
			}
		}
    }

}