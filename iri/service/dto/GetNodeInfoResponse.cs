using iri.utils;

namespace com.iota.iri.service.dto
{

	using Hash = com.iota.iri.model.Hash;

	public class GetNodeInfoResponse : AbstractResponse
	{

		private string appName;
		private string appVersion;
		private int jreAvailableProcessors;
		private long jreFreeMemory;

		private long jreMaxMemory;
		private long jreTotalMemory;
		private string latestMilestone;
		private int latestMilestoneIndex;

		private string latestSolidSubtangleMilestone;
		private int latestSolidSubtangleMilestoneIndex;

		private int neighbors;
		private int packetsQueueSize;
		private long time;
		private int tips;
		private int transactionsToRequest;

		public static AbstractResponse create(string appName, string appVersion, int jreAvailableProcessors, long jreFreeMemory, long maxMemory, long totalMemory, Hash latestMilestone, int latestMilestoneIndex, Hash latestSolidSubtangleMilestone, int latestSolidSubtangleMilestoneIndex, int neighbors, int packetsQueueSize, long currentTimeMillis, int tips, int numberOfTransactionsToRequest)
		{
			GetNodeInfoResponse res = new GetNodeInfoResponse();
			res.appName = appName;
			res.appVersion = appVersion;
			res.jreAvailableProcessors = jreAvailableProcessors;
			res.jreFreeMemory = jreFreeMemory;

			res.jreMaxMemory = maxMemory;
			res.jreTotalMemory = totalMemory;
			res.latestMilestone = latestMilestone.ToString();
			res.latestMilestoneIndex = latestMilestoneIndex;

			res.latestSolidSubtangleMilestone = latestSolidSubtangleMilestone.ToString();
			res.latestSolidSubtangleMilestoneIndex = latestSolidSubtangleMilestoneIndex;

			res.neighbors = neighbors;
			res.packetsQueueSize = packetsQueueSize;
			res.time = currentTimeMillis;
			res.tips = tips;
			res.transactionsToRequest = numberOfTransactionsToRequest;
			return res;
		}

		public virtual string AppName
		{
			get
			{
				return appName;
			}
		}

		public virtual string AppVersion
		{
			get
			{
				return appVersion;
			}
		}

		public virtual int JreAvailableProcessors
		{
			get
			{
				return jreAvailableProcessors;
			}
		}

		public virtual long JreFreeMemory
		{
			get
			{
				return jreFreeMemory;
			}
		}

		public virtual long JreMaxMemory
		{
			get
			{
				return jreMaxMemory;
			}
		}

		public virtual long JreTotalMemory
		{
			get
			{
				return jreTotalMemory;
			}
		}

		public virtual string LatestMilestone
		{
			get
			{
				return latestMilestone;
			}
		}

		public virtual int LatestMilestoneIndex
		{
			get
			{
				return latestMilestoneIndex;
			}
		}

		public virtual string LatestSolidSubtangleMilestone
		{
			get
			{
				return latestSolidSubtangleMilestone;
			}
		}

		public virtual int LatestSolidSubtangleMilestoneIndex
		{
			get
			{
				return latestSolidSubtangleMilestoneIndex;
			}
		}

		public virtual int Neighbors
		{
			get
			{
				return neighbors;
			}
		}

		public virtual int PacketsQueueSize
		{
			get
			{
				return packetsQueueSize;
			}
		}

		public virtual long Time
		{
			get
			{
				return time;
			}
		}

		public virtual int Tips
		{
			get
			{
				return tips;
			}
		}

		public virtual int TransactionsToRequest
		{
			get
			{
				return transactionsToRequest;
			}
		}

        public override string ToString()
        {
            return new ToStringBuilder<GetNodeInfoResponse>(this)
              .Append(m => m.appName)
              .Append(m => m.appVersion)
              .Append(m => m.jreAvailableProcessors)
              .Append(m => m.jreFreeMemory)
              .Append(m => m.jreMaxMemory)
              .Append(m => m.jreTotalMemory)
              .Append(m => m.latestMilestone)
              .Append(m => m.latestMilestoneIndex)
              .Append(m => m.latestSolidSubtangleMilestone)
              .Append(m => m.latestSolidSubtangleMilestoneIndex)
              .Append(m => m.neighbors)
              .Append(m => m.packetsQueueSize)
              .Append(m => m.time)
              .Append(m => m.tips)
              .Append(m => m.transactionsToRequest)
              .ToString();
        }

        public override bool Equals(object that)
        {
            return new EqualsBuilder<GetNodeInfoResponse>(this, that)
              .With(m => m.appName)
              .With(m => m.appVersion)
              .With(m => m.jreAvailableProcessors)
              .With(m => m.jreFreeMemory)
              .With(m => m.jreMaxMemory)
              .With(m => m.jreTotalMemory)
              .With(m => m.latestMilestone)
              .With(m => m.latestMilestoneIndex)
              .With(m => m.latestSolidSubtangleMilestone)
              .With(m => m.latestSolidSubtangleMilestoneIndex)
              .With(m => m.neighbors)
              .With(m => m.packetsQueueSize)
              .With(m => m.time)
              .With(m => m.tips)
              .With(m => m.transactionsToRequest)
              .Equals();
        }

        public override int GetHashCode()
        {
            return new HashCodeBuilder<GetNodeInfoResponse>(this)
              .With(m => m.appName)
              .With(m => m.appVersion)
              .With(m => m.jreAvailableProcessors)
              .With(m => m.jreFreeMemory)
              .With(m => m.jreMaxMemory)
              .With(m => m.jreTotalMemory)
              .With(m => m.latestMilestone)
              .With(m => m.latestMilestoneIndex)
              .With(m => m.latestSolidSubtangleMilestone)
              .With(m => m.latestSolidSubtangleMilestoneIndex)
              .With(m => m.neighbors)
              .With(m => m.packetsQueueSize)
              .With(m => m.time)
              .With(m => m.tips)
              .With(m => m.transactionsToRequest)
              .HashCode;
        }

	}

}