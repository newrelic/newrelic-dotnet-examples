// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

using ApiDatastoreSegments.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new DocumentStoreConfig());
builder.Services.AddSingleton<DocumentStore>();

var app = builder.Build();

app.Urls.Add("http://localhost:5002");

// POST /documents
// Creates a new document. Demonstrates RecordDatastoreSegment with operation "insert".
app.MapPost("/documents", (Document doc, DocumentStore store) =>
    store.Create(doc));

// GET /documents/{id}
// Retrieves a document by ID. Demonstrates RecordDatastoreSegment with operation "select".
app.MapGet("/documents/{id}", (string id, DocumentStore store) =>
    store.Get(id));

// PUT /documents/{id}
// Updates an existing document. Demonstrates RecordDatastoreSegment with operation "update".
app.MapPut("/documents/{id}", (string id, Document doc, DocumentStore store) =>
    store.Update(id, doc));

// DELETE /documents/{id}
// Deletes a document by ID. Demonstrates RecordDatastoreSegment with operation "delete".
app.MapDelete("/documents/{id}", (string id, DocumentStore store) =>
    store.Delete(id));

// GET /documents/search?field={field}&value={value}
// Searches documents by field value. Demonstrates commandText and multiple segments
// within a single transaction.
app.MapGet("/documents/search", (string field, string value, DocumentStore store) =>
    store.Search(field, value));

app.Run();

public record Document(string Id, string Title, string Content, DateTime CreatedAt);
