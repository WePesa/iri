using iri.utils;

namespace com.iota.iri.service.dto
{

	public class ErrorResponse : AbstractResponse
	{

		private string error;

		public static AbstractResponse create(string error)
		{
			ErrorResponse res = new ErrorResponse();
			res.error = error;
			return res;
		}

		public virtual string Error
		{
			get
			{
				return error;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<ErrorResponse>(this)
              .Append(m => m.error)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<ErrorResponse>(this, that)
              .With(m => m.error)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<ErrorResponse>(this)
              .With(m => m.error)
              .HashCode;
        }
	}

}