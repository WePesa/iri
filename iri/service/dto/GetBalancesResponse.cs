using System.Collections.Generic;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{

	using Hash = com.iota.iri.model.Hash;


	public class GetBalancesResponse : AbstractResponse
	{

		private IList<string> balances;
		private string milestone;
		private int milestoneIndex;

		public static AbstractResponse create(IList<string> elements, Hash milestone, int milestoneIndex)
		{
			GetBalancesResponse res = new GetBalancesResponse();
			res.balances = elements;
			res.milestone = milestone.ToString();
			res.milestoneIndex = milestoneIndex;
			return res;
		}

		public virtual string Milestone
		{
			get
			{
				return milestone;
			}
		}

		public virtual int MilestoneIndex
		{
			get
			{
				return milestoneIndex;
			}
		}

		public virtual IList<string> Balances
		{
			get
			{
				return balances;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetBalancesResponse>(this)
              .Append(m => m.balances)
              .Append(m => m.milestone)
              .Append(m => m.milestoneIndex)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetBalancesResponse>(this, that)
              .With(m => m.balances)
              .With(m => m.milestone)
              .With(m => m.milestoneIndex)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetBalancesResponse>(this)
              .With(m => m.balances)
              .With(m => m.milestone)
              .With(m => m.milestoneIndex)
              .HashCode;
        }
	}

}