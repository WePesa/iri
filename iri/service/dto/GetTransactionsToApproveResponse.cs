using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{

	using Hash = com.iota.iri.model.Hash;

	public class GetTransactionsToApproveResponse : AbstractResponse
	{

		private string trunkTransaction;
		private string branchTransaction;

		public static AbstractResponse create(Hash trunkTransactionToApprove, Hash branchTransactionToApprove)
		{
			GetTransactionsToApproveResponse res = new GetTransactionsToApproveResponse();
			res.trunkTransaction = trunkTransactionToApprove.ToString();
			res.branchTransaction = branchTransactionToApprove.ToString();
			return res;
		}

		public virtual string BranchTransaction
		{
			get
			{
				return branchTransaction;
			}
		}

		public virtual string TrunkTransaction
		{
			get
			{
				return trunkTransaction;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetTransactionsToApproveResponse>(this)
              .Append(m => m.trunkTransaction)
              .Append(m => m.branchTransaction)       
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetTransactionsToApproveResponse>(this, that)
              .With(m => m.trunkTransaction)
              .With(m => m.branchTransaction)  
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetTransactionsToApproveResponse>(this)
              .With(m => m.trunkTransaction)
              .With(m => m.branchTransaction)  
              .HashCode;
        }
	}

}