using iri.utils;

namespace com.iota.iri.service.dto
{

	public class ExceptionResponse : AbstractResponse
	{

		private string exception;

		public static AbstractResponse create(string exception)
		{
			ExceptionResponse res = new ExceptionResponse();
			res.exception = exception;
			return res;
		}

		public virtual string Exception
		{
			get
			{
				return exception;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<ExceptionResponse>(this)
              .Append(m => m.exception)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<ExceptionResponse>(this, that)
              .With(m => m.exception)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<ExceptionResponse>(this)
              .With(m => m.exception)
              .HashCode;
        }
	}

}