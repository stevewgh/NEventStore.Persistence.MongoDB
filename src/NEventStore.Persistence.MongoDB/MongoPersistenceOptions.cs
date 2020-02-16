﻿using NEventStore.Persistence.MongoDB.Support;

namespace NEventStore.Persistence.MongoDB
{
    using System;
    using global::MongoDB.Driver;

    /// <summary>
    /// Options for the MongoPersistence engine.
    /// http://docs.mongodb.org/manual/core/write-concern/#write-concern
    /// </summary>
    public class MongoPersistenceOptions
    {
        /// <summary>
        /// Get the  <see href="http://docs.mongodb.org/manual/core/write-concern/#write-concern">WriteConcern</see> for the commit insert operation.
        /// Concurrency / duplicate commit detection require a safe mode so level should be at least Acknowledged
        /// </summary>
        /// <returns>the write concern for the commit insert operation</returns>
        public virtual WriteConcern GetInsertCommitWriteConcern()
        {
            // for concurrency / duplicate commit detection safe mode is required
            // minimum level is Acknowledged
            return WriteConcern.Acknowledged;
        }

        public virtual MongoCollectionSettings GetCommitSettings()
        {
            return new MongoCollectionSettings
            {
                AssignIdOnInsert = !UseClientSideCheckpointAssignment,
                WriteConcern = WriteConcern.Acknowledged
            };
        }

        public virtual MongoCollectionSettings GetSnapshotSettings()
        {
            return new MongoCollectionSettings
            {
                AssignIdOnInsert = !UseClientSideCheckpointAssignment,
                WriteConcern = WriteConcern.Unacknowledged
            };
        }

        public virtual MongoCollectionSettings GetStreamSettings()
        {
            return new MongoCollectionSettings
            {
                AssignIdOnInsert = !UseClientSideCheckpointAssignment,
                WriteConcern = WriteConcern.Unacknowledged
            };
        }

        /// <summary>
        /// Connects to NEvenstore Mongo database
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <returns>nevenstore mongodatabase store</returns>
        public virtual IMongoDatabase ConnectToDatabase(string connectionString)
        {
            var builder = new MongoUrlBuilder(connectionString);
            return (new MongoClient(connectionString)).GetDatabase(builder.DatabaseName);
        }

        /// <summary>
        /// This is the instance of the Id Generator I want to use to 
        /// generate checkpoint. 
        /// </summary>
        public ICheckpointGenerator CheckpointGenerator {
            get {
                return _CheckpointGenerator;
            } 
            set {
                if(!UseClientSideCheckpointAssignment)
                {
                    throw new Exception("Checkpoint Generator can only be used when UseClientSideCheckpointAssignment is True");
                }

                _CheckpointGenerator = value;
            } 
        }

        public ConcurrencyExceptionStrategy ConcurrencyStrategy { get; set; }

        public bool UseClientSideCheckpointAssignment { get; set; }

        public String SystemBucketName { get; set; }

        /// <summary>
        /// Set this property to true to ask Persistence Engine to disable 
        /// snapshot support. If you are not using snapshot functionalities
        /// this options allows you to save the extra insert to insert Stream Heads.
        /// </summary>
        /// <remarks>
        /// If you disable Stream Heads, you are not able to ask
        /// for stream that need to be snapshotted. Basically you should set
        /// this to true if you not use NEventstore snapshot functionalities.
        /// </remarks>
        public Boolean DisableSnapshotSupport { get; set; }

        /// <summary>
        /// The default behavior when using snapshot is to persist the stream heads in
        /// a background threads, but this way it can be hard to test if the heads and snapshots
        /// are computed an updated correctly after a commit.
        /// This setting is here mainly to help testing.
        /// </summary>
        public Boolean PersistStreamHeadsOnBackgroundThread { get; set; } = true;

        public MongoPersistenceOptions()
        {
            SystemBucketName = "system";
            ConcurrencyStrategy = ConcurrencyExceptionStrategy.Continue;
            UseClientSideCheckpointAssignment = true;
        }

        private ICheckpointGenerator _CheckpointGenerator;
    }

    public enum ConcurrencyExceptionStrategy
    {
        /// <summary>
        /// When a <see cref="ConcurrencyException"/> is thrown, simply continue
        /// and ask to <see cref="ICheckpointGenerator"/> implementation new id.
        /// </summary>
        Continue = 0,

        /// <summary>
        /// When a <see cref="ConcurrencyException"/> is thrown, generate an empty
        /// commit with current <see cref="LongCheckpoint"/>, then ask to 
        /// <see cref="ICheckpointGenerator"/> implementation new id.
        /// </summary>
        FillHole = 1,
    }
}
