using iri.utils;

namespace com.iota.iri.service.dto
{

	public class AddedNeighborsResponse : AbstractResponse
	{

		private int addedNeighbors;

		public static AbstractResponse create(int numberOfAddedNeighbors)
		{
			AddedNeighborsResponse res = new AddedNeighborsResponse();
			res.addedNeighbors = numberOfAddedNeighbors;
			return res;
		}

		public virtual int AddedNeighbors
		{
			get
			{
				return addedNeighbors;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<AddedNeighborsResponse>(this)
              .Append(m => m.addedNeighbors)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<AddedNeighborsResponse>(this, that)
              .With(m => m.addedNeighbors)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<AddedNeighborsResponse>(this)
              .With(m => m.addedNeighbors)
              .HashCode;
        }

	}

}