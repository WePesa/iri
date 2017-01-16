using System.Collections.Generic;
using System.Linq;
using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{


	public class GetTrytesResponse : AbstractResponse
	{

		private string[] trytes;

		public static GetTrytesResponse create(IList<string> elements)
		{
			GetTrytesResponse res = new GetTrytesResponse();
			res.trytes = elements.ToArray();
			return res;
		}

		public virtual string [] Trytes
		{
			get
			{
				return trytes;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetTrytesResponse>(this)
              .Append(m => m.trytes)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetTrytesResponse>(this, that)
              .With(m => m.trytes)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetTrytesResponse>(this)
              .With(m => m.trytes)
              .HashCode;
        }
	}

}