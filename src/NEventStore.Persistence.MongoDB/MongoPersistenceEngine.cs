namespace NEventStore.Persistence.MongoDB
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::MongoDB.Bson;
    using global::MongoDB.Driver;
    using global::MongoDB.Driver.Core.Bindings;
    using global::MongoDB.Driver.Core.Operations;
    using NEventStore.Logging;
    using NEventStore.Serialization;

    public class MongoPersistenceEngine : IPersistStreams
    {
        private const string ConcurrencyException = "E1100";
        private const int ConcurrencyExceptionCode = 11000;
        private static readonly ILog Logger = LogFactory.BuildLogger(typeof(MongoPersistenceEngine));
        private readonly MongoCollectionSettings _commitSettings;
        private readonly IDocumentSerializer _serializer;
        private readonly MongoCollectionSettings _snapshotSettings;
        private readonly IMongoDatabase _store;
        private readonly MongoCollectionSettings _streamSettings;
        private bool _disposed;
        private int _initialized;
        private readonly Func<LongCheckpoint> _getNextCheckpointNumber;
        private readonly Func<long> _getLastCheckPointNumber;
        private readonly MongoPersistenceOptions _options;
        private readonly WriteConcern _insertCommitWriteConcern;
        private readonly BsonJavaScript _updateScript;
        private readonly LongCheckpoint _checkpointZero;
        private static readonly SortDefinition<BsonDocument> SortByAscendingCheckpointNumber = Builders<BsonDocument>.Sort.Ascending(MongoCommitFields.CheckpointNumber);

        public MongoPersistenceEngine(IMongoDatabase store, IDocumentSerializer serializer, MongoPersistenceOptions options)
        {
            if (store == null)
            {
                throw new ArgumentNullException("store");
            }

            if (serializer == null)
            {
                throw new ArgumentNullException("serializer");
            }

            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            _store = store;
            _serializer = serializer;
            _options = options;

            // set config options
            _commitSettings = _options.GetCommitSettings();
            _snapshotSettings = _options.GetSnapshotSettings();
            _streamSettings = _options.GetStreamSettings();
            _insertCommitWriteConcern = _options.GetInsertCommitWriteConcern();

            _getLastCheckPointNumber = () => TryMongo(() =>
            {
                var max =
                    PersistedCommits
                        .FindSync(
                            Builders<BsonDocument>.Filter.Empty,
                            new FindOptions<BsonDocument, long>
                            {
                                Limit = 1,
                                Sort = Builders<BsonDocument>.Sort.Descending(MongoCommitFields.CheckpointNumber),
                                Projection = Builders<BsonDocument>.Projection.Include(document => document[MongoCommitFields.CheckpointNumber])
                            })
                        .FirstOrDefault();

                return max;
            });

            _getNextCheckpointNumber = () => new LongCheckpoint(_getLastCheckPointNumber() + 1L);

            _updateScript = new BsonJavaScript("function (x){ return insertCommit(x);}");
            _checkpointZero = new LongCheckpoint(0);
        }

        protected virtual IMongoCollection<BsonDocument> PersistedCommits => _store.GetCollection<BsonDocument>("Commits", _commitSettings).WithWriteConcern(_insertCommitWriteConcern);

        protected virtual IMongoCollection<BsonDocument> PersistedStreamHeads => _store.GetCollection<BsonDocument>("Streams", _streamSettings);

        protected virtual IMongoCollection<BsonDocument> PersistedSnapshots => _store.GetCollection<BsonDocument>("Snapshots", _snapshotSettings);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public virtual void Initialize()
        {
            if (Interlocked.Increment(ref _initialized) > 1)
            {
                return;
            }

            Logger.Debug(Messages.InitializingStorage);

            TryMongo(() =>
            {
                PersistedCommits.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                        .Ascending(MongoCommitFields.Dispatched)
                        .Ascending(MongoCommitFields.CommitStamp))
                    {
                        Options = {Name = MongoCommitIndexes.Dispatched, Unique = false}
                    });

                PersistedCommits.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                        .Ascending(MongoCommitFields.BucketId)
                        .Ascending(MongoCommitFields.StreamId)
                        .Ascending(MongoCommitFields.StreamRevisionFrom)
                        .Ascending(MongoCommitFields.StreamRevisionTo)
                    )
                    {
                        Options = {Name = MongoCommitIndexes.GetFrom, Unique = true}
                    });

                PersistedCommits.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys
                        .Ascending(MongoCommitFields.BucketId)
                        .Ascending(MongoCommitFields.StreamId)
                        .Ascending(MongoCommitFields.CommitSequence)
                    )
                    {
                        Options = {Name = MongoCommitIndexes.LogicalKey, Unique = true}
                    });

                PersistedCommits.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(MongoCommitFields.CommitStamp))
                    {
                        Options = {Name = MongoCommitIndexes.CommitStamp, Unique = false}
                    });

                PersistedStreamHeads.Indexes.CreateOne(
                    new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending(MongoStreamHeadFields.Unsnapshotted))
                    {
                        Options = {Name = MongoStreamIndexes.Unsnapshotted, Unique = false}
                    });

                if (_options.ServerSideOptimisticLoop)
                {
                    PersistedCommits.Database.GetCollection<BsonDocument>("system.js").InsertOne(new BsonDocument{
                        {"_id" , "insertCommit"},
                        {"value" , new BsonJavaScript(_options.GetInsertCommitScript())}
                    });
                }

                EmptyRecycleBin();
            });
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, string streamId, int minRevision, int maxRevision)
        {
            Logger.Debug(Messages.GettingAllCommitsBetween, streamId, bucketId, minRevision, maxRevision);

            return TryMongo(() =>
            {
                var filters = new List<FilterDefinition<BsonDocument>>()
                {
                    Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId),
                    Builders<BsonDocument>.Filter.Eq(MongoCommitFields.StreamId, streamId)
                };
                if (minRevision > 0)
                {
                    filters.Add(
                        Builders<BsonDocument>.Filter.Gte(MongoCommitFields.StreamRevisionTo, minRevision)
                    );
                }
                if (maxRevision < int.MaxValue)
                {
                    filters.Add(
                        Builders<BsonDocument>.Filter.Lte(MongoCommitFields.StreamRevisionFrom, maxRevision)
                    );
                }

                var query = Builders<BsonDocument>.Filter.And(filters);

                return PersistedCommits
                    .Find(query)
                    // .Sort(Builders<BsonDocument>.Sort.Ascending(MongoCommitFields.StreamRevisionFrom))
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(mc => mc.ToCommit(_serializer));
            });
        }

        public virtual IEnumerable<ICommit> GetFrom(string bucketId, DateTime start)
        {
            Logger.Debug(Messages.GettingAllCommitsFrom, start, bucketId);

            return TryMongo(() =>
            {
                var query = Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId);
                if (start != DateTime.MinValue)
                {
                    query = Builders<BsonDocument>.Filter.And(
                        query,
                        Builders<BsonDocument>.Filter.Gte(MongoCommitFields.CommitStamp, start)
                    );
                }

                return PersistedCommits
                    .Find(query)
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(x => x.ToCommit(_serializer));
            });
        }

        public IEnumerable<ICommit> GetFrom(string bucketId, string checkpointToken)
        {
            var intCheckpoint = LongCheckpoint.Parse(checkpointToken);
            Logger.Debug(Messages.GettingAllCommitsFromBucketAndCheckpoint, bucketId, intCheckpoint.Value);

            return TryMongo(() =>
            {
                var query = Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId);
                if (intCheckpoint.LongValue > 0)
                {
                    query = Builders<BsonDocument>.Filter.And(
                        query,
                        Builders<BsonDocument>.Filter.Gt(MongoCommitFields.CheckpointNumber, intCheckpoint.LongValue)
                    );
                }

                return PersistedCommits
                    .Find(query)
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(x => x.ToCommit(_serializer));
            });
        }

        public IEnumerable<ICommit> GetFrom(string checkpointToken)
        {
            var intCheckpoint = LongCheckpoint.Parse(checkpointToken);
            Logger.Debug(Messages.GettingAllCommitsFromCheckpoint, intCheckpoint.Value);

            return TryMongo(() =>
            {
                var query = Builders<BsonDocument>.Filter.Ne(MongoCommitFields.BucketId, MongoSystemBuckets.RecycleBin);
                if (intCheckpoint.LongValue > 0)
                {
                    query = Builders<BsonDocument>.Filter.And(
                        query,
                        Builders<BsonDocument>.Filter.Gt(MongoCommitFields.CheckpointNumber, intCheckpoint.LongValue)
                    );
                }

                return PersistedCommits
                    .Find(query)
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(x => x.ToCommit(_serializer));
            });
        }

        public ICheckpoint GetCheckpoint(string checkpointToken = null)
        {
            return LongCheckpoint.Parse(checkpointToken);
        }

        public virtual IEnumerable<ICommit> GetFromTo(string bucketId, DateTime start, DateTime end)
        {
            Logger.Debug(Messages.GettingAllCommitsFromTo, start, end, bucketId);

            return TryMongo(() =>
            {
                var filters = new List<FilterDefinition<BsonDocument>>()
                {
                    Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId)
                };
                if (start > DateTime.MinValue)
                {
                    filters.Add(
                        Builders<BsonDocument>.Filter.Gte(MongoCommitFields.CommitStamp, start)
                    );
                }
                if (end < DateTime.MaxValue)
                {
                    filters.Add(
                        Builders<BsonDocument>.Filter.Lt(MongoCommitFields.CommitStamp, end)
                    );
                }

                var query = Builders<BsonDocument>.Filter.And(filters);

                return PersistedCommits
                    .Find(query)
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(x => x.ToCommit(_serializer));
            });
        }

        public virtual ICommit Commit(CommitAttempt attempt)
        {
            Logger.Debug(Messages.AttemptingToCommit, attempt.Events.Count, attempt.StreamId, attempt.CommitSequence);

            return _options.ServerSideOptimisticLoop ?
                PersistWithServerSideOptimisticLoop(attempt) :
                PersistWithClientSideOptimisticLoop(attempt);
        }

        private ICommit PersistWithServerSideOptimisticLoop(CommitAttempt attempt)
        {
            throw new NotImplementedException($"Use {nameof(PersistWithClientSideOptimisticLoop)} instead.");
        }


        private ICommit PersistWithClientSideOptimisticLoop(CommitAttempt attempt)
        {
            return TryMongo(() =>
            {
                BsonDocument commitDoc = attempt.ToMongoCommit(
                    _getNextCheckpointNumber(),
                    _serializer
                );

                bool retry = true;
                while (retry)
                {
                    try
                    {
                        // for concurrency / duplicate commit detection safe mode is required
                        PersistedCommits.InsertOne(commitDoc);

                        retry = false;
                        UpdateStreamHeadAsync(attempt.BucketId, attempt.StreamId, attempt.StreamRevision, attempt.Events.Count);
                        Logger.Debug(Messages.CommitPersisted, attempt.CommitId);
                    }
                    catch (MongoException e)
                    {
                        if (!e.Message.Contains(ConcurrencyException))
                        {
                            throw;
                        }

                        // checkpoint index? 
                        if (e.Message.Contains(MongoCommitIndexes.CheckpointNumberMMApV1) ||
                           e.Message.Contains(MongoCommitIndexes.CheckpointNumberWiredTiger))
                        {
                            commitDoc[MongoCommitFields.CheckpointNumber] = _getNextCheckpointNumber().LongValue;
                        }
                        else
                        {
                            ICommit savedCommit = PersistedCommits.Find(attempt.ToMongoCommitIdQuery()).First().ToCommit(_serializer);

                            if (savedCommit.CommitId == attempt.CommitId)
                            {
                                throw new DuplicateCommitException();
                            }
                            Logger.Debug(Messages.ConcurrentWriteDetected);
                            throw new ConcurrencyException();
                        }
                    }
                }

                return commitDoc.ToCommit(_serializer);
            });
        }

        public virtual IEnumerable<ICommit> GetUndispatchedCommits()
        {
            Logger.Debug(Messages.GettingUndispatchedCommits);

            return TryMongo(() =>
            {
                var filter = Builders<BsonDocument>.Filter.Eq(document => document[MongoCommitFields.Dispatched], false);

                return PersistedCommits
                    .Find(filter)
                    .Sort(SortByAscendingCheckpointNumber)
                    .ToEnumerable()
                    .Select(x => x.ToCommit(_serializer));
            });
        }

        public virtual void MarkCommitAsDispatched(ICommit commit)
        {
            Logger.Debug(Messages.MarkingCommitAsDispatched, commit.CommitId);

            TryMongo(() =>
            {
                var filter = commit.ToMongoCommitIdQuery();
                var update = Builders<BsonDocument>.Update.Set(MongoCommitFields.Dispatched, true);
                PersistedCommits.UpdateOne(filter, update);
            });
        }

        public virtual IEnumerable<IStreamHead> GetStreamsToSnapshot(string bucketId, int maxThreshold)
        {
            Logger.Debug(Messages.GettingStreamsToSnapshot);

            return TryMongo(() =>
            {
                var query = Builders<BsonDocument>.Filter.Gte(MongoStreamHeadFields.Unsnapshotted, maxThreshold);
                return PersistedStreamHeads
                    .Find(query)
                    .Sort(Builders<BsonDocument>.Sort.Descending(MongoStreamHeadFields.Unsnapshotted))
                    .ToEnumerable()
                    .Select(x => x.ToStreamHead());
            });
        }

        public virtual ISnapshot GetSnapshot(string bucketId, string streamId, int maxRevision)
        {
            Logger.Debug(Messages.GettingRevision, streamId, maxRevision);

            return TryMongo(() =>
            {
                var query = ExtensionMethods.GetSnapshotQuery(bucketId, streamId, maxRevision);

                return PersistedSnapshots
                    .Find(query)
                    .Sort(Builders<BsonDocument>.Sort.Descending(MongoShapshotFields.Id))
                    .Limit(1)
                    .ToEnumerable()
                    .Select(mc => mc.ToSnapshot(_serializer))
                    .FirstOrDefault();
            });
        }

        public virtual bool AddSnapshot(ISnapshot snapshot)
        {
            if (snapshot == null)
            {
                return false;
            }
            Logger.Debug(Messages.AddingSnapshot, snapshot.StreamId, snapshot.BucketId, snapshot.StreamRevision);
            
            try
            {
                BsonDocument mongoSnapshot = snapshot.ToMongoSnapshot(_serializer);
                var query = Builders<BsonDocument>.Filter.Eq(MongoShapshotFields.Id, mongoSnapshot[MongoShapshotFields.Id]);
                var update = Builders<BsonDocument>.Update.Set(MongoShapshotFields.Payload, mongoSnapshot[MongoShapshotFields.Payload]);

                // Doing an upsert instead of an insert allows us to overwrite an existing snapshot and not get stuck with a
                // stream that needs to be snapshotted because the insert fails and the SnapshotRevision isn't being updated.
                PersistedSnapshots.UpdateOne(query, update, new UpdateOptions() { IsUpsert = true });

                // More commits could have been made between us deciding that a snapshot is required and writing it so just
                // resetting the Unsnapshotted count may be a little off. Adding snapshots should be a separate process so
                // this is a good chance to make sure the numbers are still in-sync - it only adds a 'read' after all ...
                BsonDocument streamHeadId = GetStreamHeadId(snapshot.BucketId, snapshot.StreamId);
                StreamHead streamHead = PersistedStreamHeads.Find(Builders<BsonDocument>.Filter.Eq(MongoStreamHeadFields.Id, streamHeadId))
                    .First()
                    .ToStreamHead();

                int unsnapshotted = streamHead.HeadRevision - snapshot.StreamRevision;

                PersistedStreamHeads.UpdateOne(
                    Builders<BsonDocument>.Filter
                        .Eq(MongoStreamHeadFields.Id, streamHeadId),
                    Builders<BsonDocument>.Update
                        .Set(MongoStreamHeadFields.SnapshotRevision, snapshot.StreamRevision)
                        .Set(MongoStreamHeadFields.Unsnapshotted, unsnapshotted)
                );

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual void Purge()
        {
            Logger.Warn(Messages.PurgingStorage);

            PersistedCommits.DeleteMany(Builders<BsonDocument>.Filter.Empty);
            PersistedStreamHeads.DeleteMany(Builders<BsonDocument>.Filter.Empty);
            PersistedSnapshots.DeleteMany(Builders<BsonDocument>.Filter.Empty);
        }

        public void Purge(string bucketId)
        {
            Logger.Warn(Messages.PurgingBucket, bucketId);
            TryMongo(() =>
            {
                PersistedStreamHeads.DeleteMany(Builders<BsonDocument>.Filter.Eq(MongoStreamHeadFields.FullQualifiedBucketId, bucketId));
                PersistedSnapshots.DeleteMany(Builders<BsonDocument>.Filter.Eq(MongoShapshotFields.FullQualifiedBucketId, bucketId));
                PersistedCommits.DeleteMany(Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId));
            });
        }

        public void Drop()
        {
            Purge();
        }

        public void DeleteStream(string bucketId, string streamId)
        {
            Logger.Warn(Messages.DeletingStream, streamId, bucketId);

            TryMongo(() =>
            {
                PersistedStreamHeads.DeleteOne(
                    Builders<BsonDocument>.Filter.Eq(MongoStreamHeadFields.Id, new BsonDocument{
                        {MongoStreamHeadFields.BucketId, bucketId},
                        {MongoStreamHeadFields.StreamId, streamId}
                    })
                );

                PersistedSnapshots.DeleteMany(
                    Builders<BsonDocument>.Filter.Eq(MongoShapshotFields.Id, new BsonDocument{
                        {MongoShapshotFields.BucketId, bucketId},
                        {MongoShapshotFields.StreamId, streamId}
                    })
                );

                PersistedCommits.UpdateMany(
                    Builders<BsonDocument>.Filter.And(
                        Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, bucketId),
                        Builders<BsonDocument>.Filter.Eq(MongoCommitFields.StreamId, streamId)
                    ),
                    Builders<BsonDocument>.Update.Set(MongoCommitFields.BucketId, MongoSystemBuckets.RecycleBin)
                );
            });
        }

        public bool IsDisposed
        {
            get { return _disposed; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing || _disposed)
            {
                return;
            }

            Logger.Debug(Messages.ShuttingDownPersistence);
            _disposed = true;
        }

        private void UpdateStreamHeadAsync(string bucketId, string streamId, int streamRevision, int eventsCount)
        {
            ThreadPool.QueueUserWorkItem(x =>
            {
                try
                {
                    TryMongo(() =>
                    {
                        BsonDocument streamHeadId = GetStreamHeadId(bucketId, streamId);
                        PersistedStreamHeads.UpdateOne(
                            Builders<BsonDocument>.Filter.Eq(MongoStreamHeadFields.Id, streamHeadId),
                            Builders<BsonDocument>.Update
                                .Set(MongoStreamHeadFields.HeadRevision, streamRevision)
                                .Inc(MongoStreamHeadFields.SnapshotRevision, 0)
                                .Inc(MongoStreamHeadFields.Unsnapshotted, eventsCount),
                            new UpdateOptions() { IsUpsert = true }
                        );
                    });
                }
                catch (OutOfMemoryException ex)
                {
                    throw;
                }
                catch (Exception ex)
                {
                     //It is safe to ignore transient exception updating stream head.
                    Logger.Warn("Ignored Exception '{0}' when upserting the stream head Bucket Id [{1}] StreamId[{2}].\n {3}", ex.GetType().Name, bucketId, streamId, ex.ToString());
                }
            }, null
          );
        }

        protected virtual T TryMongo<T>(Func<T> callback)
        {
            T results = default(T);
            TryMongo(() => { results = callback(); });
            return results;
        }

        protected virtual void TryMongo(Action callback)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException("Attempt to use storage after it has been disposed.");
            }
            try
            {
                callback();
            }
            catch (MongoConnectionException e)
            {
                Logger.Warn(Messages.StorageUnavailable);
                throw new StorageUnavailableException(e.Message, e);
            }
            catch (MongoException e)
            {
                Logger.Error(Messages.StorageThrewException, e.GetType());
                throw new StorageException(e.Message, e);
            }
        }

        private static BsonDocument GetStreamHeadId(string bucketId, string streamId)
        {
            var id = new BsonDocument();
            id[MongoStreamHeadFields.BucketId] = bucketId;
            id[MongoStreamHeadFields.StreamId] = streamId;
            return id;
        }

        public void EmptyRecycleBin()
        {
            var lastCheckpointNumber = _getLastCheckPointNumber();
            TryMongo(() =>
            {
                PersistedCommits.DeleteMany(Builders<BsonDocument>.Filter.And(
                    Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, MongoSystemBuckets.RecycleBin),
                    Builders<BsonDocument>.Filter.Lt(MongoCommitFields.CheckpointNumber, lastCheckpointNumber)
                ));
            });
        }

        public IEnumerable<ICommit> GetDeletedCommits()
        {
            return TryMongo(() => PersistedCommits
                .Find(Builders<BsonDocument>.Filter.Eq(MongoCommitFields.BucketId, MongoSystemBuckets.RecycleBin))
                .Sort(SortByAscendingCheckpointNumber)
                .ToEnumerable()
                .Select(mc => mc.ToCommit(_serializer)));
        }

        /// <summary>
        /// Evaluates the specified javascript within a MongoDb database
        /// </summary>
        /// <param name="database">MongoDb Database to execute the javascript</param>
        /// <param name="javascript">Javascript to execute</param>
        /// <returns>A BsonValue result</returns>
        public static BsonValue Eval(IMongoDatabase database, string javascript)
        {
            var client = database.Client as MongoClient;

            if (client == null)
                throw new ArgumentException("Client is not a MongoClient");

            var function = new BsonJavaScript(javascript);
            var op = new EvalOperation(database.DatabaseNamespace, function, null);

            using (var writeBinding = new WritableServerBinding(client.Cluster, new CoreSessionHandle(NoCoreSession.Instance)))
            {
                return op.Execute(writeBinding, CancellationToken.None);
            }
        }
    }
}