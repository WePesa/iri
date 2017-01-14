using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace com.iota.iri.hash
{

	public class PearlDiver
	{

		private const int TRANSACTION_LENGTH = 8019;

		private const int HASH_LENGTH = 243;
		private const int STATE_LENGTH = HASH_LENGTH * 3;

		private bool finished, interrupted;

        // private static readonly object syncLock = new object();

		[MethodImpl(MethodImplOptions.Synchronized)]
		public virtual void interrupt()
		{

			finished = true;
			interrupted = true;

		    Monitor.PulseAll(this); // notifyAll();
		}

		[MethodImpl(MethodImplOptions.Synchronized)]
		public virtual bool search(int[] transactionTrits, int minWeightMagnitude, int numberOfThreads)
		{

			if (transactionTrits.Length != TRANSACTION_LENGTH)
			{
				throw new Exception("Invalid transaction trits length: " + transactionTrits.Length);
			}
			if (minWeightMagnitude < 0 || minWeightMagnitude > HASH_LENGTH)
			{
				throw new Exception("Invalid min weight magnitude: " + minWeightMagnitude);
			}

			finished = false;
			interrupted = false;

			long[] midStateLow = new long[STATE_LENGTH], midStateHigh = new long[STATE_LENGTH];

			{
				for (int i = HASH_LENGTH; i < STATE_LENGTH; i++)
				{
				    midStateLow[i] =  -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
				    midStateHigh[i] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
				}

				int offset = 0;

				long[] scratchpadLow = new long[STATE_LENGTH], scratchpadHigh = new long[STATE_LENGTH];
				for (int i = (TRANSACTION_LENGTH - HASH_LENGTH) / HASH_LENGTH; i-- > 0;)
				{

					for (int j = 0; j < HASH_LENGTH; j++)
					{

						switch (transactionTrits[offset++])
						{

							case 0:
						    {

						        midStateLow[j] =  -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
						        midStateHigh[j] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;

							}
							break;

							case 1:
						    {

						        midStateLow[j]   = 0; // 0b0000000000000000000000000000000000000000000000000000000000000000L;
						        midStateHigh[j] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;

							}
							break;

							default:
						    {

						        midStateLow[j] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
						        midStateHigh[j] = 0; // 0b0000000000000000000000000000000000000000000000000000000000000000L;
							}
						break;
						}
					}

					transform(midStateLow, midStateHigh, scratchpadLow, scratchpadHigh);
				}

			    midStateLow[0] =  -2635249153387078803;  // 0b1101101101101101101101101101101101101101101101101101101101101101L;
			    midStateHigh[0] = -5270498306774157605;  // 0b1011011011011011011011011011011011011011011011011011011011011011L;
			    midStateLow[1] =  -1010780497189564473;  // 0b1111000111111000111111000111111000111111000111111000111111000111L;
			    midStateHigh[1] = -8086243977516515777;  // 0b1000111111000111111000111111000111111000111111000111111000111111L;
			    midStateLow[2] =   9223336921201902079;  // 0b0111111111111111111000000000111111111111111111000000000111111111L;
			    midStateHigh[2] = -17979214271348737;    // 0b1111111111000000000111111111111111111000000000111111111111111111L;
			    midStateLow[3] =  -18014398375264257;    // 0b1111111111000000000000000000000000000111111111111111111111111111L;
			    midStateHigh[3] =  18014398509481983;    // 0b0000000000111111111111111111111111111111111111111111111111111111L;
			}

			if (numberOfThreads <= 0)
			{

				numberOfThreads = Environment.ProcessorCount - 1;
				if (numberOfThreads < 1)
				{

					numberOfThreads = 1;
				}
			}

			while (numberOfThreads-- > 0)
			{

				int threadIndex = numberOfThreads;
				(new Thread(() =>
				{
				    long[] midStateCopyLow = new long[STATE_LENGTH], midStateCopyHigh = new long[STATE_LENGTH]; 
                    Array.Copy(midStateLow, 0, midStateCopyLow, 0, STATE_LENGTH); 
                    Array.Copy(midStateHigh, 0, midStateCopyHigh, 0, STATE_LENGTH);
				    for (int i = threadIndex; i-- > 0;)
				    {
				        increment(midStateCopyLow, midStateCopyHigh, HASH_LENGTH / 3, (HASH_LENGTH / 3) * 2);
				    } 
                    long[] stateLow = new long[STATE_LENGTH], stateHigh = new long[STATE_LENGTH]; 
                    long[] scratchpadLow = new long[STATE_LENGTH], scratchpadHigh = new long[STATE_LENGTH];
				    while (!finished)
				    {
				        increment(midStateCopyLow, midStateCopyHigh, (HASH_LENGTH / 3) * 2, HASH_LENGTH);
                        Array.Copy(midStateCopyLow, 0, stateLow, 0, STATE_LENGTH); 
                        Array.Copy(midStateCopyHigh, 0, stateHigh, 0, STATE_LENGTH); 
                        transform(stateLow, stateHigh, scratchpadLow, scratchpadHigh); 
                    NEXT_BIT_INDEX:
				        for (int bitIndex = 64; bitIndex-- > 0;)
				        {
				            for (int i = minWeightMagnitude; i-- > 0;)
				            {
				                if ((((int) (stateLow[HASH_LENGTH - 1 - i] >> bitIndex)) & 1) !=
				                    (((int) (stateHigh[HASH_LENGTH - 1 - i] >> bitIndex)) & 1))
				                {
				                    goto NEXT_BIT_INDEX;
				                }
				            } 

                            finished = true; 

                            lock (this)
				            {
				                for (int i = 0; i < HASH_LENGTH; i++)
				                {
				                    transactionTrits[TRANSACTION_LENGTH - HASH_LENGTH + i] = ((((int)(midStateCopyLow[i] >> bitIndex)) & 1) == 0) ? 1 : (((((int)(midStateCopyHigh[i] >> bitIndex)) & 1) == 0) ? -1 : 0);
				                }
				                Monitor.PulseAll(this); // notifyAll();
				            } 

                            break;
				        }
				    }
				})).Start();
			}

			try
			{

			    Monitor.Wait(this); // wait();

			}
			catch (ThreadInterruptedException e)
			{
			// ignore
			}

			return interrupted;
		}

		private static void transform(long[] stateLow, long[] stateHigh, long[] scratchpadLow, long[] scratchpadHigh)
		{

			int scratchpadIndex = 0;
			for (int round = 27; round-- > 0;)
			{

				Array.Copy(stateLow, 0, scratchpadLow, 0, STATE_LENGTH);
				Array.Copy(stateHigh, 0, scratchpadHigh, 0, STATE_LENGTH);

				for (int stateIndex = 0; stateIndex < STATE_LENGTH; stateIndex++)
				{

					long alpha = scratchpadLow[scratchpadIndex];

					long beta = scratchpadHigh[scratchpadIndex];

					long gamma = scratchpadHigh[scratchpadIndex += (scratchpadIndex < 365 ? 364 : -365)];

					long delta = (alpha | (~gamma)) & (scratchpadLow[scratchpadIndex] ^ beta);

					stateLow[stateIndex] = ~delta;
					stateHigh[stateIndex] = (alpha ^ gamma) | delta;
				}
			}
		}

		private static void increment(long[] midStateCopyLow, long[] midStateCopyHigh, int fromIndex, int toIndex)
		{

			for (int i = fromIndex; i < toIndex; i++)
			{

				if (midStateCopyLow[i] == 0/* 0b0000000000000000000000000000000000000000000000000000000000000000L */)
				{
				    midStateCopyLow[i] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
				    midStateCopyHigh[i] = 0; // 0b0000000000000000000000000000000000000000000000000000000000000000L;
				}
				else
				{

					if (midStateCopyHigh[i] == 0 /* 0b0000000000000000000000000000000000000000000000000000000000000000L */)
					{
					    midStateCopyHigh[i] = -1; // 0b1111111111111111111111111111111111111111111111111111111111111111L;
					}
					else
					{
					    midStateCopyLow[i] = 0; // 0b0000000000000000000000000000000000000000000000000000000000000000L;
					}
					break;
				}
			}
		}
	}
}