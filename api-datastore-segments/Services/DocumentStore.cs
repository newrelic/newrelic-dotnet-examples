// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;

namespace ApiDatastoreSegments.Services;

/// <summary>
/// An in-memory document store instrumented with ITransaction.RecordDatastoreSegment().
///
/// RecordDatastoreSegment() manually creates a datastore segment for data stores that
/// the agent does not auto-instrument. The returned SegmentWrapper implements IDisposable —
/// the segment's duration is measured from creation to Dispose(). Use the "using var" pattern
/// so that the segment is automatically ended when the variable goes out of scope.
///
/// These methods are called from ASP.NET Core minimal API endpoints. The agent's built-in
/// ASP.NET Core instrumentation automatically creates a transaction for each incoming HTTP
/// request, so these methods do NOT need the [Transaction] attribute.
///
/// See: https://docs.newrelic.com/docs/apm/agents/net-agent/net-agent-api/net-agent-api/#RecordDatastoreSegment
/// </summary>
public class DocumentStore
{
    private readonly ConcurrentDictionary<string, Document> _store = new();
    private readonly DocumentStoreConfig _config;

    public DocumentStore(DocumentStoreConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Inserts a document. Demonstrates RecordDatastoreSegment with operation "insert".
    ///
    /// The "model" parameter maps to the table or collection name — use it even for
    /// non-relational stores so operations are grouped meaningfully.
    ///
    /// The "operation" parameter should be one of the standard database operations
    /// (select, insert, update, delete) when possible — the agent uses this for
    /// operation-level grouping.
    /// </summary>
    public IResult Create(Document doc)
    {
        var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        // RecordDatastoreSegment returns a SegmentWrapper? — it returns null if no
        // transaction is active. "using var" handles null gracefully (Dispose() is
        // not called on null), so the code works whether or not a transaction exists.
        using var segment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "insert",
            commandText: null,
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName);

        // The actual storage operation happens inside the using block — the segment's
        // duration covers this work.
        if (!_store.TryAdd(doc.Id, doc))
        {
            return Results.Conflict(new { error = "Document already exists", id = doc.Id });
        }

        return Results.Created($"/documents/{doc.Id}", doc);
    }

    /// <summary>
    /// Retrieves a document by ID. Demonstrates RecordDatastoreSegment with operation "select".
    /// </summary>
    public IResult Get(string id)
    {
        var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        // The segment is null if called outside a transaction — the code still works,
        // we just don't get datastore telemetry in that case.
        using var segment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "select",
            commandText: null,
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName);

        if (_store.TryGetValue(id, out var doc))
        {
            return Results.Ok(doc);
        }

        return Results.NotFound(new { error = "Document not found", id });
    }

    /// <summary>
    /// Updates an existing document. Demonstrates RecordDatastoreSegment with operation "update".
    /// </summary>
    public IResult Update(string id, Document doc)
    {
        var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        using var segment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "update",
            commandText: null,
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName);

        if (!_store.ContainsKey(id))
        {
            return Results.NotFound(new { error = "Document not found", id });
        }

        var updated = doc with { Id = id };
        _store[id] = updated;
        return Results.Ok(updated);
    }

    /// <summary>
    /// Deletes a document by ID. Demonstrates RecordDatastoreSegment with operation "delete".
    /// </summary>
    public IResult Delete(string id)
    {
        var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        using var segment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "delete",
            commandText: null,
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName);

        if (_store.TryRemove(id, out _))
        {
            return Results.Ok(new { message = "Document deleted", id });
        }

        return Results.NotFound(new { error = "Document not found", id });
    }

    /// <summary>
    /// Searches documents by field value. Demonstrates two things:
    ///
    /// 1. The "commandText" parameter — for real databases this would be the actual query;
    ///    for custom stores, use a descriptive pseudo-query. commandText appears in slow
    ///    query traces and should not include sensitive data (user IDs, credentials).
    ///
    /// 2. Multiple RecordDatastoreSegment() calls within the same transaction — each creates
    ///    a separate segment. The search segment and the load-details segment appear as
    ///    sibling nodes in the trace waterfall, which is useful for seeing which datastore
    ///    calls dominate a transaction's time.
    /// </summary>
    public IResult Search(string field, string value)
    {
        var txn = NewRelic.Api.Agent.NewRelic.GetAgent().CurrentTransaction;

        // First segment: the search query itself.
        // The Thread.Sleep simulates a slow query so the segment exceeds the slow SQL
        // threshold (default 500ms), causing the commandText to appear in slow query traces.
        List<Document> results;
        using (var searchSegment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "select",
            commandText: $"SEARCH documents WHERE {field} = '{value}'",
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName))
        {
            results = field.ToLowerInvariant() switch
            {
                "title" => _store.Values.Where(d =>
                    d.Title.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList(),
                "content" => _store.Values.Where(d =>
                    d.Content.Contains(value, StringComparison.OrdinalIgnoreCase)).ToList(),
                _ => []
            };

            Thread.Sleep(600);
        }

        // Second segment: simulate loading full details for each matched document.
        // This demonstrates that multiple RecordDatastoreSegment() calls in the same
        // transaction create separate segments — they appear as sibling nodes in the
        // trace waterfall.
        using (var loadSegment = txn.RecordDatastoreSegment(
            vendor: _config.Vendor,
            model: "documents",
            operation: "select",
            commandText: $"LOAD documents WHERE id IN ({string.Join(", ", results.Select(d => $"'{d.Id}'"))})",
            host: _config.Host,
            portPathOrID: _config.PortPathOrId,
            databaseName: _config.DatabaseName))
        {
            // Simulate loading full document details
            Thread.Sleep(5);
        }

        return Results.Ok(new { count = results.Count, results });
    }
}
