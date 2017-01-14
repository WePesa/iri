using System.Collections.Generic;

namespace com.iota.iri
{

	using Hash = com.iota.iri.model.Hash;
	using Transaction = com.iota.iri.model.Transaction;


	public class Snapshot
	{ 

        public static readonly IDictionary<Hash, long?> initialState = new Dictionary<Hash, long?>();

		static Snapshot()
		{
			initialState.Add(Hash.NULL_HASH, Transaction.SUPPLY);
		}
	}

}