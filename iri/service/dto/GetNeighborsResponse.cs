using System.Collections.Generic;
using System.Net;
using iri.utils;

namespace com.iota.iri.service.dto
{


	public class GetNeighborsResponse : AbstractResponse
	{

		private Neighbor[] neighbors;

		public virtual Neighbor[] Neighbors
		{
			get
			{
				return neighbors;
			}
		}

		public class Neighbor
		{

			private string address;
			public int numberOfAllTransactions, numberOfNewTransactions, numberOfInvalidTransactions;

			public virtual string Address
			{
				get
				{
					return address;
				}
			}
			public virtual int NumberOfAllTransactions
			{
				get
				{
					return numberOfAllTransactions;
				}
			}
			public virtual int NumberOfNewTransactions
			{
				get
				{
					return numberOfNewTransactions;
				}
			}
			public virtual int NumberOfInvalidTransactions
			{
				get
				{
					return numberOfInvalidTransactions;
				}
			}

			public static Neighbor createFrom(com.iota.iri.Neighbor n)
			{
				Neighbor ne = new Neighbor();

                IPEndPoint endpoint = new IPEndPoint(0, 0);
                IPEndPoint tmp = (IPEndPoint)endpoint.Create(n.Address);

                ne.address = tmp.Address + ":" + tmp.Port;
				ne.numberOfAllTransactions = n.NumberOfAllTransactions;
				ne.numberOfInvalidTransactions = n.NumberOfInvalidTransactions;
				ne.numberOfNewTransactions = n.NumberOfNewTransactions;
				return ne;
			}

            public override string ToString()
            {
                return new ToStringBuilder<Neighbor>(this)
                  .Append(m => m.address)
                  .Append(m => m.numberOfAllTransactions)
                  .Append(m => m.numberOfNewTransactions)
                  .Append(m => m.numberOfInvalidTransactions)
                  .ToString();
            }

            public override bool Equals(object that)
            {
                return new EqualsBuilder<Neighbor>(this, that)
                  .With(m => m.address)
                  .With(m => m.numberOfAllTransactions)
                  .With(m => m.numberOfNewTransactions)
                  .With(m => m.numberOfInvalidTransactions)
                  .Equals();
            }

            public override int GetHashCode()
            {
                return new HashCodeBuilder<Neighbor>(this)
                  .With(m => m.address)
                  .With(m => m.numberOfAllTransactions)
                  .With(m => m.numberOfNewTransactions)
                  .With(m => m.numberOfInvalidTransactions)
                  .HashCode;
            }
		}

		public static AbstractResponse create(IList<com.iota.iri.Neighbor> elements)
		{
			GetNeighborsResponse res = new GetNeighborsResponse();
			res.neighbors = new Neighbor[elements.Count];
			int i = 0;
			foreach (com.iota.iri.Neighbor n in elements)
			{
				res.neighbors[i++] = Neighbor.createFrom(n);
			}
			return res;
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetNeighborsResponse>(this)
              .Append(m => m.neighbors)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetNeighborsResponse>(this, that)
              .With(m => m.neighbors)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetNeighborsResponse>(this)
              .With(m => m.neighbors)
              .HashCode;
        }

	}

}