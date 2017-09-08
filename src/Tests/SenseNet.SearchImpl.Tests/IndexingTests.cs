﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SenseNet.Configuration;
using SenseNet.ContentRepository;
using SenseNet.ContentRepository.Security;
using SenseNet.ContentRepository.Storage;
using SenseNet.ContentRepository.Storage.Data;
using SenseNet.ContentRepository.Storage.Events;
using SenseNet.ContentRepository.Storage.Security;
using SenseNet.Search;
using SenseNet.Search.Indexing;
using SenseNet.Search.Lucene29;
using SenseNet.SearchImpl.Tests.Implementations;
using SafeQueries = SenseNet.SearchImpl.Tests.Implementations.SafeQueries;

namespace SenseNet.SearchImpl.Tests
{
    [TestClass]
    public class IndexingTests : TestBase
    {
        [TestMethod]
        public void InMemSearch_Indexing_Create()
        {
            Node node;
            var result = Test(() =>
            {
                // create a test node under the root.
                node = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node1",
                    DisplayName = "Node 1",
                    Index = 42
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);

                // ACTION
                node.Save();

                // reload the newly created.
                node = Node.Load<SystemFolder>(node.Id);

                // load the pre-converted index document
                var db = DataProvider.Current;
                var indexDocument = db.LoadIndexDocumentByVersionId(node.VersionId);

                // load last indexing activity
                var activityId = db.GetLastActivityId();
                var activity =
                    db.LoadIndexingActivities(activityId, activityId, 1, false, IndexingActivityFactory.Instance)
                        .FirstOrDefault();

                return new Tuple<Node, IndexDocumentData, IIndexingActivity, InMemoryIndex>(node, indexDocument,
                    activity, GetTestIndex());
            });

            node = result.Item1;
            var indexDoc = result.Item2;
            var lastActivity = result.Item3;
            var index = result.Item4;

            // check the index document head consistency
            Assert.IsNotNull(indexDoc);
            Assert.AreEqual(node.Path, indexDoc.Path);
            Assert.AreEqual(node.Id, indexDoc.NodeId);
            Assert.AreEqual(node.NodeTypeId, indexDoc.NodeTypeId);
            Assert.AreEqual(node.ParentId, indexDoc.ParentId);
            Assert.AreEqual(node.VersionId, indexDoc.VersionId);

            // check the activity
            Assert.IsNotNull(lastActivity);
            Assert.AreEqual(IndexingActivityType.AddDocument, lastActivity.ActivityType);

            var history = IndexingActivityHistory.GetHistory();
            Assert.AreEqual(1, history.RecentLength);
            var item = history.Recent[0];
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);

