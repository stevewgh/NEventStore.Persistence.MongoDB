﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace NEventStore.Persistence.MongoDB {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "16.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Messages {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Messages() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("NEventStore.Persistence.MongoDB.Messages", typeof(Messages).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Adding snapshot to stream &apos;{0}&apos; in bucket &apos;{1}&apos; at position {2}..
        /// </summary>
        internal static string AddingSnapshot {
            get {
                return ResourceManager.GetString("AddingSnapshot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Attempting to commit {0} events on stream &apos;{1}&apos; at sequence {2}..
        /// </summary>
        internal static string AttemptingToCommit {
            get {
                return ResourceManager.GetString("AttemptingToCommit", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Commit &apos;{0}&apos; persisted..
        /// </summary>
        internal static string CommitPersisted {
            get {
                return ResourceManager.GetString("CommitPersisted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Concurrent write detected..
        /// </summary>
        internal static string ConcurrentWriteDetected {
            get {
                return ResourceManager.GetString("ConcurrentWriteDetected", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Could not find connection name &apos;{0}&apos; in the configuration file..
        /// </summary>
        internal static string ConnectionNotFound {
            get {
                return ResourceManager.GetString("ConnectionNotFound", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Deleting stream &apos;{0}&apos; from bucket &apos;{1}&apos;..
        /// </summary>
        internal static string DeletingStream {
            get {
                return ResourceManager.GetString("DeletingStream", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Concurrency issue; determining whether attempt was duplicate..
        /// </summary>
        internal static string DetectingConcurrency {
            get {
                return ResourceManager.GetString("DetectingConcurrency", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting all commits for stream &apos;{0}&apos; in bucket &apos;{1}&apos; between revisions &apos;{2}&apos; and &apos;{3}&apos;..
        /// </summary>
        internal static string GettingAllCommitsBetween {
            get {
                return ResourceManager.GetString("GettingAllCommitsBetween", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting all commits from &apos;{0}&apos; forward from bucket &apos;{1}&apos;..
        /// </summary>
        internal static string GettingAllCommitsFrom {
            get {
                return ResourceManager.GetString("GettingAllCommitsFrom", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting all commits from Bucket &apos;{0}&apos; and checkpoint &apos;{1}&apos;..
        /// </summary>
        internal static string GettingAllCommitsFromBucketAndCheckpoint {
            get {
                return ResourceManager.GetString("GettingAllCommitsFromBucketAndCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting all commits since checkpoint &apos;{0}&apos;..
        /// </summary>
        internal static string GettingAllCommitsFromCheckpoint {
            get {
                return ResourceManager.GetString("GettingAllCommitsFromCheckpoint", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting all commits from &apos;{0}&apos; to &apos;{1}&apos;..
        /// </summary>
        internal static string GettingAllCommitsFromTo {
            get {
                return ResourceManager.GetString("GettingAllCommitsFromTo", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting snapshot for stream &apos;{0}&apos; on or before revision {1}..
        /// </summary>
        internal static string GettingRevision {
            get {
                return ResourceManager.GetString("GettingRevision", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting a list of streams to snapshot..
        /// </summary>
        internal static string GettingStreamsToSnapshot {
            get {
                return ResourceManager.GetString("GettingStreamsToSnapshot", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Getting the list of all undispatched commits..
        /// </summary>
        internal static string GettingUndispatchedCommits {
            get {
                return ResourceManager.GetString("GettingUndispatchedCommits", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Initializing storage engine..
        /// </summary>
        internal static string InitializingStorage {
            get {
                return ResourceManager.GetString("InitializingStorage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Marking commit &apos;{0}&apos; as dispatched..
        /// </summary>
        internal static string MarkingCommitAsDispatched {
            get {
                return ResourceManager.GetString("MarkingCommitAsDispatched", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Purging all stored data for bucket &apos;{0}&apos;..
        /// </summary>
        internal static string PurgingBucket {
            get {
                return ResourceManager.GetString("PurgingBucket", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Purging all stored data..
        /// </summary>
        internal static string PurgingStorage {
            get {
                return ResourceManager.GetString("PurgingStorage", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Shutting down persistence..
        /// </summary>
        internal static string ShuttingDownPersistence {
            get {
                return ResourceManager.GetString("ShuttingDownPersistence", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Storage threw exception of type &apos;{0}&apos;..
        /// </summary>
        internal static string StorageThrewException {
            get {
                return ResourceManager.GetString("StorageThrewException", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Storage is unavailabe..
        /// </summary>
        internal static string StorageUnavailable {
            get {
                return ResourceManager.GetString("StorageUnavailable", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unsuppored checkpoint type. Expected {0} but got {1}..
        /// </summary>
        internal static string UnsupportedCheckpointType {
            get {
                return ResourceManager.GetString("UnsupportedCheckpointType", resourceCulture);
            }
        }
    }
}
