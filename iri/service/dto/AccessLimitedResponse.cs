using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{

    ///
    /// <summary> * Created by Adrian on 07.01.2017. </summary>
    /// 
	public class AccessLimitedResponse : AbstractResponse
	{

		private string error;

		public static AbstractResponse create(string error)
		{
			AccessLimitedResponse res = new AccessLimitedResponse();
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
            return new ToStringBuilder<AccessLimitedResponse>(this)
              .Append(m => m.error)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<AccessLimitedResponse>(this, that)
              .With(m => m.error)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<AccessLimitedResponse>(this)
              .With(m => m.error)
              .HashCode;
        }
	}

}