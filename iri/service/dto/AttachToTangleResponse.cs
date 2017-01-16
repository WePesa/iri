using System.Collections.Generic;
using iri.utils;

namespace com.iota.iri.service.dto
{


	public class AttachToTangleResponse : AbstractResponse
	{

		private IList<string> trytes;

		public static AbstractResponse create(IList<string> elements)
		{
			AttachToTangleResponse res = new AttachToTangleResponse();
			res.trytes = elements;
			return res;
		}

        public virtual IList<string> Trytes
        {
            get
            {
                return trytes;
            }
        }

        public override string ToString()
        {
            return new ToStringBuilder<AttachToTangleResponse>(this)
              .Append(m => m.trytes)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<AttachToTangleResponse>(this, that)
              .With(m => m.trytes)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<AttachToTangleResponse>(this)
              .With(m => m.trytes)
              .HashCode;
        }
	}

}