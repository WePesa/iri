using iri.utils;

namespace com.iota.iri.service.dto
{

	public class RemoveNeighborsResponse : AbstractResponse
	{

		private int removedNeighbors;

		public static AbstractResponse create(int numberOfRemovedNeighbors)
		{
			RemoveNeighborsResponse res = new RemoveNeighborsResponse();
			res.removedNeighbors = numberOfRemovedNeighbors;
			return res;
		}

		public virtual int RemovedNeighbors
		{
			get
			{
				return removedNeighbors;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<RemoveNeighborsResponse>(this)
              .Append(m => m.removedNeighbors)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<RemoveNeighborsResponse>(this, that)
              .With(m => m.removedNeighbors)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<RemoveNeighborsResponse>(this)
              .With(m => m.removedNeighbors)
              .HashCode;
        }
	}

}