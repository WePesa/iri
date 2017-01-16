using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{

	public class GetInclusionStatesResponse : AbstractResponse
	{

		private bool[] states;

		public static AbstractResponse create(bool[] inclusionStates)
		{
			GetInclusionStatesResponse res = new GetInclusionStatesResponse();
			res.states = inclusionStates;
			return res;
		}

		public virtual bool[] States
		{
			get
			{
				return states;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetInclusionStatesResponse>(this)
              .Append(m => m.states)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetInclusionStatesResponse>(this, that)
              .With(m => m.states)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetInclusionStatesResponse>(this)
              .With(m => m.states)
              .HashCode;
        }

	}

}