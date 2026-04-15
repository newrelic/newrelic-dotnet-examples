// Copyright 2023 New Relic, Inc. All rights reserved.
// SPDX-License-Identifier: Apache-2.0

namespace ApiDatastoreSegments.Services;

/// <summary>
/// Connection metadata for the custom document store. These values are passed to
/// RecordDatastoreSegment() and appear in datastore segment metrics. Use consistent
/// values across all calls to the same datastore instance so that metrics are grouped
/// correctly.
///
/// Note: the agent currently categorizes all RecordDatastoreSegment() calls under the
/// vendor name "Other" regardless of the vendor string passed here. The model, operation,
/// host, port, and databaseName values are reported as provided.
/// </summary>
public record DocumentStoreConfig(
    string Vendor = "CustomDocStore",
    string Host = "localhost",
    string PortPathOrId = "9999",
    string DatabaseName = "documents"
);
