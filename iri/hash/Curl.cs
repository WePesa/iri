using System;

namespace com.iota.iri.hash
{

    ///
    /// <summary> * (c) 2016 Come-from-Beyond
    /// * 
    /// * Curl belongs to the sponge function family.
    /// *  </summary>
    /// 
	public class Curl
	{

		public const int HASH_LENGTH = 243;
		private const int STATE_LENGTH = 3 * HASH_LENGTH;

		private const int NUMBER_OF_ROUNDS = 27;
		private static readonly int[] TRUTH_TABLE = {1, 0, -1, 1, -1, 0, -1, 1, 0};

		private readonly int[] state = new int[STATE_LENGTH];

		public virtual void absorb(int[] trits, int offset, int length)
		{

			do
			{
				Array.Copy(trits, offset, state, 0, length < HASH_LENGTH ? length : HASH_LENGTH);
				transform();
				offset += HASH_LENGTH;
			} while ((length -= HASH_LENGTH) > 0);
		}


		public virtual void squeeze(int[] trits, int offset, int length)
		{

			do
			{
				Array.Copy(state, 0, trits, offset, length < HASH_LENGTH ? length : HASH_LENGTH);
				transform();
				offset += HASH_LENGTH;
			} while ((length -= HASH_LENGTH) > 0);
		}

		private void transform()
		{
			int[] scratchpad = new int[STATE_LENGTH];
			int scratchpadIndex = 0;
			for (int round = 0; round < NUMBER_OF_ROUNDS; round++)
			{
				Array.Copy(state, 0, scratchpad, 0, STATE_LENGTH);
				for (int stateIndex = 0; stateIndex < STATE_LENGTH; stateIndex++)
				{
					state[stateIndex] = TRUTH_TABLE[scratchpad[scratchpadIndex] + scratchpad[scratchpadIndex += (scratchpadIndex < 365 ? 364 : -365)] * 3 + 4];
				}
			}
		}

		public virtual void reset()
		{
			for (int stateIndex = 0; stateIndex < STATE_LENGTH; stateIndex++)
			{
				state[stateIndex] = 0;
			}
		}
	}

}