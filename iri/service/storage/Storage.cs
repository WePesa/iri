

// 1.1.2.3

using System.IO;
using slf4net;

namespace com.iota.iri.service.storage
{

    using Milestone = com.iota.iri.Milestone;
    using Hash = com.iota.iri.model.Hash;
    using Transaction = com.iota.iri.model.Transaction;

    ///
    /// <summary> * Storage is organized as 243-value tree </summary>
    /// 
    public class Storage : AbstractStorage
    {

        private static readonly ILogger log = LoggerFactory.GetLogger(typeof(Storage));

        public static readonly sbyte[][] approvedTransactionsToStore = new sbyte[2][];

        private volatile bool launched;

        public static int numberOfApprovedTransactionsToStore;

        private StorageTransactions storageTransactionInstance = StorageTransactions.instance();
        private StorageBundle storageBundleInstance = StorageBundle.instance();
        private StorageAddresses storageAddressesInstance = StorageAddresses.instance();
        private StorageTags storageTags = StorageTags.instance();
        private StorageApprovers storageApprovers = StorageApprovers.instance();
        private StorageScratchpad storageScratchpad = StorageScratchpad.instance();

        public override void init()
        {
            try
            {
                lock (typeof (Storage))
                {
                    storageTransactionInstance.init();
                    storageBundleInstance.init();
                    storageAddressesInstance.init();
                    storageTags.init();
                    storageApprovers.init();
                    storageScratchpad.init();
                    storageTransactionInstance.updateBundleAddressTagApprovers();
                    launched = true;
                }
            }
            catch
            {
                throw new IOException();
            }
        }

        public override void shutdown()
        {

            lock (typeof(Storage))
            {
                if (launched)
                {
                    storageTransactionInstance.shutdown();
                    storageBundleInstance.shutdown();
                    storageAddressesInstance.shutdown();
                    storageTags.shutdown();
                    storageApprovers.shutdown();
                    storageScratchpad.shutdown();

                    log.Info("DB successfully flushed");
                }
            }
        }

        internal virtual void updateBundleAddressTagAndApprovers(long transactionPointer)
        {

            Transaction transaction = new Transaction(mainBuffer, transactionPointer);
            for (int j = 0; j < numberOfApprovedTransactionsToStore; j++)
            {
                StorageTransactions.instance().storeTransaction(approvedTransactionsToStore[j], null, false);
            }
            numberOfApprovedTransactionsToStore = 0;

            StorageBundle.instance().updateBundle(transactionPointer, transaction);
            StorageAddresses.instance().updateAddresses(transactionPointer, transaction);
            StorageTags.instance().updateTags(transactionPointer, transaction);
            StorageApprovers.instance().updateApprover(transaction.trunkTransaction, transactionPointer);

            if (transaction.branchTransactionPointer != transaction.trunkTransactionPointer)
            {
                StorageApprovers.instance().updateApprover(transaction.branchTransaction, transactionPointer);
            }
        }

        // methods helper

        private static Storage instance = new Storage();

        private Storage()
        {
        }

        public static Storage instance()
        {
            return instance;
        }
    }


}