            var hit1 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1"));
            var hit2 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.DisplayName, "node 1"));
            var hit3 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.NodeId, node.Id));
            var hit4 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.VersionId, node.VersionId));
            var hit5 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Index, node.Index));

            Assert.IsNotNull(hit1);
            Assert.IsNotNull(hit2);
            Assert.IsNotNull(hit3);
            Assert.IsNotNull(hit4);
            Assert.IsNotNull(hit5);
        }

        [TestMethod]
        public void InMemSearch_Indexing_Update()
        {
            Node node;
            var result = Test(() =>
            {
                // create a test node under the root.
                node = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node1",
                    DisplayName = "Node 1",
                    Index = 42
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                // reload the newly created.
                node = Node.Load<SystemFolder>(node.Id);

                // ACTION
                node.DisplayName = "Node 2";
                node.Index = 43;
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();

                // reload the updated.
                node = Node.Load<SystemFolder>(node.Id);

                // load the pre-converted index document
                var db = DataProvider.Current;
                var indexDocument = db.LoadIndexDocumentByVersionId(node.VersionId);

                // load last indexing activity
                var activityId = db.GetLastActivityId();
                var activity =
                    db.LoadIndexingActivities(activityId, activityId, 1, false, IndexingActivityFactory.Instance)
                        .FirstOrDefault();

                return new Tuple<Node, IndexDocumentData, IIndexingActivity, InMemoryIndex>(node, indexDocument,
                    activity, GetTestIndex());
            });

            node = result.Item1;
            var indexDoc = result.Item2;
            var lastActivity = result.Item3;
            var index = result.Item4;

            // check the index document head consistency
            Assert.IsNotNull(indexDoc);
            Assert.AreEqual(node.Path, indexDoc.Path);
            Assert.AreEqual(node.Id, indexDoc.NodeId);
            Assert.AreEqual(node.NodeTypeId, indexDoc.NodeTypeId);
            Assert.AreEqual(node.ParentId, indexDoc.ParentId);
            Assert.AreEqual(node.VersionId, indexDoc.VersionId);

            // check the activity
            Assert.IsNotNull(lastActivity);
            Assert.AreEqual(IndexingActivityType.UpdateDocument, lastActivity.ActivityType);

            var history = IndexingActivityHistory.GetHistory();
            Assert.AreEqual(2, history.RecentLength);
            var item = history.Recent[0];
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);
            item = history.Recent[1];
            Assert.AreEqual(IndexingActivityType.UpdateDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);

            var hit1 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1"));
            var hit2 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.DisplayName, "node 1"));
            var hit3 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.DisplayName, "node 2"));
            var hit4 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.NodeId, node.Id));
            var hit5 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.VersionId, node.VersionId));
            var hit6 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Index, 42));
            var hit7 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Index, 43));

            Assert.IsNotNull(hit1);
            Assert.IsNull(hit2);
            Assert.IsNotNull(hit3);
            Assert.IsNotNull(hit4);
            Assert.IsNotNull(hit5);
            Assert.IsNull(hit6);
            Assert.IsNotNull(hit7);
        }

        [TestMethod]
        public void InMemSearch_Indexing_Delete()
        {
            Node node1, node2;

            var result = Test(() =>
            {
                // create node#1 under the root.
                node1 = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node1",
                    DisplayName = "Node 1",
                    Index = 42
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node1.DisableObserver(observer);
                node1.Save();

                // create node#2 under the root.
                node2 = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node2",
                    DisplayName = "Node 2",
                    Index = 43
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node2.DisableObserver(observer);
                node2.Save();

                // reload.
                node1 = Node.Load<SystemFolder>(node1.Id);

                // ACTION
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node1.DisableObserver(observer);
                node1.ForceDelete();

                // load last indexing activity
                var db = DataProvider.Current;
                var activityId = db.GetLastActivityId();
                var activity =
                    db.LoadIndexingActivities(activityId, activityId, 1, false, IndexingActivityFactory.Instance)
                        .FirstOrDefault();

                return new Tuple<Node, Node, IIndexingActivity, InMemoryIndex>(node1, node2, activity, GetTestIndex());
            });

            node1 = result.Item1;
            node2 = result.Item2;
            var lastActivity = result.Item3;
            var index = result.Item4;

            // check the activity
            Assert.IsNotNull(lastActivity);
            Assert.AreEqual(IndexingActivityType.RemoveTree, lastActivity.ActivityType);

            var history = IndexingActivityHistory.GetHistory();
            Assert.AreEqual(3, history.RecentLength);
            var item = history.Recent[0];
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);
            item = history.Recent[1];
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);
            item = history.Recent[2];
            Assert.AreEqual(IndexingActivityType.RemoveTree.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);

            var hit1 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1"));
            var hit2 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.DisplayName, "node 1"));
            var hit3 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.NodeId, node1.Id));
            var hit4 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.VersionId, node1.VersionId));
            var hit5 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Index, 42));
            var hit6 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node2"));
            var hit7 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.DisplayName, "node 2"));
            var hit8 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.NodeId, node2.Id));
            var hit9 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.VersionId, node2.VersionId));
            var hit10 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Index, 43));

            Assert.IsNull(hit1);
            Assert.IsNull(hit2);
            Assert.IsNull(hit3);
            Assert.IsNull(hit4);
            Assert.IsNull(hit5);
            Assert.IsNotNull(hit6);
            Assert.IsNotNull(hit7);
            Assert.IsNotNull(hit8);
            Assert.IsNotNull(hit9);
            Assert.IsNotNull(hit10);
        }

        [TestMethod]
        public void InMemSearch_Indexing_Rename()
        {
            Node node1; // /Root/Node1
            Node node2; // /Root/Node1/Node2
            Node node3; // /Root/Node1/Node2/Node3
            Node node4; // /Root/Node1/Node4
            Node node5; // /Root/Node5
            Node node6; // /Root/Node5/Node6
            Node[] nodes;

            var result = Test(() =>
            {
                // create initial structure.
                var root = Node.LoadNode(Identifiers.PortalRootId);
                node1 = new SystemFolder(root) {Name = "Node1"};  SaveNode(node1);
                node2 = new SystemFolder(node1) {Name = "Node2"}; SaveNode(node2);
                node3 = new SystemFolder(node2) {Name = "Node3"}; SaveNode(node3);
                node4 = new SystemFolder(node1) {Name = "Node4"}; SaveNode(node4);
                node5 = new SystemFolder(root) {Name = "Node5"};  SaveNode(node5);
                node6 = new SystemFolder(node5) {Name = "Node6"}; SaveNode(node6);

                // ACTION
                node1 = Node.LoadNode(node1.Id);
                node1.Name = "Node1Renamed";
                SaveNode(node1);

                // reload the newly created.
                nodes = new[]
                {
                    Node.LoadNode(node1.Id),
                    Node.LoadNode(node2.Id),
                    Node.LoadNode(node3.Id),
                    Node.LoadNode(node4.Id),
                    Node.LoadNode(node5.Id),
                    Node.LoadNode(node6.Id),
                };

                return new Tuple<Node[], IndexingActivityHistory, InMemoryIndex>(nodes,
                    IndexingActivityHistory.GetHistory(), GetTestIndex());
            });

            nodes = result.Item1;
            var history = result.Item2;
            var index = result.Item3;

            // check paths
            Assert.AreEqual("/Root/Node1Renamed", nodes[0].Path);
            Assert.AreEqual("/Root/Node1Renamed/Node2", nodes[1].Path);
            Assert.AreEqual("/Root/Node1Renamed/Node2/Node3", nodes[2].Path);
            Assert.AreEqual("/Root/Node1Renamed/Node4", nodes[3].Path);
            Assert.AreEqual("/Root/Node5", nodes[4].Path);
            Assert.AreEqual("/Root/Node5/Node6", nodes[5].Path);

            // check indexing activities and errors
            Assert.AreEqual(8, history.RecentLength);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[0].TypeName);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[1].TypeName);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[2].TypeName);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[3].TypeName);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[4].TypeName);
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), history.Recent[5].TypeName);
            Assert.AreEqual(IndexingActivityType.RemoveTree.ToString(), history.Recent[6].TypeName);
            Assert.AreEqual(IndexingActivityType.AddTree.ToString(), history.Recent[7].TypeName);
            Assert.AreEqual(null, history.Recent[0].Error);
            Assert.AreEqual(null, history.Recent[1].Error);
            Assert.AreEqual(null, history.Recent[2].Error);
            Assert.AreEqual(null, history.Recent[3].Error);
            Assert.AreEqual(null, history.Recent[4].Error);
            Assert.AreEqual(null, history.Recent[5].Error);
            Assert.AreEqual(null, history.Recent[6].Error);
            Assert.AreEqual(null, history.Recent[7].Error);

            // check name terms in index
            var hit1 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1"));
            var hit2 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1renamed"));
            var hit3 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node2"));
            var hit4 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node3"));
            var hit5 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node4"));
            var hit6 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node5"));
            var hit7 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node6"));
            Assert.IsNull(hit1);
            Assert.IsNotNull(hit2);
            Assert.IsNotNull(hit3);
            Assert.IsNotNull(hit4);
            Assert.IsNotNull(hit5);
            Assert.IsNotNull(hit6);
            Assert.IsNotNull(hit7);

            // check old subtree existence
            var node1Tree = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.InTree, "/root/node1"));
            Assert.IsNull(node1Tree);

            // check renamed tree
            var renamedTree =
                index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.InTree, "/root/node1renamed")).ToArray();
            Assert.IsNotNull(renamedTree);
            Assert.AreEqual(4, renamedTree.Length);
            // check node ids in renamed tree
            var ids = string.Join(", ", renamedTree
                .Select(x => x.Item2.First(y => y.Name == IndexFieldName.NodeId).IntegerValue)
                .OrderBy(z => z).Select(z => z.ToString()).ToArray());
            var expectedIds = string.Join(", ", new[] {nodes[0].Id, nodes[1].Id, nodes[2].Id, nodes[3].Id}
                .OrderBy(z => z).Select(z => z.ToString()).ToArray());
            Assert.AreEqual(expectedIds, ids);
            // check paths ids in renamed tree
            var paths = string.Join(", ", renamedTree
                .Select(x => x.Item2.First(y => y.Name == IndexFieldName.Path).StringValue)
                .OrderBy(z => z).ToArray());
            var expectedPaths = string.Join(", ", new[] {nodes[0].Path, nodes[1].Path, nodes[2].Path, nodes[3].Path}
                .OrderBy(z => z).ToArray());
            Assert.AreEqual(expectedPaths.ToLowerInvariant(), paths);

            // check untouched tree
            var node5Tree = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.InTree, "/root/node5")).ToArray();
            Assert.IsNotNull(node5Tree);
            Assert.AreEqual(2, node5Tree.Count());
            // check node ids in untouched tree
            ids = string.Join(", ", node5Tree
                .Select(x => x.Item2.First(y => y.Name == IndexFieldName.NodeId).IntegerValue)
                .OrderBy(z => z).Select(z => z.ToString()).ToArray());
            expectedIds = string.Join(", ", new[] {nodes[4].Id, nodes[5].Id}
                .OrderBy(z => z).Select(z => z.ToString()).ToArray());
            Assert.AreEqual(expectedIds, ids);
            // check paths ids in untouched tree
            paths = string.Join(", ", node5Tree
                .Select(x => x.Item2.First(y => y.Name == IndexFieldName.Path).StringValue)
                .OrderBy(z => z).ToArray());
            expectedPaths = string.Join(", ", new[] {nodes[4].Path, nodes[5].Path}
                .OrderBy(z => z).ToArray());
            Assert.AreEqual(expectedPaths.ToLowerInvariant(), paths);
        }


        [TestMethod]
        public void InMemSearch_Indexing_AddTextEctract()
        {
            Node node;
            var additionalText = "additionaltext";

            var result = Test(() =>
            {
                // create a test node under the root.
                node = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node1",
                    DisplayName = "Node 1",
                    Index = 42
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();

                // ACTION
                IndexingTools.AddTextExtract(node.VersionId, additionalText);

                node = Node.Load<SystemFolder>(node.Id);

                // load the pre-converted index document
                var db = DataProvider.Current;
                var indexDocument = db.LoadIndexDocumentByVersionId(node.VersionId);

                return new Tuple<Node, IndexDocumentData, InMemoryIndex>(node, indexDocument, GetTestIndex());
            });

            node = result.Item1;
            var indexDoc = result.Item2;
            var index = result.Item3;

            // check the index document head consistency
            Assert.IsNotNull(indexDoc);
            Assert.AreEqual(node.VersionId, indexDoc.VersionId);
            Assert.IsTrue(indexDoc.IndexDocument.GetStringValue(IndexFieldName.AllText).Contains(additionalText));

            // check executed activities
            var history = IndexingActivityHistory.GetHistory();
            Assert.AreEqual(2, history.RecentLength);
            var item = history.Recent[0];
            Assert.AreEqual(IndexingActivityType.AddDocument.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);
            item = history.Recent[1];
            Assert.AreEqual(IndexingActivityType.Rebuild.ToString(), item.TypeName);
            Assert.AreEqual(null, item.Error);

            // check index
            var hit1 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.Name, "node1"));
            var hit2 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.VersionId, node.VersionId));
            var hit3 = index.GetStoredFieldsByTerm(new SnTerm(IndexFieldName.AllText, additionalText));

            Assert.IsNotNull(hit1);
            Assert.IsNotNull(hit2);
            Assert.IsNotNull(hit3);
        }

        [TestMethod]
        public void InMemSearch_Indexing_ClearAndPopulateAll()
        {
            var sb = new StringBuilder();
            IIndexingActivity[] activities;
            var result = Test(() =>
            {
                SaveInitialIndexDocuments();

                // ACTION
                using (var console = new StringWriter(sb))
                    StorageContext.Search.SearchEngine.GetPopulator().ClearAndPopulateAll(console);

                // load last indexing activity
                var db = DataProvider.Current;
                var activityId = db.GetLastActivityId();
                activities = db.LoadIndexingActivities(1, activityId, 10000, false, IndexingActivityFactory.Instance);

                var nodeCount = DataProvider.GetNodeCount();
                var versionCount = DataProvider.GetVersionCount();

                return new Tuple<IIndexingActivity[], InMemoryIndex, int, int>(activities, GetTestIndex(), nodeCount, versionCount);
            });

            activities = result.Item1;
            var index = result.Item2;
            var nodeCountInDb = result.Item3;
            var versionCountInDb = result.Item4;

            // check activities
            Assert.IsNotNull(activities);
            Assert.AreEqual(0, activities.Length);

            var historyItems = IndexingActivityHistory.GetHistory().Recent;
            Assert.AreEqual(0, historyItems.Length);

            var nodeCountInIndex = index.GetTermCount(IndexFieldName.NodeId);
            var versionCountInIndex = index.GetTermCount(IndexFieldName.VersionId);

            Assert.AreEqual(nodeCountInDb, nodeCountInIndex);
            Assert.AreEqual(versionCountInDb, versionCountInIndex);
        }

        /* ============================================================================ */

        [TestMethod]
        public void InMemSearch_Query_1Term1Hit()
        {
            Node node;
            var result = Test(() =>
            {
                // create a test node under the root.
                node = new SystemFolder(Node.LoadNode(Identifiers.PortalRootId))
                {
                    Name = "Node1",
                    DisplayName = "Node 1",
                    Index = 42
                };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();

                // ACTION
                var qresult = ContentQuery_NEW.Query(SafeQueries.Name, QuerySettings.AdminSettings, "Node1");

                return new Tuple<int[], Node[]>(qresult.Identifiers.ToArray(), qresult.Nodes.ToArray());
            });

            var nodeIds = result.Item1;
            var nodes = result.Item2;

        }

        [TestMethod]
        public void InMemSearch_Query_1TermMoreHit1Order()
        {
            var createNode = new Func<Node, string, int, Node>((parent, name, index) =>
            {
                var node = new SystemFolder(parent) {Name = name, Index = index};
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                return node;
            });

            Node root, f1, f2, f3, node1, node2, node3;
            node1 = node2 = node3 = null;

            var result = Test(() =>
            {
                // create a test structure:
                //    Root
                //      F1
                //        Node1 (Index=42)
                //      F2
                //        Node1 (Index=41)
                //      F3
                //        Node1 (Index=43)
                root = Node.LoadNode(Identifiers.PortalRootId);

                f1 = createNode(root, "F1", 0);
                node1 = createNode(f1, "Node1", 42);
                f2 = createNode(root, "F2", 0);
                node2 = createNode(f2, "Node1", 41);
                f3 = createNode(root, "F3", 0);
                node3 = createNode(f3, "Node1", 43);

                // ACTION
                var settings = QuerySettings.AdminSettings;
                settings.Sort = new[] { new SortInfo(IndexFieldName.Index) };
                var qresult = ContentQuery_NEW.Query(SafeQueries.Name, settings, "Node1");

                return new Tuple<int[], Node[]>(qresult.Identifiers.ToArray(), qresult.Nodes.ToArray());
            });

            var nodeIds = result.Item1;
            var nodes = result.Item2;

            var expectedNodeIds = $"{node2.Id}, {node1.Id}, {node3.Id}";
            var actualNodeIds = string.Join(", ", nodeIds);
            Assert.AreEqual(expectedNodeIds, actualNodeIds);

            var expectedPaths = $"/Root/F2/Node1, /Root/F1/Node1, /Root/F3/Node1";
            var actualPaths = string.Join(", ", nodes.Select(n => n.Path));
            Assert.AreEqual(expectedPaths, actualPaths);
        }

        [TestMethod]
        public void InMemSearch_Query_1TermMoreHit2Order()
        {
            var createNode = new Func<Node, string, string, int, Node>((parent, name, displayName, index) =>
            {
                var node = new SystemFolder(parent) { Name = name, DisplayName = displayName, Index = index };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                return node;
            });

            var result = Test(() =>
            {
                // create a test structure:
                var root = Node.LoadNode(Identifiers.PortalRootId);
                var f1 = createNode(root, "F1", "F1", 0);
                createNode(f1, "N1", "D2", 2);
                createNode(f1, "N2", "D3", 2);
                createNode(f1, "N3", "D2", 3);
                createNode(f1, "N4", "D3", 3);
                createNode(f1, "N5", "D1", 2);
                createNode(f1, "N6", "D3", 1);
                createNode(f1, "N7", "D2", 1);
                createNode(f1, "N8", "D1", 1);

                // ACTION
                var settings = QuerySettings.AdminSettings;
                settings.Sort = new[] {new SortInfo(IndexFieldName.DisplayName), new SortInfo(IndexFieldName.Index, true) };
                var qresult = ContentQuery_NEW.Query(SafeQueries.OneTerm, settings, "ParentId", f1.Id.ToString());

                return new Tuple<int[], Node[]>(qresult.Identifiers.ToArray(), qresult.Nodes.ToArray());
            });

            var nodeIds = result.Item1;
            var nodes = result.Item2;

            var expectedNames = "N5, N8, N3, N1, N7, N4, N2, N6";
            var actualNames = string.Join(", ", nodes.Select(n => n.Name));
            Assert.AreEqual(expectedNames, actualNames);
        }

        [TestMethod]
        public void InMemSearch_Query_PrefixOrSuffix()
        {
            var createNode = new Func<Node, string, Node>((parent, name) =>
            {
                var node = new SystemFolder(parent) { Name = name };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                return node;
            });

            var result = Test(() =>
            {
                // create a test structure:
                var root = Node.LoadNode(Identifiers.PortalRootId);
                var f1 = createNode(root, "F1");
                createNode(f1, "A1");
                createNode(f1, "B1");
                createNode(f1, "C1");
                createNode(f1, "A2");
                createNode(f1, "B2");
                createNode(f1, "C2");
                createNode(f1, "A3");
                createNode(f1, "B3");
                createNode(f1, "C3");

                // ACTION
                var settings = QuerySettings.AdminSettings;
                settings.Sort = new[] { new SortInfo(IndexFieldName.Name) };
                var qresult1 = ContentQuery_NEW.Query(SafeQueries.OneTerm, settings, "Name", "B*");
                var qresult2 = ContentQuery_NEW.Query(SafeQueries.OneTerm, settings, "Name", "*3");

                return new Tuple<Node[], Node[]>(qresult1.Nodes.ToArray(), qresult2.Nodes.ToArray());
            });

            var nodes1 = result.Item1;
            var nodes2 = result.Item2;

            Assert.AreEqual("B1, B2, B3", string.Join(", ", nodes1.Select(n => n.Name)));
            Assert.AreEqual("A3, B3, C3", string.Join(", ", nodes2.Select(n => n.Name)));
        }

        [TestMethod]
        public void InMemSearch_Query_PrefixAndSuffixOrMiddle()
        {
            var createNode = new Func<Node, string, Node>((parent, name) =>
            {
                var node = new SystemFolder(parent) { Name = name };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                return node;
            });

            var result = Test(() =>
            {
                // create a test structure:
                var root = Node.LoadNode(Identifiers.PortalRootId);
                var f1 = createNode(root, "F1");
                createNode(f1, "Ax1");
                createNode(f1, "Ax2");
                createNode(f1, "Ay1");
                createNode(f1, "Ay2");
                createNode(f1, "Bx1");
                createNode(f1, "Bx2");
                createNode(f1, "By1");
                createNode(f1, "By2");

                // ACTION
                var settings = QuerySettings.AdminSettings;
                settings.Sort = new[] { new SortInfo(IndexFieldName.Name) };
                var qresult1 = ContentQuery_NEW.Query(SafeQueries.OneTerm, settings, "Name", "A*2");
                var qresult2 = ContentQuery_NEW.Query(SafeQueries.OneTerm, settings, "Name", "*y*");

                return new Tuple<Node[], Node[]>(qresult1.Nodes.ToArray(), qresult2.Nodes.ToArray());
            });

            var nodes1 = result.Item1;
            var nodes2 = result.Item2;

            Assert.AreEqual("Ax2, Ay2", string.Join(", ", nodes1.Select(n => n.Name)));
            Assert.AreEqual("Ay1, Ay2, By1, By2", string.Join(", ", nodes2.Select(n => n.Name)));
        }


        [TestMethod]
        public void InMemSearch_Query_Range()
        {
            var createNode = new Func<Node, string, Node>((parent, name) =>
            {
                var node = new SystemFolder(parent) { Name = name };
                foreach (var observer in NodeObserver.GetObserverTypes())
                    node.DisableObserver(observer);
                node.Save();
                return node;
            });

            var result = Test(() =>
            {
                // create a test structure:
                var root = Node.LoadNode(Identifiers.PortalRootId);
                createNode(root, "N0");
                createNode(root, "N1");
                createNode(root, "N2");
                createNode(root, "N3");
                createNode(root, "N4");
                createNode(root, "N5");
                createNode(root, "N6");
                createNode(root, "N7");
                createNode(root, "N8");
                createNode(root, "N9");

                // ACTION
                var settings = QuerySettings.AdminSettings;
                settings.Sort = new[] { new SortInfo(IndexFieldName.Name) };
                string[] results = new[]
                {
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.GT, settings, "Name", "N4").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.GTE, settings, "Name", "N4").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.LT, settings, "Name", "N4").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.LTE, settings, "Name", "N4").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.BracketBracketRange, settings, "Name", "N2", "N7").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.BracketBraceRange, settings, "Name", "N2", "N7").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.BraceBracketRange, settings, "Name", "N2", "N7").Nodes.Select(n => n.Name).ToArray()),
                    string.Join(", ", ContentQuery_NEW.Query(SafeQueries.BraceBraceRange, settings, "Name", "N2", "N7").Nodes.Select(n => n.Name).ToArray()),
                };

                return results;
            });

            Assert.AreEqual("N5, N6, N7, N8, N9",     result[0]);
            Assert.AreEqual("N4, N5, N6, N7, N8, N9", result[1]);
            Assert.AreEqual("N0, N1, N2, N3",         result[2]);
            Assert.AreEqual("N0, N1, N2, N3, N4",     result[3]);
            Assert.AreEqual("N2, N3, N4, N5, N6, N7", result[4]);
            Assert.AreEqual("N2, N3, N4, N5, N6",     result[5]);
            Assert.AreEqual(    "N3, N4, N5, N6, N7", result[6]);
            Assert.AreEqual(    "N3, N4, N5, N6",     result[7]);
        }

        /* ============================================================================ */

        private InMemoryIndex GetTestIndex()
        {
            return ((InMemoryIndexingEngine) IndexManager.IndexingEngine).Index;
        }

        private void SaveNode(Node node)
        {
            foreach (var observer in NodeObserver.GetObserverTypes())
                node.DisableObserver(observer);
            node.Save();
        }
    }
}