using iri.utils;

// 1.1.2.3

namespace com.iota.iri.service.dto
{

    //using EqualsBuilder = org.apache.commons.lang3.builder.EqualsBuilder;
    //using HashCodeBuilder = org.apache.commons.lang3.builder.HashCodeBuilder;
    //using ToStringBuilder = org.apache.commons.lang3.builder.ToStringBuilder;
    //using ToStringStyle = org.apache.commons.lang3.builder.ToStringStyle;

    // I removed the abstract in order to createEmptyResponse.
	public /*abstract*/ class AbstractResponse
	{
        private class Emptyness : AbstractResponse
        {
        }

		private int? duration;

		public override string ToString()
		{
			
            // return ToStringBuilder.reflectionToString(this, ToStringStyle.MULTI_LINE_STYLE);
            return new ToStringBuilder<AbstractResponse>(this)
                      .Append(m => m.duration)
                      .ToString();
		}

		public override int GetHashCode()
		{
			// return HashCodeBuilder.reflectionHashCode(this, false);
            return new HashCodeBuilder<AbstractResponse>(this)
                       .With(m => m.duration)
                       .HashCode;
		}

		public override bool Equals(object obj)
		{
			// return EqualsBuilder.reflectionEquals(this, obj, false);
            return new EqualsBuilder<AbstractResponse>(this, obj)
                       .With(m => m.duration)
                       .Equals();

		}

		public virtual int? Duration
		{
			get
			{
				return duration;
			}
			set
			{
				this.duration = value;
			}
		}

		public static AbstractResponse createEmptyResponse()
		{
            return new Emptyness();
		}

	}
}