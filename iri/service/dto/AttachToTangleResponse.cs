using System.Collections.Generic;
using iri.utils;

namespace com.iota.iri.service.dto
{


	public class AttachToTangleResponse : AbstractResponse
	{

		private IList<string> elements;

		public static AbstractResponse create(IList<string> elements)
		{
			AttachToTangleResponse res = new AttachToTangleResponse();
			res.elements = elements;
			return res;
		}

        public override string ToString()
        {
            return new ToStringBuilder<AttachToTangleResponse>(this)
              .Append(m => m.elements)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<AttachToTangleResponse>(this, that)
              .With(m => m.elements)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<AttachToTangleResponse>(this)
              .With(m => m.elements)
              .HashCode;
        }
	}

}