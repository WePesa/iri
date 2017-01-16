using System.Collections.Generic;
using System.Linq;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{


	public class FindTransactionsResponse : AbstractResponse
	{

		private string[] hashes;

		public static AbstractResponse create(IList<string> elements)
		{
			FindTransactionsResponse res = new FindTransactionsResponse();
			res.hashes = elements.ToArray();
			return res;
		}

		public virtual string[] Hashes
		{
			get
			{
				return hashes;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<FindTransactionsResponse>(this)
              .Append(m => m.hashes)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<FindTransactionsResponse>(this, that)
              .With(m => m.hashes)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<FindTransactionsResponse>(this)
              .With(m => m.hashes)
              .HashCode;
        }
	}

}